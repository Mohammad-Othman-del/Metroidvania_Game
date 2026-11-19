using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Metroidvania.Player
{
    /// <summary>
    /// Test-scene helper for comparing characters: keeps one of several player objects active and
    /// swaps to the next one in place (Tab, or the debug panel button). The camera follows along.
    /// Not used in the real game, where each player picks a character before the level loads.
    /// </summary>
    public class TestCharacterSwitcher : MonoBehaviour
    {
        [Tooltip("Player objects already in the scene. The first one starts active, the rest inactive.")]
        [SerializeField] PlayerController[] characters;
        [Tooltip("Camera that should follow whichever character is active.")]
        [SerializeField] CinemachineCamera followCamera;

        int current;

        public PlayerController Current => characters != null && characters.Length > 0 ? characters[current] : null;
        public int Count => characters?.Length ?? 0;

        void Awake()
        {
            if (Count == 0)
            {
                enabled = false;
                return;
            }

            for (int i = 0; i < characters.Length; i++)
                characters[i].gameObject.SetActive(i == 0);
            current = 0;
            PointCamera(characters[0]);
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
                SwitchToNext();
        }

        public void SwitchToNext()
        {
            if (Count < 2)
                return;

            PlayerController from = characters[current];
            current = (current + 1) % characters.Length;
            PlayerController to = characters[current];

            Vector3 position = from.transform.position;
            from.gameObject.SetActive(false);
            to.gameObject.SetActive(true); // runs Awake on first activation, so Teleport is safe right after
            to.Teleport(position + Vector3.up * 0.05f);
            PointCamera(to);
        }

        void PointCamera(PlayerController player)
        {
            if (followCamera == null)
                return;
            var target = player.GetComponent<CameraFollowTarget>();
            followCamera.Follow = target != null && target.Target != null ? target.Target : player.transform;
        }
    }
}
