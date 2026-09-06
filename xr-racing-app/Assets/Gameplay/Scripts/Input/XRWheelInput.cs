using UnityEngine;

namespace XrRacing.Gameplay.Input
{
    public class XRWheelInput : KartGame.KartSystems.BaseInput
    {
        [SerializeField] private Transform wheelTransform;
        [SerializeField] private Vector3 wheelSpinAxis = Vector3.forward;
        [SerializeField] private float maxWheelAngle = 450f;
        [SerializeField] private float triggerThreshold = 0.5f;

        private Quaternion _originRotation;

        private void Awake()
        {
            if (wheelTransform != null)
            {
                _originRotation = wheelTransform.localRotation;
            }
        }

        public override KartGame.KartSystems.InputData GenerateInput()
        {
            float turnInput = 0f;

            if (wheelTransform != null)
            {
                Quaternion delta = Quaternion.Inverse(_originRotation) * wheelTransform.localRotation;
                Vector3 axis;
                float angle;
                delta.ToAngleAxis(out angle, out axis);

                if (angle > 180f)
                {
                    angle -= 360f;
                }

                float signedAngle = angle * Vector3.Dot(axis, wheelSpinAxis.normalized);
                turnInput = Mathf.Clamp(signedAngle / maxWheelAngle, -1f, 1f);
            }

            bool accelerate = OVRInput.Get(OVRInput.Button.One, OVRInput.Controller.Touch);

            float leftTrigger = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.LTouch);
            float rightTrigger = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch);
            bool brake = Mathf.Max(leftTrigger, rightTrigger) > triggerThreshold;

            return new KartGame.KartSystems.InputData
            {
                Accelerate = accelerate,
                Brake = brake,
                TurnInput = turnInput
            };
        }
    }
}
