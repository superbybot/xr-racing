using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;

namespace XrRacing.Gameplay.Input
{
    /// <summary>
    /// One- or two-hand grab transformer that spins the wheel around a single local axis by how far
    /// the hands moved since last frame. Unlike Meta's rotate transformers it keeps no running angle,
    /// so when XRWheelInput clamps the wheel at its limit, reversing the hands moves it back immediately.
    /// Hand movement is measured in the wheel's parent (kart) space, so the kart turning or tilting
    /// doesn't read as the hands turning the wheel.
    /// </summary>
    public class SteeringWheelTransformer : MonoBehaviour, ITransformer
    {
        [Tooltip("Local axis of the wheel it spins around. Must match the wheel's spin axis in XRWheelInput (sign doesn't matter).")]
        [SerializeField] private Vector3 localSpinAxis = Vector3.right;

        private const float MinGrabRadius = 0.01f;

        private IGrabbable _grabbable;
        private readonly List<Vector3> _previousGrabVectors = new List<Vector3>();

        public void Initialize(IGrabbable grabbable)
        {
            _grabbable = grabbable;
        }

        public void BeginTransform()
        {
            _previousGrabVectors.Clear();
            Vector3 axis = ParentSpaceAxis();

            foreach (Pose grabPoint in _grabbable.GrabPoints)
            {
                _previousGrabVectors.Add(GrabVectorOnPlane(grabPoint, axis));
            }

            WheelDebugLog.Write("Transformer", "Begin", -1, _grabbable.GrabPoints.Count, float.NaN,
                $"localEuler={_grabbable.Transform.localEulerAngles} vectors={string.Join(" | ", _previousGrabVectors)}");
        }

        public void UpdateTransform()
        {
            Transform wheel = _grabbable.Transform;
            Vector3 axis = ParentSpaceAxis();
            List<Pose> grabPoints = _grabbable.GrabPoints;

            float angleSum = 0f;
            int count = 0;
            string perHand = "";

            for (int i = 0; i < grabPoints.Count && i < _previousGrabVectors.Count; i++)
            {
                Vector3 current = GrabVectorOnPlane(grabPoints[i], axis);
                Vector3 previous = _previousGrabVectors[i];

                // Skip hands too close to the hub to give a stable angle.
                if (current.sqrMagnitude > MinGrabRadius * MinGrabRadius &&
                    previous.sqrMagnitude > MinGrabRadius * MinGrabRadius)
                {
                    float handDelta = Vector3.SignedAngle(previous, current, axis);
                    angleSum += handDelta;
                    count++;
                    perHand += $"h{i}={WheelDebugLog.F(handDelta)}(r={WheelDebugLog.F(current.magnitude)}) ";
                }
                else
                {
                    perHand += $"h{i}=skipped(r={WheelDebugLog.F(current.magnitude)}) ";
                }

                _previousGrabVectors[i] = current;
            }

            Vector3 eulerBefore = wheel.localEulerAngles;

            if (count > 0)
            {
                wheel.localRotation = Quaternion.AngleAxis(angleSum / count, axis) * wheel.localRotation;
            }

            WheelDebugLog.Write("Transformer", "Update", -1, grabPoints.Count, float.NaN,
                $"{perHand}applied={WheelDebugLog.F(count > 0 ? angleSum / count : 0f)} " +
                $"prevVectors={_previousGrabVectors.Count} eulerBefore={eulerBefore} eulerAfter={wheel.localEulerAngles}");
        }

        public void EndTransform()
        {
            _previousGrabVectors.Clear();
            WheelDebugLog.Write("Transformer", "End", -1, _grabbable.GrabPoints.Count, float.NaN);
        }

        // Spin axis expressed in the wheel's parent space.
        private Vector3 ParentSpaceAxis()
        {
            return _grabbable.Transform.localRotation * localSpinAxis.normalized;
        }

        // Grab point relative to the wheel hub, in the wheel's parent space, flattened onto the wheel's plane.
        private Vector3 GrabVectorOnPlane(Pose grabPoint, Vector3 axis)
        {
            Transform wheel = _grabbable.Transform;
            Vector3 point = wheel.parent != null ? wheel.parent.InverseTransformPoint(grabPoint.position) : grabPoint.position;
            return Vector3.ProjectOnPlane(point - wheel.localPosition, axis);
        }
    }
}
