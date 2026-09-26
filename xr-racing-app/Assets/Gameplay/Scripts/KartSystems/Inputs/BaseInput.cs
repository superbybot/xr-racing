using UnityEngine;

namespace KartGame.KartSystems
{
    public struct InputData
    {
        // 0..1 amounts, so analog inputs (triggers) can apply partial throttle/brake. Digital inputs use 0 or 1.
        public float Accelerate;
        public float Brake;
        public float TurnInput;
    }

    public interface IInput
    {
        InputData GenerateInput();
    }

    public abstract class BaseInput : MonoBehaviour, IInput
    {
        /// <summary>
        /// Override this function to generate an XY input that can be used to steer and control the car.
        /// </summary>
        public abstract InputData GenerateInput();
    }
}
