using Metroidvania.Input;

namespace Metroidvania.Player.States
{
    /// <summary>Rising from a jump. Releasing the button early cuts the jump short.</summary>
    public class JumpState : PlayerState
    {
        public JumpState(PlayerController player) : base(player) { }

        public override PlayerStateId Id => PlayerStateId.Jump;

        public override void Enter() => Player.StartJump();

        public override PlayerStateId? CheckTransitions(in PlayerInputFrame input)
        {
            if (Player.DodgeRequested && Player.CanDodge)
                return PlayerStateId.Dodge;

            // Reached the apex, or bumped a ceiling (the motor zeroes upward speed).
            if (Player.Velocity.y <= 0f)
                return PlayerStateId.Fall;

            return null;
        }

        public override void Tick(in PlayerInputFrame input, float dt)
        {
            Player.FaceTowards(input.Move.x);
            Player.StandardMovement(input, dt);
        }
    }
}
