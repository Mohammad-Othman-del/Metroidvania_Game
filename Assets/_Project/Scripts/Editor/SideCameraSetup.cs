using Metroidvania.Core;
using Metroidvania.Player;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Metroidvania.EditorTools
{
    /// <summary>
    /// Sets up the side-on camera in the open scene (Tools > Metroidvania > Setup Side Camera):
    /// lens and distance, following the player with a dead zone and look-ahead, and room bounds.
    /// Safe to run again: it updates what is already there instead of adding duplicates.
    /// </summary>
    public static class SideCameraSetup
    {
        public const string VirtualCameraName = "CM_SideCamera";
        public const string BoundsName = "CameraBounds";

        // Lens. A low field of view keeps the view near-orthographic but still shows some depth.
        const float FieldOfView = 25f;       // vertical FOV in degrees; lower = flatter look
        const float VisibleHeight = 14f;     // world metres visible top to bottom at the gameplay plane
        const float NearClip = 1f;
        const float FarClip = 150f;

        // Follow. Screen sizes are fractions of the screen (1 = full screen).
        static readonly Vector2 DeadZoneSize = new(0.1f, 0.2f);   // small hops don't move the camera
        static readonly Vector2 HardLimitSize = new(0.6f, 0.7f);  // the player never gets closer to the edge than this
        static readonly Vector3 FollowDamping = new(0.4f, 0.35f, 0f);

        // Bounds: pulled in from the level's outer edges so the camera never sees past them,
        // even at the back wall, where the perspective view is slightly wider.
        const float BoundsInset = 1.5f;
        const float BoundsSlowingDistance = 1.5f;

        /// <summary>Camera distance from the gameplay plane that shows <see cref="VisibleHeight"/> at <see cref="FieldOfView"/>.</summary>
        public static float Distance => VisibleHeight * 0.5f / Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);

        [MenuItem("Tools/Metroidvania/Setup Side Camera")]
        static void SetupFromMenu()
        {
            Apply();

            var player = Object.FindFirstObjectByType<PlayerController>();
            if (player != null)
                ConfigureFollow(player);
            else
                Debug.LogWarning("[SideCameraSetup] No player in the scene; camera won't follow anything. Run Place Test Player.");

            ConfigureBounds();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[SideCameraSetup] FOV {FieldOfView}, distance {Distance:0.0} m, {VisibleHeight} m visible height.");
        }

        /// <summary>Creates or updates the brain, the Cinemachine camera and its lens.</summary>
        public static CinemachineCamera Apply()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                Undo.RegisterCreatedObjectUndo(go, "Create Main Camera");
                mainCamera = go.AddComponent<Camera>();
            }

            Undo.RecordObject(mainCamera, "Setup Side Camera");
            mainCamera.orthographic = false;
            mainCamera.fieldOfView = FieldOfView;
            mainCamera.nearClipPlane = NearClip;
            mainCamera.farClipPlane = FarClip;

            if (!mainCamera.TryGetComponent(out CinemachineBrain _))
                Undo.AddComponent<CinemachineBrain>(mainCamera.gameObject);

            CinemachineCamera vcam = FindVirtualCamera();
            if (vcam == null)
            {
                var go = new GameObject(VirtualCameraName);
                Undo.RegisterCreatedObjectUndo(go, "Create Side Camera");
                vcam = go.AddComponent<CinemachineCamera>();
            }

            Undo.RecordObject(vcam, "Setup Side Camera");
            LensSettings lens = vcam.Lens;
            lens.FieldOfView = FieldOfView;
            lens.NearClipPlane = NearClip;
            lens.FarClipPlane = FarClip;
            vcam.Lens = lens;

            // Frame the spawn point until something is followed.
            Vector3 focus = Vector3.zero;
            var spawn = GameObject.Find("PlayerSpawn");
            if (spawn != null)
                focus = spawn.transform.position;

            Undo.RecordObject(vcam.transform, "Setup Side Camera");
            vcam.transform.SetPositionAndRotation(
                new Vector3(focus.x, focus.y + VisibleHeight * 0.17f, GameplayPlane.Z - Distance),
                Quaternion.identity);
            Undo.RecordObject(mainCamera.transform, "Setup Side Camera");
            mainCamera.transform.SetPositionAndRotation(vcam.transform.position, Quaternion.identity);

            return vcam;
        }

        /// <summary>Follows the player's look-ahead target with a dead zone and hard limits.</summary>
        public static void ConfigureFollow(PlayerController player)
        {
            CinemachineCamera vcam = FindVirtualCamera();
            if (vcam == null)
                vcam = Apply();

            Transform target = GetOrAddCameraTarget(player);
            Undo.RecordObject(vcam, "Follow Player");
            vcam.Follow = target;

            if (!vcam.TryGetComponent(out CinemachinePositionComposer composer))
                composer = Undo.AddComponent<CinemachinePositionComposer>(vcam.gameObject);

            Undo.RecordObject(composer, "Configure Follow");
            composer.CameraDistance = Distance;
            composer.TargetOffset = Vector3.zero; // height and look-ahead come from CameraFollowTarget
            composer.Damping = FollowDamping;
            composer.Lookahead = new LookaheadSettings { Enabled = false };
            composer.CenterOnActivate = true;

            ScreenComposerSettings composition = composer.Composition;
            composition.ScreenPosition = Vector2.zero;
            composition.DeadZone = new ScreenComposerSettings.DeadZoneSettings { Enabled = true, Size = DeadZoneSize };
            composition.HardLimits = new ScreenComposerSettings.HardLimitSettings { Enabled = true, Size = HardLimitSize, Offset = Vector2.zero };
            composer.Composition = composition;
        }

        /// <summary>
        /// Confines the camera to a box around the level. For now the whole test level is one room;
        /// switching bounds between rooms comes when the world has rooms.
        /// </summary>
        public static void ConfigureBounds()
        {
            if (!TryGetLevelBounds(out Bounds level))
            {
                Debug.LogWarning("[SideCameraSetup] No 'Level' object with renderers found; skipping camera bounds.");
                return;
            }

            CinemachineCamera vcam = FindVirtualCamera();
            if (vcam == null)
                vcam = Apply();

            GameObject boundsGo = GameObject.Find(BoundsName);
            if (boundsGo == null)
            {
                boundsGo = new GameObject(BoundsName);
                Undo.RegisterCreatedObjectUndo(boundsGo, "Create Camera Bounds");
            }
            boundsGo.layer = 2; // Ignore Raycast: this collider only exists for the camera

            if (!boundsGo.TryGetComponent(out BoxCollider2D box))
                box = Undo.AddComponent<BoxCollider2D>(boundsGo);

            Undo.RecordObject(boundsGo.transform, "Camera Bounds");
            Undo.RecordObject(box, "Camera Bounds");
            boundsGo.transform.SetPositionAndRotation(new Vector3(level.center.x, level.center.y, GameplayPlane.Z), Quaternion.identity);
            boundsGo.transform.localScale = Vector3.one;
            box.isTrigger = true;
            box.offset = Vector2.zero;
            box.size = new Vector2(
                Mathf.Max(level.size.x - 2f * BoundsInset, 1f),
                Mathf.Max(level.size.y - 2f * BoundsInset, 1f));

            if (!vcam.TryGetComponent(out CinemachineConfiner2D confiner))
                confiner = Undo.AddComponent<CinemachineConfiner2D>(vcam.gameObject);

            Undo.RecordObject(confiner, "Camera Bounds");
            confiner.BoundingShape2D = box;
            confiner.Damping = 0f;
            confiner.SlowingDistance = BoundsSlowingDistance;
            confiner.InvalidateBoundingShapeCache();
        }

        public static CinemachineCamera FindVirtualCamera()
        {
            foreach (var cam in Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (cam.name == VirtualCameraName)
                    return cam;
            return null;
        }

        /// <summary>
        /// Returns the player's camera target, adding it first if the player predates it.
        /// For prefab instances the prefab asset itself is upgraded, so every instance gets it.
        /// </summary>
        static Transform GetOrAddCameraTarget(PlayerController player)
        {
            var follow = player.GetComponent<CameraFollowTarget>();
            if (follow != null && follow.Target != null)
                return follow.Target;

            GameObject go = player.gameObject;
            if (PrefabUtility.IsPartOfPrefabInstance(go))
            {
                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    TestPlayerSetup.EnsureCameraTarget(contents);
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
            else
            {
                Undo.RegisterFullObjectHierarchyUndo(go, "Add Camera Target");
                TestPlayerSetup.EnsureCameraTarget(go);
            }

            follow = player.GetComponent<CameraFollowTarget>();
            if (follow != null && follow.Target != null)
                return follow.Target;

            Transform child = player.transform.Find(TestPlayerSetup.CameraTargetName);
            return child != null ? child : player.transform;
        }

        static bool TryGetLevelBounds(out Bounds bounds)
        {
            bounds = default;
            GameObject level = GameObject.Find("Level");
            if (level == null)
                return false;

            bool found = false;
            foreach (Renderer r in level.GetComponentsInChildren<Renderer>())
            {
                if (!found)
                {
                    bounds = r.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }
            return found;
        }
    }
}
