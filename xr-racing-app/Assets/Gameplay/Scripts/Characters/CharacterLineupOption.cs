using Oculus.Interaction;
using UnityEngine;

namespace XrRacing.Gameplay.Characters
{
    /// <summary>
    /// One display model in the character lineup. Pointing a controller ray at it smoothly enlarges it slightly;
    /// pulling the index trigger while pointing switches the player to that character.
    /// </summary>
    public class CharacterLineupOption : MonoBehaviour
    {
        [SerializeField] private CharacterLineup _lineup;
        [SerializeField] private int _index;
        [SerializeField] private RayInteractable _interactable;
        [SerializeField] private float _hoverScale = 1.08f;
        [SerializeField] private float _scaleSpeed = 8f;

        private Vector3 _restScale;
        private Vector3 _targetScale;

        private void Awake()
        {
            _restScale = transform.localScale;
            _targetScale = _restScale;
        }

        private void Update()
        {
            // Exponential ease toward the target so hovering grows the model smoothly and frame-rate independently.
            var t = 1f - Mathf.Exp(-_scaleSpeed * Time.deltaTime);
            transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, t);
        }

        private void OnEnable()
        {
            if (_interactable != null)
            {
                _interactable.WhenStateChanged += HandleStateChanged;
            }
        }

        private void OnDisable()
        {
            if (_interactable != null)
            {
                _interactable.WhenStateChanged -= HandleStateChanged;
            }

            _targetScale = _restScale;
            transform.localScale = _restScale;
        }

        private void HandleStateChanged(InteractableStateChangeArgs args)
        {
            var highlighted = args.NewState == InteractableState.Hover || args.NewState == InteractableState.Select;
            _targetScale = highlighted ? _restScale * _hoverScale : _restScale;

            if (args.NewState == InteractableState.Select && _lineup != null)
            {
                _lineup.Select(_index);
            }
        }
    }
}
