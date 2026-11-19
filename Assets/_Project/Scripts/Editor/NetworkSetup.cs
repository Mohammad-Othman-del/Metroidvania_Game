using System.Collections.Generic;
using System.IO;
using Metroidvania.Network;
using Metroidvania.Player;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Metroidvania.EditorTools
{
    /// <summary>
    /// Tools > Metroidvania > Setup Network Test Scene:
    /// 1. Makes Player_Base networked (NetworkObject, owner-authoritative NetworkTransform, NetworkPlayer);
    ///    Forge and Spark inherit this because they are variants.
    /// 2. Copies the Movement test scene to NetworkTest, removes the offline players, and adds a
    ///    NetworkManager with Relay transport, session handling, spawning and the spike menu.
    /// The offline Movement scene is left as it is.
    /// </summary>
    public static class NetworkSetup
    {
        const string SourceScene = "Assets/_Project/Scenes/Test/Movement.unity";
        const string NetworkScene = "Assets/_Project/Scenes/Test/NetworkTest.unity";
        const string ForgePath = "Assets/_Project/Prefabs/Player/Forge.prefab";
        const string SparkPath = "Assets/_Project/Prefabs/Player/Spark.prefab";

        [MenuItem("Tools/Metroidvania/Setup Network Test Scene")]
        static void Setup()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ForgePath) == null || AssetDatabase.LoadAssetAtPath<GameObject>(SparkPath) == null)
            {
                EditorUtility.DisplayDialog("Forge and Spark missing", "Run Tools > Metroidvania > Place Forge and Spark first.", "OK");
                return;
            }
            if (!File.Exists(SourceScene))
            {
                EditorUtility.DisplayDialog("Movement scene missing", $"{SourceScene} not found.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            if (File.Exists(NetworkScene) && !EditorUtility.DisplayDialog("Rebuild network test scene?",
                    $"{NetworkScene} already exists. Rebuilding copies the Movement scene again and replaces it.", "Rebuild", "Cancel"))
                return;

            // 1. Networked prefabs.
            MakeNetworked(TestPlayerSetup.PrefabPath);
            ResaveVariant(ForgePath); // gives each variant its own network id
            ResaveVariant(SparkPath);
            var forge = AssetDatabase.LoadAssetAtPath<GameObject>(ForgePath);
            var spark = AssetDatabase.LoadAssetAtPath<GameObject>(SparkPath);
            NetworkPrefabsList prefabList = GetPrefabList();
            Register(prefabList, forge);
            Register(prefabList, spark);

            // 2. Scene.
            if (File.Exists(NetworkScene))
                AssetDatabase.DeleteAsset(NetworkScene);
            AssetDatabase.CopyAsset(SourceScene, NetworkScene);
            var scene = EditorSceneManager.OpenScene(NetworkScene, OpenSceneMode.Single);

            RemoveOfflinePlayers();
            CreateNetworkManager(forge, spark, prefabList);

            EditorSceneManager.SaveScene(scene);
            AddToBuildSettings(NetworkScene);
            Debug.Log($"[NetworkSetup] Built {NetworkScene}. Host plays Forge, the joining player plays Spark.");
        }

        static void MakeNetworked(string prefabPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                if (!root.TryGetComponent(out NetworkObject _))
                    root.AddComponent<NetworkObject>();

                if (!root.TryGetComponent(out NetworkTransform transform))
                    transform = root.AddComponent<NetworkTransform>();

                // The owner moves its own character; everyone else receives the position.
                transform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
                transform.SyncPositionX = true;
                transform.SyncPositionY = true;
                transform.SyncPositionZ = false; // always on the gameplay plane
                transform.SyncRotAngleX = transform.SyncRotAngleY = transform.SyncRotAngleZ = false; // root never rotates
                transform.SyncScaleX = transform.SyncScaleY = transform.SyncScaleZ = false;          // facing is synced separately
                transform.Interpolate = true;

                if (!root.TryGetComponent(out NetworkPlayer _))
                    root.AddComponent<NetworkPlayer>();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void ResaveVariant(string path)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        const string DefaultPrefabListPath = "Assets/DefaultNetworkPrefabs.asset"; // created by Netcode itself

        static NetworkPrefabsList GetPrefabList()
        {
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(DefaultPrefabListPath);
            if (list != null)
                return list;

            foreach (string guid in AssetDatabase.FindAssets("t:NetworkPrefabsList"))
            {
                list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(AssetDatabase.GUIDToAssetPath(guid));
                if (list != null)
                    return list;
            }

            list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
            AssetDatabase.CreateAsset(list, DefaultPrefabListPath);
            return list;
        }

        static void Register(NetworkPrefabsList list, GameObject prefab)
        {
            if (list.Contains(prefab))
                return;
            list.Add(new NetworkPrefab { Prefab = prefab });
            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssetIfDirty(list);
        }

        static void RemoveOfflinePlayers()
        {
            foreach (var switcher in Object.FindObjectsByType<TestCharacterSwitcher>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(switcher.gameObject);
            foreach (var player in Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(player.gameObject);
        }

        static void CreateNetworkManager(GameObject forge, GameObject spark, NetworkPrefabsList prefabList)
        {
            var go = new GameObject("NetworkManager");
            var manager = go.AddComponent<NetworkManager>();
            var transport = go.AddComponent<UnityTransport>();

            manager.NetworkConfig ??= new NetworkConfig();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.PlayerPrefab = null; // PlayerSpawner decides who gets which character
            if (!manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Contains(prefabList))
                manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabList);

            var spawner = go.AddComponent<PlayerSpawner>();
            var so = new SerializedObject(spawner);
            so.FindProperty("hostCharacter").objectReferenceValue = forge.GetComponent<NetworkObject>();
            so.FindProperty("clientCharacter").objectReferenceValue = spark.GetComponent<NetworkObject>();
            so.ApplyModifiedPropertiesWithoutUndo();

            go.AddComponent<SessionService>();
            go.AddComponent<NetworkSpikeMenu>();
        }

        static void AddToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => !File.Exists(s.path));
            if (!scenes.Exists(s => s.path == path))
                scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
