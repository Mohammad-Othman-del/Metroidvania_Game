using Metroidvania.Input;

namespace Metroidvania.Player.States
{
    /// <summary>
    /// One movement state. Each step the machine first asks the current state whether to switch
    /// (<see cref="CheckTransitions"/>), then lets the resulting state simulate (<see cref="Tick"/>).
    /// States use the shared movement helpers on <see cref="PlayerController"/> rather than moving
    /// the character themselves, so the physics stays in one place.
    /// </summary>
    public abstract class PlayerState
    {
        protected readonly PlayerController Player;

        protected PlayerState(PlayerController player)
        {
            Player = player;
        }

        public abstract PlayerStateId Id { get; }

        /// <summary>Seconds of simulation spent in this state since it was entered.</summary>
        public float TimeInState { get; internal set; }

        public virtual void Enter() { }

        public virtual void Exit() { }

        /// <summary>Returns the state to switch to, or null to stay. Runs before <see cref="Tick"/>.</summary>
        public abstract PlayerStateId? CheckTransitions(in PlayerInputFrame input);

        /// <summary>Simulates one step in this state.</summary>
        public abstract void Tick(in PlayerInputFrame input, float dt);
    }
}
