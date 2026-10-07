using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XrRacing.Gameplay.UI;

namespace XrRacing.Gameplay.UI
{
    /// <summary>
    /// Displays a 3, 2, 1, START! countdown in front of the driver's view before a race starts.
    /// Uses Meta Interaction SDK UISet styling and fades out smoothly once the race begins.
    /// </summary>
    public class RaceCountdownUI : MonoBehaviour
    {
        [Tooltip("The root panel GameObject containing the canvas and backplate.")]
        [SerializeField] private GameObject panel;
        [Tooltip("Text component displaying the countdown numbers and START!")]
        [SerializeField] private TMP_Text countdownText;
        [Tooltip("CanvasGroup used to smoothly fade out the panel.")]
        [SerializeField] private CanvasGroup canvasGroup;
        [Tooltip("Optional AudioSource for countdown beeps.")]
        [SerializeField] private AudioSource audioSource;
        [Tooltip("Sound played on 3, 2, 1.")]
        [SerializeField] private AudioClip countClip;
        [Tooltip("Sound played on START!")]
        [SerializeField] private AudioClip startClip;
        [Tooltip("Seconds each count (3, 2, 1) remains on screen.")]
        [SerializeField] private float countDuration = 1.0f;
        [Tooltip("Seconds START! remains before fading.")]
        [SerializeField] private float startDuration = 0.8f;
        [Tooltip("Seconds to fade out after START!")]
        [SerializeField] private float fadeDuration = 0.5f;
        [Tooltip("Color of the 3, 2, 1 countdown text.")]
        [SerializeField] private Color countColor = Color.white;
        [Tooltip("Color of the START! text (Meta-style accent).")]
        [SerializeField] private Color startColor = new Color(0.0f, 0.65f, 1f, 1f);

        private CancellationTokenSource _countdownCts;

        private void Awake()
        {
            if (canvasGroup == null && panel != null)
            {
                canvasGroup = panel.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = panel.AddComponent<CanvasGroup>();
                }
            }

            if (panel != null)
            {
                OverlayLayer.Apply(panel);
                panel.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            Cancel();
        }

        public void Cancel()
        {
            if (_countdownCts != null)
            {
                _countdownCts.Cancel();
                _countdownCts.Dispose();
                _countdownCts = null;
            }

            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        /// <summary>
        /// Starts the countdown sequence. onStartAction fires immediately when "START!" appears.
        /// </summary>
        public async UniTask PlayCountdownAsync(Action onStartAction, CancellationToken cancellationToken)
        {
            Cancel();
            _countdownCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            CancellationToken token = _countdownCts.Token;

            if (panel == null || countdownText == null)
            {
                onStartAction?.Invoke();
                return;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }

            panel.SetActive(true);

            // 3, 2, 1
            for (int i = 3; i >= 1; i--)
            {
                countdownText.text = i.ToString();
                countdownText.color = countColor;
                PlaySound(countClip);

                await UniTask.Delay(TimeSpan.FromSeconds(countDuration), cancellationToken: token);
            }

            // START!
            countdownText.text = "START!";
            countdownText.color = startColor;
            PlaySound(startClip);

            // Signal game logic to unlock kart acceleration immediately
            onStartAction?.Invoke();

            await UniTask.Delay(TimeSpan.FromSeconds(startDuration), cancellationToken: token);

            // Smooth fade out
            if (canvasGroup != null)
            {
                float elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.deltaTime;
                    canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token);
                }

                canvasGroup.alpha = 0f;
            }

            panel.SetActive(false);
        }

        private void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }
    }
}
