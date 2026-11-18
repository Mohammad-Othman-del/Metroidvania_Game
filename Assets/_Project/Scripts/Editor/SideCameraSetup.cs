using Metroidvania.Core;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Metroidvania.EditorTools
{
    /// <summary>
    /// Sets up the side-on camera in the open scene (Tools > Metroidvania > Setup Side Camera).
    /// A low field of view keeps the view close to orthographic but still shows a little of the
    /// geometry's depth. Safe to run again: it updates the existing camera instead of adding another.
    /// </summary>
    public static class SideCameraSetup
    {
        public const string VirtualCameraName = "CM_SideCamera";

        // Starting values, tune them on the CinemachineCamera afterwards.
        const float FieldOfView = 25f;       // vertical FOV in degrees; lower = flatter look
        const float VisibleHeight = 14f;     // world metres visible top to bottom at the gameplay plane
        const float NearClip = 1f;
        const float FarClip = 150f;

        /// <summary>Camera distance from the gameplay plane that shows <see cref="VisibleHeight"/> at <see cref="FieldOfView"/>.</summary>
        public static float Distance => VisibleHeight * 0.5f / Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);

        [MenuItem("Tools/Metroidvania/Setup Side Camera")]
        static void SetupFromMenu()
        {
            Apply();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[SideCameraSetup] FOV {FieldOfView}, distance {Distance:0.0} m, {VisibleHeight} m visible height.");
        }

        public static void Apply()
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

            // Frame the spawn point with the player about a third of the way up the screen.
            // Following the player is added in the Cinemachine step.
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
        }

        public static CinemachineCamera FindVirtualCamera()
        {
            foreach (var cam in Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (cam.name == VirtualCameraName)
                    return cam;
            return null;
        }
    }
}
