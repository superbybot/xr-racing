using System;
using UnityEngine;
using XrRacing.Gameplay.Tracks;

namespace XrRacing.Gameplay.Race
{
    /// <summary>
    /// Tracks the kart's progress through track checkpoints and detects lap completion and race finish.
    /// Attached to the player kart (which has the Rigidbody and physics colliders).
    /// </summary>
    public class LapTracker : MonoBehaviour
    {
        private TrackInfo _track;
        private int _totalLaps = 2;
        private int _currentLap = 1;
        private int _lastCheckpointIndex = -1;
        private int _passedCheckpointsCount = 0;
        private bool _isActive = false;

        public int CurrentLap => _currentLap;
        public int TotalLaps => _totalLaps;
        public bool IsActive => _isActive;

        /// <summary>Raised when any valid track checkpoint is passed (checkpoint index).</summary>
        public event Action<int> CheckpointPassed;

        /// <summary>Raised when a lap is completed (new currentLap, totalLaps).</summary>
        public event Action<int, int> LapCompleted;

        /// <summary>Raised when entering the final lap.</summary>
        public event Action FinalLapReached;

        /// <summary>Raised when the final lap is finished (goal crossed).</summary>
        public event Action RaceCompleted;

        /// <summary>Configures the tracker for a new track and lap count.</summary>
        public void Initialize(TrackInfo track, int totalLaps)
        {
            _track = track;
            _totalLaps = Mathf.Max(1, totalLaps);
            ResetProgress();
            _isActive = _track != null && _track.Checkpoints != null && _track.Checkpoints.Length > 0;
        }

        /// <summary>Resets progress to Lap 1 at the starting line.</summary>
        public void ResetProgress()
        {
            _currentLap = 1;
            _lastCheckpointIndex = 0;
            _passedCheckpointsCount = 0;
        }

        public void SetActive(bool active)
        {
            _isActive = active;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_isActive || _track == null || _track.Checkpoints == null || _track.Checkpoints.Length == 0)
            {
                return;
            }

            int index = FindCheckpointIndex(other);
            if (index < 0)
            {
                return;
            }

            int count = _track.Checkpoints.Length;

            // Start/Finish line (checkpoint 0)
            if (index == 0)
            {
                // Must have cleared at least half the track's checkpoints to count as a legitimate lap
                int minRequired = Mathf.Max(1, count / 2);
                if (_passedCheckpointsCount >= minRequired)
                {
                    _passedCheckpointsCount = 0;
                    _lastCheckpointIndex = 0;

                    if (_currentLap >= _totalLaps)
                    {
                        _isActive = false;
                        RaceCompleted?.Invoke();
                    }
                    else
                    {
                        _currentLap++;
                        LapCompleted?.Invoke(_currentLap, _totalLaps);
                        if (_currentLap == _totalLaps)
                        {
                            FinalLapReached?.Invoke();
                        }
                    }
                }

                return;
            }

            // Intermediate checkpoint
            if (index > _lastCheckpointIndex || (_lastCheckpointIndex >= count - 2 && index < 3))
            {
                _lastCheckpointIndex = index;
                _passedCheckpointsCount++;
                CheckpointPassed?.Invoke(index);
            }
        }

        private int FindCheckpointIndex(Collider col)
        {
            Collider[] checkpoints = _track.Checkpoints;
            for (int i = 0; i < checkpoints.Length; i++)
            {
                if (checkpoints[i] == col)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
