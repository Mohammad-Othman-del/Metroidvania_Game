namespace Metroidvania.Input
{
    /// <summary>
    /// Source of player intent. The character controller only talks to this interface, so a human,
    /// the AI companion, or a networked remote player can drive the same character.
    /// </summary>
    public interface IInputProvider
    {
        /// <summary>
        /// Returns the current input. Press flags are reported once: the first read after a press
        /// returns true, and later reads return false until the button is pressed again.
        /// Call it exactly once per simulation step.
        /// </summary>
        PlayerInputFrame ReadInput();
    }
}
