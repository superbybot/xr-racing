using System;
using UnityEngine;

namespace XrRacing.Gameplay.Settings
{
    public enum PedalAction
    {
        Accelerate,
        Brake
    }

    /// <summary>
    /// Player driving preferences. Recenter and the pedal mapping are saved to PlayerPrefs; Height and Distance
    /// are per-session fine-tuning and always start at the default (5).
    /// </summary>
    [Serializable]
    public class DriverSettings
    {
        public const int MinStep = 1;
        public const int MaxStep = 10;
        public const int DefaultStep = 5;

        private const string Prefix = "xr-racing.driver.";

        /// <summary>Seat height, 1 (lowest) to 10 (tallest). Not saved: every session starts at 5.</summary>
        public int Height = DefaultStep;
        /// <summary>Distance from the wheel, 1 (closest) to 10 (farthest). Not saved: every session starts at 5.</summary>
        public int Distance = DefaultStep;

        public PedalAction LeftIndex = PedalAction.Accelerate;
        public PedalAction LeftThumb = PedalAction.Brake;
        public PedalAction RightIndex = PedalAction.Accelerate;
        public PedalAction RightThumb = PedalAction.Brake;

        /// <summary>Rig offset (kart-local) from the last Recenter Seat, before Height/Distance are applied.</summary>
        public Vector3 RecenterOffset;
        /// <summary>Rig yaw (degrees) from the last Recenter Seat.</summary>
        public float RecenterYaw;

        private static DriverSettings _current;

        /// <summary>The saved settings everything reads at runtime. Loaded from PlayerPrefs on first access.</summary>
        public static DriverSettings Current => _current ??= Load();

        /// <summary>Raised after new settings are saved.</summary>
        public static event Action<DriverSettings> Changed;

        public DriverSettings Clone()
        {
            return (DriverSettings)MemberwiseClone();
        }

        /// <summary>Height/Distance back to 5 and the default pedal mapping. Keeps the recenter.</summary>
        public void ResetToDefaults()
        {
            var defaults = new DriverSettings();
            Height = defaults.Height;
            Distance = defaults.Distance;
            LeftIndex = defaults.LeftIndex;
            LeftThumb = defaults.LeftThumb;
            RightIndex = defaults.RightIndex;
            RightThumb = defaults.RightThumb;
        }

        /// <summary>Makes these the current settings and saves the persistent parts to PlayerPrefs.</summary>
        public static void Save(DriverSettings settings)
        {
            _current = settings.Clone();

            PlayerPrefs.SetInt(Prefix + "leftIndex", (int)_current.LeftIndex);
            PlayerPrefs.SetInt(Prefix + "leftThumb", (int)_current.LeftThumb);
            PlayerPrefs.SetInt(Prefix + "rightIndex", (int)_current.RightIndex);
            PlayerPrefs.SetInt(Prefix + "rightThumb", (int)_current.RightThumb);
            PlayerPrefs.SetFloat(Prefix + "recenterX", _current.RecenterOffset.x);
            PlayerPrefs.SetFloat(Prefix + "recenterY", _current.RecenterOffset.y);
            PlayerPrefs.SetFloat(Prefix + "recenterZ", _current.RecenterOffset.z);
            PlayerPrefs.SetFloat(Prefix + "recenterYaw", _current.RecenterYaw);
            PlayerPrefs.Save();

            Changed?.Invoke(_current);
        }

        private static DriverSettings Load()
        {
            var defaults = new DriverSettings();

            // Height/Distance used to be saved; drop any old values so every session starts at 5.
            PlayerPrefs.DeleteKey(Prefix + "height");
            PlayerPrefs.DeleteKey(Prefix + "distance");

            return new DriverSettings
            {
                LeftIndex = LoadAction("leftIndex", defaults.LeftIndex),
                LeftThumb = LoadAction("leftThumb", defaults.LeftThumb),
                RightIndex = LoadAction("rightIndex", defaults.RightIndex),
                RightThumb = LoadAction("rightThumb", defaults.RightThumb),
                RecenterOffset = new Vector3(
                    PlayerPrefs.GetFloat(Prefix + "recenterX", 0f),
                    PlayerPrefs.GetFloat(Prefix + "recenterY", 0f),
                    PlayerPrefs.GetFloat(Prefix + "recenterZ", 0f)),
                RecenterYaw = PlayerPrefs.GetFloat(Prefix + "recenterYaw", 0f)
            };
        }

        private static PedalAction LoadAction(string key, PedalAction fallback)
        {
            int value = PlayerPrefs.GetInt(Prefix + key, (int)fallback);
            return Enum.IsDefined(typeof(PedalAction), value) ? (PedalAction)value : fallback;
        }

        public static int ClampStep(int step)
        {
            return Mathf.Clamp(step, MinStep, MaxStep);
        }
    }
}
