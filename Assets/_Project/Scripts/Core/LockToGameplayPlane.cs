using UnityEngine;

namespace Metroidvania.Core
{
    /// <summary>
    /// Keeps an object on the gameplay plane.
    /// Dynamic rigidbodies get physics constraints (no Z movement, no tumbling out of the plane);
    /// anything else is snapped back to the plane after it moves each frame.
    /// The player controller keeps itself on the plane and does not need this.
    /// </summary>
    [DisallowMultipleComponent]
    public class LockToGameplayPlane : MonoBehaviour
    {
        [Tooltip("Dynamic rigidbodies only: stop rotation around X and Y so the object can't tip out of the plane.")]
        [SerializeField] bool freezeOutOfPlaneRotation = true;

        Rigidbody body;
        bool usesPhysics;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            usesPhysics = body != null && !body.isKinematic;

            if (usesPhysics)
            {
                RigidbodyConstraints constraints = body.constraints | RigidbodyConstraints.FreezePositionZ;
                if (freezeOutOfPlaneRotation)
                    constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
                body.constraints = constraints;
            }

            Snap();
        }

        void LateUpdate()
        {
            if (!usesPhysics)
                Snap();
        }

        void Snap()
        {
            Vector3 position = transform.position;
            if (!Mathf.Approximately(position.z, GameplayPlane.Z))
                transform.position = GameplayPlane.Project(position);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.6f);
            Vector3 p = GameplayPlane.Project(transform.position);
            Gizmos.DrawLine(p + Vector3.left * 2f, p + Vector3.right * 2f);
            Gizmos.DrawLine(p + Vector3.down * 2f, p + Vector3.up * 2f);
        }
#endif
    }
}
