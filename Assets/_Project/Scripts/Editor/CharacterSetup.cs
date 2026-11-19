using System;
using System.IO;
using Metroidvania.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Metroidvania.EditorTools
{
    /// <summary>
    /// Tools > Metroidvania > Place Forge and Spark: creates the two placeholder characters as prefab
    /// variants of Player_Base (each with its own MovementSettings asset), then puts both in the open
    /// scene with a TestCharacterSwitcher so Tab swaps between them.
    /// Existing settings assets and prefabs are reused as they are, so tuning is never overwritten.
    /// </summary>
    public static class CharacterSetup
    {
        const string PrefabDir = "Assets/_Project/Prefabs/Player";
        const string SettingsDir = "Assets/_Project/ScriptableObjects/Movement";

        sealed class CharacterSpec
        {
            public string Name;
            public float Radius;
            public float Height;
            public Color Color;
            public Action<MovementSettings> Tune;
        }

        // Forge: the tank. Slower, heavier, more momentum, falls hard.
        static readonly CharacterSpec Forge = new()
        {
            Name = "Forge",
            Radius = 0.5f,
            Height = 2.2f,
            Color = new Color(0.85f, 0.42f, 0.18f),
            Tune = s =>
            {
                s.maxRunSpeed = 6.5f;
                s.groundAcceleration = 50f;
                s.groundDeceleration = 60f;
                s.airAcceleration = 35f;
                s.airDeceleration = 20f;
                s.turnMultiplier = 1.3f;
                s.jumpHeight = 2.8f;
                s.timeToApex = 0.36f;
                s.fallGravityMultiplier = 2.0f;
                s.jumpCutGravityMultiplier = 3.0f;
                s.maxFallSpeed = 26f;
                s.coyoteTime = 0.1f;
                s.jumpBufferTime = 0.12f;
                s.dodgeSpeed = 12f;
                s.dodgeDuration = 0.22f;
                s.dodgeCooldown = 0.5f;
                s.airDodges = 1;
            },
        };

        // Spark: the agile one. Faster, lighter, more air control, floatier.
        static readonly CharacterSpec Spark = new()
        {
            Name = "Spark",
            Radius = 0.35f,
            Height = 1.8f,
            Color = new Color(0.3f, 0.9f, 1f),
            Tune = s =>
            {
                s.maxRunSpeed = 10f;
                s.groundAcceleration = 110f;
                s.groundDeceleration = 120f;
                s.airAcceleration = 80f;
                s.airDeceleration = 40f;
                s.turnMultiplier = 1.6f;
                s.jumpHeight = 3.8f;
                s.timeToApex = 0.42f;
                s.fallGravityMultiplier = 1.5f;
                s.jumpCutGravityMultiplier = 3.2f;
                s.maxFallSpeed = 18f;
                s.coyoteTime = 0.12f;
                s.jumpBufferTime = 0.12f;
                s.groundSnapDistance = 0.4f;
                s.dodgeSpeed = 20f;
                s.dodgeDuration = 0.2f;
                s.dodgeCooldown = 0.3f;
                s.airDodges = 1;
            },
        };

        [MenuItem("Tools/Metroidvania/Place Forge and Spark")]
        static void PlaceForgeAndSpark()
        {
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TestPlayerSetup.PrefabPath);
            if (basePrefab == null)
            {
                EditorUtility.DisplayDialog("Player_Base missing",
                    $"{TestPlayerSetup.PrefabPath} not found. Run Tools > Metroidvania > Place Test Player first.", "OK");
                return;
            }

            var existing = Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var switchers = Object.FindObjectsByType<TestCharacterSwitcher>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (existing.Length > 0 && !EditorUtility.DisplayDialog("Replace players?",
                    $"The scene has {existing.Length} player object(s). Replace them with Forge and Spark?", "Replace", "Cancel"))
                return;

            foreach (var p in existing)
                Undo.DestroyObjectImmediate(p.gameObject);
            foreach (var s in switchers)
                Undo.DestroyObjectImmediate(s.gameObject);

            GameObject forgePrefab = GetOrCreateVariant(basePrefab, Forge);
            GameObject sparkPrefab = GetOrCreateVariant(basePrefab, Spark);

            GameObject spawn = GameObject.Find("PlayerSpawn");
            Vector3 position = (spawn != null ? spawn.transform.position : new Vector3(3f, 0f, 0f)) + Vector3.up * 0.05f;

            PlayerController forge = Place(forgePrefab, position);
            PlayerController spark = Place(sparkPrefab, position);
            spark.gameObject.SetActive(false);

            SideCameraSetup.ConfigureFollow(forge);
            SideCameraSetup.ConfigureBounds();

            var switcherGo = new GameObject("CharacterSwitcher");
            Undo.RegisterCreatedObjectUndo(switcherGo, "Place Forge and Spark");
            var switcher = switcherGo.AddComponent<TestCharacterSwitcher>();
            var so = new SerializedObject(switcher);
            SerializedProperty list = so.FindProperty("characters");
            list.arraySize = 2;
            list.GetArrayElementAtIndex(0).objectReferenceValue = forge;
            list.GetArrayElementAtIndex(1).objectReferenceValue = spark;
            so.FindProperty("followCamera").objectReferenceValue = SideCameraSetup.FindVirtualCamera();
            so.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = forge.gameObject;
            EditorSceneManager.MarkSceneDirty(forge.gameObject.scene);
            Debug.Log("[CharacterSetup] Placed Forge (active) and Spark (inactive). Press Tab in Play mode to switch.");
        }

        static PlayerController Place(GameObject prefab, Vector3 position)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Place Forge and Spark");
            instance.transform.position = position;
            return instance.GetComponent<PlayerController>();
        }

        static GameObject GetOrCreateVariant(GameObject basePrefab, CharacterSpec spec)
        {
            string path = $"{PrefabDir}/{spec.Name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
                return existing;

            MovementSettings settings = GetOrCreateSettings(spec);
            Material bodyMat = TestPlayerSetup.GetOrCreateMaterial($"M_{spec.Name}_Body", spec.Color);

            // Saving a prefab instance as a new asset creates a Prefab Variant of the original.
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            try
            {
                instance.name = spec.Name;

                var capsule = instance.GetComponent<CapsuleCollider>();
                capsule.radius = spec.Radius;
                capsule.height = spec.Height;
                capsule.center = new Vector3(0f, spec.Height * 0.5f, 0f);

                Transform body = instance.transform.Find("Visual/Body");
                body.localPosition = new Vector3(0f, spec.Height * 0.5f, 0f);
                body.localScale = new Vector3(spec.Radius * 2f, spec.Height * 0.5f, spec.Radius * 2f);
                body.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;

                Transform nose = instance.transform.Find("Visual/FacingIndicator");
                nose.localPosition = new Vector3(spec.Radius * 0.9f, spec.Height * 0.72f, 0f);

                SetField(instance.GetComponent<PlayerController>(), "settings", settings);
                SetFloat(instance.GetComponent<CameraFollowTarget>(), "height", spec.Height * 0.75f);

                return PrefabUtility.SaveAsPrefabAsset(instance, path);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        static MovementSettings GetOrCreateSettings(CharacterSpec spec)
        {
            string path = $"{SettingsDir}/SO_Movement_{spec.Name}.asset";
            var settings = AssetDatabase.LoadAssetAtPath<MovementSettings>(path);
            if (settings != null)
                return settings;

            Directory.CreateDirectory(SettingsDir);
            settings = ScriptableObject.CreateInstance<MovementSettings>();
            spec.Tune(settings);
            AssetDatabase.CreateAsset(settings, path);
            return settings;
        }

        static void SetField(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetFloat(Object target, string field, float value)
        {
            if (target == null)
                return;
            var so = new SerializedObject(target);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
