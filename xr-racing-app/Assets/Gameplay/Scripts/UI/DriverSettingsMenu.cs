using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XrRacing.Gameplay.Settings;
using XrRacing.Gameplay.Vehicle;

namespace XrRacing.Gameplay.UI
{
    /// <summary>
    /// Driver settings panel, toggled by the left controller's menu button or the left-hand menu gesture.
    /// Every change applies and saves immediately; Reset restores the defaults. While open, the panel lazily
    /// follows the player's head. Built by the "XR Racing/Build Driver Settings Menu" editor command.
    /// </summary>
    public class DriverSettingsMenu : MonoBehaviour
    {
        [Serializable]
        public class PedalSelector
        {
            public Toggle accelerate;
            public Toggle brake;

            private TMP_Text _accelerateLabel;
            private TMP_Text _brakeLabel;
            private Color _offColor;

            public PedalAction Value => brake != null && brake.isOn ? PedalAction.Brake : PedalAction.Accelerate;

            public void Init(Action onChanged)
            {
                _accelerateLabel = FindLabel(accelerate);
                _brakeLabel = FindLabel(brake);
                _offColor = _accelerateLabel != null ? _accelerateLabel.color : Color.white;

                // A toggle group fires for both the one turning off and the one turning on; react once.
                accelerate.onValueChanged.AddListener(isOn => { if (isOn) onChanged(); });
                brake.onValueChanged.AddListener(isOn => { if (isOn) onChanged(); });
            }

            public void Show(PedalAction action)
            {
                accelerate.SetIsOnWithoutNotify(action == PedalAction.Accelerate);
                brake.SetIsOnWithoutNotify(action == PedalAction.Brake);
            }

            /// <summary>White text on the selected (blue) option. Run after the tiles' Animators.</summary>
            public void UpdateLabelColors(Color selectedColor)
            {
                if (_accelerateLabel != null)
                {
                    _accelerateLabel.color = accelerate.isOn ? selectedColor : _offColor;
                }

                if (_brakeLabel != null)
                {
                    _brakeLabel.color = brake.isOn ? selectedColor : _offColor;
                }
            }

            private static TMP_Text FindLabel(Toggle toggle)
            {
                foreach (TMP_Text text in toggle.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (text.name == "Label")
                    {
                        return text;
                    }
                }

                return null;
            }
        }

        [Tooltip("The panel shown/hidden by the menu button. Must not be this GameObject.")]
        [SerializeField] private GameObject panel;
        [SerializeField] private DriverSeatAdjuster seat;
        [SerializeField] private Slider heightSlider;
        [SerializeField] private TMP_Text heightValue;
        [SerializeField] private Slider distanceSlider;
        [SerializeField] private TMP_Text distanceValue;
        [SerializeField] private Button recenterButton;
        [SerializeField] private PedalSelector leftIndex;
        [SerializeField] private PedalSelector leftThumb;
        [SerializeField] private PedalSelector rightIndex;
        [SerializeField] private PedalSelector rightThumb;
        [SerializeField] private Button resetButton;
        [Tooltip("Text color of the selected Accel/Brake option.")]
        [SerializeField] private Color selectedTextColor = Color.white;
        [Tooltip("How far in front of the head the panel sits (meters).")]
        [SerializeField] private float distanceFromHead = 0.5f;
        [Tooltip("How far below eye level the panel's center sits (meters).")]
        [SerializeField] private float belowEyes = 0.1f;
        [Tooltip("The panel starts following once it's this many degrees away from where the head is facing.")]
        [SerializeField] private float followAngle = 25f;
        [Tooltip("...or once the head has moved this far (meters) from where the panel expects it.")]
        [SerializeField] private float followDistance = 0.2f;
        [Tooltip("Seconds the panel takes to catch up while following.")]
        [SerializeField] private float followTime = 0.3f;

        private DriverSettings _settings;
        private bool _following;
        private Vector3 _followVelocity;

        /// <summary>True while the panel is showing; the kart ignores pedals meanwhile.</summary>
        public static bool IsOpen { get; private set; }

        private void Awake()
        {
            heightSlider.onValueChanged.AddListener(value => OnSeatSliderChanged());
            distanceSlider.onValueChanged.AddListener(value => OnSeatSliderChanged());
            recenterButton.onClick.AddListener(OnRecenter);
            resetButton.onClick.AddListener(OnReset);
            leftIndex.Init(OnPedalsChanged);
            leftThumb.Init(OnPedalsChanged);
            rightIndex.Init(OnPedalsChanged);
            rightThumb.Init(OnPedalsChanged);
            panel.SetActive(false);
        }

        private void OnDisable()
        {
            IsOpen = false;
        }

        private void Update()
        {
            // Button.Start is the left controller's menu button and the left-hand menu (palm pinch) gesture.
            if (OVRInput.GetDown(OVRInput.Button.Start))
            {
                if (IsOpen)
                {
                    Close();
                }
                else
                {
                    Open();
                }
            }
        }

        private void LateUpdate()
        {
            if (!IsOpen)
            {
                return;
            }

            leftIndex.UpdateLabelColors(selectedTextColor);
            leftThumb.UpdateLabelColors(selectedTextColor);
            rightIndex.UpdateLabelColors(selectedTextColor);
            rightThumb.UpdateLabelColors(selectedTextColor);
            FollowHead();
        }

        public void Open()
        {
            _settings = DriverSettings.Current.Clone();
            ShowSettings();

            PlaceInFrontOfHead();
            panel.SetActive(true);
            IsOpen = true;
        }

        /// <summary>Hides the panel. Changes are already saved.</summary>
        public void Close()
        {
            panel.SetActive(false);
            IsOpen = false;
        }

        private void ShowSettings()
        {
            heightSlider.SetValueWithoutNotify(_settings.Height);
            distanceSlider.SetValueWithoutNotify(_settings.Distance);
            UpdateValueLabels();
            leftIndex.Show(_settings.LeftIndex);
            leftThumb.Show(_settings.LeftThumb);
            rightIndex.Show(_settings.RightIndex);
            rightThumb.Show(_settings.RightThumb);
        }

        private void OnSeatSliderChanged()
        {
            _settings.Height = DriverSettings.ClampStep(Mathf.RoundToInt(heightSlider.value));
            _settings.Distance = DriverSettings.ClampStep(Mathf.RoundToInt(distanceSlider.value));
            UpdateValueLabels();
            DriverSettings.Save(_settings); // the seat glides there via DriverSettings.Changed
        }

        private void OnPedalsChanged()
        {
            _settings.LeftIndex = leftIndex.Value;
            _settings.LeftThumb = leftThumb.Value;
            _settings.RightIndex = rightIndex.Value;
            _settings.RightThumb = rightThumb.Value;
            DriverSettings.Save(_settings);
        }

        private void OnRecenter()
        {
            if (seat != null && seat.Recenter(_settings))
            {
                // Recenter is a fresh starting point, so fine-tuning goes back to the middle.
                _settings.Height = DriverSettings.DefaultStep;
                _settings.Distance = DriverSettings.DefaultStep;
                ShowSettings();

                // Recenter is usually a big move, so it fades; the panel is re-placed in the new view while black.
                seat.Apply(_settings, PlaceInFrontOfHead);
                DriverSettings.Save(_settings);
            }
        }

        private void OnReset()
        {
            _settings.ResetToDefaults();
            ShowSettings();
            DriverSettings.Save(_settings);
        }

        private void UpdateValueLabels()
        {
            heightValue.text = _settings.Height.ToString();
            distanceValue.text = _settings.Distance.ToString();
        }

        private Transform GetHead()
        {
            Transform head = seat != null ? seat.Head : null;
            if (head == null && Camera.main != null)
            {
                head = Camera.main.transform;
            }

            return head;
        }

        // Where the panel should be: in front of the head (level, ignoring head pitch), facing it.
        private bool GetTargetPose(out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = default;

            Transform head = GetHead();
            if (head == null)
            {
                return false;
            }

            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(head.up, Vector3.up);
            }

            forward.Normalize();
            position = head.position + forward * distanceFromHead - Vector3.up * belowEyes;
            rotation = Quaternion.LookRotation(forward, Vector3.up);
            return true;
        }

        // Opens facing the player, parented to the kart so it rides along while driving.
        private void PlaceInFrontOfHead()
        {
            if (!GetTargetPose(out Vector3 position, out Quaternion rotation))
            {
                return;
            }

            if (seat != null && seat.transform.parent != null)
            {
                panel.transform.SetParent(seat.transform.parent, true);
            }

            panel.transform.SetPositionAndRotation(position, rotation);
            _following = false;
            _followVelocity = Vector3.zero;
        }

        // Lazy follow: the panel holds still while it's roughly in view (so it's easy to poke), and glides back
        // in front of the head once the player looks or moves away. Smoothed in the kart's space so driving
        // doesn't make it trail behind.
        private void FollowHead()
        {
            Transform head = GetHead();
            if (head == null || !GetTargetPose(out Vector3 targetPosition, out Quaternion targetRotation))
            {
                return;
            }

            Transform panelTransform = panel.transform;
            Vector3 toPanel = Vector3.ProjectOnPlane(panelTransform.position - head.position, Vector3.up);
            Vector3 headForward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            float angle = Vector3.Angle(headForward, toPanel);
            float distanceError = Vector3.Distance(panelTransform.position, targetPosition);

            if (!_following && (angle > followAngle || distanceError > followDistance))
            {
                _following = true;
            }

            if (!_following)
            {
                return;
            }

            Transform space = panelTransform.parent;
            Vector3 local = space != null ? space.InverseTransformPoint(panelTransform.position) : panelTransform.position;
            Vector3 targetLocal = space != null ? space.InverseTransformPoint(targetPosition) : targetPosition;
            local = Vector3.SmoothDamp(local, targetLocal, ref _followVelocity, followTime);

            panelTransform.position = space != null ? space.TransformPoint(local) : local;
            panelTransform.rotation = Quaternion.Slerp(panelTransform.rotation, targetRotation,
                1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, followTime * 0.5f)));

            if (Vector3.Distance(local, targetLocal) < 0.01f)
            {
                _following = false;
            }
        }
    }
}
