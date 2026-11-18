using UnityEngine;

namespace Metroidvania.Core
{
    /// <summary>
    /// The game is 2.5D: everything that plays happens on a single vertical plane at a fixed Z.
    /// Level geometry has depth for looks, but movement never leaves this plane.
    /// </summary>
    public static class GameplayPlane
    {
        public const float Z = 0f;

        /// <summary>Returns the point moved onto the gameplay plane.</summary>
        public static Vector3 Project(Vector3 point)
        {
            point.z = Z;
            return point;
        }
    }
}
