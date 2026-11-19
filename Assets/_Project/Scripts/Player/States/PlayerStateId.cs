namespace Metroidvania.Player.States
{
    /// <summary>
    /// Identifies a movement state. Small and serialisable, so it can drive animation,
    /// show up in the debug panel, and later be sent over the network.
    /// </summary>
    public enum PlayerStateId : byte
    {
        Idle,
        Run,
        Jump,
        Fall,
        Dodge,
    }
}
