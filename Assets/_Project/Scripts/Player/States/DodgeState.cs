using Metroidvania.Input;
using UnityEngine;

namespace Metroidvania.Player.States
{
    /// <summary>
    /// A short horizontal dash at fixed speed with gravity switched off. Goes toward the held direction,
    /// or the facing direction if nothing is held. On the ground it can be cancelled into a jump.
    /// </summary>
    public class DodgeState : PlayerState
    {
        int direction;

        public DodgeState(PlayerController player) : base(player) { }

        public override PlayerStateId Id => PlayerStateId.Dodge;

        public override void Enter()
        {
            direction = Player.HasMoveInput(Player.LastInput) ? (int)Mathf.Sign(Player.LastInput.Move.x) : Player.Facing;
            Player.BeginDodge(direction);
        }

        public override void Exit() => Player.EndDodge();

        public override PlayerStateId? CheckTransitions(in PlayerInputFrame input)
        {
            if (Player.JumpBuffered && Player.Motor.IsGrounded)
                return PlayerStateId.Jump;

            if (TimeInState < Player.Settings.dodgeDuration)
                return null;

            if (!Player.Motor.IsGrounded)
                return PlayerStateId.Fall;
            return Player.IsMovingOrTryingTo(input) ? PlayerStateId.Run : PlayerStateId.Idle;
        }

        public override void Tick(in PlayerInputFrame input, float dt)
        {
            Player.Velocity = new Vector2(direction * Player.Settings.dodgeSpeed, 0f);
            Player.MoveMotor(dt, 0f);
        }
    }
}
