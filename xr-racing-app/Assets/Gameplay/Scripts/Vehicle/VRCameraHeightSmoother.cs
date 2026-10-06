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
    [DefaultExecutionOrder(-200)] // before OVRCameraRig, the interactors and the menu, so they all see this frame's rig pose
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

        [Header("Steering wheel")]
        [Tooltip("Moves with the view (not the kart's tilt), so it stays in the player's hands on ramps, jumps and " +
            "bumps. Defaults to the wheel of the kart's XRWheelInput.")]
        [SerializeField] private Transform steeringWheel;
        [Tooltip("How much of the kart's tilt the wheel shows, turning around its own hub so the rim barely moves " +
            "under the hands. Gives a feel of the kart pitching on ramps. 0 = none.")]
        [SerializeField, Range(0f, 1f)] private float wheelTiltFeel = 0.3f;
        [Tooltip("Max degrees of that tilt.")]
        [SerializeField, Range(0f, 30f)] private float wheelMaxTilt = 10f;
        [Tooltip("Seconds the wheel's tilt takes to follow, so bumps don't jolt it.")]
        [SerializeField, Min(0f)] private float wheelTiltSmoothTime = 0.2f;

        private Vector3 _baseLocalPosition;
        private float _smoothedY;
        private float _velocity;
        private Vector3 _smoothedUp = Vector3.up;
        private Vector3 _heading = Vector3.forward;
        private Transform _head;
        private Transform _wheelMount;
        private Vector3 _wheelMountLocalPosition;
        private Quaternion _wheelTilt = Quaternion.identity;

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

        // After every Awake, so XRWheelInput has cached the wheel's rest rotation.
        private void Start()
        {
            if (steeringWheel == null && followTarget != null)
            {
                XrRacing.Gameplay.Input.XRWheelInput wheelInput = followTarget.GetComponentInChildren<XrRacing.Gameplay.Input.XRWheelInput>(true);
                steeringWheel = wheelInput != null ? wheelInput.WheelTransform : null;
            }

            if (steeringWheel == null || steeringWheel.parent == null)
            {
                return;
            }

            // A mount between the wheel and its parent that this owns, so the wheel keeps its own local rotation
            // (XRWheelInput spins it) while the mount is steadied. The mount's axes match the old parent's.
            _wheelMount = new GameObject("SteeringWheelMount").transform;
            _wheelMount.SetParent(steeringWheel.parent, false);
            _wheelMount.localPosition = steeringWheel.localPosition;
            _wheelMountLocalPosition = steeringWheel.localPosition;
            steeringWheel.SetParent(_wheelMount, false);
            steeringWheel.localPosition = Vector3.zero;
        }

        // Update, not LateUpdate: the kart has already moved (physics and interpolation run before Update), and
        // moving the rig after the controllers have cast their rays makes the menu ray flicker.
        private void Update()
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
            Vector3 kartFixedPosition = transform.position;
            Quaternion kartFixedRotation = transform.rotation;
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

            SteadyWheel(kartFixedPosition, kartFixedRotation);
        }

        // The player's hands live in the rig's space, so the wheel gets exactly the rig's move away from the kart
        // (height smoothing, Horizon Lock): it stays where it sits relative to the player when the kart is level,
        // however the kart pitches or rolls. Otherwise a 40° ramp swings the wheel tens of cm out of the hands. A
        // small, smoothed share of the tilt is put back around the wheel's own hub, for the feel of the slope.
        private void SteadyWheel(Vector3 kartFixedPosition, Quaternion kartFixedRotation)
        {
            if (_wheelMount == null)
            {
                return;
            }

            // Rig's pose now vs where it would be if it were simply fixed to the kart.
            Quaternion rigDelta = transform.rotation * Quaternion.Inverse(kartFixedRotation);

            Transform mountParent = _wheelMount.parent;
            Vector3 position = transform.position + rigDelta * (mountParent.TransformPoint(_wheelMountLocalPosition) - kartFixedPosition);
            Quaternion rotation = rigDelta * mountParent.rotation;

            Quaternion tiltTarget = Quaternion.Slerp(Quaternion.identity, Quaternion.Inverse(rigDelta), wheelTiltFeel);
            tiltTarget.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f)
            {
                angle -= 360f;
            }

            tiltTarget = float.IsFinite(axis.x) ? Quaternion.AngleAxis(Mathf.Clamp(angle, -wheelMaxTilt, wheelMaxTilt), axis) : Quaternion.identity;
            _wheelTilt = wheelTiltSmoothTime > 0f
                ? Quaternion.Slerp(_wheelTilt, tiltTarget, 1f - Mathf.Exp(-Time.deltaTime / wheelTiltSmoothTime))
                : tiltTarget;

            _wheelMount.SetPositionAndRotation(position, _wheelTilt * rotation);
        }

        private void OnDisable()
        {
            if (_wheelMount != null)
            {
                _wheelMount.localPosition = _wheelMountLocalPosition;
                _wheelMount.localRotation = Quaternion.identity;
            }

            transform.localPosition = _baseLocalPosition + SeatOffset;
            transform.localRotation = SeatRotation;
        }
    }
}
