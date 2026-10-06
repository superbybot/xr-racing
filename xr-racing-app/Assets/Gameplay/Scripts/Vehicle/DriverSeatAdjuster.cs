using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using XrRacing.Gameplay.Settings;

namespace XrRacing.Gameplay.Vehicle
{
    /// <summary>
    /// Positions the VR camera rig in the kart from DriverSettings. Recenter snaps the player's current head
    /// to the driver eye point (works whether they stand or sit, whatever their height); Height and Distance
    /// then fine-tune from there in fixed steps. Sits on the camera rig next to VRCameraHeightSmoother.
    /// Small moves (a Height/Distance step) glide; big moves (Recenter) dip to black, jump, and fade back,
    /// since a long glide of the whole view is uncomfortable in VR.
    /// </summary>
    [RequireComponent(typeof(VRCameraHeightSmoother))]
    [DefaultExecutionOrder(-300)] // before VRCameraHeightSmoother, which places the rig using SeatOffset
    public class DriverSeatAdjuster : MonoBehaviour
    {
        [Tooltip("Where the driver's eyes should be, as a child of the kart. Its forward is the kart's forward.")]
        [SerializeField] private Transform eyePoint;
        [Tooltip("Player head. Defaults to the OVRCameraRig's center eye.")]
        [SerializeField] private Transform head;
        [Tooltip("Meters moved per Height / Distance step away from the default (5).")]
        [SerializeField] private float stepMeters = 0.03f;
        [Tooltip("Seconds a small seat move takes to glide into place.")]
        [SerializeField] private float glideTime = 0.15f;
        [Tooltip("Moves farther than this (meters), or any turn, fade to black instead of gliding. Kept above the full " +
            "Height/Distance range (9 steps) so dragging a slider fast still glides.")]
        [SerializeField] private float fadeAboveMeters = 0.35f;
        [Tooltip("Seconds to fade out, and again to fade back in, around a big move.")]
        [SerializeField] private float fadeTime = 0.1f;

        private const float FadeAboveDegrees = 1f;

        private VRCameraHeightSmoother _smoother;
        private Quaternion _baseLocalRotation;
        private Vector3 _targetOffset;
        private Quaternion _targetRotation = Quaternion.identity;
        private Vector3 _glideVelocity;
        private Coroutine _fadeMove;
        private Image _fade;

        public Transform Head => head;

        private void Awake()
        {
            _smoother = GetComponent<VRCameraHeightSmoother>();
            _baseLocalRotation = transform.localRotation;

            if (head == null)
            {
                OVRCameraRig rig = GetComponentInChildren<OVRCameraRig>();
                head = rig != null ? rig.centerEyeAnchor : null;
            }
        }

        private void OnEnable()
        {
            DriverSettings.Changed += Apply;
        }

        private void OnDisable()
        {
            DriverSettings.Changed -= Apply;
            _fadeMove = null;
            SetFade(0f);
        }

        private void Start()
        {
            ComputeTarget(DriverSettings.Current, out _targetOffset, out _targetRotation);
            JumpTo(_targetOffset, _targetRotation);
        }

        // Runs before VRCameraHeightSmoother.Update (see the execution order above).
        private void Update()
        {
            if (_fadeMove == null)
            {
                _smoother.SeatOffset = Vector3.SmoothDamp(_smoother.SeatOffset, _targetOffset, ref _glideVelocity, glideTime);
            }
        }

        /// <summary>Moves the rig to match the settings (also used to preview unsaved values).</summary>
        public void Apply(DriverSettings settings)
        {
            Apply(settings, null);
        }

        /// <summary>
        /// Moves the rig to match the settings. onMoved runs once the view is in its new place (during the black
        /// frame for a faded move), e.g. to re-place UI in front of the head.
        /// </summary>
        public void Apply(DriverSettings settings, Action onMoved)
        {
            Apply(settings, onMoved, false);
        }

        /// <summary>
        /// As above; forceFade fades even for a small move, so an action like Recenter always gives visible
        /// feedback (otherwise a recenter that barely moves the seat looks like nothing happened).
        /// </summary>
        public void Apply(DriverSettings settings, Action onMoved, bool forceFade)
        {
            if (_smoother == null)
            {
                return;
            }

            ComputeTarget(settings, out Vector3 offset, out Quaternion rotation);

            // Already headed there (e.g. saving re-applies what the menu just applied): don't restart a fade.
            if (_fadeMove != null && offset == _targetOffset && rotation == _targetRotation)
            {
                return;
            }

            _targetOffset = offset;
            _targetRotation = rotation;

            bool bigMove = forceFade || Vector3.Distance(_smoother.SeatOffset, offset) > fadeAboveMeters ||
                Quaternion.Angle(_smoother.SeatRotation, rotation) > FadeAboveDegrees;

            if (bigMove && isActiveAndEnabled)
            {
                if (_fadeMove != null)
                {
                    StopCoroutine(_fadeMove);
                }

                _fadeMove = StartCoroutine(FadeMove(offset, rotation, onMoved));
                return;
            }

            // Small move: rotation is unchanged (only Recenter turns the rig), Update glides the offset.
            _smoother.SeatRotation = rotation; // the smoother applies it (with tilt smoothing) in LateUpdate
            onMoved?.Invoke();
        }

        /// <summary>
        /// Fills in settings' recenter offset/yaw so the head, as it is right now, lands on the eye point facing
        /// the kart's forward. Returns false if there's no eye point or head to work with.
        /// </summary>
        public bool Recenter(DriverSettings settings)
        {
            if (eyePoint == null || head == null || transform.parent == null)
            {
                return false;
            }

            // Head pose relative to the rig itself, so the rig's current offset/yaw doesn't matter.
            Vector3 headInRig = transform.InverseTransformPoint(head.position);
            Vector3 headForwardInRig = transform.InverseTransformDirection(head.forward);
            float headYaw = Mathf.Atan2(headForwardInRig.x, headForwardInRig.z) * Mathf.Rad2Deg;

            // Eye point's yaw relative to the rig's base rotation, so the head ends up facing the eye point's forward.
            Vector3 eyeForwardInParent = transform.parent.InverseTransformDirection(eyePoint.forward);
            Vector3 eyeForwardInBase = Quaternion.Inverse(_baseLocalRotation) * eyeForwardInParent;
            float eyeYaw = Mathf.Atan2(eyeForwardInBase.x, eyeForwardInBase.z) * Mathf.Rad2Deg;

            float yaw = Mathf.DeltaAngle(0f, eyeYaw - headYaw);
            Quaternion localRotation = _baseLocalRotation * Quaternion.Euler(0f, yaw, 0f);
            Vector3 eyeInParent = transform.parent.InverseTransformPoint(eyePoint.position);
            Vector3 localPosition = eyeInParent - localRotation * Vector3.Scale(transform.localScale, headInRig);

            settings.RecenterYaw = yaw;
            settings.RecenterOffset = localPosition - _smoother.BaseLocalPosition;
            return true;
        }

        private void ComputeTarget(DriverSettings settings, out Vector3 offset, out Quaternion rotation)
        {
            // Kart-local axes, so the seat moves with the kart when it tilts.
            Vector3 up = Vector3.up;
            Vector3 forward = Vector3.forward;

            if (eyePoint != null && transform.parent != null)
            {
                forward = Vector3.ProjectOnPlane(transform.parent.InverseTransformDirection(eyePoint.forward), up).normalized;
            }

            Vector3 fineTune = up * ((settings.Height - DriverSettings.DefaultStep) * stepMeters)
                - forward * ((settings.Distance - DriverSettings.DefaultStep) * stepMeters);

            offset = settings.RecenterOffset + fineTune;
            rotation = _baseLocalRotation * Quaternion.Euler(0f, settings.RecenterYaw, 0f);
        }

        private void JumpTo(Vector3 offset, Quaternion rotation)
        {
            _glideVelocity = Vector3.zero;
            _smoother.SeatOffset = offset;
            _smoother.SeatRotation = rotation;
        }

        private IEnumerator FadeMove(Vector3 offset, Quaternion rotation, Action onMoved)
        {
            yield return FadeTo(1f);

            JumpTo(offset, rotation);
            yield return null; // let the rig and head settle in the new spot before anything reads them
            onMoved?.Invoke();

            yield return FadeTo(0f);
            _fadeMove = null;
        }

        /// <summary>
        /// Fades the view to black (alpha 1) or back to clear (alpha 0) over Fade Time, using the same fade as a
        /// Recenter. Used by TrackLoader around a track switch.
        /// </summary>
        public async UniTask FadeAsync(float alpha, CancellationToken cancellationToken)
        {
            float start = _fade != null ? _fade.color.a : 0f;
            for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
            {
                SetFade(Mathf.Lerp(start, alpha, t / fadeTime));
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            SetFade(alpha);
        }

        private IEnumerator FadeTo(float alpha)
        {
            float start = _fade != null ? _fade.color.a : 0f;
            for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
            {
                SetFade(Mathf.Lerp(start, alpha, t / fadeTime));
                yield return null;
            }

            SetFade(alpha);
        }

        private void SetFade(float alpha)
        {
            if (_fade == null)
            {
                if (alpha <= 0f || head == null)
                {
                    return;
                }

                _fade = CreateFade();
            }

            _fade.color = new Color(0f, 0f, 0f, alpha);
            _fade.enabled = alpha > 0f;
        }

        // A black world-space UI quad just past the near clip plane. UI/Default is always in builds, unlike the
        // shader OVRScreenFade looks up by name.
        private Image CreateFade()
        {
            Camera eyeCamera = head.GetComponent<Camera>();
            float distance = (eyeCamera != null ? eyeCamera.nearClipPlane : 0.05f) + 0.02f;

            var go = new GameObject("SeatMoveFade", typeof(RectTransform), typeof(Canvas), typeof(Image));
            // On the menu's draw-on-top layer when it exists, so a fade still covers the menu.
            int overlay = XrRacing.Gameplay.UI.OverlayLayer.Index;
            go.layer = overlay >= 0 ? overlay : head.gameObject.layer;
            go.transform.SetParent(head, false);
            go.transform.localPosition = new Vector3(0f, 0f, distance);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * 0.01f;

            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = short.MaxValue;
            ((RectTransform)go.transform).sizeDelta = new Vector2(200f, 200f); // 2 m square, covers the whole view

            Image image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }
    }
}
