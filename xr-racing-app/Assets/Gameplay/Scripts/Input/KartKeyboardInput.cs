using UnityEngine;
using UnityEngine.InputSystem;
using XrRacing.Gameplay.Race;
using XrRacing.References.KartGame.KartSystems;

namespace XrRacing.Gameplay.Input
{
    /// <summary>
    /// Keyboard driving for testing without a headset: A/D or arrows steer, W/up accelerates, S/down brakes.
    /// Gives no input while RaceManager has the controls locked.
    /// </summary>
    public class KartKeyboardInput : BaseInput
    {
        public override InputData GenerateInput()
        {
            if (Keyboard.current == null || RaceManager.IsControlsLocked)
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

            return new InputData
            {
                Accelerate = accelerate ? 1f : 0f,
                Brake = brake ? 1f : 0f,
                TurnInput = turnInput
            };
        }
    }
}
