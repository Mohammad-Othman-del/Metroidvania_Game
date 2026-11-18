using UnityEngine;
using UnityEngine.InputSystem;

namespace Metroidvania.Input
{
    /// <summary>
    /// Reads keyboard and gamepad through the Input System's PlayerControls asset.
    /// Button presses are latched by callbacks, so a press is never lost even if the controller
    /// reads input less often than once per frame (for example from FixedUpdate).
    /// </summary>
    [DisallowMultipleComponent]
    public class HumanInputProvider : MonoBehaviour, IInputProvider
    {
        [Tooltip("Input actions with a 'Player' map containing Move, Jump and Dodge.")]
        [SerializeField] InputActionAsset actions;

        InputActionAsset instance;
        InputAction move, jump, dodge;
        bool jumpPressed, dodgePressed;

        void Awake()
        {
            if (actions == null)
            {
                Debug.LogError($"{nameof(HumanInputProvider)} on '{name}' has no Input Actions asset assigned.", this);
                enabled = false;
                return;
            }

            // Work on a copy so two providers in one scene never share action state.
            instance = Instantiate(actions);
            InputActionMap map = instance.FindActionMap("Player", throwIfNotFound: true);
            move = map.FindAction("Move", throwIfNotFound: true);
            jump = map.FindAction("Jump", throwIfNotFound: true);
            dodge = map.FindAction("Dodge", throwIfNotFound: true);
        }

        void OnEnable()
        {
            if (instance == null)
                return;

            jump.performed += OnJump;
            dodge.performed += OnDodge;
            instance.Enable();
        }

        void OnDisable()
        {
            if (instance == null)
                return;

            jump.performed -= OnJump;
            dodge.performed -= OnDodge;
            instance.Disable();
            jumpPressed = dodgePressed = false;
        }

        void OnDestroy()
        {
            if (instance != null)
                Destroy(instance);
        }

        void OnJump(InputAction.CallbackContext _) => jumpPressed = true;
        void OnDodge(InputAction.CallbackContext _) => dodgePressed = true;

        public PlayerInputFrame ReadInput()
        {
            if (!isActiveAndEnabled)
                return default;

            var frame = new PlayerInputFrame
            {
                Move = move.ReadValue<Vector2>(),
                JumpHeld = jump.IsPressed(),
                JumpPressed = jumpPressed,
                DodgePressed = dodgePressed,
            };

            jumpPressed = dodgePressed = false;
            return frame;
        }

#if UNITY_EDITOR
        // Auto-fill the asset when the component is added in the editor.
        void Reset()
        {
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("PlayerControls t:InputActionAsset"))
            {
                actions = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (actions != null)
                    break;
            }
        }
#endif
    }
}
