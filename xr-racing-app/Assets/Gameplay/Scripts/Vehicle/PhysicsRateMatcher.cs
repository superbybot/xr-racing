using UnityEngine;

namespace XrRacing.Gameplay.Vehicle
{
    /// <summary>
    /// Runs physics at the headset's refresh rate (72/90/120 Hz) instead of the project's fixed 50 Hz, so there's
    /// exactly one physics step per rendered frame. At 50 Hz on a 72 Hz display, frames alternate between 0 and 1
    /// steps, and the kart's stiff suspension oscillated every step (the kart pitched ~0.5° at 25 Hz, shaking the
    /// distant scenery). Keeps the project setting when no headset refresh rate is available (e.g. Editor without Link).
    /// Hooks itself up at startup, so it needs no scene setup.
    /// </summary>
    public static class PhysicsRateMatcher
    {
        private const float MinRefreshHz = 60f;
        private const float MaxRefreshHz = 144f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            // Unsubscribe first: with domain reload off, statics survive between Play sessions.
            OVRManager.DisplayRefreshRateChanged -= OnRefreshRateChanged;
            OVRManager.DisplayRefreshRateChanged += OnRefreshRateChanged;
            Apply(OVRManager.display != null ? OVRManager.display.displayFrequency : 0f);
        }

        private static void OnRefreshRateChanged(float fromHz, float toHz)
        {
            Apply(toHz);
        }

        private static void Apply(float refreshHz)
        {
            if (refreshHz < MinRefreshHz || refreshHz > MaxRefreshHz)
            {
                return;
            }

            Time.fixedDeltaTime = 1f / refreshHz;
            Debug.Log($"[PhysicsRateMatcher] Physics now runs at {refreshHz:F0} Hz to match the headset.");
        }
    }
}
