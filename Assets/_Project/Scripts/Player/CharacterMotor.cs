using Metroidvania.Core;
using UnityEngine;

namespace Metroidvania.Player
{
    /// <summary>
    /// Kinematic collide-and-slide movement on the gameplay plane. It knows nothing about input or
    /// game feel: give it a velocity, it moves the character as far as the world allows and reports
    /// what it touched (ground, walls, ceiling).
    ///
    /// Assumptions: the root object is unrotated and unscaled (flip the visual child instead), and the
    /// CapsuleCollider is upright with its bottom at the pivot (center.y = height / 2).
    /// </summary>
    [RequireComponent(typeof(CapsuleCollider), typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public class CharacterMotor : MonoBehaviour
    {
        const int MaxSlideIterations = 4;
        const float MinMoveDistance = 0.0001f;
        const float WallMaxNormalY = 0.3f;     // surfaces flatter than this are not walls
        const float CeilingMaxNormalY = -0.1f; // surfaces facing down count as ceilings
        const float OneWayMinNormalY = 0.7f;   // only the top face of a one-way platform blocks
        const float OneWayTolerance = 0.05f;

        [Tooltip("Layers that always block movement.")]
        [SerializeField] LayerMask solidLayers;
        [Tooltip("Layers that only block from above while falling.")]
        [SerializeField] LayerMask oneWayLayers;
        [Tooltip("Gap kept between the capsule and surfaces so casts never start inside geometry.")]
        [SerializeField, Min(0.001f)] float skinWidth = 0.02f;
        [Tooltip("Extra distance below the skin that still counts as standing on the ground.")]
        [SerializeField, Min(0f)] float groundProbeDistance = 0.05f;
        [Tooltip("Extra distance beside the skin that still counts as touching a wall.")]
        [SerializeField, Min(0f)] float wallProbeDistance = 0.08f;

        /// <summary>Steepest walkable slope in degrees.</summary>
        public float MaxSlopeAngle { get; set; } = 45f;

        /// <summary>How far down the character is pulled each move to stay on slopes and steps.</summary>
        public float GroundSnapDistance { get; set; } = 0.35f;

        public bool IsGrounded { get; private set; }
        public Vector2 GroundNormal { get; private set; } = Vector2.up;
        public bool IsOnOneWayPlatform { get; private set; }

        /// <summary>-1 wall on the left, +1 wall on the right, 0 none. Ignores one-way platforms.</summary>
        public int WallDirection { get; private set; }

        public bool HitCeilingThisMove { get; private set; }

        CapsuleCollider capsule;
        readonly RaycastHit[] castHits = new RaycastHit[16];
        readonly Collider[] overlaps = new Collider[16];
        float dropThroughTimer;

        void Reset()
        {
            solidLayers = LayerMask.GetMask("Default", "Ground");
            oneWayLayers = LayerMask.GetMask("OneWayPlatform");
        }

        void Awake()
        {
            capsule = GetComponent<CapsuleCollider>();

            var body = GetComponent<Rigidbody>();
            body.isKinematic = true;   // kept only so triggers detect the player
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.None;

            transform.position = GameplayPlane.Project(transform.position);
        }

        /// <summary>Ignore one-way platforms for a moment so the character falls through.</summary>
        public void DropThrough(float duration)
        {
            dropThroughTimer = duration;
            IsGrounded = false;
            IsOnOneWayPlatform = false;
        }

        /// <summary>
        /// Moves by <paramref name="velocity"/> * <paramref name="dt"/>, sliding along anything hit.
        /// Returns the velocity with the parts that ran into surfaces removed.
        /// </summary>
        public Vector2 Move(Vector2 velocity, float dt)
        {
            if (dropThroughTimer > 0f)
                dropThroughTimer -= dt;
            HitCeilingThisMove = false;

            Vector3 pos = Depenetrate(transform.position);

            bool stickToGround = IsGrounded && velocity.y <= 0f;
            Vector2 displacement;
            if (stickToGround)
            {
                // Walk along the surface: slopes don't change speed and we don't launch off downhill.
                var tangent = new Vector2(GroundNormal.y, -GroundNormal.x);
                displacement = tangent * (velocity.x * dt);
                velocity.y = 0f;
            }
            else
            {
                displacement = velocity * dt;
            }

            pos = Slide(pos, displacement, ref velocity);
            if (stickToGround)
                pos = SnapToGround(pos);

            pos.z = GameplayPlane.Z;
            transform.position = pos;

            ProbeGround(pos, velocity);
            ProbeWalls(pos);

            if (IsGrounded && velocity.y < 0f)
                velocity.y = 0f;
            return velocity;
        }

        // ------------------------------------------------------------------ movement

        Vector3 Slide(Vector3 pos, Vector2 displacement, ref Vector2 velocity)
        {
            for (int i = 0; i < MaxSlideIterations; i++)
            {
                float distance = displacement.magnitude;
                if (distance < MinMoveDistance)
                    break;

                Vector2 dir = displacement / distance;
                if (!Cast(pos, dir, distance + skinWidth, true, out RaycastHit hit))
                {
                    pos += (Vector3)displacement;
                    break;
                }

                float travel = Mathf.Max(hit.distance - skinWidth, 0f);
                pos += (Vector3)(dir * travel);
                displacement -= dir * travel;

                Vector2 normal = Flatten(hit.normal);
                if (normal.y < CeilingMaxNormalY)
                    HitCeilingThisMove = true;

                displacement = Clip(displacement, normal);
                velocity = Clip(velocity, normal);
            }
            return pos;
        }

        /// <summary>
        /// Removes the part of the motion that points into the surface. On slopes too steep to stand on,
        /// sliding is never allowed to turn sideways motion into upward motion, so they can't be walked up.
        /// </summary>
        Vector2 Clip(Vector2 motion, Vector2 normal)
        {
            float into = Vector2.Dot(motion, normal);
            if (into >= 0f)
                return motion;

            Vector2 clipped = motion - normal * into;
            bool steepFloor = normal.y > 0f && !IsWalkable(normal);
            if (steepFloor && motion.y <= 0f && clipped.y > 0f)
                clipped = new Vector2(0f, motion.y);
            return clipped;
        }

        Vector3 SnapToGround(Vector3 pos)
        {
            if (Cast(pos, Vector2.down, GroundSnapDistance + skinWidth, true, out RaycastHit hit)
                && IsWalkable(Flatten(hit.normal)))
                pos.y -= Mathf.Max(hit.distance - skinWidth, 0f);
            return pos;
        }

        /// <summary>Pushes the capsule out of solid geometry it starts inside (e.g. after spawning).</summary>
        Vector3 Depenetrate(Vector3 pos)
        {
            GetCapsule(pos, out Vector3 bottom, out Vector3 top, out float radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, overlaps, solidLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider other = overlaps[i];
                if (other == capsule)
                    continue;

                if (Physics.ComputePenetration(capsule, pos, transform.rotation,
                        other, other.transform.position, other.transform.rotation,
                        out Vector3 direction, out float distance))
                {
                    direction.z = 0f;
                    if (direction.sqrMagnitude > 0.01f)
                        pos += direction.normalized * (distance + skinWidth);
                }
            }
            return pos;
        }

        // ------------------------------------------------------------------ probes

        void ProbeGround(Vector3 pos, Vector2 velocity)
        {
            IsGrounded = false;
            IsOnOneWayPlatform = false;
            GroundNormal = Vector2.up;

            if (velocity.y > 0.01f)
                return; // rising: never grounded, otherwise a jump would be cancelled on its first frame

            if (Cast(pos, Vector2.down, skinWidth + groundProbeDistance, true, out RaycastHit hit))
            {
                Vector2 normal = Flatten(hit.normal);
                if (IsWalkable(normal))
                {
                    IsGrounded = true;
                    GroundNormal = normal;
                    IsOnOneWayPlatform = IsOneWay(hit.collider);
                }
            }
        }

        void ProbeWalls(Vector3 pos)
        {
            WallDirection = 0;
            float distance = skinWidth + wallProbeDistance;
            if (Cast(pos, Vector2.right, distance, false, out RaycastHit hit) && IsWall(hit.normal))
                WallDirection = 1;
            else if (Cast(pos, Vector2.left, distance, false, out hit) && IsWall(hit.normal))
                WallDirection = -1;
        }

        // ------------------------------------------------------------------ queries

        bool Cast(Vector3 pos, Vector2 dir, float distance, bool includeOneWay, out RaycastHit closest)
        {
            GetCapsule(pos, out Vector3 bottom, out Vector3 top, out float radius);
            int mask = solidLayers;
            if (includeOneWay && dropThroughTimer <= 0f)
                mask |= oneWayLayers;

            int count = Physics.CapsuleCastNonAlloc(bottom, top, radius, dir, castHits, distance, mask, QueryTriggerInteraction.Ignore);
            float feetY = bottom.y - radius;
            float best = float.MaxValue;
            closest = default;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = castHits[i];
                if (hit.collider == capsule)
                    continue;
                if (hit.distance <= 0f && hit.point == Vector3.zero)
                    continue; // started overlapping: Depenetrate deals with solids, one-ways are passed through
                if (IsOneWay(hit.collider) && !OneWayBlocks(hit, dir, feetY))
                    continue;
                if (hit.distance < best)
                {
                    best = hit.distance;
                    closest = hit;
                }
            }
            return best < float.MaxValue;
        }

        static bool OneWayBlocks(RaycastHit hit, Vector2 dir, float feetY) =>
            dir.y < 0f && hit.normal.y >= OneWayMinNormalY && feetY >= hit.point.y - OneWayTolerance;

        void GetCapsule(Vector3 pos, out Vector3 bottom, out Vector3 top, out float radius)
        {
            radius = capsule.radius;
            Vector3 centre = pos + capsule.center;
            float half = Mathf.Max(capsule.height * 0.5f - radius, 0f);
            bottom = centre + Vector3.down * half;
            top = centre + Vector3.up * half;
        }

        bool IsWalkable(Vector2 normal) => Vector2.Angle(normal, Vector2.up) <= MaxSlopeAngle + 0.01f;

        static bool IsWall(Vector3 normal) => Mathf.Abs(normal.y) < WallMaxNormalY;

        bool IsOneWay(Collider c) => (oneWayLayers.value & (1 << c.gameObject.layer)) != 0;

        static Vector2 Flatten(Vector3 normal)
        {
            var n = new Vector2(normal.x, normal.y);
            return n.sqrMagnitude > 1e-6f ? n.normalized : Vector2.up;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!Application.isPlaying)
                return;

            Vector3 feet = transform.position;
            Gizmos.color = IsGrounded ? Color.green : Color.red;
            Gizmos.DrawLine(feet, feet + (Vector3)GroundNormal);

            if (WallDirection != 0)
            {
                Gizmos.color = Color.yellow;
                Vector3 mid = feet + Vector3.up * (capsule != null ? capsule.height * 0.5f : 1f);
                Gizmos.DrawLine(mid, mid + Vector3.right * WallDirection);
            }
        }
#endif
    }
}
