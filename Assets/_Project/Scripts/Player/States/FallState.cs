using Metroidvania.Input;

namespace Metroidvania.Player.States
{
    /// <summary>In the air and not rising: after a jump's apex, walking off a ledge, or dropping through a platform.</summary>
    public class FallState : PlayerState
    {
        public FallState(PlayerController player) : base(player) { }

        public override PlayerStateId Id => PlayerStateId.Fall;

        public override PlayerStateId? CheckTransitions(in PlayerInputFrame input)
        {
            if (Player.DodgeRequested && Player.CanDodge)
                return PlayerStateId.Dodge;

            // Coyote jump shortly after leaving a ledge, or a buffered jump on the frame we land.
            if (Player.JumpBuffered && Player.CanJump)
                return PlayerStateId.Jump;

            if (Player.Motor.IsGrounded)
                return Player.IsMovingOrTryingTo(input) ? PlayerStateId.Run : PlayerStateId.Idle;

            return null;
        }

        public override void Tick(in PlayerInputFrame input, float dt)
        {
            Player.FaceTowards(input.Move.x);
            Player.StandardMovement(input, dt);
        }
    }
}
