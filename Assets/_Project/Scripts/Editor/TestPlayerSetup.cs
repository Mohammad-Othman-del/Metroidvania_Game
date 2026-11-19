using System.IO;
using Metroidvania.Input;
using Metroidvania.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Metroidvania.EditorTools
{
    /// <summary>
    /// Tools > Metroidvania > Place Test Player: builds the placeholder capsule player prefab (if needed),
    /// puts an instance at PlayerSpawn in the open scene and points the side camera at it.
    /// </summary>
    public static class TestPlayerSetup
    {
        public const string CameraTargetName = "CameraTarget";

        internal const string PrefabPath = "Assets/_Project/Prefabs/Player/Player_Base.prefab";
        const string SettingsPath = "Assets/_Project/ScriptableObjects/Movement/SO_Movement_Default.asset";
        const string MaterialDir = "Assets/_Project/Art/Materials/Placeholder";

        const float CapsuleRadius = 0.4f;
        const float CapsuleHeight = 2f;

        [MenuItem("Tools/Metroidvania/Place Test Player")]
        static void PlaceTestPlayer()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab != null)
            {
                int choice = EditorUtility.DisplayDialogComplex(
                    "Player prefab exists",
                    $"{PrefabPath} already exists. Use it as is, or rebuild it from scratch (loses edits made to the prefab)?",
                    "Use existing", "Cancel", "Rebuild");
                if (choice == 1)
                    return;
                if (choice == 2)
                    prefab = null;
            }

            if (prefab == null)
                prefab = BuildPrefab();

            var existing = Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (existing.Length > 0)
            {
                if (!EditorUtility.DisplayDialog("Replace player?",
                        $"The scene already has {existing.Length} player object(s). Replace them?", "Replace", "Cancel"))
                    return;
                foreach (var p in existing)
                    Undo.DestroyObjectImmediate(p.gameObject);
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Place Test Player");
            GameObject spawn = GameObject.Find("PlayerSpawn");
            Vector3 spawnPos = spawn != null ? spawn.transform.position : new Vector3(3f, 0f, 0f);
            instance.transform.position = spawnPos + Vector3.up * 0.05f;

            SideCameraSetup.ConfigureFollow(instance.GetComponent<PlayerController>());
            SideCameraSetup.ConfigureBounds();

            Selection.activeGameObject = instance;
            EditorSceneManager.MarkSceneDirty(instance.scene);
            Debug.Log($"[TestPlayerSetup] Placed {prefab.name} at {instance.transform.position}.");
        }

        static GameObject BuildPrefab()
        {
            MovementSettings settings = GetOrCreateSettings();
            Material bodyMat = GetOrCreateMaterial("M_Player_Body", new Color(0.25f, 0.55f, 0.95f));
            Material noseMat = GetOrCreateMaterial("M_Player_Facing", new Color(1f, 0.9f, 0.3f));

            var root = new GameObject("Player_Base");
            root.layer = TestLevelBuilder.PlayerLayer;

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.radius = CapsuleRadius;
            capsule.height = CapsuleHeight;
            capsule.center = new Vector3(0f, CapsuleHeight * 0.5f, 0f);

            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            var motor = root.AddComponent<CharacterMotor>();
            var motorSo = new SerializedObject(motor);
            motorSo.FindProperty("solidLayers").intValue = (1 << 0) | (1 << TestLevelBuilder.GroundLayer);
            motorSo.FindProperty("oneWayLayers").intValue = 1 << TestLevelBuilder.OneWayPlatformLayer;
            motorSo.ApplyModifiedPropertiesWithoutUndo();

            var inputProvider = root.AddComponent<HumanInputProvider>();
            var inputSo = new SerializedObject(inputProvider);
            inputSo.FindProperty("actions").objectReferenceValue = FindPlayerControls();
            inputSo.ApplyModifiedPropertiesWithoutUndo();

            // Visuals live under a child so they can be mirrored without touching the physics root.
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);

            GameObject bodyMesh = Primitive(PrimitiveType.Capsule, "Body", visual, bodyMat);
            bodyMesh.transform.localPosition = new Vector3(0f, CapsuleHeight * 0.5f, 0f);
            bodyMesh.transform.localScale = new Vector3(CapsuleRadius * 2f, CapsuleHeight * 0.5f, CapsuleRadius * 2f);

            GameObject nose = Primitive(PrimitiveType.Cube, "FacingIndicator", visual, noseMat);
            nose.transform.localPosition = new Vector3(CapsuleRadius * 0.9f, CapsuleHeight * 0.72f, 0f);
            nose.transform.localScale = new Vector3(0.35f, 0.18f, 0.5f);

            var controller = root.AddComponent<PlayerController>();
            var controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("settings").objectReferenceValue = settings;
            controllerSo.FindProperty("visual").objectReferenceValue = visual;
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            EnsureCameraTarget(root);

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.layer = TestLevelBuilder.PlayerLayer;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>Adds the CameraTarget child and the CameraFollowTarget component that drives it, if missing.</summary>
        internal static void EnsureCameraTarget(GameObject root)
        {
            Transform target = root.transform.Find(CameraTargetName);
            if (target == null)
            {
                target = new GameObject(CameraTargetName).transform;
                target.SetParent(root.transform, false);
                target.localPosition = new Vector3(0f, 1.5f, 0f);
            }

            if (!root.TryGetComponent(out CameraFollowTarget follow))
                follow = root.AddComponent<CameraFollowTarget>();

            var so = new SerializedObject(follow);
            so.FindProperty("target").objectReferenceValue = target;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static MovementSettings GetOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<MovementSettings>(SettingsPath);
            if (settings != null)
                return settings;

            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            settings = ScriptableObject.CreateInstance<MovementSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            return settings;
        }

        internal static Material GetOrCreateMaterial(string name, Color color)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
                return mat;

            Directory.CreateDirectory(MaterialDir);
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.3f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static InputActionAsset FindPlayerControls()
        {
            foreach (string guid in AssetDatabase.FindAssets("PlayerControls t:InputActionAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null)
                    return asset;
            }
            Debug.LogError("[TestPlayerSetup] PlayerControls input actions not found; assign it on HumanInputProvider by hand.");
            return null;
        }
    }
}
