using Metroidvania.Player;
using UnityEngine;

namespace Metroidvania.Input
{
    /// <summary>
    /// On-screen readout of player input.
    /// With a <see cref="PlayerController"/> on the same object it only watches what the controller
    /// received (plus its state and ground contact), so it is safe to leave on the player.
    /// Without one, it reads the <see cref="IInputProvider"/> itself, which consumes the input.
    /// </summary>
    public class InputDebugOverlay : MonoBehaviour
    {
        PlayerController controller;
        IInputProvider provider;
        PlayerInputFrame last;
        int jumpPresses, dodgePresses;
        GUIStyle style;

        void Awake()
        {
            controller = GetComponent<PlayerController>();
            provider = GetComponent<IInputProvider>();
            if (controller == null && provider == null)
            {
                Debug.LogError($"{nameof(InputDebugOverlay)} on '{name}' needs a PlayerController or an IInputProvider on the same GameObject.", this);
                enabled = false;
            }
        }

        // LateUpdate so the controller has already handled this frame's input.
        void LateUpdate()
        {
            last = controller != null ? controller.LastInput : provider.ReadInput();
            if (last.JumpPressed) jumpPresses++;
            if (last.DodgePressed) dodgePresses++;
        }

        void OnGUI()
        {
            style ??= new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.UpperLeft };

            string text =
                $"Move: ({last.Move.x:+0.00;-0.00}, {last.Move.y:+0.00;-0.00})\n" +
                $"Jump held: {last.JumpHeld}   presses: {jumpPresses}\n" +
                $"Dodge presses: {dodgePresses}";

            if (controller != null)
                text += $"\nState: {controller.State}   Grounded: {controller.IsGrounded}\n" +
                        $"Velocity: ({controller.Velocity.x:0.0}, {controller.Velocity.y:0.0})";

            GUI.Box(new Rect(10, 10, 380, controller != null ? 170 : 110), text, style);
        }
    }
}
