using Unity.Netcode;
using UnityEngine;

namespace Metroidvania.Network
{
    /// <summary>
    /// Server-side: spawns a character for every player that connects. Slots follow the GDD:
    /// the host plays Forge, the client plays Spark (a choice screen comes later).
    /// The NetworkManager's own "Player Prefab" must stay empty so it doesn't spawn a second one.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public class PlayerSpawner : MonoBehaviour
    {
        [Tooltip("Character for the host (Forge).")]
        [SerializeField] NetworkObject hostCharacter;
        [Tooltip("Character for the joining player (Spark).")]
        [SerializeField] NetworkObject clientCharacter;
        [Tooltip("Players spawn this far apart along X so they don't overlap.")]
        [SerializeField] float spacing = 2f;

        NetworkManager networkManager;

        void Awake() => networkManager = GetComponent<NetworkManager>();

        void OnEnable() => networkManager.OnClientConnectedCallback += OnClientConnected;

        void OnDisable()
        {
            if (networkManager != null)
                networkManager.OnClientConnectedCallback -= OnClientConnected;
        }

        void OnClientConnected(ulong clientId)
        {
            if (!networkManager.IsServer)
                return;
            if (networkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) && client.PlayerObject != null)
                return;

            bool isHost = clientId == NetworkManager.ServerClientId;
            NetworkObject prefab = isHost ? hostCharacter : clientCharacter;
            if (prefab == null)
            {
                Debug.LogError($"[PlayerSpawner] No character assigned for the {(isHost ? "host" : "client")}.", this);
                return;
            }

            GameObject spawn = GameObject.Find("PlayerSpawn");
            Vector3 origin = spawn != null ? spawn.transform.position : Vector3.zero;
            Vector3 position = origin + new Vector3(isHost ? 0f : spacing, 0.05f, 0f);

            NetworkObject instance = Instantiate(prefab, position, Quaternion.identity);
            instance.SpawnAsPlayerObject(clientId, destroyWithScene: true);
        }
    }
}
