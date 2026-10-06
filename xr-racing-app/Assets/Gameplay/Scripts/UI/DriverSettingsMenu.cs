using System;
using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XrRacing.Gameplay.Settings;
using XrRacing.Gameplay.Tracks;
using XrRacing.Gameplay.Vehicle;

namespace XrRacing.Gameplay.UI
{
    /// <summary>
    /// Driver settings panel, toggled by the left controller's menu button or the left-hand menu gesture.
    /// Every change applies and saves immediately; Reset restores the defaults. While open, the panel lazily
    /// follows the player's head. Built by the "XR Racing/Build Driver Settings Menu" editor command.
    /// </summary>
    public class DriverSettingsMenu : MonoBehaviour
    {
        [Serializable]
        public class PedalSelector
        {
            public Toggle accelerate;
            public Toggle brake;

            private TMP_Text _accelerateLabel;
            private TMP_Text _brakeLabel;
            private Color _offColor;

            public PedalAction Value => brake != null && brake.isOn ? PedalAction.Brake : PedalAction.Accelerate;

            public void Init(Action onChanged)
            {
                _accelerateLabel = FindLabel(accelerate);
                _brakeLabel = FindLabel(brake);
                _offColor = _accelerateLabel != null ? _accelerateLabel.color : Color.white;

                // A toggle group fires for both the one turning off and the one turning on; react once.
                accelerate.onValueChanged.AddListener(isOn => { if (isOn) onChanged(); });
                brake.onValueChanged.AddListener(isOn => { if (isOn) onChanged(); });
            }

            public void Show(PedalAction action)
            {
                accelerate.SetIsOnWithoutNotify(action == PedalAction.Accelerate);
                brake.SetIsOnWithoutNotify(action == PedalAction.Brake);
            }

            /// <summary>White text on the selected (blue) option. Run after the tiles' Animators.</summary>
            public void UpdateLabelColors(Color selectedColor)
            {
                if (_accelerateLabel != null)
                {
                    _accelerateLabel.color = accelerate.isOn ? selectedColor : _offColor;
                }

                if (_brakeLabel != null)
                {
                    _brakeLabel.color = brake.isOn ? selectedColor : _offColor;
                }
            }
        }

        // The UISet toggle tile's main text.
        private static TMP_Text FindLabel(Toggle toggle)
        {
            foreach (TMP_Text text in toggle.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.name == "Label")
                {
                    return text;
                }
            }

            return null;
        }

        [Tooltip("The panel shown/hidden by the menu button. Must not be this GameObject.")]
        [SerializeField] private GameObject panel;
        [SerializeField] private DriverSeatAdjuster seat;
        [SerializeField] private Slider heightSlider;
        [SerializeField] private TMP_Text heightValue;
        [SerializeField] private Slider distanceSlider;
        [SerializeField] private TMP_Text distanceValue;
        [SerializeField] private Button recenterButton;
        [SerializeField] private PedalSelector leftIndex;
        [SerializeField] private PedalSelector leftThumb;
        [SerializeField] private PedalSelector rightIndex;
        [SerializeField] private PedalSelector rightThumb;
        [SerializeField] private Button resetButton;
        [Tooltip("Switches tracks when a track tile is picked.")]
        [SerializeField] private TrackLoader trackLoader;
        [Tooltip("One tile per TrackLoader track, in the same order.")]
        [SerializeField] private Toggle[] trackToggles = new Toggle[0];
        [Tooltip("Puts the kart back at the track's starting point (with a fade).")]
        [SerializeField] private Button respawnButton;
        [Tooltip("Text color of the selected Accel/Brake and track option.")]
        [SerializeField] private Color selectedTextColor = Color.white;
        [Tooltip("How far in front of the head the panel sits (meters).")]
        [SerializeField] private float distanceFromHead = 0.5f;
        [Tooltip("How far below eye level the panel's center sits (meters).")]
        [SerializeField] private float belowEyes = 0.1f;
        [Tooltip("The panel starts following once it's this many degrees away from where the head is facing.")]
        [SerializeField] private float followAngle = 25f;
        [Tooltip("...or once the head has moved this far (meters) from where the panel expects it.")]
        [SerializeField] private float followDistance = 0.2f;
        [Tooltip("Seconds the panel takes to catch up while following.")]
        [SerializeField] private float followTime = 0.3f;
        [Tooltip("Following stops once the panel is this close (meters) to its spot in front of the head...")]
        [SerializeField, Min(0f)] private float followSettleDistance = 0.05f;
        [Tooltip("...and turned within this many degrees of facing the head.")]
        [SerializeField, Min(0f)] private float followSettleAngle = 5f;
        [Tooltip("Depth (meters) of the panel's ray/poke hit area around its surface. Must stay above float precision " +
            "at the track's distance from the world origin, or rays randomly miss the panel.")]
        [SerializeField, Min(0.0001f)] private float pointerSurfaceDepth = 0.002f;
        [Tooltip("Temporary: log the pointer rays to Logs/menu_ray.csv while the menu is open (dev builds only).")]
        [SerializeField] private bool debugRayLog = true;

        private DriverSettings _settings;
        private bool _following;
        private Vector3 _followVelocity;
        private TMP_Text[] _trackLabels = new TMP_Text[0];
        private Color _trackOffColor = Color.white;
        private IInteractableView[] _panelInteractables = new IInteractableView[0];
        private readonly List<(GameObject go, int layer)> _handLayers = new List<(GameObject, int)>();

        /// <summary>True while the panel is showing; the kart ignores pedals meanwhile.</summary>
        public static bool IsOpen { get; private set; }

        private void Awake()
        {
            heightSlider.onValueChanged.AddListener(value => OnSeatSliderChanged());
            distanceSlider.onValueChanged.AddListener(value => OnSeatSliderChanged());
            recenterButton.onClick.AddListener(OnRecenter);
            resetButton.onClick.AddListener(OnReset);
            if (respawnButton != null)
            {
                respawnButton.onClick.AddListener(OnRespawn);
            }

            leftIndex.Init(OnPedalsChanged);
            leftThumb.Init(OnPedalsChanged);
            rightIndex.Init(OnPedalsChanged);
            rightThumb.Init(OnPedalsChanged);
            InitTrackToggles();
            _panelInteractables = panel.GetComponentsInChildren<IInteractableView>(true); // the panel's ray and poke targets
            DrawOnTop();
            ThickenPointerSurface();
            panel.SetActive(false);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugRayLog)
            {
                gameObject.AddComponent<MenuRayDebugLog>().Init(panel, seat != null ? seat.transform : null, GetHead());
            }
#endif
        }

        private void OnEnable()
        {
            if (trackLoader != null)
            {
                trackLoader.TrackChanged += ShowTrack;
            }
        }

        private void OnDisable()
        {
            if (trackLoader != null)
            {
                trackLoader.TrackChanged -= ShowTrack;
            }

            IsOpen = false;
            SetHandsOnTop(false);
        }

        private void Update()
        {
            // Button.Start is the left controller's menu button and the left-hand menu (palm pinch) gesture.
            if (OVRInput.GetDown(OVRInput.Button.Start))
            {
                if (IsOpen)
                {
                    Close();
                }
                else
                {
                    Open();
                }
            }
        }

        private void LateUpdate()
        {
            if (!IsOpen)
            {
                return;
            }

            leftIndex.UpdateLabelColors(selectedTextColor);
            leftThumb.UpdateLabelColors(selectedTextColor);
            rightIndex.UpdateLabelColors(selectedTextColor);
            rightThumb.UpdateLabelColors(selectedTextColor);
            UpdateTrackLabelColors();
            FollowHead();
        }

        public void Open()
        {
            _settings = DriverSettings.Current.Clone();
            ShowSettings();
            ShowTrack(trackLoader != null ? trackLoader.CurrentIndex : -1);

            PlaceInFrontOfHead();
            panel.SetActive(true);
            SetHandsOnTop(true);
            IsOpen = true;
        }

        /// <summary>Hides the panel. Changes are already saved.</summary>
        public void Close()
        {
            panel.SetActive(false);
            SetHandsOnTop(false);
            IsOpen = false;
        }

        private void ShowSettings()
        {
            heightSlider.SetValueWithoutNotify(_settings.Height);
            distanceSlider.SetValueWithoutNotify(_settings.Distance);
            UpdateValueLabels();
            leftIndex.Show(_settings.LeftIndex);
            leftThumb.Show(_settings.LeftThumb);
            rightIndex.Show(_settings.RightIndex);
            rightThumb.Show(_settings.RightThumb);
        }

        private void OnSeatSliderChanged()
        {
            _settings.Height = DriverSettings.ClampStep(Mathf.RoundToInt(heightSlider.value));
            _settings.Distance = DriverSettings.ClampStep(Mathf.RoundToInt(distanceSlider.value));
            UpdateValueLabels();
            DriverSettings.Save(_settings); // the seat glides there via DriverSettings.Changed
        }

        private void OnPedalsChanged()
        {
            _settings.LeftIndex = leftIndex.Value;
            _settings.LeftThumb = leftThumb.Value;
            _settings.RightIndex = rightIndex.Value;
            _settings.RightThumb = rightThumb.Value;
            DriverSettings.Save(_settings);
        }

        private void OnRecenter()
        {
            if (seat != null && seat.Recenter(_settings))
            {
                // Recenter is a fresh starting point, so fine-tuning goes back to the middle.
                _settings.Height = DriverSettings.DefaultStep;
                _settings.Distance = DriverSettings.DefaultStep;
                ShowSettings();

                // Always fades (even when the seat barely moves) so every press visibly does something; the panel
                // is re-placed in the new view while black.
                seat.Apply(_settings, PlaceInFrontOfHead, true);
                DriverSettings.Save(_settings);
            }
        }

        private void OnReset()
        {
            _settings.ResetToDefaults();
            ShowSettings();
            DriverSettings.Save(_settings);
        }

        // Back to the start line; the menu closes while the view is black so the player can drive straight away.
        private void OnRespawn()
        {
            if (trackLoader != null)
            {
                trackLoader.RequestRespawn(Close);
            }
        }

        private void InitTrackToggles()
        {
            _trackLabels = new TMP_Text[trackToggles.Length];
            for (int i = 0; i < trackToggles.Length; i++)
            {
                int index = i;
                _trackLabels[i] = FindLabel(trackToggles[i]);
                // A toggle group fires for both the one turning off and the one turning on; react once.
                trackToggles[i].onValueChanged.AddListener(isOn =>
                {
                    if (isOn && trackLoader != null)
                    {
                        trackLoader.RequestTrack(index);
                    }
                });
            }

            if (_trackLabels.Length > 0 && _trackLabels[0] != null)
            {
                _trackOffColor = _trackLabels[0].color;
            }
        }

        // Marks the loaded track's tile (also re-syncs after a pick was ignored because a switch was running).
        private void ShowTrack(int index)
        {
            for (int i = 0; i < trackToggles.Length; i++)
            {
                trackToggles[i].SetIsOnWithoutNotify(i == index);
            }
        }

        private void UpdateTrackLabelColors()
        {
            for (int i = 0; i < _trackLabels.Length; i++)
            {
                if (_trackLabels[i] != null)
                {
                    _trackLabels[i].color = trackToggles[i].isOn ? selectedTextColor : _trackOffColor;
                }
            }
        }

        private void UpdateValueLabels()
        {
            heightValue.text = _settings.Height.ToString();
            distanceValue.text = _settings.Distance.ToString();
        }

        private Transform GetHead()
        {
            Transform head = seat != null ? seat.Head : null;
            if (head == null && Camera.main != null)
            {
                head = Camera.main.transform;
            }

            return head;
        }

        // Where the panel should be: in front of the head (level, ignoring head pitch), facing it.
        private bool GetTargetPose(out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = default;

            Transform head = GetHead();
            if (head == null)
            {
                return false;
            }

            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(head.up, Vector3.up);
            }

            forward.Normalize();
            position = head.position + forward * distanceFromHead - Vector3.up * belowEyes;
            rotation = Quaternion.LookRotation(forward, Vector3.up);
            return true;
        }

        // Opens facing the player, parented to the camera rig: the same space as the controllers and hands, so the
        // rig's bounce smoothing (VRCameraHeightSmoother) doesn't shift the panel relative to the pointing ray.
        private void PlaceInFrontOfHead()
        {
            if (!GetTargetPose(out Vector3 position, out Quaternion rotation))
            {
                return;
            }

            if (seat != null)
            {
                panel.transform.SetParent(seat.transform, true);
            }

            panel.transform.SetPositionAndRotation(position, rotation);
            _following = false;
            _followVelocity = Vector3.zero;
        }

        // Lazy follow: the panel holds still while it's roughly in view (so it's easy to poke), and glides back
        // in front of the head once the player looks or moves away. Smoothed in the rig's space so driving
        // doesn't make it trail behind. Never moves while a ray or poke is pressing it.
        private void FollowHead()
        {
            if (IsBeingPressed())
            {
                _following = false;
                _followVelocity = Vector3.zero;
                return;
            }

            Transform head = GetHead();
            if (head == null || !GetTargetPose(out Vector3 targetPosition, out Quaternion targetRotation))
            {
                return;
            }

            Transform panelTransform = panel.transform;
            Vector3 toPanel = Vector3.ProjectOnPlane(panelTransform.position - head.position, Vector3.up);
            Vector3 headForward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            float angle = Vector3.Angle(headForward, toPanel);
            float distanceError = Vector3.Distance(panelTransform.position, targetPosition);

            if (!_following && (angle > followAngle || distanceError > followDistance))
            {
                _following = true;
            }

            if (!_following)
            {
                return;
            }

            Transform space = panelTransform.parent;
            Vector3 local = space != null ? space.InverseTransformPoint(panelTransform.position) : panelTransform.position;
            Vector3 targetLocal = space != null ? space.InverseTransformPoint(targetPosition) : targetPosition;
            local = Vector3.SmoothDamp(local, targetLocal, ref _followVelocity, followTime);

            panelTransform.position = space != null ? space.TransformPoint(local) : local;
            panelTransform.rotation = Quaternion.Slerp(panelTransform.rotation, targetRotation,
                1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, followTime * 0.5f)));

            // Stop once roughly in place rather than chasing every small head movement.
            if (Vector3.Distance(local, targetLocal) < followSettleDistance &&
                Quaternion.Angle(panelTransform.rotation, targetRotation) < followSettleAngle)
            {
                _following = false;
                _followVelocity = Vector3.zero;
            }
        }

        // The panel and the pointer rays (their line and cursor) go on the overlay layer, which is drawn over the kart
        // and track; otherwise the cursor dot on the panel would be hidden under it.
        private void DrawOnTop()
        {
            OverlayLayer.Apply(panel);

            foreach (MonoBehaviour visual in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            {
                if (visual is ControllerRayVisual || visual is RayInteractorRayVisual || visual is RayInteractorCursorVisual ||
                    visual is HandRayInteractorCursorVisual || visual is RayInteractorPinchVisual)
                {
                    OverlayLayer.Apply(visual.gameObject);
                }
            }
        }

        // The panel draws over everything, which also hides the hands reaching in to poke it. While it's open the hands
        // go on the overlay layer too (drawn after the panel, being nearer); restored on close so the kart's body and
        // wheel still cover them while driving.
        private void SetHandsOnTop(bool onTop)
        {
            if (!onTop)
            {
                foreach ((GameObject go, int layer) in _handLayers)
                {
                    if (go != null)
                    {
                        go.layer = layer;
                    }
                }

                _handLayers.Clear();
                return;
            }

            int overlay = OverlayLayer.Index;
            if (overlay < 0 || _handLayers.Count > 0)
            {
                return;
            }

            foreach (HandVisual hand in FindObjectsByType<HandVisual>(FindObjectsInactive.Include))
            {
                foreach (Renderer renderer in hand.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.gameObject.layer == overlay)
                    {
                        continue; // already moved (nested hand visuals)
                    }

                    _handLayers.Add((renderer.gameObject, renderer.gameObject.layer));
                    renderer.gameObject.layer = overlay;
                }
            }
        }

        // The ray/poke surface only counts hits within its clip box, which Meta's RectTransformBoundsClipperDriver makes
        // 0.01 canvas units deep: 10 micrometers at the panel's 0.001 scale. Far from the world origin (the Playground
        // spawn is ~180 m out) float precision is coarser than that, so rays randomly missed the panel every few frames
        // (the "blinking" ray). Give the clip box a real depth instead; the driver would reset it, so it goes.
        private void ThickenPointerSurface()
        {
            foreach (RectTransformBoundsClipperDriver driver in panel.GetComponentsInChildren<RectTransformBoundsClipperDriver>(true))
            {
                BoundsClipper clipper = driver.GetComponent<BoundsClipper>();
                if (clipper == null)
                {
                    continue;
                }

                float scaleZ = Mathf.Abs(clipper.transform.lossyScale.z);
                float depth = scaleZ > 0f ? pointerSurfaceDepth / scaleZ : clipper.Size.z;
                clipper.Size = new Vector3(clipper.Size.x, clipper.Size.y, depth);
                Destroy(driver);
            }
        }

        // True while any ray or poke is pressing the panel (a button press or slider drag). Hovering alone doesn't
        // count: a controller resting in the lap often points at the panel, which would stop it ever following.
        private bool IsBeingPressed()
        {
            foreach (IInteractableView view in _panelInteractables)
            {
                if (view.State == InteractableState.Select)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
