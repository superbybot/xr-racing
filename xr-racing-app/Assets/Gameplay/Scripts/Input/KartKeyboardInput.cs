using UnityEngine;
using UnityEngine.InputSystem;

namespace XrRacing.Gameplay.Input
{
    public class KartKeyboardInput : KartGame.KartSystems.BaseInput
    {
        public override KartGame.KartSystems.InputData GenerateInput()
        {
            if (Keyboard.current == null)
            {
                return default;
            }

            float turnInput = 0f;

            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            {
                turnInput = -1f;
            }
            else if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            {
                turnInput = 1f;
            }

            bool accelerate = Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed;
            bool brake = Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed;

            return new KartGame.KartSystems.InputData
            {
                Accelerate = accelerate,
                Brake = brake,
                TurnInput = turnInput
            };
        }
    }
}
