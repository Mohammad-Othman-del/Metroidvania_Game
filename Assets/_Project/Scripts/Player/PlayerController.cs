using System;
using Metroidvania.Core;
using Metroidvania.Input;
using Metroidvania.Player.States;
using UnityEngine;

namespace Metroidvania.Player
{
    /// <summary>
    /// Runs the movement state machine (Idle, Run, Jump, Fall, Dodge) and owns everything the states
    /// share: velocity, timers (coyote, jump buffer, dodge cooldown) and the movement helpers.
    /// Collisions are left to <see cref="CharacterMotor"/>, and input comes from whatever
    /// <see cref="IInputProvider"/> sits on the same object.
    /// </summary>
    [RequireComponent(typeof(CharacterMotor))]
    [DisallowMultipleComponent]
    public class PlayerController : MonoBehaviour
    {
        const float MaxStep = 1f / 30f;          // a hitch never simulates more than this in one go
        const float MoveDeadzone = 0.1f;
        const float StoppedSpeed = 0.05f;        // below this horizontal speed we count as standing still
        const float DropThroughInputY = -0.5f;   // how far down the stick must be to drop through
        const float DodgeBufferTime = 0.1f;      // a dodge pressed this long before it's allowed still happens

        [SerializeField] MovementSettings settings;
        [Tooltip("Child holding the visuals. It is mirrored on X to face the movement direction.")]
        [SerializeField] Transform visual;

        CharacterMotor motor;
        IInputProvider input;
        PlayerStateMachine stateMachine;
        Vector2 velocity;
        float coyoteTimer;
        float jumpBufferTimer;
        float dodgeBufferTimer;
        float dodgeCooldownTimer;
        int airDodgesUsed;
        bool isJumping; // rising from our own jump, so releasing the button may cut it short

        // ------------------------------------------------------------------ public API

        public MovementSettings Settings
        {
            get => settings;
            set
            {
                settings = value;
                ApplySettingsToMotor();
            }
        }

        public CharacterMotor Motor => motor;
        public bool IsGrounded => motor.IsGrounded;

        public Vector2 Velocity
        {
            get => velocity;
            internal set => velocity = value;
        }

        /// <summary>1 facing right, -1 facing left.</summary>
        public int Facing { get; private set; } = 1;

        public PlayerInputFrame LastInput { get; private set; }

        public PlayerStateId State => stateMachine.CurrentId;
        public PlayerStateId PreviousState => stateMachine.PreviousId;

        /// <summary>True while dodging. Combat will use this to ignore hits.</summary>
        public bool IsInvulnerable => State == PlayerStateId.Dodge;

        public event Action Jumped;
        public event Action Landed;
        public event Action Dodged;

        /// <summary>Raised with (from, to) whenever the movement state changes.</summary>
        public event Action<PlayerStateId, PlayerStateId> StateChanged;

        /// <summary>Moves the character instantly and clears its motion, e.g. for respawning.</summary>
        public void Teleport(Vector3 position)
        {
            transform.position = GameplayPlane.Project(position);
            velocity = Vector2.zero;
            isJumping = false;
            coyoteTimer = jumpBufferTimer = dodgeBufferTimer = 0f;
            if (State != PlayerStateId.Fall)
                stateMachine.ChangeState(PlayerStateId.Fall);
        }

        /// <summary>
        /// Shows the character facing a direction without simulating it. Used when something else
        /// drives this character, e.g. a remote player whose position comes from the network.
        /// </summary>
        public void ShowFacing(int direction)
        {
            Facing = direction >= 0 ? 1 : -1;
            ApplyFacingToVisual();
        }

        // ------------------------------------------------------------------ Unity

        void Reset()
        {
            Transform child = transform.Find("Visual");
            if (child != null)
                visual = child;
        }

        void Awake()
        {
            motor = GetComponent<CharacterMotor>();
            input = GetComponent<IInputProvider>();

            if (settings == null)
            {
                Debug.LogWarning($"{nameof(PlayerController)} on '{name}' has no MovementSettings; using defaults.", this);
                settings = ScriptableObject.CreateInstance<MovementSettings>();
            }

            stateMachine = new PlayerStateMachine();
            stateMachine.Add(new IdleState(this));
            stateMachine.Add(new RunState(this));
            stateMachine.Add(new JumpState(this));
            stateMachine.Add(new FallState(this));
            stateMachine.Add(new DodgeState(this));
            stateMachine.StateChanged += (from, to) => StateChanged?.Invoke(from, to);
            stateMachine.Start(PlayerStateId.Fall); // Fall settles into Idle on the first grounded step

            if (input == null)
            {
                Debug.LogError($"{nameof(PlayerController)} on '{name}' needs an IInputProvider (e.g. HumanInputProvider).", this);
                enabled = false;
                return;
            }

            ApplySettingsToMotor();
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, MaxStep);
            if (dt > 0f)
                Tick(input.ReadInput(), dt);
        }

        void Tick(PlayerInputFrame frame, float dt)
        {
            LastInput = frame;
            ApplySettingsToMotor(); // cheap, and keeps values edited while playing in sync

            if (frame.JumpPressed)
                jumpBufferTimer = settings.jumpBufferTime;
            if (frame.DodgePressed)
                dodgeBufferTimer = DodgeBufferTime;

            bool wasGrounded = motor.IsGrounded;
            stateMachine.Tick(frame, dt);

            if (velocity.y <= 0f)
                isJumping = false;
            if (motor.IsGrounded)
            {
                coyoteTimer = settings.coyoteTime;
                airDodgesUsed = 0;
            }
            else
            {
                coyoteTimer -= dt;
            }
            jumpBufferTimer -= dt;
            dodgeBufferTimer -= dt;
            dodgeCooldownTimer -= dt;

            if (!wasGrounded && motor.IsGrounded)
                Landed?.Invoke();

            ApplyFacingToVisual();
        }

        // ------------------------------------------------------------------ queries used by states

        internal bool JumpBuffered => jumpBufferTimer > 0f;

        /// <summary>On the ground, or just left it (coyote time).</summary>
        internal bool CanJump => motor.IsGrounded || coyoteTimer > 0f;

        internal bool DodgeRequested => dodgeBufferTimer > 0f;

        internal bool CanDodge =>
            dodgeCooldownTimer <= 0f && (motor.IsGrounded || airDodgesUsed < settings.airDodges);

        internal bool HasMoveInput(in PlayerInputFrame frame) => Mathf.Abs(frame.Move.x) > MoveDeadzone;

        internal bool IsMovingOrTryingTo(in PlayerInputFrame frame) =>
            HasMoveInput(frame) || Mathf.Abs(velocity.x) > StoppedSpeed;

        // ------------------------------------------------------------------ actions used by states

        /// <summary>Run acceleration, gravity and collision for one step: what Idle, Run, Jump and Fall all do.</summary>
        internal void StandardMovement(in PlayerInputFrame frame, float dt)
        {
            ApplyRun(frame.Move.x, dt);
            float verticalBefore = velocity.y;
            ApplyGravity(frame.JumpHeld, dt);
            MoveMotor(dt, verticalBefore);
        }

        /// <summary>
        /// Moves the motor by the current velocity. Vertically it uses the average of
        /// <paramref name="verticalBefore"/> and the current speed, so jump height doesn't depend on frame rate.
        /// </summary>
        internal void MoveMotor(float dt, float verticalBefore)
        {
            var stepVelocity = new Vector2(velocity.x, 0.5f * (verticalBefore + velocity.y));
            Vector2 resolved = motor.Move(stepVelocity, dt);

            velocity.x = resolved.x;
            if (!Mathf.Approximately(resolved.y, stepVelocity.y))
                velocity.y = resolved.y; // a floor, ceiling or slope changed our vertical motion
        }

        internal void StartJump()
        {
            velocity.y = settings.JumpVelocity;
            isJumping = true;
            coyoteTimer = 0f;
            jumpBufferTimer = 0f;
            Jumped?.Invoke();
        }

        /// <summary>Down + jump while standing on a one-way platform: falls through it and uses up the jump press.</summary>
        internal bool TryDropThrough(in PlayerInputFrame frame)
        {
            if (frame.Move.y > DropThroughInputY || !motor.IsGrounded || !motor.IsOnOneWayPlatform)
                return false;

            motor.DropThrough(settings.dropThroughTime);
            coyoteTimer = 0f;
            jumpBufferTimer = 0f;
            return true;
        }

        internal void BeginDodge(int direction)
        {
            Facing = direction;
            if (!motor.IsGrounded)
                airDodgesUsed++;
            dodgeBufferTimer = 0f;
            isJumping = false;
            velocity = new Vector2(direction * settings.dodgeSpeed, 0f);
            Dodged?.Invoke();
        }

        internal void EndDodge()
        {
            dodgeCooldownTimer = settings.dodgeCooldown;
            velocity.x = Mathf.Clamp(velocity.x, -settings.maxRunSpeed, settings.maxRunSpeed);
        }

        internal void FaceTowards(float moveX)
        {
            if (Mathf.Abs(moveX) > MoveDeadzone)
                Facing = moveX > 0f ? 1 : -1;
        }

        // ------------------------------------------------------------------ internals

        void ApplyRun(float moveX, float dt)
        {
            float target = Mathf.Abs(moveX) > MoveDeadzone ? moveX * settings.maxRunSpeed : 0f;
            bool grounded = motor.IsGrounded;

            float rate;
            if (target == 0f)
            {
                rate = grounded ? settings.groundDeceleration : settings.airDeceleration;
            }
            else
            {
                rate = grounded ? settings.groundAcceleration : settings.airAcceleration;
                bool turning = velocity.x != 0f && Mathf.Sign(target) != Mathf.Sign(velocity.x);
                if (turning)
                    rate *= settings.turnMultiplier;
            }

            velocity.x = Mathf.MoveTowards(velocity.x, target, rate * dt);
        }

        void ApplyGravity(bool jumpHeld, float dt)
        {
            if (motor.IsGrounded && velocity.y <= 0f)
            {
                velocity.y = 0f;
                return;
            }

            float gravity = settings.Gravity;
            if (velocity.y < 0f)
                gravity *= settings.fallGravityMultiplier;
            else if (isJumping && !jumpHeld)
                gravity *= settings.jumpCutGravityMultiplier;

            velocity.y = Mathf.Max(velocity.y - gravity * dt, -settings.maxFallSpeed);
        }

        void ApplyFacingToVisual()
        {
            if (visual == null)
                return;
            Vector3 scale = visual.localScale;
            scale.x = Mathf.Abs(scale.x) * Facing;
            visual.localScale = scale;
        }

        void ApplySettingsToMotor()
        {
            if (motor == null || settings == null)
                return;
            motor.MaxSlopeAngle = settings.maxSlopeAngle;
            motor.GroundSnapDistance = settings.groundSnapDistance;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!Application.isPlaying || stateMachine == null)
                return;
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2.6f, State.ToString());
        }
#endif
    }
}
