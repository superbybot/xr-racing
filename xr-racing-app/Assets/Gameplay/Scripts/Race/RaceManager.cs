using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using KartGame.KartSystems;
using UnityEngine;
using XrRacing.Gameplay.Tracks;
using XrRacing.Gameplay.UI;

namespace XrRacing.Gameplay.Race
{
    public enum RaceState
    {
        None,       // Playground / Free roam
        Ready,      // Placed at spawn
        Countdown,  // 3, 2, 1, START!
        Racing,     // Race active
        Finished    // Cross goal, controls locked
    }

    /// <summary>
    /// Central manager for the racing mechanics: start countdown, lap tracking, goal detection,
    /// and track-scoped state management (disabling race logic for Playground).
    /// </summary>
    public class RaceManager : MonoBehaviour
    {
        [Tooltip("The player's ArcadeKart.")]
        [SerializeField] private ArcadeKart kart;
        [Tooltip("TrackLoader for track changes and respawns.")]
        [SerializeField] private TrackLoader trackLoader;
        [Tooltip("LapTracker component on the kart.")]
        [SerializeField] private LapTracker lapTracker;
        [Tooltip("Countdown UI in front of the driver.")]
        [SerializeField] private RaceCountdownUI countdownUI;
        [Tooltip("Floating lap counter HUD on the driver's left side view.")]
        [SerializeField] private FloatingLapUI floatingLapUI;
        [Tooltip("Ending menu dialog presented after race completion.")]
        [SerializeField] private RaceEndingMenu endingMenu;
        [Tooltip("Audio source for race sound effects.")]
        [SerializeField] private AudioSource audioSource;
        [Tooltip("Fanfare audio clip played when crossing the goal.")]
        [SerializeField] private AudioClip finishClip;
        [Tooltip("Seconds after crossing the finish before the ending menu opens.")]
        [SerializeField] private float postRaceMenuDelay = 2.0f;

        private CancellationTokenSource _lifetime;

        public RaceState State { get; private set; } = RaceState.None;

        /// <summary>True during countdown or after race finish; suppresses player pedal input.</summary>
        public static bool IsControlsLocked { get; private set; }

        private void Awake()
        {
            _lifetime = new CancellationTokenSource();
            IsControlsLocked = false;
        }

        private void OnEnable()
        {
            if (trackLoader != null)
            {
                trackLoader.TrackChanged += OnTrackChanged;
                trackLoader.Respawned += OnRespawned;
            }

            if (lapTracker != null)
            {
                lapTracker.LapCompleted += OnLapCompleted;
                lapTracker.FinalLapReached += OnFinalLapReached;
                lapTracker.RaceCompleted += OnRaceCompleted;
            }
        }

        private void OnDisable()
        {
            if (trackLoader != null)
            {
                trackLoader.TrackChanged -= OnTrackChanged;
                trackLoader.Respawned -= OnRespawned;
            }

            if (lapTracker != null)
            {
                lapTracker.LapCompleted -= OnLapCompleted;
                lapTracker.FinalLapReached -= OnFinalLapReached;
                lapTracker.RaceCompleted -= OnRaceCompleted;
            }

            IsControlsLocked = false;
        }

        private void OnDestroy()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
            IsControlsLocked = false;
        }

        private void Start()
        {
            if (trackLoader != null && trackLoader.CurrentTrack != null)
            {
                SetupTrack(trackLoader.CurrentTrack);
            }
        }

        private void OnTrackChanged(int trackIndex)
        {
            if (trackLoader != null && trackLoader.CurrentTrack != null)
            {
                SetupTrack(trackLoader.CurrentTrack);
            }
        }

        private void OnRespawned()
        {
            if (trackLoader != null && trackLoader.CurrentTrack != null)
            {
                SetupTrack(trackLoader.CurrentTrack);
            }
        }

        private void SetupTrack(TrackInfo track)
        {
            if (track == null || IsPlayground(track))
            {
                EnterPlaygroundMode();
                return;
            }

            StartRaceSequence(track).Forget();
        }

        private bool IsPlayground(TrackInfo track)
        {
            return track == null ||
                   track.Checkpoints == null ||
                   track.Checkpoints.Length == 0 ||
                   track.DisplayName.IndexOf("Playground", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void EnterPlaygroundMode()
        {
            State = RaceState.None;
            IsControlsLocked = false;

            if (kart != null)
            {
                kart.SetCanMove(true);
            }

            if (countdownUI != null)
            {
                countdownUI.Cancel();
            }

            if (floatingLapUI != null)
            {
                floatingLapUI.SetVisible(false);
            }

            if (endingMenu != null)
            {
                endingMenu.Close();
            }

            if (lapTracker != null)
            {
                lapTracker.SetActive(false);
            }
        }

        private async UniTaskVoid StartRaceSequence(TrackInfo track)
        {
            State = RaceState.Countdown;
            IsControlsLocked = true;

            if (kart != null)
            {
                kart.SetCanMove(false);
                Rigidbody body = kart.GetComponent<Rigidbody>();
                if (body != null && !body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }

            if (endingMenu != null)
            {
                endingMenu.Close();
            }

            int laps = track.LapsToComplete;
            if (lapTracker != null)
            {
                lapTracker.Initialize(track, laps);
            }

            if (floatingLapUI != null)
            {
                floatingLapUI.ShowLap(1, laps);
            }

            if (countdownUI != null)
            {
                await countdownUI.PlayCountdownAsync(OnCountdownFinished, _lifetime.Token);
            }
            else
            {
                OnCountdownFinished();
            }
        }

        private void OnCountdownFinished()
        {
            if (State != RaceState.Countdown)
            {
                return;
            }

            State = RaceState.Racing;
            IsControlsLocked = false;

            if (kart != null)
            {
                kart.SetCanMove(true);
            }
        }

        private void OnLapCompleted(int currentLap, int totalLaps)
        {
            if (State != RaceState.Racing)
            {
                return;
            }

            if (floatingLapUI != null)
            {
                floatingLapUI.ShowLap(currentLap, totalLaps);
            }
        }

        private void OnFinalLapReached()
        {
            if (State != RaceState.Racing)
            {
                return;
            }

            if (floatingLapUI != null && lapTracker != null)
            {
                floatingLapUI.ShowFinalLap(lapTracker.CurrentLap, lapTracker.TotalLaps);
            }
        }

        private void OnRaceCompleted()
        {
            State = RaceState.Finished;
            IsControlsLocked = true;

            if (kart != null)
            {
                kart.SetCanMove(false);
            }

            if (floatingLapUI != null)
            {
                floatingLapUI.ShowGoal();
            }

            if (audioSource != null && finishClip != null)
            {
                audioSource.PlayOneShot(finishClip);
            }

            HandlePostRaceAsync().Forget();
        }

        private async UniTaskVoid HandlePostRaceAsync()
        {
            // Decelerate kart smoothly
            if (kart != null)
            {
                Rigidbody body = kart.GetComponent<Rigidbody>();
                if (body != null)
                {
                    float elapsed = 0f;
                    float duration = 1.5f;
                    Vector3 initialVel = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
                    while (elapsed < duration)
                    {
                        elapsed += Time.deltaTime;
                        if (body.isKinematic) break;
                        Vector3 horizontal = Vector3.Lerp(initialVel, Vector3.zero, elapsed / duration);
                        body.linearVelocity = new Vector3(horizontal.x, body.linearVelocity.y, horizontal.z);
                        await UniTask.Yield(PlayerLoopTiming.FixedUpdate, cancellationToken: _lifetime.Token);
                    }

                    if (!body.isKinematic)
                    {
                        body.linearVelocity = new Vector3(0f, body.linearVelocity.y, 0f);
                    }
                }
            }

            await UniTask.Delay(TimeSpan.FromSeconds(postRaceMenuDelay), cancellationToken: _lifetime.Token);

            if (State == RaceState.Finished && endingMenu != null)
            {
                endingMenu.Open();
            }
        }
    }
}
