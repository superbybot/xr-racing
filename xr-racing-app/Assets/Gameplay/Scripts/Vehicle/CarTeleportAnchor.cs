using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace XrRacing.Gameplay.Vehicle
{
    public class CarTeleportAnchor : TeleportationAnchor
    {
        [Header("Car Teleport Settings")]
        [SerializeField] private Transform exitPoint;
        [SerializeField] private Transform xrOrigin;
        [SerializeField] private LocomotionMediator locomotionMediator;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private ContinuousMoveProvider continuousMoveProvider;


        [Header("Events")]
        public UnityEvent OnCarEnter;
        public UnityEvent OnCarExit;

        private readonly Subject<Unit> _onCarEnterSubject = new();
        private readonly Subject<Unit> _onCarExitSubject = new();

        public Observable<Unit> CarEnterObservable => _onCarEnterSubject;
        public Observable<Unit> CarExitObservable => _onCarExitSubject;

        [Header("Debug Info")]
        [SerializeField] private bool isSeated = false;

        protected override void OnEnable()
        {
            base.OnEnable();
            
            selectEntered.AddListener(OnSelectEntered);
            selectExited.AddListener(OnSelectExited);
            activated.AddListener(OnActivated);
            teleporting.AddListener(OnTeleporting);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            
            selectEntered.RemoveListener(OnSelectEntered);
            selectExited.RemoveListener(OnSelectExited);
            activated.RemoveListener(OnActivated);
            teleporting.RemoveListener(OnTeleporting);
        }

        private void OnDestroy()
        {
            _onCarEnterSubject.Dispose();
            _onCarExitSubject.Dispose();
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
        }

        protected override void OnActivated(ActivateEventArgs args)
        {
        }

        private Transform _currentDriver;

        private void OnTeleporting(TeleportingEventArgs args)
        {
            var interactor = args.interactorObject;
            if (interactor != null && interactor.transform != null)
            {
                EnterCarAsync(interactor.transform.root).Forget();
            }
        }
        
        private async UniTaskVoid EnterCarAsync(Transform player)
        {
            if (isSeated) return;
            isSeated = true;
            _currentDriver = player;

            DisableLocomotion();

            if (_currentDriver != null && xrOrigin != null)
            {
                _currentDriver.SetParent(xrOrigin);
                _currentDriver.rotation = xrOrigin.rotation;
            }

            OnCarEnter?.Invoke();
            _onCarEnterSubject.OnNext(Unit.Default);
        }

        public void ExitCar()
        {
            if (!isSeated) return;
            
            ExitCarAsync().Forget();
        }
        
        private async UniTaskVoid ExitCarAsync()
        {
            isSeated = false;

            await UniTask.WaitForSeconds(0.2f);

            EnableLocomotion();

            if (_currentDriver != null)
            {
                _currentDriver.SetParent(null);

                // Position player at exit point if specified
                if (exitPoint != null)
                {
                    _currentDriver.position = exitPoint.position;

                    // Ensure player is standing upright (up vector aligned to world up)
                    // Use LookRotation to align forward direction while keeping up vector world-aligned
                    Vector3 forward = exitPoint.forward;
                    forward.y = 0; // Remove any vertical component to ensure upright orientation
                    if (forward == Vector3.zero)
                    {
                        forward = Vector3.forward; // Default forward if exit point has no horizontal forward direction
                    }
                    _currentDriver.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
                }

                _currentDriver = null;
            }

            OnCarExit?.Invoke();
            _onCarExitSubject.OnNext(Unit.Default);
        }
        
        private void DisableLocomotion()
        {
            if (continuousMoveProvider != null)
            {
                continuousMoveProvider.enabled = false;
            }
            
            if (characterController != null)
            {
                characterController.detectCollisions = false;
            }
        }

        private void EnableLocomotion()
        {
            if (continuousMoveProvider != null)
            {
                continuousMoveProvider.enabled = true;
            }
            
            if (characterController != null)
            {
                characterController.detectCollisions = true;
            }
        }
    }
}
