using System;
using UnityEngine;

namespace XrRacing.Gameplay.Characters
{
    /// <summary>
    /// Poses the player character from the headset and the two hand anchors, for when body tracking can't: the Meta XR
    /// Simulator replays one recorded body pose instead of tracking the user, so the retargeted character stands frozen.
    /// The root follows the headset across the floor and turns with it, the head copies the headset's rotation, and each
    /// arm reaches its hand anchor with two-bone IK. Unless forced on, it switches itself off outside the Simulator and
    /// leaves the Movement SDK body tracking in charge.
    /// </summary>
    // Runs before MirrorTransforms (150) so the reflection copies this frame's pose.
    [DefaultExecutionOrder(100)]
    public class HeadAndHandsAvatarDriver : MonoBehaviour
    {
        public enum Mode
        {
            SimulatorOnly,
            Always,
            Off
        }

        [SerializeField] private Mode _mode = Mode.SimulatorOnly;
        [SerializeField] private Animator _animator;
        [SerializeField] private Transform _head;
        [SerializeField] private Transform _leftHand;
        [SerializeField] private Transform _rightHand;

        [Tooltip("Body-tracking components (retargeter, source data provider) to switch off while this driver runs.")]
        [SerializeField] private Behaviour[] _bodyTrackingBehaviours = Array.Empty<Behaviour>();

        [Tooltip("How far behind the eyes the character's root stands, in metres.")]
        [SerializeField] private float _neckOffset = 0.1f;

        [Tooltip("Distance from a hand anchor back to the wrist, along the anchor's forward axis, in metres.")]
        [SerializeField] private float _wristOffset = 0.06f;

        [Tooltip("Scale the character once, on the first tracked frame, so its eyes are at the player's eye height. " +
                 "Without it a shorter character's arms can't reach the player's hands.")]
        [SerializeField] private bool _matchPlayerHeight = true;

        [Tooltip("Height of the character's eyes above its head bone at its original scale, in metres.")]
        [SerializeField] private float _eyesAboveHeadBone = 0.08f;

        // Headset heights below this are treated as "not tracking yet" (the anchors start at the floor).
        private const float MinTrackedEyeHeight = 0.8f;

        private Arm _leftArm;
        private Arm _rightArm;
        private Transform _headBone;
        private Quaternion _headBoneInRoot;
        private float _floorHeight;
        private float _restEyeHeight;
        private Vector3 _restScale;
        private bool _heightMatched;

        private static bool IsSimulatorRuntime()
        {
#if UNITY_EDITOR
            // The Simulator's Activate menu points this variable at its own OpenXR runtime for the Editor process.
            var runtime = Environment.GetEnvironmentVariable("XR_SELECTED_RUNTIME_JSON");
            return runtime != null && runtime.IndexOf("simulator", StringComparison.OrdinalIgnoreCase) >= 0;
#else
            return false;
#endif
        }

        private void Awake()
        {
            var active = _mode == Mode.Always || (_mode == Mode.SimulatorOnly && IsSimulatorRuntime());
            if (!active || _animator == null || _head == null)
            {
                enabled = false;
                return;
            }

            foreach (var behaviour in _bodyTrackingBehaviours)
            {
                if (behaviour != null)
                {
                    behaviour.enabled = false;
                }
            }

            // Rest pose relationships are taken relative to the root, whose forward is the character's facing.
            var root = _animator.transform;
            _floorHeight = root.position.y;
            _headBone = _animator.GetBoneTransform(HumanBodyBones.Head);
            _restScale = root.localScale;
            if (_headBone != null)
            {
                _headBoneInRoot = Quaternion.Inverse(root.rotation) * _headBone.rotation;
                _restEyeHeight = _headBone.position.y - _floorHeight + _eyesAboveHeadBone;
            }

            _leftArm = Arm.Create(_animator, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm,
                HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleProximal, -1f);
            _rightArm = Arm.Create(_animator, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm,
                HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal, 1f);
        }

        private void LateUpdate()
        {
            var root = _animator.transform;
            var eyeHeight = _head.position.y - _floorHeight;
            if (_matchPlayerHeight && !_heightMatched && _restEyeHeight > 0f && eyeHeight > MinTrackedEyeHeight)
            {
                root.localScale = _restScale * (eyeHeight / _restEyeHeight);
                _heightMatched = true;
            }

            var facing = Vector3.ProjectOnPlane(_head.forward, Vector3.up);
            if (facing.sqrMagnitude > 1e-4f)
            {
                root.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
            }

            var rootPosition = _head.position - root.forward * _neckOffset;
            rootPosition.y = _floorHeight;
            root.position = rootPosition;

            if (_headBone != null)
            {
                _headBone.rotation = _head.rotation * _headBoneInRoot;
            }

            _leftArm?.Solve(root, _leftHand, _wristOffset);
            _rightArm?.Solve(root, _rightHand, _wristOffset);
        }

        private sealed class Arm
        {
            private Transform _upper;
            private Transform _lower;
            private Transform _hand;
            private Quaternion _upperRestLocal;
            private Quaternion _lowerRestLocal;
            private Quaternion _handFromAnchor;
            private float _side;

            public static Arm Create(Animator animator, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones hand,
                HumanBodyBones middleFinger, float side)
            {
                var arm = new Arm
                {
                    _upper = animator.GetBoneTransform(upper),
                    _lower = animator.GetBoneTransform(lower),
                    _hand = animator.GetBoneTransform(hand),
                    _side = side
                };
                if (arm._upper == null || arm._lower == null || arm._hand == null)
                {
                    return null;
                }

                arm._upperRestLocal = arm._upper.localRotation;
                arm._lowerRestLocal = arm._lower.localRotation;

                // Line the fingers up with the anchor's forward and the back of the hand with its up, matching how a
                // controller or tracked hand reports its pose.
                var root = animator.transform;
                var finger = animator.GetBoneTransform(middleFinger);
                var fingerDirection = finger != null
                    ? finger.position - arm._hand.position
                    : arm._hand.position - arm._lower.position;
                var fingerInRoot = Quaternion.Inverse(root.rotation) * fingerDirection.normalized;
                var handInRoot = Quaternion.Inverse(root.rotation) * arm._hand.rotation;
                arm._handFromAnchor = Quaternion.Inverse(Quaternion.LookRotation(fingerInRoot, Vector3.up)) * handInRoot;
                return arm;
            }

            public void Solve(Transform root, Transform anchor, float wristOffset)
            {
                if (anchor == null)
                {
                    return;
                }

                // Solve from the rest pose every frame so rotation errors can't build up.
                _upper.localRotation = _upperRestLocal;
                _lower.localRotation = _lowerRestLocal;

                // Bone lengths are measured every frame because the height match rescales the character.
                var shoulder = _upper.position;
                var upperLength = Vector3.Distance(shoulder, _lower.position);
                var lowerLength = Vector3.Distance(_lower.position, _hand.position);
                var target = anchor.position - anchor.forward * wristOffset;
                var toTarget = target - shoulder;
                var reach = Mathf.Clamp(toTarget.magnitude,
                    Mathf.Abs(upperLength - lowerLength) + 1e-3f, upperLength + lowerLength - 1e-3f);
                var direction = toTarget.sqrMagnitude > 1e-8f ? toTarget.normalized : -root.up;

                // Elbows point down, back and slightly out, like a relaxed arm.
                var pole = root.TransformDirection(new Vector3(0.3f * _side, -1f, -0.5f));
                var bendAxis = Vector3.ProjectOnPlane(pole, direction);
                if (bendAxis.sqrMagnitude < 1e-6f)
                {
                    bendAxis = Vector3.ProjectOnPlane(-root.forward, direction);
                }

                bendAxis.Normalize();

                var cosShoulder = (upperLength * upperLength + reach * reach - lowerLength * lowerLength) /
                                  (2f * upperLength * reach);
                var shoulderAngle = Mathf.Acos(Mathf.Clamp(cosShoulder, -1f, 1f));
                var elbow = shoulder + direction * (upperLength * Mathf.Cos(shoulderAngle)) +
                            bendAxis * (upperLength * Mathf.Sin(shoulderAngle));
                var wrist = shoulder + direction * reach;

                _upper.rotation = Quaternion.FromToRotation(_lower.position - shoulder, elbow - shoulder) * _upper.rotation;
                _lower.rotation = Quaternion.FromToRotation(_hand.position - _lower.position, wrist - _lower.position) *
                                  _lower.rotation;
                _hand.rotation = anchor.rotation * _handFromAnchor;
            }
        }
    }
}
