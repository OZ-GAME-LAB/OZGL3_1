using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>Runs real input, physics and animation integration in the isolated playground.</summary>
[InitializeOnLoad]
public static class ProtagonistPlayValidation
{
    private const string Prefix = "Protagonist.PlayValidation.";
    private const string OutputPath = "ArtSources/Characters/Protagonist01/v002-rigged/play-validation.json";

    [Serializable]
    private sealed class Report
    {
        public bool success;
        public string unity;
        public string scene;
        public string prefab;
        public string failure;
        public string phase;
        public float elapsedSeconds;
        public float moveDistance;
        public float slowWalkSpeed;
        public float peakHorizontalSpeed;
        public float maximumJumpHeight;
        public float expectedAttackSpeed;
        public float actualAttackSpeed;
        public List<string> checks = new List<string>();
        public List<string> damageMessages = new List<string>();
        public List<string> errors = new List<string>();
    }

    private enum Phase { WaitingForIdle, SlowWalking, StopSlowWalking, Repositioning, Moving, Stopping, Attacking, Agility, Jumping }
    private static Report _report;
    private static Phase _phase;
    private static PlayerController _player;
    private static Animator _animator;
    private static Rigidbody _body;
    private static PunchAttack _punch;
    private static TestDummy _dummy;
    private static Keyboard _keyboard;
    private static Mouse _mouse;
    private static InputActionAsset _inputAsset;
    private static InputDevice[] _previousDevices;
    private static InputSettings _previousSettings;
    private static InputSettings _validationSettings;
    private static float _phaseStart;
    private static float _initialX;
    private static Vector3 _initialPosition;
    private static float _jumpBase;
    private static bool _sawLocomotion;
    private static bool _sawSlowWalk;
    private static bool _sawAttack;
    private static bool _sawJump;
    private static bool _sawAir;
    private static bool _sawLand;
    private static bool _released;
    private static bool _logging;
    private static bool _finishing;

    static ProtagonistPlayValidation()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        if (SessionState.GetBool(Prefix + "Active", false))
        {
            RestoreReport();
            Hook();
            EditorApplication.delayCall += Resume;
        }
    }

    // Invoke with -batchmode -executeMethod ProtagonistPlayValidation.Run, WITHOUT -quit.
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("Play validation must start in Edit Mode.");
        }
        _report = new Report
        {
            unity = Application.unityVersion,
            scene = ProtagonistUnitySetup.ScenePath,
            prefab = ProtagonistUnitySetup.PlayerPath
        };
        SessionState.SetBool(Prefix + "Active", true);
        SessionState.SetString(Prefix + "State", "Entering");
        SessionState.SetFloat(Prefix + "Started", (float)EditorApplication.timeSinceStartup);
        PersistReport();
        Hook();
        try
        {
            EditorSceneManager.OpenScene(ProtagonistUnitySetup.ScenePath, OpenSceneMode.Single);
            PlayerController scenePlayer = UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None)
                .SingleOrDefault(player => player.name == "Protagonist01_Player");
            Require(scenePlayer != null, "Playground contains the named generated player instance");
            string sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(scenePlayer.gameObject);
            if (string.IsNullOrEmpty(sourcePath))
            {
                sourcePath = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(scenePlayer.gameObject));
            }
            Require(sourcePath == ProtagonistUnitySetup.PlayerPath,
                "Playground prefab source is verified before entering Play Mode");
            PersistReport();
            EditorApplication.isPlaying = true;
        }
        catch (Exception exception)
        {
            Finish(exception.ToString());
        }
    }

    private static void Hook()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        if (!_logging)
        {
            Application.logMessageReceived += CaptureLog;
            _logging = true;
        }
    }

    private static void Resume()
    {
        string state = SessionState.GetString(Prefix + "State", "");
        if (state == "Stopping" && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Exit();
        }
        else if (state == "Running" && EditorApplication.isPlaying && _player == null)
        {
            InitializePlay();
        }
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (!SessionState.GetBool(Prefix + "Active", false)) return;
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            InitializePlay();
        }
        else if (change == PlayModeStateChange.EnteredEditMode)
        {
            if (SessionState.GetString(Prefix + "State", "") != "Stopping")
            {
                Finish("Play Mode ended before the integration checks completed.");
            }
            else Exit();
        }
    }

    private static void InitializePlay()
    {
        try
        {
            RestoreReport();
            _player = UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None)
                .SingleOrDefault(player => player.name == "Protagonist01_Player");
            _dummy = UnityEngine.Object.FindFirstObjectByType<TestDummy>();
            Require(_player != null && _dummy != null, "Playground player and damage target exist");
            _animator = _player.GetComponent<Animator>();
            _body = _player.GetComponent<Rigidbody>();
            _punch = _player.GetComponent<PunchAttack>();
            Require(_animator != null && _body != null && _punch != null, "Gameplay components remain on player root");
            GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProtagonistUnitySetup.PlayerPath);
            Animator sourceAnimator = sourcePrefab != null ? sourcePrefab.GetComponent<Animator>() : null;
            Require(sourceAnimator != null && _animator.runtimeAnimatorController == sourceAnimator.runtimeAnimatorController &&
                _animator.avatar == sourceAnimator.avatar,
                "Named runtime player retains the generated prefab controller and Avatar");
            Require(_animator.avatar != null && _animator.avatar.isValid && _animator.avatar.isHuman &&
                _animator.runtimeAnimatorController != null && !_animator.applyRootMotion,
                "Valid Humanoid controller with root motion disabled");

            InputManager input = _player.GetComponent<InputManager>();
            var playerProperties = new SerializedObject(_player);
            Require(playerProperties.FindProperty("_inputManager").objectReferenceValue == input &&
                playerProperties.FindProperty("_groundLayer").intValue == 1 << 6,
                "Root input reference and Ground layer are bound");
            var inputProperties = new SerializedObject(input);
            var reference = inputProperties.FindProperty("_moveAction").objectReferenceValue as InputActionReference;
            Require(reference != null && reference.action != null, "Configured input actions are available");
            _inputAsset = reference.action.actionMap.asset;
            _previousDevices = _inputAsset.devices.HasValue ? _inputAsset.devices.Value.ToArray() : null;
            _previousSettings = InputSystem.settings;
            _validationSettings = UnityEngine.Object.Instantiate(_previousSettings);
            _validationSettings.hideFlags = HideFlags.HideAndDontSave;
            _validationSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            _validationSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = _validationSettings;
            _keyboard = InputSystem.AddDevice<Keyboard>("ProtagonistValidationKeyboard");
            _mouse = InputSystem.AddDevice<Mouse>("ProtagonistValidationMouse");
            _inputAsset.devices = new InputDevice[] { _keyboard, _mouse };
            QueueKeyboard();
            QueueMouse(false);
            _initialX = _player.transform.position.x;
            SessionState.SetString(Prefix + "State", "Running");
            Begin(Phase.WaitingForIdle);
        }
        catch (Exception exception)
        {
            Finish(exception.ToString());
        }
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Prefix + "Active", false) || _finishing) return;
        try
        {
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Prefix + "Started", 0f) > 20f)
            {
                Finish("Integration validation exceeded its 20-second bound.");
                return;
            }
            if (!EditorApplication.isPlaying || _player == null ||
                SessionState.GetString(Prefix + "State", "") != "Running") return;
            if (_report.errors.Count > 0)
            {
                Finish("Unity emitted an Error, Assert or Exception during Play Mode.");
                return;
            }

            float elapsed = Time.realtimeSinceStartup - _phaseStart;
            float speed = Mathf.Abs(_body.linearVelocity.x);
            _report.peakHorizontalSpeed = Mathf.Max(_report.peakHorizontalSpeed, speed);
            switch (_phase)
            {
                case Phase.WaitingForIdle:
                    if (elapsed > 0.15f && _player.IsGrounded && IsAnimation("Idle") &&
                        _player.CurrentStateType == PlayerState.Idle)
                    {
                        Require(true, "Idle is grounded in the actual scene");
                        _initialPosition = _player.transform.position;
                        _initialX = _initialPosition.x;
                        QueueKeyboard(Key.LeftCtrl, Key.D);
                        Begin(Phase.SlowWalking);
                    }
                    else WaitWithin(elapsed, 2.5f, "Player did not settle to grounded Idle.");
                    break;
                case Phase.SlowWalking:
                    if (IsAnimation("Locomotion") && speed > 1.9f && speed < 2.5f &&
                        Mathf.Abs(_animator.GetFloat("Speed") - 2.2f) < 0.3f)
                    {
                        _sawSlowWalk = true;
                        _report.slowWalkSpeed = speed;
                    }
                    if (elapsed > 0.25f && _player.transform.position.x - _initialX > 0.35f)
                    {
                        Require(_sawSlowWalk, "Ctrl+D drives slow-walk Locomotion at approximately 2.2 units per second");
                        QueueKeyboard();
                        Begin(Phase.StopSlowWalking);
                    }
                    else WaitWithin(elapsed, 1.5f, "Ctrl+D did not produce slow walking.");
                    break;
                case Phase.StopSlowWalking:
                    if (elapsed > 0.12f && _player.IsGrounded && IsAnimation("Idle") && speed < 0.1f)
                    {
                        _body.linearVelocity = Vector3.zero;
                        _body.position = _initialPosition;
                        _player.transform.position = _initialPosition;
                        Physics.SyncTransforms();
                        Begin(Phase.Repositioning);
                    }
                    else WaitWithin(elapsed, 1.5f, "Releasing Ctrl+D did not return to Idle.");
                    break;
                case Phase.Repositioning:
                    if (elapsed > 0.12f && _player.IsGrounded && speed < 0.1f)
                    {
                        Require(Mathf.Abs(_player.transform.position.x - _initialX) < 0.05f,
                            "Slow-walk probe resets before the normal movement and target-hit checks");
                        QueueKeyboard(Key.D);
                        Begin(Phase.Moving);
                    }
                    else WaitWithin(elapsed, 1f, "Player did not settle after resetting the slow-walk probe.");
                    break;
                case Phase.Moving:
                    _sawLocomotion |= IsAnimation("Locomotion") && speed > 4.4f && speed < 5.6f &&
                        Mathf.Abs(_animator.GetFloat("Speed") - speed) < 0.6f;
                    if (_player.transform.position.x - _initialX >= 1.9f)
                    {
                        _report.moveDistance = _player.transform.position.x - _initialX;
                        Require(_sawLocomotion, "D input drives Locomotion at approximately 5 units per second");
                        Require(_report.moveDistance < 2.4f, "D input moves the player positively without passing the target");
                        QueueKeyboard();
                        Begin(Phase.Stopping);
                    }
                    else WaitWithin(elapsed, 1.5f, "D input did not produce forward movement.");
                    break;
                case Phase.Stopping:
                    if (elapsed > 0.12f && _player.IsGrounded && IsAnimation("Idle") && speed < 0.1f)
                    {
                        Require(true, "Releasing D returns to stationary Idle");
                        QueueMouse(true);
                        Begin(Phase.Attacking);
                    }
                    else WaitWithin(elapsed, 1.5f, "Movement release did not return to Idle.");
                    break;
                case Phase.Attacking:
                    _sawAttack |= _player.CurrentStateType == PlayerState.Attack && IsAnimation("Attack");
                    if (!_released && elapsed > 0.05f)
                    {
                        QueueMouse(false);
                        _released = true;
                    }
                    if (elapsed > 0.4f && _player.CurrentStateType == PlayerState.Idle && IsAnimation("Idle"))
                    {
                        Require(_sawAttack, "Left mouse input enters the actual Attack state and animation");
                        Require(_report.damageMessages.Count == 1 && _report.damageMessages[0] == "[TestDummy] 20 피해",
                            "One punch produces exactly one 20-damage target hit");
                        Require(true, "Punch recovery returns to Idle");
                        _punch.SetSpeedBonus(0.09f);
                        Begin(Phase.Agility);
                    }
                    else WaitWithin(elapsed, 1.5f, "Punch did not complete and return to Idle.");
                    break;
                case Phase.Agility:
                    if (elapsed > 0.08f)
                    {
                        var actions = AssetDatabase.LoadAllAssetsAtPath(ProtagonistUnitySetup.Folder + "/Protagonist01_Actions.fbx");
                        AnimationClip punchClip = actions.OfType<AnimationClip>().First(clip => clip.name == "Punch");
                        _report.expectedAttackSpeed = punchClip.length / _punch.Duration;
                        _report.actualAttackSpeed = _animator.GetFloat("AttackSpeed");
                        Require(Mathf.Abs(_report.expectedAttackSpeed - _report.actualAttackSpeed) < 0.02f,
                            "AttackSpeed follows the 9-percent agility-adjusted gameplay duration");
                        _punch.SetSpeedBonus(0f);
                        _jumpBase = _player.transform.position.y;
                        QueueKeyboard(Key.Space);
                        Begin(Phase.Jumping);
                    }
                    break;
                case Phase.Jumping:
                    if (!_released && elapsed > 0.05f)
                    {
                        QueueKeyboard();
                        _released = true;
                    }
                    _sawJump |= _player.CurrentStateType == PlayerState.Jump || IsAnimation("Jump");
                    _sawAir |= _player.CurrentStateType == PlayerState.Air && IsAnimation("Air") && !_player.IsGrounded;
                    _sawLand |= _player.CurrentStateType == PlayerState.Land || IsAnimation("Land");
                    _report.maximumJumpHeight = Mathf.Max(_report.maximumJumpHeight, _player.transform.position.y - _jumpBase);
                    if (elapsed > 0.5f && _sawAir && _player.IsGrounded &&
                        _player.CurrentStateType == PlayerState.Idle && IsAnimation("Idle"))
                    {
                        Require(_sawJump && _sawAir && _report.maximumJumpHeight > 0.25f,
                            "Space input plays Jump and Air while physics raises the player");
                        Require(_sawLand && Mathf.Abs(_player.transform.position.y - _jumpBase) < 0.12f,
                            "Jump lands at ground height and returns through Land to Idle");
                        Require(_report.damageMessages.Count == 1, "No additional damage is emitted after the punch");
                        Finish(null);
                    }
                    else WaitWithin(elapsed, 6f, "Jump did not return to grounded Idle within its time bound.");
                    break;
            }
        }
        catch (Exception exception)
        {
            Finish(exception.ToString());
        }
    }

    private static void Begin(Phase phase)
    {
        _phase = phase;
        _phaseStart = Time.realtimeSinceStartup;
        _released = false;
        _report.phase = phase.ToString();
        PersistReport();
    }

    private static bool IsAnimation(string name)
    {
        return _animator.GetCurrentAnimatorStateInfo(0).IsName(name) ||
            (_animator.IsInTransition(0) && _animator.GetNextAnimatorStateInfo(0).IsName(name));
    }

    private static void QueueKeyboard(params Key[] keys)
    {
        InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));
    }

    private static void QueueMouse(bool pressed)
    {
        Camera camera = Camera.main;
        if (camera == null) throw new InvalidOperationException("No MainCamera for cursor aiming.");
        Vector3 screen = camera.WorldToScreenPoint(_dummy.transform.position);
        MouseState state = new MouseState { position = new Vector2(screen.x, screen.y) };
        InputSystem.QueueStateEvent(_mouse, state.WithButton(MouseButton.Left, pressed));
    }

    private static void Require(bool condition, string check)
    {
        if (!condition) throw new InvalidOperationException(check);
        if (!_report.checks.Contains(check)) _report.checks.Add(check);
    }

    private static void WaitWithin(float elapsed, float limit, string failure)
    {
        if (elapsed >= limit) throw new InvalidOperationException(failure);
    }

    private static void CaptureLog(string condition, string stackTrace, LogType type)
    {
        if (_report == null) RestoreReport();
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            _report.errors.Add(condition + "\n" + stackTrace);
            PersistReport();
        }
        else if (condition.StartsWith("[TestDummy]", StringComparison.Ordinal))
        {
            _report.damageMessages.Add(condition);
            PersistReport();
        }
    }

    private static void Finish(string failure)
    {
        if (_finishing) return;
        _finishing = true;
        if (_report == null) RestoreReport();
        _report.failure = failure;
        _report.success = failure == null && _report.errors.Count == 0;
        _report.elapsedSeconds = (float)EditorApplication.timeSinceStartup - SessionState.GetFloat(Prefix + "Started", 0f);
        SessionState.SetString(Prefix + "State", "Stopping");
        PersistReport();
        WriteReport();
        CleanupInputs();
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        else if (!EditorApplication.isPlayingOrWillChangePlaymode) Exit();
    }

    private static void CleanupInputs()
    {
        if (_punch != null) _punch.SetSpeedBonus(0f);
        if (_inputAsset != null) _inputAsset.devices = _previousDevices;
        if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
        if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
        if (_previousSettings != null) InputSystem.settings = _previousSettings;
        if (_validationSettings != null) UnityEngine.Object.DestroyImmediate(_validationSettings);
        _keyboard = null;
        _mouse = null;
    }

    private static void PersistReport()
    {
        SessionState.SetString(Prefix + "Report", JsonUtility.ToJson(_report));
    }

    private static void RestoreReport()
    {
        _report = JsonUtility.FromJson<Report>(SessionState.GetString(Prefix + "Report", "{}")) ?? new Report();
    }

    private static void WriteReport()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
        File.WriteAllText(OutputPath, JsonUtility.ToJson(_report, true));
    }

    private static void Exit()
    {
        RestoreReport();
        _report.success &= _report.errors.Count == 0;
        WriteReport();
        SessionState.SetBool(Prefix + "Active", false);
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= CaptureLog;
        _logging = false;
        Debug.Log(_report.success ? "PROTAGONIST_PLAY_VALIDATION_SUCCESS" : "PROTAGONIST_PLAY_VALIDATION_FAILED " + _report.failure);
        EditorApplication.Exit(_report.success ? 0 : 1);
    }
}
