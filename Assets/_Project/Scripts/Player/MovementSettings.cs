using UnityEngine;

namespace Metroidvania.Player
{
    /// <summary>
    /// All the numbers that decide how a character moves. Forge and Spark each get their own asset.
    /// Jumps are described by height and time-to-apex; gravity and launch speed are derived from them.
    /// </summary>
    [CreateAssetMenu(menuName = "Metroidvania/Movement Settings", fileName = "SO_Movement_New")]
    public class MovementSettings : ScriptableObject
    {
        [Header("Run")]
        [Tooltip("Top horizontal speed in m/s.")]
        [Min(0f)] public float maxRunSpeed = 8f;
        [Tooltip("m/s² while speeding up on the ground.")]
        [Min(0f)] public float groundAcceleration = 80f;
        [Tooltip("m/s² while slowing down on the ground (no input).")]
        [Min(0f)] public float groundDeceleration = 90f;
        [Tooltip("m/s² while speeding up in the air.")]
        [Min(0f)] public float airAcceleration = 55f;
        [Tooltip("m/s² while slowing down in the air (no input).")]
        [Min(0f)] public float airDeceleration = 30f;
        [Tooltip("Acceleration multiplier when pushing against current motion, for snappy turns.")]
        [Min(1f)] public float turnMultiplier = 1.5f;

        [Header("Jump")]
        [Tooltip("Apex height in metres of a full (held) jump.")]
        [Min(0.1f)] public float jumpHeight = 3.2f;
        [Tooltip("Seconds from take-off to the apex of a full jump.")]
        [Min(0.05f)] public float timeToApex = 0.38f;
        [Tooltip("Gravity multiplier while falling. Above 1 gives a snappier, less floaty fall.")]
        [Min(1f)] public float fallGravityMultiplier = 1.7f;
        [Tooltip("Gravity multiplier while rising with jump released. Higher = shorter tap jumps.")]
        [Min(1f)] public float jumpCutGravityMultiplier = 3f;
        [Tooltip("Terminal fall speed in m/s.")]
        [Min(0f)] public float maxFallSpeed = 22f;

        [Header("Forgiveness")]
        [Tooltip("Seconds after walking off a ledge during which a jump still works.")]
        [Min(0f)] public float coyoteTime = 0.1f;
        [Tooltip("Seconds a jump press is remembered before landing.")]
        [Min(0f)] public float jumpBufferTime = 0.12f;

        [Header("Ground")]
        [Tooltip("Steepest slope, in degrees, that counts as ground.")]
        [Range(0f, 89f)] public float maxSlopeAngle = 45f;
        [Tooltip("How far down the character is pulled to stay on slopes and small steps.")]
        [Min(0f)] public float groundSnapDistance = 0.35f;
        [Tooltip("Seconds one-way platforms are ignored after down + jump.")]
        [Min(0f)] public float dropThroughTime = 0.25f;

        [Header("Dodge")]
        [Tooltip("Horizontal speed in m/s during a dodge. Gravity is off while dodging.")]
        [Min(0f)] public float dodgeSpeed = 16f;
        [Tooltip("Seconds a dodge lasts. Distance = speed x duration.")]
        [Min(0.01f)] public float dodgeDuration = 0.2f;
        [Tooltip("Seconds after a dodge ends before the next one can start.")]
        [Min(0f)] public float dodgeCooldown = 0.35f;
        [Tooltip("Dodges allowed per jump/fall before touching the ground again.")]
        [Min(0)] public int airDodges = 1;

        /// <summary>Upward gravity in m/s² (positive number).</summary>
        public float Gravity => 2f * jumpHeight / (timeToApex * timeToApex);

        /// <summary>Launch speed that reaches <see cref="jumpHeight"/> under <see cref="Gravity"/>.</summary>
        public float JumpVelocity => 2f * jumpHeight / timeToApex;
    }
}
