using UnityEngine;
using UnityEngine.Rendering;

namespace XrRacing.Gameplay.Characters
{
    /// <summary>
    /// Hides the character's head from the first-person camera only, so the player doesn't see inside their own head, while other cameras (mirror, spectator) still see it.
    /// </summary>
    public class FirstPersonHeadHider : MonoBehaviour
    {
        [Tooltip("The character's humanoid Animator, used to find its head bone.")]
        [SerializeField] private Animator _animator;

        [Tooltip("The player's own view (the camera rig's center eye). Only this camera skips the head.")]
        [SerializeField] private Camera _firstPersonCamera;

        private Transform _headBone;
        private Vector3 _headScale;
        private bool _hidden;

        private void Awake()
        {
            _headBone = _animator != null ? _animator.GetBoneTransform(HumanBodyBones.Head) : null;

            if (_headBone == null || _firstPersonCamera == null)
            {
                enabled = false;
            }
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            RenderPipelineManager.endCameraRendering += OnEndCamera;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.endCameraRendering -= OnEndCamera;
            Restore();
        }

        private void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera != _firstPersonCamera || _hidden)
            {
                return;
            }

            _headScale = _headBone.localScale;
            _headBone.localScale = Vector3.zero;
            _hidden = true;
        }

        private void OnEndCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera == _firstPersonCamera)
            {
                Restore();
            }
        }

        private void Restore()
        {
            if (!_hidden)
            {
                return;
            }

            _headBone.localScale = _headScale;
            _hidden = false;
        }
    }
}
