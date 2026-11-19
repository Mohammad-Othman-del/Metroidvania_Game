using Metroidvania.Input;

namespace Metroidvania.Player.States
{
    /// <summary>Shared rules for standing on the ground (Idle and Run).</summary>
    public abstract class GroundedState : PlayerState
    {
        protected GroundedState(PlayerController player) : base(player) { }

        public override PlayerStateId? CheckTransitions(in PlayerInputFrame input)
        {
            if (Player.DodgeRequested && Player.CanDodge)
                return PlayerStateId.Dodge;

            if (Player.JumpBuffered)
                return Player.TryDropThrough(input) ? PlayerStateId.Fall : PlayerStateId.Jump;

            if (!Player.Motor.IsGrounded)
                return PlayerStateId.Fall; // walked off a ledge; Fall still allows a coyote jump

            return CheckGroundedTransitions(input);
        }

        /// <summary>Idle/Run switching, checked only when nothing more important happened.</summary>
        protected abstract PlayerStateId? CheckGroundedTransitions(in PlayerInputFrame input);

        public override void Tick(in PlayerInputFrame input, float dt)
        {
            Player.FaceTowards(input.Move.x);
            Player.StandardMovement(input, dt);
        }
    }
}
