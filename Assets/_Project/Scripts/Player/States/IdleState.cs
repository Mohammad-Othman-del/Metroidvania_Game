using Metroidvania.Input;

namespace Metroidvania.Player.States
{
    /// <summary>On the ground with no movement input and (almost) no speed.</summary>
    public class IdleState : GroundedState
    {
        public IdleState(PlayerController player) : base(player) { }

        public override PlayerStateId Id => PlayerStateId.Idle;

        protected override PlayerStateId? CheckGroundedTransitions(in PlayerInputFrame input) =>
            Player.IsMovingOrTryingTo(input) ? PlayerStateId.Run : null;
    }
}
