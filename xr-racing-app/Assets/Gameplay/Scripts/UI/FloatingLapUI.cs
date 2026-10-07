using System;
using TMPro;
using UnityEngine;
using XrRacing.Gameplay.UI;

namespace XrRacing.Gameplay.UI
{
    /// <summary>
    /// Displays a floating lap counter HUD card on the driver's left side view.
    /// Shows current / total laps, 'FINAL LAP!', and 'GOAAAAL!!!'.
    /// </summary>
    public class FloatingLapUI : MonoBehaviour
    {
        [Tooltip("Root panel GameObject for the floating lap HUD.")]
        [SerializeField] private GameObject panel;
        [Tooltip("Text component showing lap information.")]
        [SerializeField] private TMP_Text lapLabel;
        [Tooltip("Text component showing lap numbers (e.g. '1 / 2').")]
        [SerializeField] private TMP_Text lapValue;
        [Tooltip("Color of normal lap text.")]
        [SerializeField] private Color normalColor = Color.white;
        [Tooltip("Color of the FINAL LAP text.")]
        [SerializeField] private Color finalLapColor = new Color(1f, 0.75f, 0f, 1f); // Gold / amber accent
        [Tooltip("Color of the GOAAAAL text.")]
        [SerializeField] private Color goalColor = new Color(0.1f, 0.9f, 0.3f, 1f); // Green accent

        private void Awake()
        {
            if (panel != null)
            {
                OverlayLayer.Apply(panel);
                panel.SetActive(false);
            }
        }

        public void SetVisible(bool visible)
        {
            if (panel != null)
            {
                panel.SetActive(visible);
            }
        }

        /// <summary>Updates the display for a standard lap (e.g. '1 / 2 LAPS').</summary>
        public void ShowLap(int currentLap, int totalLaps)
        {
            SetVisible(true);

            if (lapLabel != null)
            {
                lapLabel.text = "LAP";
                lapLabel.color = normalColor;
            }

            if (lapValue != null)
            {
                lapValue.text = $"{currentLap} / {totalLaps}";
                lapValue.color = normalColor;
            }
        }

        /// <summary>Updates the display when entering the final lap.</summary>
        public void ShowFinalLap(int currentLap, int totalLaps)
        {
            SetVisible(true);

            if (lapLabel != null)
            {
                lapLabel.text = "FINAL LAP!";
                lapLabel.color = finalLapColor;
            }

            if (lapValue != null)
            {
                lapValue.text = $"{currentLap} / {totalLaps}";
                lapValue.color = finalLapColor;
            }
        }

        /// <summary>Updates the display when the race goal is crossed.</summary>
        public void ShowGoal()
        {
            SetVisible(true);

            if (lapLabel != null)
            {
                lapLabel.text = "GOAAAAL!!!";
                lapLabel.color = goalColor;
            }

            if (lapValue != null)
            {
                lapValue.text = "FINISHED";
                lapValue.color = goalColor;
            }
        }
    }
}
