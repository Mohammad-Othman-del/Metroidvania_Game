using Metroidvania.Input;
using Metroidvania.Player;
using Metroidvania.Player.States;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

namespace Metroidvania.Network
{
    /// <summary>
    /// Connects a player character to Netcode. Movement is owner-authoritative (GDD section 7):
    /// the owning machine runs the controller with its own input, and its NetworkTransform
    /// (Authority Mode: Owner) sends the position to everyone else. Remote copies only display it.
    /// Outside a network session this component does nothing, so the same prefab still works offline.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    [DisallowMultipleComponent]
    public class NetworkPlayer : NetworkBehaviour
    {
        // Owner writes, everyone reads. Sent only when the value changes.
        readonly NetworkVariable<sbyte> facing = new(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<byte> state = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        PlayerController controller;
        HumanInputProvider humanInput;
        CameraFollowTarget cameraTarget;

        /// <summary>Movement state as seen by everyone (for animation and UI later).</summary>
        public PlayerStateId State => (PlayerStateId)state.Value;

        void Awake()
        {
            controller = GetComponent<PlayerController>();
            humanInput = GetComponent<HumanInputProvider>();
            cameraTarget = GetComponent<CameraFollowTarget>();
        }

        public override void OnNetworkSpawn()
        {
            bool local = IsOwner;

            // Only the owner simulates and reads input; remote copies just follow the network.
            controller.enabled = local;
            if (humanInput != null)
                humanInput.enabled = local;
            if (cameraTarget != null)
                cameraTarget.enabled = local;

            name = $"{name.Replace("(Clone)", "")} [{(local ? "local" : "remote")} {OwnerClientId}]";

            if (local)
                PointCameraAtMe();
        }

        void Update()
        {
            if (!IsSpawned)
                return;

            if (IsOwner)
            {
                facing.Value = (sbyte)controller.Facing;
                state.Value = (byte)controller.State;
            }
            else
            {
                controller.ShowFacing(facing.Value);
            }
        }

        void PointCameraAtMe()
        {
            var vcam = FindFirstObjectByType<CinemachineCamera>();
            if (vcam == null)
                return;

            vcam.Follow = cameraTarget != null && cameraTarget.Target != null ? cameraTarget.Target : transform;
            vcam.PreviousStateIsValid = false; // snap to the player instead of panning from the spawn view
        }
    }
}
