using UnityEngine;

namespace XrRacing.Gameplay.Input
{
    public class XRWheelInput : KartGame.KartSystems.BaseInput
    {
        [SerializeField] private Transform wheelTransform;
        [SerializeField] private Vector3 wheelSpinAxis = Vector3.right;
        [Tooltip("Wheel angle (degrees) for full steering lock. The wheel is also physically stopped at this angle.")]
        [SerializeField] private float maxWheelAngle = 90f;
        // Which finger works which pedal comes from DriverSettings (per hand/controller: index and thumb).
        // Controllers: index = index trigger (analog), thumb = A / X button.
        // Hand tracking: finger curl, only while that hand is holding the wheel.
        [Tooltip("Trigger travel ignored at the start, so resting a finger on it doesn't drive.")]
        [SerializeField, Range(0f, 0.5f)] private float triggerDeadzone = 0.05f;
        [Tooltip("Hand tracking: index finger curl in degrees mapped to its pedal, x = released, y = fully pressed.")]
        [SerializeField] private Vector2 indexCurlRange = new Vector2(200f, 240f);
        [Tooltip("Hand tracking: thumb curl in degrees mapped to its pedal, x = released, y = fully pressed.")]
        [SerializeField] private Vector2 thumbCurlRange = new Vector2(190f, 210f);
        [Tooltip("Grabbable on the wheel; used to detect release. Defaults to the one on Wheel Transform.")]
        [SerializeField] private Oculus.Interaction.Grabbable wheelGrabbable;
        [Tooltip("Degrees per second the wheel turns back to center after it is released. 0 disables.")]
        [SerializeField] private float returnSpeed = 360f;
        [Tooltip("Temporary: write wheel rotation to Logs/wheel_debug.csv while the wheel is held or returning.")]
        [SerializeField] private bool debugLog = false;

        private Quaternion _originRotation;
        private float _wheelAngle;
        private Transform _trackingSpace;
        private float _nextStatusTime;
        private string _lastStatus;
        private Oculus.Interaction.HandGrab.HandGrabInteractor[] _handInteractors;
        private Oculus.Interaction.HandGrab.HandGrabInteractable[] _wheelInteractables;
        private float _nextGrabDebugTime;
        private readonly System.Collections.Generic.Dictionary<Oculus.Interaction.HandGrab.HandGrabInteractor, string> _lastGrabState =
            new System.Collections.Generic.Dictionary<Oculus.Interaction.HandGrab.HandGrabInteractor, string>();
        private static readonly System.Reflection.FieldInfo GripColliderField =
            typeof(Oculus.Interaction.HandGrab.HandGrabInteractor).GetField("_gripCollider",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        [Tooltip("Temporary: hands closer than this (meters) to the wheel center get their grab attempt logged.")]
        [SerializeField] private float grabDebugRadius = 0.6f;

        private void Awake()
        {
            WheelDebugLog.Enabled = debugLog;
            WheelDebugLog.Begin();

            if (wheelTransform != null)
            {
                _originRotation = wheelTransform.localRotation;

                if (wheelGrabbable == null)
                {
                    wheelGrabbable = wheelTransform.GetComponent<Oculus.Interaction.Grabbable>();
                }
            }
        }

        // LateUpdate so this runs after the grab transformers have rotated the wheel this frame.
        private void LateUpdate()
        {
            if (wheelTransform == null)
            {
                return;
            }

            LogInteractorStatus();
            LogHandGrabAttempts();

            // Unwrap relative to last frame so turning the hand past 180 degrees doesn't flip to the other limit.
            float previousAngle = _wheelAngle;
            float rawAngle = GetWheelAngle();
            float angle = _wheelAngle + Mathf.DeltaAngle(_wheelAngle, rawAngle);
            float unwrappedAngle = angle;
            bool released = wheelGrabbable != null && wheelGrabbable.SelectingPointsCount == 0;

            if (released && returnSpeed > 0f)
            {
                angle = Mathf.MoveTowards(angle, 0f, returnSpeed * Time.deltaTime);
            }

            _wheelAngle = Mathf.Clamp(angle, -maxWheelAngle, maxWheelAngle);
            wheelTransform.localRotation = _originRotation * Quaternion.AngleAxis(_wheelAngle, wheelSpinAxis.normalized);

            if (WheelDebugLog.Enabled && (!released || previousAngle != 0f || _wheelAngle != 0f))
            {
                int selecting = wheelGrabbable != null ? wheelGrabbable.SelectingPointsCount : -1;
                int grabPoints = wheelGrabbable != null ? wheelGrabbable.GrabPoints.Count : -1;
                WheelDebugLog.Write("Input", "LateUpdate", selecting, grabPoints, _wheelAngle,
                    $"prev={WheelDebugLog.F(previousAngle)} raw={WheelDebugLog.F(rawAngle)} " +
                    $"unwrapped={WheelDebugLog.F(unwrappedAngle)} afterReturn={WheelDebugLog.F(angle)} " +
                    $"clamped={_wheelAngle != angle} released={released}");
                LogHands(selecting, grabPoints);
            }
        }

        private void Start()
        {
            _handInteractors = FindObjectsByType<Oculus.Interaction.HandGrab.HandGrabInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            _wheelInteractables = System.Array.FindAll(
                FindObjectsByType<Oculus.Interaction.HandGrab.HandGrabInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None),
                IsWheelInteractable);

            if (!WheelDebugLog.Enabled || wheelTransform == null)
            {
                return;
            }

            if (wheelGrabbable != null)
            {
                wheelGrabbable.WhenPointerEventRaised += LogPointerEvent;
            }

            OVRCameraRig rig = FindFirstObjectByType<OVRCameraRig>();
            _trackingSpace = rig != null ? rig.trackingSpace : null;

            Renderer wheelRenderer = wheelTransform.GetComponentInChildren<Renderer>();
            Collider wheelCollider = wheelTransform.GetComponent<Collider>();
            Vector3 pivot = wheelTransform.position;

            string meshInfo = wheelRenderer != null
                ? $"meshCenter={DescribeAroundWheel(wheelRenderer.bounds.center)} meshSize={wheelRenderer.bounds.size}"
                : "meshCenter=none";
            string colliderInfo = wheelCollider != null
                ? $"colliderCenter={DescribeAroundWheel(wheelCollider.bounds.center)} colliderSize={wheelCollider.bounds.size}"
                : "collider=none";

            WheelDebugLog.Write("Setup", "Start", -1, -1, _wheelAngle,
                $"pivot={pivot} worldAxis={WheelAxisWorld()} lossyScale={wheelTransform.lossyScale} " +
                $"{meshInfo} {colliderInfo} trackingSpace={(_trackingSpace != null ? _trackingSpace.name : "none")} " +
                $"grabbable={(wheelGrabbable != null ? wheelGrabbable.name : "none")} transformers=({DescribeTransformers()})");
        }

        // Every 0.5s (and whenever it changes), logs the input mode and which grab interactors exist and are active,
        // to see whether hand grabbing is still available after switching between hands and controllers.
        private void LogInteractorStatus()
        {
            if (!WheelDebugLog.Enabled || Time.time < _nextStatusTime)
            {
                return;
            }

            _nextStatusTime = Time.time + 0.5f;

            string status = $"active={OVRInput.GetActiveController()} " +
                $"handsL={OVRInput.IsControllerConnected(OVRInput.Controller.LHand)} handsR={OVRInput.IsControllerConnected(OVRInput.Controller.RHand)} " +
                $"touchL={OVRInput.IsControllerConnected(OVRInput.Controller.LTouch)} touchR={OVRInput.IsControllerConnected(OVRInput.Controller.RTouch)} |";

            if (_handInteractors != null)
            {
                foreach (var interactor in _handInteractors)
                {
                    if (interactor != null)
                    {
                        status += $" {DescribeInteractor(interactor, interactor.State.ToString())}";
                    }
                }
            }

            if (status != _lastStatus)
            {
                _lastStatus = status;
                WheelDebugLog.Write("Status", "Interactors", wheelGrabbable != null ? wheelGrabbable.SelectingPointsCount : -1, -1, _wheelAngle, status);
            }
        }

        // Logs why a hand near the wheel does or doesn't grab it. A palm grab only starts on the frame the grab
        // fingers close (selectChanged) while the hand's grip collider overlaps the wheel's colliders (overlap).
        // Logged every 0.1s while a hand is near the wheel, and immediately on state changes and finger-close frames.
        private void LogHandGrabAttempts()
        {
            if (!WheelDebugLog.Enabled || _handInteractors == null || _wheelInteractables == null)
            {
                return;
            }

            bool periodic = Time.time >= _nextGrabDebugTime;
            if (periodic)
            {
                _nextGrabDebugTime = Time.time + 0.1f;
            }

            foreach (var interactor in _handInteractors)
            {
                Oculus.Interaction.Input.IHand hand = interactor != null ? interactor.Hand : null;
                if (hand == null || !hand.IsConnected)
                {
                    continue;
                }

                Transform palm = interactor.PalmPoint != null ? interactor.PalmPoint : interactor.transform;
                float distance = Vector3.Distance(palm.position, wheelTransform.position);
                string state = $"{interactor.State} candidate={NameOf(interactor.Interactable)} selected={NameOf(interactor.SelectedInteractable)}";
                _lastGrabState.TryGetValue(interactor, out string lastState);
                bool stateChanged = state != lastState;
                _lastGrabState[interactor] = state;

                Oculus.Interaction.GrabAPI.HandGrabAPI api = interactor.HandGrabApi;
                Collider grip = GripColliderField != null ? GripColliderField.GetValue(interactor) as Collider : null;
                bool fingersClosed = false;
                string perInteractable = "";

                foreach (var interactable in _wheelInteractables)
                {
                    bool selectChanged = api != null && api.IsHandSelectPalmFingersChanged(interactable.PalmGrabRules);
                    fingersClosed |= selectChanged;
                    perInteractable += $" [{interactable.name}: active={interactable.isActiveAndEnabled} state={interactable.State} " +
                        $"hand={interactable.SupportsHandedness(hand.Handedness)} overlap={DescribeOverlap(grip, interactable)} " +
                        $"palmScore={(api != null ? WheelDebugLog.F(api.GetHandPalmScore(interactable.PalmGrabRules)) : "n/a")} " +
                        $"palmGrabbing={(api != null && api.IsHandPalmGrabbing(interactable.PalmGrabRules))} selectChanged={selectChanged}]";
                }

                if (!stateChanged && !fingersClosed && !(periodic && distance <= grabDebugRadius))
                {
                    continue;
                }

                string fingers = "";
                if (api != null)
                {
                    for (var finger = Oculus.Interaction.Input.HandFinger.Thumb; finger <= Oculus.Interaction.Input.HandFinger.Pinky; finger++)
                    {
                        fingers += $"{finger}={WheelDebugLog.F(api.GetFingerPalmStrength(finger))} ";
                    }
                }

                string evt = stateChanged ? "StateChange" : fingersClosed ? "FingersClosed" : "Near";
                WheelDebugLog.Write("Grab", evt, wheelGrabbable != null ? wheelGrabbable.SelectingPointsCount : -1, -1, _wheelAngle,
                    $"{interactor.name} ({hand.Handedness}, in {(interactor.transform.parent != null ? interactor.transform.parent.name : "none")}) " +
                    $"state={state} tracked={hand.IsTrackedDataValid} highConf={hand.IsHighConfidence} " +
                    $"palm {DescribeAroundWheel(palm.position)} dist={WheelDebugLog.F(distance)} " +
                    $"grip={(grip != null ? $"{grip.name}:{grip.enabled}" : "none")} palmStrength({fingers.Trim()}){perInteractable}");
            }
        }

        // yes/no whether the grip collider touches any enabled collider of the interactable, same test the interactor uses.
        private static string DescribeOverlap(Collider grip, Oculus.Interaction.HandGrab.HandGrabInteractable interactable)
        {
            if (grip == null)
            {
                return "noGrip";
            }

            if (interactable.Colliders == null || interactable.Colliders.Length == 0)
            {
                return "noColliders";
            }

            foreach (Collider collider in interactable.Colliders)
            {
                if (collider.enabled && Physics.ComputePenetration(
                        grip, grip.transform.position, grip.transform.rotation,
                        collider, collider.transform.position, collider.transform.rotation,
                        out _, out _))
                {
                    return "yes";
                }
            }

            return "no";
        }

        private static string NameOf(Object obj)
        {
            return obj != null ? obj.name : "none";
        }

        private bool IsWheelInteractable(Oculus.Interaction.HandGrab.HandGrabInteractable interactable)
        {
            return interactable != null && wheelTransform != null &&
                (interactable.transform.IsChildOf(wheelTransform) ||
                 (interactable.Rigidbody != null && interactable.Rigidbody.transform == wheelTransform));
        }

        private static string DescribeInteractor(MonoBehaviour interactor, string state)
        {
            Transform parent = interactor.transform.parent;
            string grandparent = parent != null && parent.parent != null ? parent.parent.name : "none";
            return $"[{interactor.name} in {(parent != null ? parent.name : "none")}/{grandparent}: " +
                $"activeInHierarchy={interactor.gameObject.activeInHierarchy} enabled={interactor.enabled} state={state}]";
        }

        // Logs which interactor (e.g. HandGrabInteractor vs GrabInteractor) grabbed or released the wheel, and its pose.
        private void LogPointerEvent(Oculus.Interaction.PointerEvent evt)
        {
            if (evt.Type == Oculus.Interaction.PointerEventType.Move)
            {
                return;
            }

            string interactor = evt.Data is Component component
                ? $"{component.GetType().Name} on '{component.gameObject.name}' (parent '{(component.transform.parent != null ? component.transform.parent.name : "none")}')"
                : evt.Data != null ? evt.Data.GetType().Name : "null";

            WheelDebugLog.Write("Pointer", evt.Type.ToString(), wheelGrabbable.SelectingPointsCount, wheelGrabbable.GrabPoints.Count, _wheelAngle,
                $"id={evt.Identifier} interactor={interactor} {DescribeAroundWheel(evt.Pose.position)}");
        }

        private void OnDestroy()
        {
            if (wheelGrabbable != null)
            {
                wheelGrabbable.WhenPointerEventRaised -= LogPointerEvent;
            }
        }

        private string DescribeTransformers()
        {
            string result = "";

            foreach (MonoBehaviour behaviour in wheelTransform.GetComponents<MonoBehaviour>())
            {
                if (behaviour is Oculus.Interaction.ITransformer)
                {
                    result += $"{behaviour.GetType().Name}:{(behaviour.enabled ? "on" : "off")} ";
                }
            }

            return result.Trim();
        }

        // Logs where each grab point and each controller sits around the wheel. While a hand holds the wheel
        // without slipping, (hand angle - wheel angle) should stay constant.
        private void LogHands(int selecting, int grabPoints)
        {
            if (wheelGrabbable != null)
            {
                for (int i = 0; i < wheelGrabbable.GrabPoints.Count; i++)
                {
                    WheelDebugLog.Write("Hand", $"GrabPoint{i}", selecting, grabPoints, _wheelAngle,
                        DescribeAroundWheel(wheelGrabbable.GrabPoints[i].position));
                }
            }

            WheelDebugLog.Write("Hand", "Controllers", selecting, grabPoints, _wheelAngle,
                $"active={OVRInput.GetActiveController()} " +
                $"L[{DescribeController(OVRInput.Controller.LTouch)}] R[{DescribeController(OVRInput.Controller.RTouch)}]");

            var shapes = Oculus.Interaction.PoseDetection.FingerFeatureStateProvider.DefaultFingerShapes;
            foreach (var handedness in new[] { Oculus.Interaction.Input.Handedness.Left, Oculus.Interaction.Input.Handedness.Right })
            {
                Oculus.Interaction.Input.IHand hand = GetHandHoldingWheel(handedness);
                if (hand != null)
                {
                    WheelDebugLog.Write("Hand", "PedalCurl", selecting, grabPoints, _wheelAngle,
                        $"{handedness} index={WheelDebugLog.F(shapes.GetCurlValue(Oculus.Interaction.Input.HandFinger.Index, hand))} " +
                        $"thumb={WheelDebugLog.F(shapes.GetCurlValue(Oculus.Interaction.Input.HandFinger.Thumb, hand))} " +
                        $"indexPedal={WheelDebugLog.F(ReadFingerCurl(hand, Oculus.Interaction.Input.HandFinger.Index, indexCurlRange))} " +
                        $"thumbPedal={WheelDebugLog.F(ReadFingerCurl(hand, Oculus.Interaction.Input.HandFinger.Thumb, thumbCurlRange))}");
                }
            }
        }

        private string DescribeController(OVRInput.Controller controller)
        {
            float grip = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, controller);
            float trigger = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, controller);
            string grabInput = $"grip={WheelDebugLog.F(grip)} trigger={WheelDebugLog.F(trigger)}";

            if (_trackingSpace == null)
            {
                return $"{grabInput} pos=no tracking space";
            }

            Vector3 world = _trackingSpace.TransformPoint(OVRInput.GetLocalControllerPosition(controller));
            return $"{grabInput} {DescribeAroundWheel(world)}";
        }

        // Position of a world point relative to the wheel, in a frame fixed to the kart (not spinning with the wheel):
        // ang = angle around the spin axis (same sign convention as the wheel angle), r = distance from the axis,
        // ax = offset along the axis.
        private string DescribeAroundWheel(Vector3 world)
        {
            Quaternion baseRotation = (wheelTransform.parent != null ? wheelTransform.parent.rotation : Quaternion.identity) * _originRotation;
            Vector3 axis = WheelAxisWorld();
            Vector3 localReference = Mathf.Abs(Vector3.Dot(wheelSpinAxis.normalized, Vector3.up)) > 0.9f ? Vector3.forward : Vector3.up;
            Vector3 reference = Vector3.ProjectOnPlane(baseRotation * localReference, axis);

            Vector3 offset = world - wheelTransform.position;
            float axial = Vector3.Dot(offset, axis);
            Vector3 planar = offset - axis * axial;
            float angle = Vector3.SignedAngle(reference, planar, axis);

            return $"ang={WheelDebugLog.F(angle)} r={WheelDebugLog.F(planar.magnitude)} ax={WheelDebugLog.F(axial)}";
        }

        private Vector3 WheelAxisWorld()
        {
            Quaternion baseRotation = (wheelTransform.parent != null ? wheelTransform.parent.rotation : Quaternion.identity) * _originRotation;
            return baseRotation * wheelSpinAxis.normalized;
        }

        public override KartGame.KartSystems.InputData GenerateInput()
        {
            float turnInput = 0f;

            if (wheelTransform != null)
            {
                turnInput = Mathf.Clamp(_wheelAngle / maxWheelAngle, -1f, 1f);
            }

            float accelerate = 0f;
            float brake = 0f;

            // No pedals while the settings menu is open, so poking the panel doesn't drive.
            if (!XrRacing.Gameplay.UI.DriverSettingsMenu.IsOpen)
            {
                var settings = XrRacing.Gameplay.Settings.DriverSettings.Current;
                AddSidePedals(Oculus.Interaction.Input.Handedness.Left, OVRInput.Controller.LTouch,
                    settings.LeftIndex, settings.LeftThumb, ref accelerate, ref brake);
                AddSidePedals(Oculus.Interaction.Input.Handedness.Right, OVRInput.Controller.RTouch,
                    settings.RightIndex, settings.RightThumb, ref accelerate, ref brake);
            }

            return new KartGame.KartSystems.InputData
            {
                Accelerate = accelerate,
                Brake = brake,
                TurnInput = turnInput
            };
        }

        // Adds one side's index and thumb inputs (controller, or tracked hand holding the wheel) to their mapped pedals.
        private void AddSidePedals(Oculus.Interaction.Input.Handedness handedness, OVRInput.Controller controller,
            XrRacing.Gameplay.Settings.PedalAction indexAction, XrRacing.Gameplay.Settings.PedalAction thumbAction,
            ref float accelerate, ref float brake)
        {
            float index = ReadTrigger(controller);
            float thumb = OVRInput.Get(OVRInput.Button.One, controller) ? 1f : 0f;

            Oculus.Interaction.Input.IHand hand = GetHandHoldingWheel(handedness);
            if (hand != null)
            {
                index = Mathf.Max(index, ReadFingerCurl(hand, Oculus.Interaction.Input.HandFinger.Index, indexCurlRange));
                thumb = Mathf.Max(thumb, ReadFingerCurl(hand, Oculus.Interaction.Input.HandFinger.Thumb, thumbCurlRange));
            }

            AddToPedal(indexAction, index, ref accelerate, ref brake);
            AddToPedal(thumbAction, thumb, ref accelerate, ref brake);
        }

        private static void AddToPedal(XrRacing.Gameplay.Settings.PedalAction action, float value, ref float accelerate, ref float brake)
        {
            if (action == XrRacing.Gameplay.Settings.PedalAction.Brake)
            {
                brake = Mathf.Max(brake, value);
            }
            else
            {
                accelerate = Mathf.Max(accelerate, value);
            }
        }

        // The tracked (not controller-driven) hand on that side while it is holding the wheel, or null otherwise.
        private Oculus.Interaction.Input.IHand GetHandHoldingWheel(Oculus.Interaction.Input.Handedness handedness)
        {
            if (_handInteractors == null || (OVRInput.GetActiveController() & OVRInput.Controller.Hands) == 0)
            {
                return null;
            }

            foreach (var interactor in _handInteractors)
            {
                Oculus.Interaction.Input.IHand hand = interactor != null ? interactor.Hand : null;
                if (hand != null && hand.Handedness == handedness && hand.IsConnected && hand.IsTrackedDataValid &&
                    IsWheelInteractable(interactor.SelectedInteractable))
                {
                    return hand;
                }
            }

            return null;
        }

        // Finger curl as 0..1, where curlRange.x degrees is released and curlRange.y is fully pressed.
        private static float ReadFingerCurl(Oculus.Interaction.Input.IHand hand, Oculus.Interaction.Input.HandFinger finger, Vector2 curlRange)
        {
            float curl = Oculus.Interaction.PoseDetection.FingerFeatureStateProvider.DefaultFingerShapes.GetCurlValue(finger, hand);
            return Mathf.Clamp01(Mathf.InverseLerp(curlRange.x, curlRange.y, curl));
        }

        // Index trigger as 0..1 with the deadzone removed and the remaining travel rescaled to the full range.
        private float ReadTrigger(OVRInput.Controller controller)
        {
            float value = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, controller);
            return Mathf.Clamp01(Mathf.InverseLerp(triggerDeadzone, 1f, value));
        }

        // Signed rotation of the wheel around wheelSpinAxis relative to its starting rotation, in (-180, 180].
        private float GetWheelAngle()
        {
            Quaternion delta = Quaternion.Inverse(_originRotation) * wheelTransform.localRotation;
            delta.ToAngleAxis(out float angle, out Vector3 axis);

            if (angle > 180f)
            {
                angle -= 360f;
            }

            return angle * Vector3.Dot(axis, wheelSpinAxis.normalized);
        }
    }
}
