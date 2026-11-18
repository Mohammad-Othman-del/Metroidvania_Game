using System;
using Metroidvania.Core;
using Metroidvania.Input;
using UnityEngine;

namespace Metroidvania.Player
{
    /// <summary>
    /// Turns player intent into velocity: run acceleration, gravity, variable-height jump,
    /// coyote time and jump buffer. Collisions are left to <see cref="CharacterMotor"/>,
    /// and input comes from whatever <see cref="IInputProvider"/> sits on the same object.
    /// </summary>
    [RequireComponent(typeof(CharacterMotor))]
    [DisallowMultipleComponent]
    public class PlayerController : MonoBehaviour
    {
        const float MaxStep = 1f / 30f;          // a hitch never simulates more than this in one go
        const float MoveDeadzone = 0.1f;
        const float DropThroughInputY = -0.5f;   // how far down the stick must be to drop through

        [SerializeField] MovementSettings settings;
        [Tooltip("Child holding the visuals. It is mirrored on X to face the movement direction.")]
        [SerializeField] Transform visual;

        CharacterMotor motor;
        IInputProvider input;
        Vector2 velocity;
        float coyoteTimer;
        float jumpBufferTimer;
        bool isJumping; // rising from our own jump, so releasing the button may cut it short

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
        public Vector2 Velocity => velocity;
        public bool IsGrounded => motor.IsGrounded;

        /// <summary>1 facing right, -1 facing left.</summary>
        public int Facing { get; private set; } = 1;

        public PlayerInputFrame LastInput { get; private set; }

        public event Action Jumped;
        public event Action Landed;

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

        /// <summary>Moves the character instantly and clears its motion, e.g. for respawning.</summary>
        public void Teleport(Vector3 position)
        {
            transform.position = GameplayPlane.Project(position);
            velocity = Vector2.zero;
            isJumping = false;
            coyoteTimer = jumpBufferTimer = 0f;
        }

        // ------------------------------------------------------------------ simulation step

        void Tick(PlayerInputFrame frame, float dt)
        {
            LastInput = frame;
            ApplySettingsToMotor(); // cheap, and keeps values edited while playing in sync

            UpdateFacing(frame.Move.x);
            ApplyRun(frame.Move.x, dt);

            bool wantsJump = frame.JumpPressed || jumpBufferTimer > 0f;
            bool consumed = frame.JumpPressed && TryDropThrough(frame.Move.y);
            if (!consumed)
                consumed = wantsJump && TryJump();
            if (!consumed && frame.JumpPressed)
                jumpBufferTimer = settings.jumpBufferTime;

            float verticalBefore = velocity.y;
            ApplyGravity(frame.JumpHeld, dt);

            // Move with the average vertical speed over the step, so jump height doesn't depend on frame rate.
            var stepVelocity = new Vector2(velocity.x, 0.5f * (verticalBefore + velocity.y));
            bool wasGrounded = motor.IsGrounded;
            Vector2 resolved = motor.Move(stepVelocity, dt);

            velocity.x = resolved.x;
            if (!Mathf.Approximately(resolved.y, stepVelocity.y))
                velocity.y = resolved.y; // a floor, ceiling or slope changed our vertical motion

            if (velocity.y <= 0f)
                isJumping = false;
            if (motor.IsGrounded)
                coyoteTimer = settings.coyoteTime;
            else
                coyoteTimer -= dt;
            jumpBufferTimer -= dt;

            if (!wasGrounded && motor.IsGrounded)
                Landed?.Invoke();
        }

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

        bool TryJump()
        {
            if (!motor.IsGrounded && coyoteTimer <= 0f)
                return false;

            velocity.y = settings.JumpVelocity;
            isJumping = true;
            coyoteTimer = 0f;
            jumpBufferTimer = 0f;
            Jumped?.Invoke();
            return true;
        }

        bool TryDropThrough(float moveY)
        {
            if (moveY > DropThroughInputY || !motor.IsGrounded || !motor.IsOnOneWayPlatform)
                return false;

            motor.DropThrough(settings.dropThroughTime);
            coyoteTimer = 0f;
            jumpBufferTimer = 0f;
            return true;
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

        void UpdateFacing(float moveX)
        {
            if (Mathf.Abs(moveX) > MoveDeadzone)
                Facing = moveX > 0f ? 1 : -1;

            if (visual != null)
            {
                Vector3 scale = visual.localScale;
                scale.x = Mathf.Abs(scale.x) * Facing;
                visual.localScale = scale;
            }
        }

        void ApplySettingsToMotor()
        {
            if (motor == null || settings == null)
                return;
            motor.MaxSlopeAngle = settings.maxSlopeAngle;
            motor.GroundSnapDistance = settings.groundSnapDistance;
        }
    }
}
