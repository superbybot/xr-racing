using System;
using UnityEngine;
using UnityEngine.UI;
using XrRacing.Gameplay.Tracks;
using XrRacing.Gameplay.Vehicle;

namespace XrRacing.Gameplay.UI
{
    /// <summary>
    /// Modal dialog presented to the player when a race finishes.
    /// Offers options to 'Play Again', return to 'Physics Playground', or open the driver settings.
    /// </summary>
    public class RaceEndingMenu : MonoBehaviour
    {
        [Tooltip("The root panel GameObject containing the canvas and backplate.")]
        [SerializeField] private GameObject panel;
        [Tooltip("Button to restart the race on the current track.")]
        [SerializeField] private Button playAgainButton;
        [Tooltip("Button to load the Physics Playground sandbox.")]
        [SerializeField] private Button playgroundButton;
        [Tooltip("Button to open the driver settings menu.")]
        [SerializeField] private Button settingsButton;
        [Tooltip("TrackLoader reference for switching tracks or respawning.")]
        [SerializeField] private TrackLoader trackLoader;
        [Tooltip("DriverSettingsMenu reference.")]
        [SerializeField] private DriverSettingsMenu settingsMenu;
        [Tooltip("DriverSeatAdjuster reference to position in front of driver head.")]
        [SerializeField] private DriverSeatAdjuster seat;
        [Tooltip("Distance in front of driver head (meters).")]
        [SerializeField] private float distanceFromHead = 0.6f;

        private void Awake()
        {
            if (playAgainButton != null)
            {
                playAgainButton.onClick.AddListener(OnPlayAgain);
            }

            if (playgroundButton != null)
            {
                playgroundButton.onClick.AddListener(OnBackToPlayground);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(OnOpenSettings);
            }

            if (panel != null)
            {
                OverlayLayer.Apply(panel);
                panel.SetActive(false);
            }
        }

        public void Open()
        {
            if (panel == null)
            {
                return;
            }

            PlaceInFrontOfHead();
            panel.SetActive(true);
        }

        public void Close()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        private void OnPlayAgain()
        {
            Close();
            if (trackLoader != null)
            {
                trackLoader.RequestRespawn();
            }
        }

        private void OnBackToPlayground()
        {
            Close();
            if (trackLoader == null)
            {
                return;
            }

            // Find Playground track
            for (int i = 0; i < trackLoader.Tracks.Count; i++)
            {
                if (trackLoader.Tracks[i].displayName.IndexOf("Playground", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    trackLoader.RequestTrack(i);
                    return;
                }
            }

            // Fallback to first track if named differently
            trackLoader.RequestTrack(0);
        }

        private void OnOpenSettings()
        {
            Close();
            if (settingsMenu != null)
            {
                settingsMenu.Open();
            }
        }

        private void PlaceInFrontOfHead()
        {
            Transform head = seat != null ? seat.Head : null;
            if (head == null && Camera.main != null)
            {
                head = Camera.main.transform;
            }

            if (head == null)
            {
                return;
            }

            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(head.up, Vector3.up);
            }

            forward.Normalize();
            Vector3 position = head.position + forward * distanceFromHead - Vector3.up * 0.05f;
            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);

            if (seat != null)
            {
                panel.transform.SetParent(seat.transform, true);
            }

            panel.transform.SetPositionAndRotation(position, rotation);
        }
    }
}
