using Unity.Netcode;
using UnityEngine;

namespace Metroidvania.Network
{
    /// <summary>
    /// Bare-bones Host / Join box for the networking spike. The proper Host and Join screen
    /// replaces it later in Phase 2; this one only has to prove the connection works.
    /// </summary>
    [RequireComponent(typeof(SessionService))]
    public class NetworkSpikeMenu : MonoBehaviour
    {
        const float ReferenceHeight = 1080f;

        SessionService sessions;
        string joinCode = "";
        GUIStyle label, button, field, box;

        void Awake() => sessions = GetComponent<SessionService>();

        void OnGUI()
        {
            CreateStyles();
            float scale = Mathf.Max(Screen.height / ReferenceHeight, 0.6f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            GUILayout.BeginArea(new Rect(20f, 20f, 440f, 400f), box);
            GUILayout.Label(sessions.Status, label);

            GUI.enabled = !sessions.Busy;
            if (!sessions.InSession)
            {
                if (GUILayout.Button("Host (play Forge)", button))
                    _ = sessions.HostAsync();

                GUILayout.Space(10f);
                GUILayout.BeginHorizontal();
                joinCode = GUILayout.TextField(joinCode, 12, field, GUILayout.Width(200f));
                if (GUILayout.Button("Join (play Spark)", button))
                    _ = sessions.JoinAsync(joinCode);
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label($"Code: {sessions.Session.Code}    Players: {sessions.Session.PlayerCount}/{sessions.Session.MaxPlayers}", label);
                if (GUILayout.Button("Copy code", button))
                    GUIUtility.systemCopyBuffer = sessions.Session.Code;
                DrawRoundTrip();
                if (GUILayout.Button("Leave", button))
                    _ = sessions.LeaveAsync();
            }
            GUI.enabled = true;

            GUILayout.EndArea();
        }

        void DrawRoundTrip()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsConnectedClient || nm.IsServer)
                return; // the host has no round trip to itself
            ulong rtt = nm.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.ServerClientId);
            GUILayout.Label($"Round trip to host: {rtt} ms", label);
        }

        void CreateStyles()
        {
            if (label != null)
                return;
            label = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            button = new GUIStyle(GUI.skin.button) { fontSize = 18, fixedHeight = 38f };
            field = new GUIStyle(GUI.skin.textField) { fontSize = 22, fixedHeight = 38f, alignment = TextAnchor.MiddleCenter };
            box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(14, 14, 14, 14) };
        }
    }
}
