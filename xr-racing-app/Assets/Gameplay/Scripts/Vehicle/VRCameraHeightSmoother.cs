using UnityEngine;

namespace XrRacing.Gameplay.Vehicle
{
    /// <summary>
    /// Keeps the kart's tilt and small, fast motion out of the VR view. The camera rig still follows the kart's
    /// position and steering, but:
    ///   - Horizon Lock keeps the view level with the world while the kart pitches and rolls (ramps, slopes), so
    ///     the kart tilts around the player instead of the world tilting in front of them. 1 = fully level.
    ///   - Whatever tilt Horizon Lock lets through is lightly low-pass filtered (suspension buzz).
    ///   - World height is low-pass filtered (suspension bounce).
    /// The rig's own seat pose (SeatOffset / SeatRotation, set by DriverSeatAdjuster) is applied underneath.
    /// </summary>
    public class VRCameraHeightSmoother : MonoBehaviour
    {
        [Tooltip("The kart; defaults to the parent.")]
        [SerializeField] private Transform followTarget;
        [Tooltip("Seconds the view takes to follow the kart's up/down movement.")]
        [SerializeField, Min(0f)] private float smoothTime = 0.12f;
        [Tooltip("Max meters the view may lag the kart vertically.")]
        [SerializeField, Min(0f)] private float maxOffset = 0.1f;
        [Tooltip("How much the view stays level while the kart pitches and rolls. 1 = always level with the world " +
            "(most comfortable), 0 = tilts fully with the kart.")]
        [SerializeField, Range(0f, 1f)] private float horizonLock = 1f;
        [Tooltip("Seconds the view takes to follow the tilt Horizon Lock lets through; filters suspension buzz. " +
            "Keep short: a view that keeps rotating after the kart has settled is nauseating. 0 = no smoothing.")]
        [SerializeField, Min(0f)] private float tiltSmoothTime = 0.1f;
        [Tooltip("Max degrees the view may lag that tilt, so big sudden tilts (crashes) still come through at once.")]
        [SerializeField, Range(0f, 45f)] private float maxTiltLag = 5f;

        private Vector3 _baseLocalPosition;
        private float _smoothedY;
        private float _velocity;
        private Vector3 _smoothedUp = Vector3.up;
        private Vector3 _heading = Vector3.forward;
        private Transform _head;

        /// <summary>0..1, see the Horizon Lock tooltip. Settable at runtime (e.g. from a comfort setting).</summary>
        public float HorizonLock
        {
            get => horizonLock;
            set => horizonLock = Mathf.Clamp01(value);
        }

        /// <summary>The rig's local position as placed in the scene, before any seat offset.</summary>
        public Vector3 BaseLocalPosition => _baseLocalPosition;

        /// <summary>Extra local offset for seat adjustment (see DriverSeatAdjuster), applied on top of the base position.</summary>
        public Vector3 SeatOffset { get; set; }

        /// <summary>The rig's local rotation in the kart (see DriverSeatAdjuster); tilt smoothing is applied on top.</summary>
        public Quaternion SeatRotation { get; set; }

        private void Awake()
        {
            if (followTarget == null)
            {
                followTarget = transform.parent;
            }

            _baseLocalPosition = transform.localPosition;
            SeatRotation = transform.localRotation;

            OVRCameraRig rig = GetComponentInChildren<OVRCameraRig>();
            _head = rig != null ? rig.centerEyeAnchor : null;

            if (followTarget != null)
            {
                _smoothedY = followTarget.position.y;
                _smoothedUp = Vector3.Slerp(followTarget.up, Vector3.up, horizonLock);
            }
        }

        private void LateUpdate()
        {
            if (followTarget == null)
            {
                return;
            }

            // Height: lag the kart's vertical movement a little.
            float targetY = followTarget.position.y;
            _smoothedY = Mathf.SmoothDamp(_smoothedY, targetY, ref _velocity, smoothTime);
            float offset = Mathf.Clamp(_smoothedY - targetY, -maxOffset, maxOffset);
            _smoothedY = targetY + offset;

            transform.localPosition = _baseLocalPosition + SeatOffset;
            transform.localRotation = SeatRotation;
            transform.position += Vector3.up * offset;

            // Tilt: the view's target orientation is the kart's, leveled toward the world by Horizon Lock (same heading).
            Quaternion kartRotation = followTarget.rotation;
            Vector3 flatForward = Vector3.ProjectOnPlane(followTarget.forward, Vector3.up);
            if (flatForward.sqrMagnitude > 0.0001f) // keep the last heading while the kart points straight up/down
            {
                _heading = flatForward.normalized;
            }

            Quaternion level = Quaternion.LookRotation(_heading, Vector3.up);
            Quaternion target = Quaternion.Slerp(kartRotation, level, horizonLock);
            Vector3 targetUp = target * Vector3.up;

            // Lightly smooth the tilt that remains (nothing left to smooth at full lock).
            if (tiltSmoothTime > 0f)
            {
                _smoothedUp = Vector3.Slerp(_smoothedUp, targetUp, 1f - Mathf.Exp(-Time.deltaTime / tiltSmoothTime));
                float lag = Vector3.Angle(_smoothedUp, targetUp);
                if (lag > maxTiltLag)
                {
                    _smoothedUp = Vector3.Slerp(targetUp, _smoothedUp, maxTiltLag / lag);
                }
            }
            else
            {
                _smoothedUp = targetUp;
            }

            // Counter-rotate the rig from the kart's orientation to the target, plus the (small) smoothing lag.
            Quaternion correction = Quaternion.FromToRotation(targetUp, _smoothedUp) * target * Quaternion.Inverse(kartRotation);
            if (correction != Quaternion.identity)
            {
                // Pivot around the head so filtering the tilt doesn't swing the viewpoint.
                Vector3 pivot = _head != null ? _head.position : transform.position;
                transform.SetPositionAndRotation(
                    pivot + correction * (transform.position - pivot),
                    correction * transform.rotation);
            }
        }

        private void OnDisable()
        {
            transform.localPosition = _baseLocalPosition + SeatOffset;
            transform.localRotation = SeatRotation;
        }
    }
}
