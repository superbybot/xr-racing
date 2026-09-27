using UnityEngine;

namespace XrRacing.Gameplay.Vehicle
{
    /// <summary>
    /// Low-pass filters the kart's vertical (world Y) movement for the VR camera rig to prevent suspension jitter from shaking the player's head.
    /// </summary>
    public class VRCameraHeightSmoother : MonoBehaviour
    {
        [SerializeField] private Transform followTarget; // the kart; defaults to parent if null
        [SerializeField] private float smoothTime = 0.12f;
        [SerializeField] private float maxOffset = 0.1f; // max vertical metres the rig may lag behind the kart

        private Vector3 _baseLocalPosition;
        private float _smoothedY;
        private float _velocity;

        /// <summary>The rig's local position as placed in the scene, before any seat offset.</summary>
        public Vector3 BaseLocalPosition => _baseLocalPosition;

        /// <summary>Extra local offset for seat adjustment (see DriverSeatAdjuster), applied on top of the base position.</summary>
        public Vector3 SeatOffset { get; set; }

        private void Awake()
        {
            if (followTarget == null)
            {
                followTarget = transform.parent;
            }

            _baseLocalPosition = transform.localPosition;

            if (followTarget != null)
            {
                _smoothedY = followTarget.position.y;
            }
        }

        private void LateUpdate()
        {
            if (followTarget == null)
            {
                return;
            }

            float targetY = followTarget.position.y;
            _smoothedY = Mathf.SmoothDamp(_smoothedY, targetY, ref _velocity, smoothTime);
            float offset = Mathf.Clamp(_smoothedY - targetY, -maxOffset, maxOffset);
            _smoothedY = targetY + offset;

            transform.localPosition = _baseLocalPosition + SeatOffset;
            transform.position += Vector3.up * offset;
        }

        private void OnDisable()
        {
            transform.localPosition = _baseLocalPosition + SeatOffset;
        }
    }
}
