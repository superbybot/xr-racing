using Meta.XR.Movement.AI;
using UnityEngine;

namespace XrRacing.Gameplay.Characters
{
    /// <summary>
    /// Slides the camera rig across the floor at the velocity the AI Motion Synthesizer's joystick input reports, so
    /// the player moves exactly as fast as the character's synthesized legs walk. Test-scene locomotion only; the game
    /// itself moves the player with XR Interaction Toolkit locomotion.
    /// </summary>
    public class JoystickRigMover : MonoBehaviour
    {
        [SerializeField] private Transform _rig;

        [Tooltip("Component implementing IAIMotionSynthesizerInputProvider, e.g. AIMotionSynthesizerJoystickInput.")]
        [SerializeField] private MonoBehaviour _inputProvider;

        private IAIMotionSynthesizerInputProvider _input;

        private void Awake()
        {
            _input = _inputProvider as IAIMotionSynthesizerInputProvider;
            if (_rig == null || _input == null)
            {
                enabled = false;
            }
        }

        private void Update()
        {
            var velocity = Vector3.ProjectOnPlane(_input.GetVelocity(), Vector3.up);
            _rig.position += velocity * Time.deltaTime;
        }
    }
}
