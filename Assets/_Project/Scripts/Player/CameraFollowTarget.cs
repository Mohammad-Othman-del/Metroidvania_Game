using UnityEngine;

namespace Metroidvania.Player
{
    /// <summary>
    /// Moves a child transform that the Cinemachine camera follows, instead of the camera following
    /// the player directly. The point leads ahead in the facing direction (and keeps leading when the
    /// player stops), and drops below the player during long falls so you can see where you'll land.
    /// </summary>
    [DefaultExecutionOrder(100)] // after PlayerController has moved, before Cinemachine updates in LateUpdate
    [RequireComponent(typeof(PlayerController))]
    [DisallowMultipleComponent]
    public class CameraFollowTarget : MonoBehaviour
    {
        [Tooltip("Child transform the Cinemachine camera follows.")]
        [SerializeField] Transform target;
        [Tooltip("Height above the feet that the camera centres on.")]
        [SerializeField] float height = 1.5f;

        [Header("Look-ahead")]
        [Tooltip("How far ahead of the player, in the facing direction, the camera centres.")]
        [SerializeField, Min(0f)] float lookAheadDistance = 2.5f;
        [Tooltip("Seconds to swing across after turning around. Higher = calmer camera.")]
        [SerializeField, Min(0.01f)] float lookAheadSmoothTime = 0.6f;

        [Header("Look down on long falls")]
        [SerializeField, Min(0f)] float fallLookDownDistance = 3f;
        [Tooltip("Seconds of falling before the camera starts looking down. A normal jump's fall is about 0.3 s.")]
        [SerializeField, Min(0f)] float fallLookDownDelay = 0.4f;
        [SerializeField, Min(0.01f)] float fallSmoothTime = 0.25f;

        PlayerController player;
        Vector2 offset;
        Vector2 offsetVelocity;
        float fallTime;

        public Transform Target => target;

        void Awake()
        {
            player = GetComponent<PlayerController>();
            if (target == null)
            {
                Debug.LogError($"{nameof(CameraFollowTarget)} on '{name}' has no target assigned.", this);
                enabled = false;
                return;
            }

            // Start already leading, so the first frame doesn't swing.
            offset = new Vector2(player.Facing * lookAheadDistance, 0f);
            Apply();
        }

        void Update()
        {
            bool falling = !player.IsGrounded && player.Velocity.y < 0f;
            fallTime = falling ? fallTime + Time.deltaTime : 0f;

            float desiredX = player.Facing * lookAheadDistance;
            float desiredY = fallTime > fallLookDownDelay ? -fallLookDownDistance : 0f;

            offset.x = Mathf.SmoothDamp(offset.x, desiredX, ref offsetVelocity.x, lookAheadSmoothTime);
            offset.y = Mathf.SmoothDamp(offset.y, desiredY, ref offsetVelocity.y, fallSmoothTime);
            Apply();
        }

        void Apply() => target.localPosition = new Vector3(offset.x, height + offset.y, 0f);

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (target == null)
                return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(target.position, 0.25f);
        }
#endif
    }
}
