using System.Collections.Generic;
using Metroidvania.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Metroidvania.UI
{
    /// <summary>
    /// In-game tuning panel. Created automatically in the editor and in development builds (never in
    /// release builds), so no scene needs to contain it. Toggle with F1 or the gamepad View/Select button.
    ///
    /// Sliders edit the player's MovementSettings asset directly, exactly like the Inspector does:
    /// in the editor, changes stay saved after leaving Play mode. "Revert" restores the values the
    /// asset had when the panel first saw it this session.
    /// </summary>
    public class DebugPanel : MonoBehaviour
    {
        const float ReferenceHeight = 1080f;   // UI is scaled so it looks the same at any resolution
        const float WindowWidth = 480f;
        const float LabelWidth = 190f;
        const float ValueWidth = 90f;
        const float PlayerSearchInterval = 1f;
        const float FpsSampleTime = 0.5f;

        static DebugPanel instance;

        bool visible;
        Rect windowRect;
        Vector2 scroll;
        float timeScale = 1f;
        string status = "";

        readonly List<PlayerController> players = new();
        int playerIndex;
        TestCharacterSwitcher switcher;
        float nextPlayerSearch;
        Vector3 spawnPosition;
        bool hasSpawn;
        readonly Dictionary<MovementSettings, string> startValues = new();

        int fpsFrames;
        float fpsTime;
        float fpsWorstFrame;
        float fps;
        float worstFrameMs;

        GUIStyle labelStyle, valueStyle, headerStyle, buttonStyle, windowStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void CreateInDevelopmentBuilds()
        {
            if (!Debug.isDebugBuild || instance != null)
                return;

            var go = new GameObject("[DebugPanel]");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<DebugPanel>();
        }

        PlayerController Player => players.Count > 0 ? players[Mathf.Clamp(playerIndex, 0, players.Count - 1)] : null;

        void OnDestroy()
        {
            Time.timeScale = 1f;
        }

        void Update()
        {
            if (TogglePressed())
                visible = !visible;

            SampleFps();

            // Destroyed or deactivated (e.g. swapped out by TestCharacterSwitcher): look again right away.
            if (players.RemoveAll(p => p == null || !p.isActiveAndEnabled) > 0)
                nextPlayerSearch = 0f;
            if (players.Count == 0 && Time.unscaledTime >= nextPlayerSearch)
            {
                nextPlayerSearch = Time.unscaledTime + PlayerSearchInterval;
                FindPlayers();
            }
        }

        static bool TogglePressed()
        {
            bool key = Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame;
            bool pad = Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame;
            return key || pad;
        }

        void FindPlayers()
        {
            players.Clear();
            players.AddRange(FindObjectsByType<PlayerController>(FindObjectsSortMode.InstanceID));
            playerIndex = 0;
            switcher = FindFirstObjectByType<TestCharacterSwitcher>();

            foreach (PlayerController p in players)
                RememberStartValues(p.Settings);

            if (!hasSpawn && players.Count > 0)
            {
                GameObject spawn = GameObject.Find("PlayerSpawn");
                spawnPosition = spawn != null ? spawn.transform.position : players[0].transform.position;
                hasSpawn = true;
            }
        }

        void RememberStartValues(MovementSettings settings)
        {
            if (settings != null && !startValues.ContainsKey(settings))
                startValues[settings] = JsonUtility.ToJson(settings);
        }

        void SampleFps()
        {
            float dt = Time.unscaledDeltaTime;
            fpsFrames++;
            fpsTime += dt;
            fpsWorstFrame = Mathf.Max(fpsWorstFrame, dt);

            if (fpsTime >= FpsSampleTime)
            {
                fps = fpsFrames / fpsTime;
                worstFrameMs = fpsWorstFrame * 1000f;
                fpsFrames = 0;
                fpsTime = 0f;
                fpsWorstFrame = 0f;
            }
        }

        // ------------------------------------------------------------------ drawing

        void OnGUI()
        {
            if (!visible)
                return;

            CreateStyles();

            float scale = Mathf.Max(Screen.height / ReferenceHeight, 0.6f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float screenW = Screen.width / scale;
            float screenH = Screen.height / scale;

            if (windowRect.width <= 0f)
                windowRect = new Rect(screenW - WindowWidth - 20f, 20f, WindowWidth, screenH - 40f);

            windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, "Debug panel  (F1 / View to hide)", windowStyle);
        }

        void DrawWindow(int id)
        {
            scroll = GUILayout.BeginScrollView(scroll);

            DrawPerformance();
            DrawPlayerInfo();

            PlayerController player = Player;
            if (player != null && player.Settings != null)
                DrawSettings(player.Settings);

            DrawTools(player);

            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 30f));
        }

        void DrawPerformance()
        {
            Header("Performance");
            Color previous = GUI.contentColor;
            GUI.contentColor = fps >= 58f && worstFrameMs < 20f ? Color.green
                : fps >= 50f ? Color.yellow
                : Color.red;
            GUILayout.Label($"FPS {fps:0.0}    worst frame {worstFrameMs:0.0} ms", labelStyle);
            GUI.contentColor = previous;
        }

        void DrawPlayerInfo()
        {
            Header("Player");
            PlayerController player = Player;
            if (player == null)
            {
                GUILayout.Label("No player in the scene.", labelStyle);
                return;
            }

            if (players.Count > 1 && GUILayout.Button($"Next player ({playerIndex + 1}/{players.Count}): {player.name}", buttonStyle))
                playerIndex = (playerIndex + 1) % players.Count;

            if (switcher != null && switcher.Count > 1 && GUILayout.Button($"Switch character (Tab)  — now: {player.name}", buttonStyle))
                switcher.SwitchToNext();

            GUILayout.Label($"State: {player.State}    Grounded: {player.IsGrounded}    Facing: {(player.Facing > 0 ? "right" : "left")}", labelStyle);
            GUILayout.Label($"Velocity: ({player.Velocity.x:0.0}, {player.Velocity.y:0.0}) m/s", labelStyle);
            GUILayout.Label($"Settings: {player.Settings.name}", labelStyle);
        }

        void DrawSettings(MovementSettings s)
        {
            bool changed = false;

            Header("Run");
            changed |= Slider("Max run speed", ref s.maxRunSpeed, 1f, 20f, 0.1f, " m/s");
            changed |= Slider("Ground accel", ref s.groundAcceleration, 5f, 200f, 1f);
            changed |= Slider("Ground decel", ref s.groundDeceleration, 5f, 200f, 1f);
            changed |= Slider("Air accel", ref s.airAcceleration, 5f, 200f, 1f);
            changed |= Slider("Air decel", ref s.airDeceleration, 0f, 200f, 1f);
            changed |= Slider("Turn multiplier", ref s.turnMultiplier, 1f, 4f, 0.05f, "x");

            Header("Jump");
            changed |= Slider("Jump height", ref s.jumpHeight, 0.5f, 8f, 0.05f, " m");
            changed |= Slider("Time to apex", ref s.timeToApex, 0.15f, 1f, 0.01f, " s");
            changed |= Slider("Fall gravity", ref s.fallGravityMultiplier, 1f, 4f, 0.05f, "x");
            changed |= Slider("Jump cut gravity", ref s.jumpCutGravityMultiplier, 1f, 6f, 0.05f, "x");
            changed |= Slider("Max fall speed", ref s.maxFallSpeed, 5f, 50f, 0.5f, " m/s");
            GUILayout.Label($"   gravity {s.Gravity:0.0} m/s²   launch speed {s.JumpVelocity:0.0} m/s", valueStyle);

            Header("Forgiveness");
            changed |= Slider("Coyote time", ref s.coyoteTime, 0f, 0.3f, 0.01f, " s");
            changed |= Slider("Jump buffer", ref s.jumpBufferTime, 0f, 0.3f, 0.01f, " s");

            Header("Dodge");
            changed |= Slider("Dodge speed", ref s.dodgeSpeed, 4f, 40f, 0.5f, " m/s");
            changed |= Slider("Dodge duration", ref s.dodgeDuration, 0.05f, 0.6f, 0.01f, " s");
            changed |= Slider("Dodge cooldown", ref s.dodgeCooldown, 0f, 2f, 0.05f, " s");
            float airDodges = s.airDodges;
            if (Slider("Air dodges", ref airDodges, 0f, 3f, 1f))
            {
                s.airDodges = Mathf.RoundToInt(airDodges);
                changed = true;
            }
            GUILayout.Label($"   dodge distance {s.dodgeSpeed * s.dodgeDuration:0.0} m", valueStyle);

            Header("Ground");
            changed |= Slider("Max slope", ref s.maxSlopeAngle, 10f, 80f, 1f, "°");

#if UNITY_EDITOR
            if (changed)
                UnityEditor.EditorUtility.SetDirty(s); // keep the asset in sync, same as editing in the Inspector
#endif
        }

        void DrawTools(PlayerController player)
        {
            Header("Tools");

            if (Slider("Time scale", ref timeScale, 0.1f, 1f, 0.05f, "x"))
                Time.timeScale = timeScale;

            GUILayout.BeginHorizontal();
            if (player != null && hasSpawn && GUILayout.Button("Respawn", buttonStyle))
                player.Teleport(spawnPosition + Vector3.up * 0.05f);
            if (GUILayout.Button("Normal speed", buttonStyle))
                Time.timeScale = timeScale = 1f;
            GUILayout.EndHorizontal();

            if (player == null || player.Settings == null)
                return;

            MovementSettings s = player.Settings;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Revert", buttonStyle) && startValues.TryGetValue(s, out string json))
            {
                JsonUtility.FromJsonOverwrite(json, s);
                MarkDirty(s);
                status = "Reverted to the values at the start of this session.";
            }
            if (GUILayout.Button("Copy values", buttonStyle))
            {
                GUIUtility.systemCopyBuffer = JsonUtility.ToJson(s, true);
                status = "Values copied to the clipboard.";
            }
            if (GUILayout.Button("Paste values", buttonStyle))
                status = TryPaste(s);
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(status))
                GUILayout.Label(status, valueStyle);
        }

        static string TryPaste(MovementSettings s)
        {
            string text = GUIUtility.systemCopyBuffer;
            if (string.IsNullOrWhiteSpace(text) || !text.TrimStart().StartsWith("{"))
                return "Clipboard doesn't contain copied values.";

            try
            {
                JsonUtility.FromJsonOverwrite(text, s);
                MarkDirty(s);
                return "Values pasted from the clipboard.";
            }
            catch (System.ArgumentException)
            {
                return "Clipboard doesn't contain valid values.";
            }
        }

        static void MarkDirty(Object asset)
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(asset);
#endif
        }

        // ------------------------------------------------------------------ widgets

        void Header(string text)
        {
            GUILayout.Space(8f);
            GUILayout.Label(text, headerStyle);
        }

        /// <summary>Labelled slider snapped to <paramref name="step"/>. Returns true if the value changed.</summary>
        bool Slider(string label, ref float value, float min, float max, float step, string unit = "")
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, labelStyle, GUILayout.Width(LabelWidth));
            float raw = GUILayout.HorizontalSlider(value, min, max, GUILayout.ExpandWidth(true), GUILayout.Height(24f));
            float snapped = Mathf.Clamp(Mathf.Round(raw / step) * step, min, max);
            string format = step >= 1f ? "0" : step >= 0.1f ? "0.0" : "0.00";
            GUILayout.Label(snapped.ToString(format) + unit, valueStyle, GUILayout.Width(ValueWidth));
            GUILayout.EndHorizontal();

            if (Mathf.Approximately(snapped, value))
                return false;
            value = snapped;
            return true;
        }

        void CreateStyles()
        {
            if (labelStyle != null)
                return;

            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 17 };
            valueStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, normal = { textColor = new Color(0.8f, 0.85f, 0.9f) } };
            headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.75f, 0.35f) } };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 16, fixedHeight = 32f };
            windowStyle = new GUIStyle(GUI.skin.window) { fontSize = 17, padding = new RectOffset(12, 12, 32, 12) };
        }
    }
}
