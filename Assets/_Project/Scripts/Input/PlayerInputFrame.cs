using UnityEngine;

namespace Metroidvania.Input
{
    /// <summary>
    /// What a player wants to do during one simulation step. Plain data, so the same struct can come
    /// from a keyboard or gamepad, the AI companion, or (later) the network.
    /// </summary>
    public struct PlayerInputFrame
    {
        /// <summary>x: -1 left to 1 right. y: -1 down to 1 up.</summary>
        public Vector2 Move;

        /// <summary>Jump button is currently down (used for variable jump height).</summary>
        public bool JumpHeld;

        /// <summary>Jump was pressed since the previous read.</summary>
        public bool JumpPressed;

        /// <summary>Dodge was pressed since the previous read.</summary>
        public bool DodgePressed;
    }
}
