using UnityEngine;

namespace Metroidvania.Input
{
    /// <summary>
    /// Shows what an <see cref="IInputProvider"/> on the same GameObject is reporting.
    /// It consumes the input (calls ReadInput), so use it only on an object with no controller,
    /// e.g. to check bindings or an AI provider in isolation.
    /// </summary>
    public class InputDebugOverlay : MonoBehaviour
    {
        IInputProvider provider;
        PlayerInputFrame last;
        int jumpPresses, dodgePresses;
        GUIStyle style;

        void Awake()
        {
            provider = GetComponent<IInputProvider>();
            if (provider == null)
            {
                Debug.LogError($"{nameof(InputDebugOverlay)} on '{name}' needs an IInputProvider on the same GameObject.", this);
                enabled = false;
            }
        }

        void Update()
        {
            last = provider.ReadInput();
            if (last.JumpPressed) jumpPresses++;
            if (last.DodgePressed) dodgePresses++;
        }

        void OnGUI()
        {
            style ??= new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.UpperLeft };
            GUI.Box(new Rect(10, 10, 320, 120),
                $"Provider: {provider.GetType().Name}\n" +
                $"Move: ({last.Move.x:+0.00;-0.00}, {last.Move.y:+0.00;-0.00})\n" +
                $"Jump held: {last.JumpHeld}   presses: {jumpPresses}\n" +
                $"Dodge presses: {dodgePresses}",
                style);
        }
    }
}
