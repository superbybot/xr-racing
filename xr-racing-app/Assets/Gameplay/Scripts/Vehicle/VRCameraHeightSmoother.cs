using UnityEngine;

namespace XrRacing.Gameplay.Vehicle
{
    /// <summary>
    /// Keeps the kart's small, fast motion out of the VR view. The camera rig still follows the kart's position and
    /// steering, but its world height is low-pass filtered (suspension bounce) and so is its tilt (pitch and roll):
    /// fast wobble such as the suspension buzzing is filtered out, while slow tilts like hills and banked turns come
    /// through. The rig's own seat pose (SeatOffset / SeatRotation, set by DriverSeatAdjuster) is applied underneath.
    /// </summary>
    public class VRCameraHeightSmoother : MonoBehaviour
    {
        [Tooltip("The kart; defaults to the parent.")]
        [SerializeField] private Transform followTarget;
        [Tooltip("Seconds the view takes to follow the kart's up/down movement.")]
        [SerializeField, Min(0f)] private float smoothTime = 0.12f;
        [Tooltip("Max meters the view may lag the kart vertically.")]
        [SerializeField, Min(0f)] private float maxOffset = 0.1f;
        [Tooltip("Seconds the view takes to follow the kart's tilt. Higher filters more wobble but lags more on hills. 0 = no tilt smoothing.")]
        [SerializeField, Min(0f)] private float tiltSmoothTime = 0.3f;
        [Tooltip("Max degrees the view may lag the kart's tilt, so big sudden tilts (crashes, ramps) still come through.")]
        [SerializeField, Range(0f, 45f)] private float maxTiltLag = 10f;

        private Vector3 _baseLocalPosition;
        private float _smoothedY;
        private float _velocity;
        private Vector3 _smoothedUp = Vector3.up;
        private Transform _head;

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
                _smoothedUp = followTarget.up;
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

            // Tilt: follow the kart's up direction slowly, and counter-rotate the rig by the difference.
            Vector3 kartUp = followTarget.up;
            if (tiltSmoothTime > 0f)
            {
                _smoothedUp = Vector3.Slerp(_smoothedUp, kartUp, 1f - Mathf.Exp(-Time.deltaTime / tiltSmoothTime));
                float lag = Vector3.Angle(_smoothedUp, kartUp);
                if (lag > maxTiltLag)
                {
                    _smoothedUp = Vector3.Slerp(kartUp, _smoothedUp, maxTiltLag / lag);
                }
            }
            else
            {
                _smoothedUp = kartUp;
            }

            Quaternion correction = Quaternion.FromToRotation(kartUp, _smoothedUp);
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
