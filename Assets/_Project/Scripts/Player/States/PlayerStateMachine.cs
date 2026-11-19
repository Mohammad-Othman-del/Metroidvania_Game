using System;
using System.Collections.Generic;
using Metroidvania.Input;

namespace Metroidvania.Player.States
{
    /// <summary>Runs one <see cref="PlayerState"/> at a time and handles switching between them.</summary>
    public class PlayerStateMachine
    {
        // Lets a single step chain e.g. Fall -> Jump on a buffered landing jump, without risking a loop.
        const int MaxTransitionsPerTick = 3;

        readonly Dictionary<PlayerStateId, PlayerState> states = new();

        public PlayerState Current { get; private set; }
        public PlayerStateId CurrentId => Current.Id;
        public PlayerStateId PreviousId { get; private set; }

        /// <summary>Raised with (from, to) whenever the state changes.</summary>
        public event Action<PlayerStateId, PlayerStateId> StateChanged;

        public void Add(PlayerState state) => states.Add(state.Id, state);

        /// <summary>Enters the first state without raising <see cref="StateChanged"/>.</summary>
        public void Start(PlayerStateId id)
        {
            Current = states[id];
            PreviousId = id;
            Current.TimeInState = 0f;
            Current.Enter();
        }

        public void ChangeState(PlayerStateId id)
        {
            PlayerState next = states[id];
            PlayerStateId from = Current.Id;

            Current.Exit();
            PreviousId = from;
            Current = next;
            Current.TimeInState = 0f;
            Current.Enter();

            StateChanged?.Invoke(from, id);
        }

        public void Tick(in PlayerInputFrame input, float dt)
        {
            for (int i = 0; i < MaxTransitionsPerTick; i++)
            {
                PlayerStateId? next = Current.CheckTransitions(input);
                if (next == null || next.Value == Current.Id)
                    break;
                ChangeState(next.Value);
            }

            Current.Tick(input, dt);
            Current.TimeInState += dt;
        }
    }
}
