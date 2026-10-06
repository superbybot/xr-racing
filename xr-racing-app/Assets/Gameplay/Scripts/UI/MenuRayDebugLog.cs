using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
#endif

namespace XrRacing.Gameplay.UI
{
    /// <summary>
    /// Diagnostic: logs what the pointer rays do while the driver menu is open, to find why they blink.
    /// Writes Logs/menu_ray.csv in the Editor (Application.persistentDataPath/menu_ray.csv on device):
    ///   HOVER_CHANGE   a ray's target changed (e.g. panel -> nothing -> panel: the ray is losing the panel)
    ///   VISUAL_TOGGLE  a ray line/cursor renderer switched on or off
    ///   SORT_FLIP      a ray renderer went from nearer to farther than the panel (or back) as seen from the camera;
    ///                  the overlay draws with depth testing off, so that order decides which one is drawn on top
    ///   SUMMARY        once a second: counts of the above, and how much the rig turned relative to the kart
    ///   F              every frame, if Log Raw Samples is on
    /// Added by DriverSettingsMenu when its Debug Ray Log is on. Compiled out of release builds.
    /// </summary>
    [DefaultExecutionOrder(2000)] // after the interactors, their visuals and the menu
    public class MenuRayDebugLog : MonoBehaviour
    {
        [Tooltip("Also write one row per frame while the menu is open. Large files; off for events/summaries only.")]
        [SerializeField] private bool logRawSamples = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const float SummaryInterval = 1f;

        private GameObject _panel;
        private Transform _rig;
        private Transform _head;
        private RayInteractor[] _rays = new RayInteractor[0];
        private string[] _lastTargets = new string[0];
        private Renderer[] _visuals = new Renderer[0];
        private bool[] _lastVisible = new bool[0];
        private int[] _lastNearer = new int[0];
        private Transform _surface;
        private BoundsClipper _clipper;
        private MonoBehaviour[] _interactors = new MonoBehaviour[0];
        private string[] _lastInteractorStates = new string[0];
        private Quaternion _lastRigLocalRotation;
        private bool _hasLastRig;
        private bool _wasOpen;

        private StreamWriter _writer;
        private readonly StringBuilder _line = new StringBuilder(512);

        private float _summaryStart;
        private int _frames, _hoverChanges, _visualToggles, _sortFlips;
        private float _maxRigTurn;

        public void Init(GameObject panel, Transform rig, Transform head)
        {
            _panel = panel;
            _rig = rig;
            _head = head;
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            string dir = Path.Combine(Application.dataPath, "..", "Logs");
#else
            string dir = Application.persistentDataPath;
#endif
            Directory.CreateDirectory(dir);
            string path = Path.GetFullPath(Path.Combine(dir, "menu_ray.csv"));
            _writer = new StreamWriter(path, false, Encoding.UTF8) { AutoFlush = false };
            _writer.WriteLine("time,frame,kind,detail");
            Debug.Log($"[MenuRayDebugLog] Writing to {path}");
        }

        private void OnDisable()
        {
            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;
        }

        private void Start()
        {
            _rays = FindObjectsByType<RayInteractor>(FindObjectsInactive.Include);
            _lastTargets = new string[_rays.Length];

            var visuals = new List<Renderer>();
            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            {
#pragma warning disable CS0618 // the obsolete visuals are still what the rig uses
                if (behaviour is ControllerRayVisual || behaviour is RayInteractorRayVisual || behaviour is RayInteractorCursorVisual ||
                    behaviour is HandRayInteractorCursorVisual || behaviour is RayInteractorPinchVisual)
#pragma warning restore CS0618
                {
                    visuals.AddRange(behaviour.GetComponentsInChildren<Renderer>(true));
                }
            }

            // Every interactor (poke, grab, distance grab, ...): they share a BestHoverInteractorGroup with the ray,
            // where only one may hover at a time, so another one grabbing the hover would switch the ray off.
            var interactors = new List<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            {
                if (behaviour is IInteractorView && !(behaviour is InteractorGroup))
                {
                    interactors.Add(behaviour);
                }
            }

            _interactors = interactors.ToArray();
            _lastInteractorStates = new string[_interactors.Length];

            _visuals = visuals.ToArray();
            _lastVisible = new bool[_visuals.Length];
            _lastNearer = new int[_visuals.Length];

            _line.Clear().Append("rays=");
            foreach (RayInteractor ray in _rays)
            {
                _line.Append(Label(ray.transform)).Append(' ');
            }

            _line.Append("visuals=");
            foreach (Renderer visual in _visuals)
            {
                _line.Append(Label(visual.transform)).Append("(layer ").Append(LayerMask.LayerToName(visual.gameObject.layer))
                    .Append(" order ").Append(visual.sortingOrder).Append(") ");
            }

            Write("SETUP", _line.ToString());
        }

        private void LateUpdate()
        {
            bool open = DriverSettingsMenu.IsOpen;
            if (open != _wasOpen)
            {
                Write(open ? "MENU_OPEN" : "MENU_CLOSE", "");
                _wasOpen = open;
                _hasLastRig = false;
                ResetSummary();
            }

            if (!open || _writer == null)
            {
                return;
            }

            Transform cameraTransform = _head != null ? _head : (Camera.main != null ? Camera.main.transform : null);
            Vector3 cameraPosition = cameraTransform != null ? cameraTransform.position : Vector3.zero;
            float panelDistance = _panel != null ? Vector3.Distance(cameraPosition, _panel.transform.position) : -1f;

            // How much the rig turned relative to the kart since last frame (horizon lock / tilt smoothing at work).
            float rigTurn = 0f;
            if (_rig != null)
            {
                if (_hasLastRig)
                {
                    rigTurn = Quaternion.Angle(_lastRigLocalRotation, _rig.localRotation);
                }

                _lastRigLocalRotation = _rig.localRotation;
                _hasLastRig = true;
            }

            _maxRigTurn = Mathf.Max(_maxRigTurn, rigTurn);
            _frames++;

            if (logRawSamples)
            {
                _line.Clear().Append("dt=").Append(F(Time.deltaTime)).Append(" rigTurn=").Append(F(rigTurn))
                    .Append(" panelDist=").Append(F(panelDistance));
            }

            for (int i = 0; i < _rays.Length; i++)
            {
                RayInteractor ray = _rays[i];
                if (ray == null || !ray.isActiveAndEnabled)
                {
                    continue;
                }

                string target = ray.Interactable != null ? ray.Interactable.name : "-";
                if (_lastTargets[i] != null && target != _lastTargets[i])
                {
                    _hoverChanges++;
                    Write("HOVER_CHANGE", $"{ray.name}: {_lastTargets[i]} -> {target} state={ray.State} " +
                        $"hitDist={F(HitDistance(ray))} rigTurn={F(rigTurn)}");
                }

                _lastTargets[i] = target;

                if (logRawSamples)
                {
                    _line.Append(" | ").Append(ray.name).Append(' ').Append(ray.State).Append(" on=").Append(target)
                        .Append(" hit=").Append(F(HitDistance(ray)));
                    AppendPlaneCrossing(ray);
                }
            }

            for (int i = 0; i < _interactors.Length; i++)
            {
                MonoBehaviour behaviour = _interactors[i];
                if (behaviour == null || !behaviour.isActiveAndEnabled || behaviour is RayInteractor)
                {
                    continue;
                }

                var view = (IInteractorView)behaviour;
                string state = view.State + (view.HasCandidate ? " candidate" : "") + (view.HasInteractable ? " hovering" : "");
                if (_lastInteractorStates[i] != null && state != _lastInteractorStates[i])
                {
                    Write("INTERACTOR", $"{Label(behaviour.transform)} ({behaviour.GetType().Name}): " +
                        $"{_lastInteractorStates[i]} -> {state} target={TargetName(behaviour)}");
                }

                _lastInteractorStates[i] = state;
            }

            for (int i = 0; i < _visuals.Length; i++)
            {
                Renderer visual = _visuals[i];
                if (visual == null)
                {
                    continue;
                }

                bool visible = visual.enabled && visual.gameObject.activeInHierarchy;
                if (visible != _lastVisible[i])
                {
                    _visualToggles++;
                    Write("VISUAL_TOGGLE", $"{Label(visual.transform)} {(visible ? "on" : "off")} rigTurn={F(rigTurn)}");
                    _lastVisible[i] = visible;
                }

                if (!visible || panelDistance < 0f)
                {
                    continue;
                }

                float visualDistance = Vector3.Distance(cameraPosition, visual.bounds.center);
                int nearer = visualDistance < panelDistance ? 1 : -1;
                if (_lastNearer[i] != 0 && nearer != _lastNearer[i])
                {
                    _sortFlips++;
                    Write("SORT_FLIP", $"{Label(visual.transform)} now {(nearer > 0 ? "nearer" : "farther")} than the panel " +
                        $"({F(visualDistance)} vs {F(panelDistance)})");
                }

                _lastNearer[i] = nearer;

                if (logRawSamples)
                {
                    _line.Append(" | ").Append(visual.name).Append(" dist=").Append(F(visualDistance));
                }
            }

            if (logRawSamples)
            {
                Write("F", _line.ToString());
            }

            if (Time.time - _summaryStart >= SummaryInterval)
            {
                Write("SUMMARY", $"frames={_frames} hoverChanges={_hoverChanges} visualToggles={_visualToggles} " +
                    $"sortFlips={_sortFlips} maxRigTurnPerFrame={F(_maxRigTurn)}");
                _writer.Flush();
                ResetSummary();
            }
        }

        private void ResetSummary()
        {
            _summaryStart = Time.time;
            _frames = _hoverChanges = _visualToggles = _sortFlips = 0;
            _maxRigTurn = 0f;
            for (int i = 0; i < _lastNearer.Length; i++)
            {
                _lastNearer[i] = 0;
            }
        }

        private static float HitDistance(RayInteractor ray)
        {
            return ray.CollisionInfo.HasValue ? Vector3.Distance(ray.Origin, ray.CollisionInfo.Value.Point) : -1f;
        }

        // Where the ray is and where it crosses the panel's plane, in the panel surface's local space (canvas units),
        // next to the clip rectangle: a miss inside the rectangle points at the ray/surface, one outside at the aim.
        private void AppendPlaneCrossing(RayInteractor ray)
        {
            if (_surface == null && _panel != null)
            {
                RayInteractable interactable = _panel.GetComponentInChildren<RayInteractable>(true);
                _surface = interactable != null ? interactable.Surface.Transform : null;
                _clipper = _panel.GetComponentInChildren<BoundsClipper>(true);
            }

            if (_surface == null)
            {
                return;
            }

            Ray r = ray.Ray;
            Vector3 localOrigin = _surface.InverseTransformPoint(r.origin);
            Vector3 localDirection = _surface.InverseTransformDirection(r.direction);
            _line.Append(" origin=").Append(V(localOrigin)).Append(" dir=").Append(V(r.direction));
            if (Mathf.Abs(localDirection.z) > 1e-6f)
            {
                float t = -localOrigin.z / localDirection.z;
                Vector3 crossing = localOrigin + localDirection * t;
                _line.Append(" crossing=").Append(V(crossing)).Append(t < 0f ? " (behind)" : "");
            }

            if (_clipper != null)
            {
                _line.Append(" clip=").Append(V(_clipper.Size)).Append(_clipper.isActiveAndEnabled ? "" : " (clipper off)");
            }

            _line.Append(" surfaceScale=").Append(F(_surface.lossyScale.x));
        }

        private static string V(Vector3 v)
        {
            return "(" + F(v.x) + " " + F(v.y) + " " + F(v.z) + ")";
        }

        // Interactor<,>.Interactable / Candidate, whatever the interactable type.
        private static string TargetName(MonoBehaviour interactor)
        {
            try
            {
                System.Type type = interactor.GetType();
                object target = type.GetProperty("Interactable")?.GetValue(interactor) ?? type.GetProperty("Candidate")?.GetValue(interactor);
                return target is Component component ? Label(component.transform) : "-";
            }
            catch (System.Reflection.AmbiguousMatchException)
            {
                return "?";
            }
        }

        private static string Label(Transform t)
        {
            return t.parent != null ? t.parent.name + "/" + t.name : t.name;
        }

        private void Write(string kind, string detail)
        {
            if (_writer == null)
            {
                return;
            }

            _writer.WriteLine(string.Join(",",
                Time.time.ToString("F3", CultureInfo.InvariantCulture),
                Time.frameCount.ToString(CultureInfo.InvariantCulture),
                kind,
                "\"" + detail + "\""));
        }

        private static string F(float value)
        {
            return value.ToString("F3", CultureInfo.InvariantCulture);
        }
#endif
    }
}
