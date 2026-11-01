using UnityEngine;

namespace XrRacing.Gameplay.Characters
{
    /// <summary>
    /// Turns and slides each finger across the palm after body retargeting, so the character's hands rest the way a
    /// hand holds a controller or wheel. CharacterTestSceneBuilder adds it with per-character values; all zeros leaves
    /// the tracked pose unchanged.
    /// </summary>
    // After the Movement SDK retargeter writes the pose (LateUpdate, order 0) and before MirrorTransforms (150).
    [DefaultExecutionOrder(110)]
    public class FingerSpreadTuner : MonoBehaviour
    {
        [SerializeField] private Animator _animator;

        [Header("Angle away from the middle finger (degrees); middle: toward the index finger")]
        [Range(-20f, 30f)] [SerializeField] private float _index = -15f;
        [Range(-20f, 30f)] [SerializeField] private float _middle;
        [Range(-20f, 30f)] [SerializeField] private float _ring;
        [Range(-20f, 30f)] [SerializeField] private float _little;

        [Header("Knuckle slide away from the middle finger (cm); middle: toward the index finger")]
        [Range(-2f, 5f)] [SerializeField] private float _indexOffset = 3.6f;
        [Range(-2f, 5f)] [SerializeField] private float _middleOffset = 0.45f;
        [Range(-2f, 5f)] [SerializeField] private float _ringOffset = 1.62f;
        [Range(-2f, 5f)] [SerializeField] private float _littleOffset = 5f;

        private struct Finger
        {
            public Transform Bone;
            // The finger moves away from this bone; the middle finger uses the ring finger so positive means toward
            // the index finger.
            public Transform AwayFrom;
        }

        // Per hand, in the order index, middle, ring, little.
        private Finger[,] _fingers;

        private readonly float[] _angles = new float[4];
        private readonly float[] _offsets = new float[4];
        private readonly Vector3[] _aways = new Vector3[4];
        private readonly Vector3[] _alongs = new Vector3[4];

        private void Awake()
        {
            if (_animator == null)
            {
                enabled = false;
                return;
            }

            _fingers = new Finger[2, 4];
            CreateHand(0, HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftMiddleProximal,
                HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftLittleProximal);
            CreateHand(1, HumanBodyBones.RightIndexProximal, HumanBodyBones.RightMiddleProximal,
                HumanBodyBones.RightRingProximal, HumanBodyBones.RightLittleProximal);
        }

        private void CreateHand(int side, HumanBodyBones index, HumanBodyBones middle, HumanBodyBones ring,
            HumanBodyBones little)
        {
            var middleBone = _animator.GetBoneTransform(middle);
            var ringBone = _animator.GetBoneTransform(ring);
            _fingers[side, 0] = new Finger { Bone = _animator.GetBoneTransform(index), AwayFrom = middleBone };
            _fingers[side, 1] = new Finger { Bone = middleBone, AwayFrom = ringBone };
            _fingers[side, 2] = new Finger { Bone = ringBone, AwayFrom = middleBone };
            _fingers[side, 3] = new Finger { Bone = _animator.GetBoneTransform(little), AwayFrom = middleBone };
        }

        private void LateUpdate()
        {
            _angles[0] = _index;
            _angles[1] = _middle;
            _angles[2] = _ring;
            _angles[3] = _little;

            _offsets[0] = _indexOffset;
            _offsets[1] = _middleOffset;
            _offsets[2] = _ringOffset;
            _offsets[3] = _littleOffset;

            // Directions are read before any finger moves, so moving the middle finger doesn't change where the others go.
            for (var side = 0; side < 2; side++)
            {
                for (var i = 0; i < 4; i++)
                {
                    var finger = _fingers[side, i];
                    _aways[i] = Vector3.zero;
                    if (finger.Bone == null || finger.AwayFrom == null || finger.Bone.childCount == 0)
                    {
                        continue;
                    }

                    _alongs[i] = finger.Bone.GetChild(0).position - finger.Bone.position;
                    _aways[i] = Vector3.ProjectOnPlane(finger.Bone.position - finger.AwayFrom.position, _alongs[i]);
                }

                for (var i = 0; i < 4; i++)
                {
                    if (_aways[i].sqrMagnitude < 1e-10f ||
                        (Mathf.Approximately(_angles[i], 0f) && Mathf.Approximately(_offsets[i], 0f)))
                    {
                        continue;
                    }

                    // The retargeter rewrites every joint each frame, so these changes don't accumulate.
                    var bone = _fingers[side, i].Bone;
                    bone.position += _aways[i].normalized * (_offsets[i] * 0.01f);
                    bone.rotation = Quaternion.AngleAxis(_angles[i], Vector3.Cross(_alongs[i], _aways[i]).normalized) *
                                    bone.rotation;
                }
            }
        }
    }
}
