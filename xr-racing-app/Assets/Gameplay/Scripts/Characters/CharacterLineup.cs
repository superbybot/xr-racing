using System;
using TMPro;
using UnityEngine;

namespace XrRacing.Gameplay.Characters
{
    /// <summary>
    /// Keeps exactly one of several player characters active, together with its mirror reflection, and highlights the
    /// matching label in the lineup. Each character is a complete body-tracked rig of its own, so switching is just
    /// enabling one and disabling the rest; the active one picks up the player's pose on its first tracked frame.
    /// </summary>
    public class CharacterLineup : MonoBehaviour
    {
        [Serializable]
        private struct Choice
        {
            public GameObject Player;
            public GameObject Reflection;
            public TMP_Text Label;
        }

        [SerializeField] private Choice[] _choices = Array.Empty<Choice>();
        [SerializeField] private int _selected;
        [SerializeField] private Color _selectedLabelColor = new(1f, 0.78f, 0.2f);
        [SerializeField] private Color _labelColor = Color.white;

        public int Selected => _selected;

        private void Start()
        {
            Apply();
        }

        public void Select(int index)
        {
            if (index < 0 || index >= _choices.Length || index == _selected)
            {
                return;
            }

            _selected = index;
            Apply();
        }

        private void Apply()
        {
            // Disable first so two characters never run body tracking in the same frame.
            for (var i = 0; i < _choices.Length; i++)
            {
                if (i != _selected)
                {
                    SetChoiceActive(_choices[i], false);
                }
            }

            if (_selected >= 0 && _selected < _choices.Length)
            {
                SetChoiceActive(_choices[_selected], true);
            }

            for (var i = 0; i < _choices.Length; i++)
            {
                if (_choices[i].Label != null)
                {
                    _choices[i].Label.color = i == _selected ? _selectedLabelColor : _labelColor;
                }
            }
        }

        private static void SetChoiceActive(Choice choice, bool active)
        {
            if (choice.Player != null)
            {
                choice.Player.SetActive(active);
            }

            if (choice.Reflection != null)
            {
                choice.Reflection.SetActive(active);
            }
        }
    }
}
