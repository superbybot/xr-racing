using UnityEngine;

namespace XrRacing.Gameplay.Vehicle
{
    /// <summary>
    /// Runs physics at the headset's refresh rate (72/90/120 Hz) instead of the project's fixed 50 Hz, so there's
    /// exactly one physics step per rendered frame. At 50 Hz on a 72 Hz display, frames alternate between 0 and 1
    /// steps, and the kart's stiff suspension oscillated every step (the kart pitched ~0.5° at 25 Hz, shaking the
    /// distant scenery). Keeps the project setting when no headset refresh rate is available (e.g. Editor without Link).
    /// Installs itself at startup, so it needs no scene setup.
    /// </summary>
    public class PhysicsRateMatcher : MonoBehaviour
    {
        [Tooltip("Ignore refresh rates outside this range (Hz) and keep the project's fixed timestep.")]
        [SerializeField] private Vector2 validRefreshRange = new Vector2(60f, 144f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<PhysicsRateMatcher>() == null)
            {
                var go = new GameObject(nameof(PhysicsRateMatcher));
                DontDestroyOnLoad(go);
                go.AddComponent<PhysicsRateMatcher>();
            }
        }

        private void OnEnable()
        {
            OVRManager.DisplayRefreshRateChanged += OnRefreshRateChanged;
            Apply(OVRManager.display != null ? OVRManager.display.displayFrequency : 0f);
        }

        private void OnDisable()
        {
            OVRManager.DisplayRefreshRateChanged -= OnRefreshRateChanged;
        }

        private void OnRefreshRateChanged(float fromHz, float toHz)
        {
            Apply(toHz);
        }

        private void Apply(float refreshHz)
        {
            if (refreshHz < validRefreshRange.x || refreshHz > validRefreshRange.y)
            {
                return;
            }

            Time.fixedDeltaTime = 1f / refreshHz;
            Debug.Log($"[PhysicsRateMatcher] Physics now runs at {refreshHz:F0} Hz to match the headset.");
        }
    }
}
