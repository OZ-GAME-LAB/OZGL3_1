#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OZ.UI;
using OZ.UI.Contracts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

/// <summary>
/// Editor-only manual play-test bridge. Sends normal device input to the real game;
/// observes state and captures the actual Game View, including overlay HUD.
/// Never starts a run, moves an actor, changes statistics, or resolves an encounter.
/// </summary>
[InitializeOnLoad]
public static class Stage01VerticalQA
{
    const string EnabledKey = "OZ.Stage01.VerticalQA.Enabled";
    const string DirectoryKey = "OZ.Stage01.VerticalQA.Directory";
    const string ScenePath = "Assets/Scenes/MainScene.unity";
    static readonly Queue<Command> Commands = new Queue<Command>();
    static readonly List<string> Errors = new List<string>();
    static Command active;
    static Key[] heldKeys = Array.Empty<Key>();
    static Vector2 pointer = new Vector2(.5f, .5f);
    static double deadline, lastTelemetry;
    static int injectedFrames;
    static string lastFileId = "", lastBatchFileId = "", lastCompletedId = "", controlDirectory;
    static bool attached, releasePending;
    static Keyboard addedKeyboard;
    static Mouse addedMouse;

    [Serializable]
    public sealed class Command
    {
        public string id;
        public string[] keys;
        public float seconds = .1f;
        // Normalized Game View coordinates, origin at top left.
        public float[] mouse;
        public bool click;
        public string capture;
        // Test-session cleanup only. Exits editor play mode, never alters a run.
        public bool endPlay;
    }

    [Serializable]
    public sealed class CommandBatch
    {
        public string id;
        public Command[] commands;
    }

    [Serializable]
    sealed class Snapshot
    {
        public string utc, project, scene, directory, commandId, lastCompletedId;
        public bool playing, editorPaused, commandActive, inputBlocked, pausedByUI;
        public bool keyboardPresent, mousePresent, keyboardEnabled, mouseEnabled;
        public string keyboardDevice, mouseDevice, focusedEditorWindow;
        public string[] observedKeys;
        public bool observedMouseLeft;
        public float gameTime, timeScale, commandSecondsRemaining;
        public int frame, screenWidth, screenHeight, injectedFrames, stageNumber;
        public string stageState;
        public PlayerSnapshot player;
        public CameraSnapshot camera;
        public EnemySnapshot[] enemies;
        public GateSnapshot[] gates;
        public ButtonSnapshot[] buttons;
        public string[] uiStack, errors;
    }

    [Serializable]
    sealed class PlayerSnapshot
    {
        public Vector3 position, velocity;
        public Vector2 moveInput;
        public Vector2 moveActionValue;
        public bool moveActionEnabled, jumpActionEnabled, attackActionEnabled;
        public string moveActionPhase;
        public float hp, maxHp, facing;
        public bool grounded, dead;
        public string state, weapon, ladder;
    }

    [Serializable]
    sealed class CameraSnapshot
    {
        public Vector3 position, euler;
        public float orthographicSize, aspect;
    }

    [Serializable]
    sealed class EnemySnapshot
    {
        public string name, dataId, type;
        public Vector3 position;
        public float hp, maxHp;
        public bool dead;
    }

    [Serializable]
    sealed class GateSnapshot
    {
        public string name, state;
        public Vector3 position;
        public bool waiting;
        public int maxSpawnCount;
    }

    [Serializable]
    sealed class ButtonSnapshot
    {
        public string name, hierarchy, label;
        public bool interactable;
        public float x, y, width, height;
    }

    [Serializable]
    sealed class HistoryEvent
    {
        public string utc, type, detail;
        public double editorSeconds;
        public float gameSeconds;
        public int frame;
        public Command command;
    }

    static Stage01VerticalQA()
    {
        EditorApplication.playModeStateChanged += PlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload += ReleaseAndDetach;
        if (SessionState.GetBool(EnabledKey, false) && EditorApplication.isPlaying)
            EditorApplication.delayCall += Attach;
    }

    /// <summary>Called with -executeMethod; opens the real title flow and waits for input.</summary>
    [MenuItem("OZ/Level Design/Start Manual Vertical Play QA")]
    public static void Start()
    {
        if (!EditorApplication.isPlaying)
        {
            // Do not discard a designer's unsaved scene when used from the menu.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }
        string requested = Environment.GetEnvironmentVariable("OZ_STAGE_CONTROL_DIR");
        controlDirectory = Path.GetFullPath(string.IsNullOrWhiteSpace(requested)
            ? Path.Combine(Path.GetTempPath(), "OZStage01VerticalQA") : requested);
        string assets = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if ((controlDirectory + Path.DirectorySeparatorChar).StartsWith(assets, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("QA commands and captures must be outside Assets.");
        Directory.CreateDirectory(controlDirectory);
        SessionState.SetString(DirectoryKey, controlDirectory);
        SessionState.SetBool(EnabledKey, true);
        if (EditorApplication.isPlaying) Attach();
        else EditorApplication.EnterPlaymode();
    }

    /// <summary>Accepts the same JSON as command.json. Can be called by an Editor console bridge.</summary>
    public static void SubmitJson(string json)
    {
        if (!attached || !EditorApplication.isPlaying)
            throw new InvalidOperationException("Start Manual Vertical Play QA first.");
        Command command = JsonUtility.FromJson<Command>(json);
        Validate(command);
        EnqueueCommand(command);
    }

    /// <summary>Queues an entire sequence atomically after validating every ordinary input command.</summary>
    public static void SubmitBatchJson(string json)
    {
        if (!attached || !EditorApplication.isPlaying)
            throw new InvalidOperationException("Start Manual Vertical Play QA first.");
        EnqueueBatch(JsonUtility.FromJson<CommandBatch>(json));
    }

    [MenuItem("OZ/Level Design/Stop Manual Vertical Play QA")]
    public static void Stop()
    {
        Record("stopped", "Manual bridge disabled; play mode remains under editor control.");
        SessionState.SetBool(EnabledKey, false);
        ReleaseAndDetach();
    }

    static void PlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(EnabledKey, false)) Attach();
        else if (change == PlayModeStateChange.ExitingPlayMode)
        {
            Record("play-mode-exit", "");
            ReleaseAndDetach();
            SessionState.SetBool(EnabledKey, false);
        }
    }

    static void Attach()
    {
        if (attached || !EditorApplication.isPlaying) return;
        controlDirectory = SessionState.GetString(DirectoryKey,
            Path.Combine(Path.GetTempPath(), "OZStage01VerticalQA"));
        Directory.CreateDirectory(controlDirectory);
        attached = true;
        // Some batch-editor hosts expose no native input devices. Standard test
        // devices still traverse the game's normal bindings and action callbacks.
        if (Keyboard.current == null) addedKeyboard = InputSystem.AddDevice<Keyboard>("OZ_StageQA_Keyboard");
        if (Mouse.current == null) addedMouse = InputSystem.AddDevice<Mouse>("OZ_StageQA_Mouse");
        InputSystem.onBeforeUpdate += InjectBeforePlayerUpdate;
        EditorApplication.update += Tick;
        Application.logMessageReceived += RecordError;
        FocusGameView();
        Record("ready", "Real title UI; no gameplay shortcut invoked.");
        WriteSnapshot();
        Debug.Log("[Stage01VerticalQA] Ready: " + controlDirectory);
    }

    static void FocusGameView()
    {
        Type gameView = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        if (gameView != null) EditorWindow.GetWindow(gameView).Focus();
    }

    static void Validate(Command command)
    {
        if (command == null || string.IsNullOrWhiteSpace(command.id))
            throw new ArgumentException("Command requires a unique string id.");
        if (float.IsNaN(command.seconds) || float.IsInfinity(command.seconds) || command.seconds < 0 || command.seconds > 30)
            throw new ArgumentException("seconds must be between 0 and 30.");
        foreach (string key in command.keys ?? Array.Empty<string>()) ParseKey(key);
        if (command.mouse != null && command.mouse.Length != 0 &&
            (command.mouse.Length != 2 || command.mouse.Any(v => float.IsNaN(v) || float.IsInfinity(v) || v < 0 || v > 1)))
            throw new ArgumentException("mouse must be [x,y] normalized 0..1, top-left origin.");
        if (!string.IsNullOrEmpty(command.capture) &&
            (command.capture.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || command.capture.Contains("..")))
            throw new ArgumentException("capture must be a simple filename label.");
    }

    static Key ParseKey(string key)
    {
        string normalized = (key ?? "").Replace(" ", "").ToLowerInvariant();
        switch (normalized)
        {
            case "shift": normalized = "leftshift"; break;
            case "ctrl": normalized = "leftctrl"; break;
            case "alt": normalized = "leftalt"; break;
            case "esc": normalized = "escape"; break;
            case "return": normalized = "enter"; break;
        }
        if (normalized.Length == 1 && char.IsDigit(normalized[0])) normalized = "digit" + normalized;
        if (!Enum.TryParse(normalized, true, out Key result) || result == Key.None || !Enum.IsDefined(typeof(Key), result))
            throw new ArgumentException("Unsupported key: " + key);
        return result;
    }

    static void Tick()
    {
        if (!attached || !EditorApplication.isPlaying) return;
        try
        {
            ReadCommandFile();
            ReadBatchFile();
            if (active != null && EditorApplication.timeSinceStartup >= deadline && injectedFrames > 0)
            {
                // Release through the next real input update, just like physical key-up.
                releasePending = true;
                Command finished = active;
                heldKeys = Array.Empty<Key>();
                active = null;
                lastCompletedId = finished.id;
                Record("completed", "device-up pending next player input update", finished);
                if (!string.IsNullOrWhiteSpace(finished.capture)) Capture(finished.capture);
            }
            if (active == null && Commands.Count > 0)
            {
                active = Commands.Dequeue();
                if (active.endPlay)
                {
                    lastCompletedId = active.id;
                    Record("completed", "Editor play-session cleanup requested.", active);
                    WriteSnapshot();
                    SessionState.SetBool(EnabledKey, false);
                    ReleaseAndDetach();
                    EditorApplication.ExitPlaymode();
                    return;
                }
                heldKeys = (active.keys ?? Array.Empty<string>()).Select(ParseKey).ToArray();
                if (active.mouse != null && active.mouse.Length == 2) pointer = new Vector2(active.mouse[0], active.mouse[1]);
                deadline = EditorApplication.timeSinceStartup + Mathf.Max(.05f, active.seconds);
                injectedFrames = 0;
                FocusGameView();
                Record("started", "", active);
            }
            if (EditorApplication.timeSinceStartup - lastTelemetry >= .1)
            {
                WriteSnapshot();
                lastTelemetry = EditorApplication.timeSinceStartup;
            }
        }
        catch (Exception exception)
        {
            Errors.Add(exception.Message);
            Record("bridge-error", exception.ToString());
            Debug.LogException(exception);
        }
    }

    static void ReadCommandFile()
    {
        string path = Path.Combine(controlDirectory, "command.json");
        if (!File.Exists(path)) return;
        string json;
        try { json = File.ReadAllText(path); }
        catch (IOException) { return; } // Atomic replacement can briefly be sharing-locked.
        catch (UnauthorizedAccessException) { return; }
        Command command;
        try { command = JsonUtility.FromJson<Command>(json); }
        catch (ArgumentException) { return; } // A non-atomic writer may still be writing.
        if (command == null || command.id == lastFileId) return;
        lastFileId = command.id;
        Validate(command);
        EnqueueCommand(command);
    }

    static void ReadBatchFile()
    {
        string path = Path.Combine(controlDirectory, "batch.json");
        if (!File.Exists(path)) return;
        string json;
        try { json = File.ReadAllText(path); }
        catch (IOException) { return; }
        catch (UnauthorizedAccessException) { return; }
        CommandBatch batch;
        try { batch = JsonUtility.FromJson<CommandBatch>(json); }
        catch (ArgumentException) { return; }
        if (batch == null || batch.id == lastBatchFileId) return;
        lastBatchFileId = batch.id;
        EnqueueBatch(batch);
    }

    static void EnqueueBatch(CommandBatch batch)
    {
        if (batch == null || string.IsNullOrWhiteSpace(batch.id))
            throw new ArgumentException("Batch requires a unique string id.");
        if (batch.commands == null || batch.commands.Length == 0 || batch.commands.Length > 512)
            throw new ArgumentException("Batch must contain between 1 and 512 commands.");
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (Command command in batch.commands)
        {
            Validate(command);
            if (!identifiers.Add(command.id)) throw new ArgumentException("Duplicate command id in batch: " + command.id);
        }
        // All validation occurs before touching the queue. A queued endPlay is
        // honored in sequence; the standalone endPlay command still preempts.
        foreach (Command command in batch.commands) Commands.Enqueue(command);
        Record("batch-enqueued", batch.id + ": " + batch.commands.Length + " normal device commands.");
    }

    static void EnqueueCommand(Command command)
    {
        if (command.endPlay)
        {
            // Cleanup must remain possible if the editor is paused while an
            // ordinary input command is waiting for its first player frame.
            if (active != null) Record("interrupted", "Editor play-session cleanup requested.", active);
            active = null;
            heldKeys = Array.Empty<Key>();
            releasePending = true;
            Commands.Clear();
        }
        Commands.Enqueue(command);
    }

    static void InjectBeforePlayerUpdate()
    {
        if (!attached || !EditorApplication.isPlaying) return;
        InputUpdateType update = InputState.currentUpdateType;
        if (update != InputUpdateType.Dynamic && update != InputUpdateType.Fixed) return;
        if (releasePending)
        {
            QueueState(Array.Empty<Key>(), false);
            releasePending = false;
            Record("device-up", "Released in real player input update.");
        }
        if (active == null) return;
        QueueState(heldKeys, active.click);
        injectedFrames++;
    }

    static void QueueState(Key[] keys, bool click)
    {
        // These are device events, not calls to any gameplay or UI action handler.
        if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));
        if (Mouse.current != null)
        {
            var mouse = new MouseState
            {
                position = new Vector2(pointer.x * Screen.width, (1f - pointer.y) * Screen.height)
            }.WithButton(MouseButton.Left, click);
            InputSystem.QueueStateEvent(Mouse.current, mouse);
        }
    }

    static void Capture(string label)
    {
        string path = Path.Combine(controlDirectory, label + ".png");
        ScreenCapture.CaptureScreenshot(path);
        Record("capture-requested", path);
    }

    static void WriteSnapshot()
    {
        var manager = GameManager.Instance;
        var stageObject = manager != null && manager.StageManager != null ? manager.StageManager.CurrentStage : null;
        var stage = stageObject != null ? stageObject.GetComponent<Stage>() : null;
        var player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        var health = player != null ? player.GetComponent<PlayerHealth>() : null;
        var weapon = player != null ? player.GetComponent<PlayerWeapon>() : null;
        var body = player != null ? player.GetComponent<Rigidbody>() : null;
        InputAction moveAction = ReadAction(player != null ? player.Input : null, "_moveAction");
        InputAction jumpAction = ReadAction(player != null ? player.Input : null, "_jumpAction");
        InputAction attackAction = ReadAction(player != null ? player.Input : null, "_attackAction");
        Camera camera = Camera.main;
        var snapshot = new Snapshot
        {
            utc = DateTime.UtcNow.ToString("O"), project = Application.dataPath,
            scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
            directory = controlDirectory, playing = EditorApplication.isPlaying,
            editorPaused = EditorApplication.isPaused,
            gameTime = Time.time, frame = Time.frameCount, timeScale = Time.timeScale,
            inputBlocked = UIState.IsGameplayInputBlocked, pausedByUI = UIState.IsPausedByUI,
            keyboardPresent = Keyboard.current != null, mousePresent = Mouse.current != null,
            keyboardEnabled = Keyboard.current != null && Keyboard.current.enabled,
            mouseEnabled = Mouse.current != null && Mouse.current.enabled,
            keyboardDevice = Keyboard.current != null ? Keyboard.current.name : "none",
            mouseDevice = Mouse.current != null ? Mouse.current.name : "none",
            focusedEditorWindow = EditorWindow.focusedWindow != null ? EditorWindow.focusedWindow.GetType().FullName : "none",
            observedKeys = Keyboard.current == null ? Array.Empty<string>() : Keyboard.current.allKeys
                .Where(k => k.isPressed).Select(k => k.keyCode.ToString()).ToArray(),
            observedMouseLeft = Mouse.current != null && Mouse.current.leftButton.isPressed,
            screenWidth = Screen.width, screenHeight = Screen.height,
            commandId = active?.id, commandActive = active != null, lastCompletedId = lastCompletedId,
            commandSecondsRemaining = active == null ? 0 : (float)Math.Max(0, deadline - EditorApplication.timeSinceStartup),
            injectedFrames = injectedFrames, stageNumber = manager != null ? manager.StageNumber : 0,
            stageState = stage != null ? stage.State.ToString() : "none",
            player = player == null ? null : new PlayerSnapshot
            {
                position = player.transform.position, velocity = body != null ? body.linearVelocity : Vector3.zero,
                moveInput = player.Input != null ? player.Input.MoveInput : Vector2.zero,
                moveActionValue = moveAction != null && moveAction.enabled ? moveAction.ReadValue<Vector2>() : Vector2.zero,
                moveActionEnabled = moveAction != null && moveAction.enabled,
                jumpActionEnabled = jumpAction != null && jumpAction.enabled,
                attackActionEnabled = attackAction != null && attackAction.enabled,
                moveActionPhase = moveAction != null ? moveAction.phase.ToString() : "none",
                hp = health != null ? health.HP : 0, maxHp = health != null ? health.MaxHP : 0,
                facing = player.FacingX, grounded = player.IsGrounded, dead = player.IsDead,
                state = player.CurrentStateType.ToString(), weapon = weapon != null ? weapon.Current.ToString() : "none",
                ladder = player.CurrentLadder != null ? player.CurrentLadder.name : "none"
            },
            camera = camera == null ? null : new CameraSnapshot
            {
                position = camera.transform.position, euler = camera.transform.eulerAngles,
                orthographicSize = camera.orthographicSize, aspect = camera.aspect
            },
            enemies = UnityEngine.Object.FindObjectsByType<Monster>(FindObjectsSortMode.None)
                .Where(m => m.gameObject.activeInHierarchy).Select(m => new EnemySnapshot
                {
                    name = m.name, dataId = m.DataId, type = m.MonsterType.ToString(),
                    position = m.transform.position, hp = m.CurrentHP, maxHp = m.MaxHP, dead = m.IsDead
                }).ToArray(),
            gates = stage == null ? Array.Empty<GateSnapshot>() : stage.GetComponentsInChildren<StageGate>(true)
                .Select(g => new GateSnapshot
                {
                    name = g.name, state = g.State.ToString(), position = g.transform.position,
                    waiting = g is Gate normal && normal.IsWaitingForPlayer,
                    maxSpawnCount = g is Gate encounter ? encounter.MaxSpawnCount : 0
                }).ToArray(),
            uiStack = UIManager.Instance == null ? Array.Empty<string>() : UIManager.Instance.OpenStack
                .Where(w => w != null).Select(w => w.Id + ":" + w.name).ToArray(),
            buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Where(b => b.isActiveAndEnabled && b.gameObject.activeInHierarchy).Select(DescribeButton).ToArray(),
            errors = Errors.Skip(Math.Max(0, Errors.Count - 30)).ToArray()
        };
        WriteAtomic(Path.Combine(controlDirectory, "state.json"), JsonUtility.ToJson(snapshot, true));
    }

    static InputAction ReadAction(InputManager input, string field)
    {
        if (input == null) return null;
        var reference = typeof(InputManager).GetField(field,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(input) as InputActionReference;
        return reference != null ? reference.action : null;
    }

    static ButtonSnapshot DescribeButton(Button button)
    {
        var rect = button.transform as RectTransform;
        var canvas = button.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 center = rect == null ? Vector2.zero : RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
        var corners = new Vector3[4];
        if (rect != null) rect.GetWorldCorners(corners);
        Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
        Vector2 topRight = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
        // TMP and legacy Text are read without depending on a particular text package.
        string label = string.Join(" ", button.GetComponentsInChildren<Component>(true)
            .Where(c => c != null && (c.GetType().Name == "Text" || c.GetType().Name == "TextMeshProUGUI"))
            .Select(c => c.GetType().GetProperty("text")?.GetValue(c) as string).Where(s => !string.IsNullOrWhiteSpace(s)));
        return new ButtonSnapshot
        {
            name = button.name, hierarchy = AnimationUtility.CalculateTransformPath(button.transform, null), label = label,
            interactable = button.IsInteractable(), x = center.x / Screen.width, y = 1f - center.y / Screen.height,
            width = Mathf.Abs(topRight.x - bottomLeft.x) / Screen.width, height = Mathf.Abs(topRight.y - bottomLeft.y) / Screen.height
        };
    }

    static void WriteAtomic(string path, string content)
    {
        string temporary = path + ".tmp";
        try
        {
            File.WriteAllText(temporary, content);
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
        catch (IOException)
        {
            // A telemetry reader can hold the old state without FileShare.Delete.
            // Preserve the previous valid JSON and retry with a fresh snapshot on
            // the next tick; this is not a game error and must not trigger pause.
        }
    }

    static void Record(string type, string detail, Command command = null)
    {
        if (string.IsNullOrEmpty(controlDirectory)) return;
        var entry = new HistoryEvent
        {
            utc = DateTime.UtcNow.ToString("O"), editorSeconds = EditorApplication.timeSinceStartup,
            gameSeconds = Time.time, frame = Time.frameCount, type = type, detail = detail, command = command
        };
        File.AppendAllText(Path.Combine(controlDirectory, "history.jsonl"), JsonUtility.ToJson(entry) + Environment.NewLine);
    }

    static void RecordError(string message, string stackTrace, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        if (Errors.Count > 200) Errors.RemoveAt(0);
        Errors.Add(message);
        Record("game-error", message + "\n" + stackTrace);
    }

    static void ReleaseAndDetach()
    {
        if (!attached) return;
        QueueState(Array.Empty<Key>(), false);
        active = null;
        heldKeys = Array.Empty<Key>();
        Commands.Clear();
        releasePending = false;
        InputSystem.onBeforeUpdate -= InjectBeforePlayerUpdate;
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= RecordError;
        if (addedKeyboard != null && addedKeyboard.added) InputSystem.RemoveDevice(addedKeyboard);
        if (addedMouse != null && addedMouse.added) InputSystem.RemoveDevice(addedMouse);
        addedKeyboard = null;
        addedMouse = null;
        attached = false;
    }
}
#endif
