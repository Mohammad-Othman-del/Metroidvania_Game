using Metroidvania.Input;

namespace Metroidvania.Player.States
{
    /// <summary>On the ground and moving, or holding a direction (includes speeding up and slowing down).</summary>
    public class RunState : GroundedState
    {
        public RunState(PlayerController player) : base(player) { }

        public override PlayerStateId Id => PlayerStateId.Run;

        protected override PlayerStateId? CheckGroundedTransitions(in PlayerInputFrame input) =>
            Player.IsMovingOrTryingTo(input) ? null : PlayerStateId.Idle;
    }
}
