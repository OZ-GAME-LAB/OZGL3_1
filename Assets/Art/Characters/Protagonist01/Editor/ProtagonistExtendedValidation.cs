using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>Independent asset checks and optional real Play Mode callback/cancellation checks.</summary>
[InitializeOnLoad]
public static class ProtagonistExtendedValidation
{
    private const string Prefix = "Protagonist.ExtendedValidation.";
    private const string Path = "ArtSources/Characters/Protagonist01/v003-extended/validation.json";

    [Serializable] private sealed class Report
    {
        public bool success;
        public bool editorPassed;
        public bool playModeExecuted;
        public bool playPassed;
        public string unity;
        public string failure;
        public string phase;
        public int drinkCommits;
        public int drinkCompletions;
        public int throwCommits;
        public int throwCompletions;
        public int cancellationCommits;
        public int cancellationCompletions;
        public int cancellations;
        public int unexpectedDamageMessages;
        public int attackProbeExecutions;
        public int cancelledAttackProbeExecutions;
        public float expectedAttackContactSeconds;
        public float actualAttackContactSeconds;
        public List<string> editorChecks = new List<string>();
        public List<string> playChecks = new List<string>();
        public List<string> errors = new List<string>();
    }

    private enum Phase
    {
        Idle, SwordEquip, SwordFaceLeft, SwordFaceRight, StaffEquip, ThrowHide,
        SwapHiddenWeapon, RightDrinkStart, RightDrink, LeftDrinkStart, LeftDrink,
        UnarmedRestore, Drink, Throw, StartCancel, CancelMove, Settle,
        AttackBeforeContact, AttackAfterContact, AttackSettle, CancelAttackBeforeContact, CancelAttackSettle
    }

    private sealed class AttackProbe : IPlayerAttack
    {
        public float Duration => 1f;
        public int executions;
        public float firstExecutedAt;
        public void Execute(PlayerController player, Vector3 aimPoint)
        {
            executions++;
            if (executions == 1) firstExecutedAt = Time.time;
        }
    }
    private static Report _report;
    private static PlayerController _player;
    private static ProtagonistActionAnimator _actions;
    private static Animator _animator;
    private static HandSocketEquipment _equipment;
    private static ProtagonistAnimationDriver _driver;
    private static SocketWeapon _sword;
    private static SocketWeapon _staff;
    private static RuntimeAnimatorController _unarmedController;
    private static AnimationClip _unarmedPunch;
    private static Keyboard _keyboard;
    private static InputActionAsset _asset;
    private static InputDevice[] _oldDevices;
    private static InputSettings _settings;
    private static Phase _phase;
    private static float _phaseStart;
    private static float _cancelStartX;
    private static bool _sawDrink;
    private static bool _sawThrow;
    private static bool _finishing;
    private static bool _logging;
    private static bool _exitScheduled;
    private static IPlayerAttack _originalAttack;
    private static AttackProbe _attackProbe;
    private static float _attackStartedAt;
    private static int _attackCancelIndex;
    private static bool _revivedCancelledDeath;
    private static readonly PlayerState[] AttackCancellationStates = { PlayerState.Hit, PlayerState.Dash, PlayerState.Dead };

    static ProtagonistExtendedValidation()
    {
        EditorApplication.playModeStateChanged += OnMode;
        if (SessionState.GetBool(Prefix + "Active", false))
        {
            Restore();
            Hook();
            EditorApplication.delayCall += Resume;
        }
    }

    public static void Validate()
    {
        _report = new Report { unity = Application.unityVersion };
        try
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                ProtagonistUnitySetup.Folder + "/Protagonist01_Unarmed.controller");
            if (controller == null) throw new InvalidOperationException("Missing protagonist controller.");
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(ProtagonistExtendedAnimationSetup.ExtendedFbxPath)
                .OfType<AnimationClip>().ToArray();
            foreach (ProtagonistExtraAction action in Enum.GetValues(typeof(ProtagonistExtraAction)))
            {
                string name = action.ToString();
                AnimationClip clip = clips.FirstOrDefault(candidate => candidate.name == name);
                Check(clip != null && clip.humanMotion && clip.length > 0f && !clip.isLooping &&
                    AnimationUtility.GetAnimationEvents(clip).Length == 0, name + ": finite Humanoid clip, no side-effect events");
                AnimatorState state = machine.states.Select(child => child.state).FirstOrDefault(candidate => candidate.name == name);
                Check(state != null && state.motion == clip && state.speedParameterActive && state.speedParameter == "ActionSpeed",
                    name + ": state uses the imported clip and configurable speed");
                Check(controller.parameters.Any(parameter => parameter.name == name && parameter.type == AnimatorControllerParameterType.Trigger),
                    name + ": callable trigger exists");
                Check(state.behaviours.OfType<ProtagonistActionStateCallbacks>().Count(callback => callback.action == action) == 1,
                    name + ": exactly one progress callback behaviour");
                Check(machine.anyStateTransitions.Any(transition => transition.destinationState == state &&
                    transition.conditions.Any(condition => condition.parameter == name)), name + ": trigger reaches state");
            }

            foreach (string prefabPath in new[] { ProtagonistUnitySetup.PlayerPath, ProtagonistUnitySetup.VisualPath })
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                ProtagonistActionAnimator component = prefab != null ? prefab.GetComponent<ProtagonistActionAnimator>() : null;
                Check(component != null, prefabPath + ": root action API exists");
                foreach (ProtagonistExtraAction action in Enum.GetValues(typeof(ProtagonistExtraAction)))
                    Check(component.GetClip(action) == clips.First(clip => clip.name == action.ToString()),
                        prefabPath + ": " + action + " is configured");
                SerializedProperty definitions = new SerializedObject(component).FindProperty("_actions");
                Check(definitions.arraySize == 6, prefabPath + ": six action definitions");
                for (int index = 0; index < definitions.arraySize; index++)
                {
                    SerializedProperty definition = definitions.GetArrayElementAtIndex(index);
                    float commit = definition.FindPropertyRelative("commitNormalizedTime").floatValue;
                    Check(definition.FindPropertyRelative("duration").floatValue > 0f && commit >= 0f && commit <= 1f,
                        prefabPath + ": valid callback timing " + index);
                }
            }

            foreach (var pair in new[]
            {
                new KeyValuePair<string, string>(ProtagonistExtendedAnimationSetup.SwordOverridePath, "SwordSlash"),
                new KeyValuePair<string, string>(ProtagonistExtendedAnimationSetup.StaffOverridePath, "MagicCast")
            })
            {
                AnimatorOverrideController weapon = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(pair.Key);
                Check(weapon != null && weapon.runtimeAnimatorController == controller && weapon["Punch"] != null &&
                    weapon["Punch"].name == pair.Value, pair.Key + ": Punch maps to " + pair.Value);
            }
            _report.editorPassed = true;
            _report.success = true;
            Write();
            Debug.Log("PROTAGONIST_EXTENDED_EDITOR_VALIDATION_SUCCESS");
        }
        catch (Exception exception)
        {
            _report.failure = exception.ToString();
            Write();
            throw;
        }
    }

    // Independent batch entry point; omit -quit so the Play/stop callbacks can finish.
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start in Edit Mode.");
        Validate();
        _report.playModeExecuted = true;
        _report.success = false;
        SessionState.SetBool(Prefix + "Active", true);
        SessionState.SetString(Prefix + "State", "Entering");
        SessionState.SetFloat(Prefix + "Started", (float)EditorApplication.timeSinceStartup);
        Persist();
        Hook();
        try
        {
            EditorSceneManager.OpenScene(ProtagonistUnitySetup.ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }
        catch (Exception exception) { Finish(exception.ToString()); }
    }

    private static void Hook()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        if (!_logging) { Application.logMessageReceived += CaptureLog; _logging = true; }
    }

    private static void Resume()
    {
        if (SessionState.GetString(Prefix + "State", "") == "Stopping" && !EditorApplication.isPlayingOrWillChangePlaymode) ScheduleExit();
    }

    private static void OnMode(PlayModeStateChange change)
    {
        if (!SessionState.GetBool(Prefix + "Active", false)) return;
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            try
            {
                Restore();
                _player = UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None)
                    .Single(player => player.name == "Protagonist01_Player");
                _actions = _player.GetComponent<ProtagonistActionAnimator>();
                _animator = _player.GetComponent<Animator>();
                Check(_actions != null && _animator != null, "Actual playable root exposes the action API");
                _equipment = _player.GetComponent<HandSocketEquipment>();
                _driver = _player.GetComponent<ProtagonistAnimationDriver>();
                Check(_equipment != null && _driver != null, "Actual playable root exposes socket equipment and animation timing");
                _unarmedController = _animator.runtimeAnimatorController;
                _unarmedPunch = DriverAttackClip();
                _sword = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Stage01Kit/Prefabs/Weapons/Sword.prefab")
                    ?.GetComponent<SocketWeapon>();
                _staff = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Stage01Kit/Prefabs/Weapons/Staff.prefab")
                    ?.GetComponent<SocketWeapon>();
                Check(_sword != null && _staff != null && _unarmedPunch != null,
                    "Stage01Kit Sword, Staff and the baseline Punch clip exist");
                _actions.Cancelled += OnCancelled;
                InputManager input = _player.GetComponent<InputManager>();
                var reference = new SerializedObject(input).FindProperty("_moveAction").objectReferenceValue as InputActionReference;
                if (reference == null) throw new InvalidOperationException("Missing player movement action.");
                _asset = reference.action.actionMap.asset;
                _oldDevices = _asset.devices.HasValue ? _asset.devices.Value.ToArray() : null;
                // Replacing InputSystem.settings can destroy its prior temporary settings.
                // Keep the live object so the Input System's own Play Mode snapshot remains valid.
                _settings = InputSystem.settings;
                SessionState.SetInt(Prefix + "Background", (int)_settings.backgroundBehavior);
                SessionState.SetInt(Prefix + "EditorInput", (int)_settings.editorInputBehaviorInPlayMode);
                SessionState.SetBool(Prefix + "SettingsWereDirty", EditorUtility.IsDirty(_settings));
                SessionState.SetBool(Prefix + "SettingsTweaked", true);
                _settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                _settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                _keyboard = InputSystem.AddDevice<Keyboard>("ProtagonistExtraValidationKeyboard");
                _asset.devices = new InputDevice[] { _keyboard };
                QueueKeys();
                SessionState.SetString(Prefix + "State", "Running");
                Begin(Phase.Idle);
            }
            catch (Exception exception) { Finish(exception.ToString()); }
        }
        else if (change == PlayModeStateChange.EnteredEditMode)
        {
            if (SessionState.GetString(Prefix + "State", "") == "Stopping") ScheduleExit();
            else Finish("Play Mode stopped before callback validation completed.");
        }
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Prefix + "Active", false) || _finishing) return;
        try
        {
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Prefix + "Started", 0f) > 30f)
                throw new InvalidOperationException("Extended Play validation exceeded 30 seconds.");
            if (!EditorApplication.isPlaying || _player == null || SessionState.GetString(Prefix + "State", "") != "Running") return;
            if (_report.errors.Count > 0) throw new InvalidOperationException("Unity emitted an Error/Exception/Assert.");
            float elapsed = Time.realtimeSinceStartup - _phaseStart;
            switch (_phase)
            {
                case Phase.Idle:
                    if (elapsed > 0.15f && Ready())
                    {
                        _player.LookAtImmediately(1);
                        Check(_equipment.Equip(_sword), "Sword equips on the real player during Play Mode");
                        Begin(Phase.SwordEquip);
                    }
                    else Limit(elapsed, 2.5f, "Player never reached grounded Idle.");
                    break;
                case Phase.SwordEquip:
                    if (elapsed > 0.15f)
                    {
                        CheckEquipment(_sword, KitWeaponKind.Sword, "SwordSlash");
                        _player.LookAtImmediately(-1);
                        Begin(Phase.SwordFaceLeft);
                    }
                    break;
                case Phase.SwordFaceLeft:
                    if (elapsed > 0.15f)
                    {
                        CheckEquipment(_sword, KitWeaponKind.Sword, "SwordSlash", true);
                        _player.LookAtImmediately(1);
                        Begin(Phase.SwordFaceRight);
                    }
                    break;
                case Phase.SwordFaceRight:
                    if (elapsed > 0.15f)
                    {
                        CheckEquipment(_sword, KitWeaponKind.Sword, "SwordSlash");
                        Check(_equipment.Equip(_staff), "Staff replaces Sword on the real player during Play Mode");
                        Begin(Phase.StaffEquip);
                    }
                    break;
                case Phase.StaffEquip:
                    if (elapsed > 0.15f)
                    {
                        CheckEquipment(_staff, KitWeaponKind.Staff, "MagicCast");
                        Check(_actions.TryThrow(duration: 1.4f), "Staff-equipped Throw accepts a grounded idle request");
                        Check(!_equipment.Equipped.gameObject.activeSelf, "Throw immediately hides the equipped Staff");
                        Begin(Phase.ThrowHide);
                    }
                    break;
                case Phase.ThrowHide:
                    if (elapsed > 0.1f && _actions.IsBusy)
                    {
                        Check(_equipment.Equip(_sword), "Sword can replace Staff during an active utility action");
                        Check(_equipment.Equipped.Kind == KitWeaponKind.Sword && !_equipment.Equipped.gameObject.activeSelf,
                            "A replacement Sword remains hidden during the same Throw action");
                        Begin(Phase.SwapHiddenWeapon);
                    }
                    else Limit(elapsed, 0.5f, "Throw ended before its weapon-visibility replacement probe.");
                    break;
                case Phase.SwapHiddenWeapon:
                    if (elapsed > 0.1f)
                    {
                        Check(_actions.IsBusy && !_equipment.Equipped.gameObject.activeSelf,
                            "Replacement Sword stays hidden across subsequent Play frames");
                        _equipment.enabled = false;
                        Check(_equipment.Equipped.gameObject.activeSelf, "Disabling equipment restores its hidden weapon visibility");
                        _equipment.enabled = true;
                        Check(!_equipment.Equipped.gameObject.activeSelf, "Re-enabling equipment reapplies the current utility action visibility");
                        _actions.Cancel();
                        Check(!_actions.IsBusy && _equipment.Equipped.gameObject.activeSelf,
                            "Cancelling Throw restores the replacement Sword immediately");
                        Begin(Phase.RightDrinkStart);
                    }
                    break;
                case Phase.RightDrinkStart:
                    if (elapsed > 0.12f && Ready())
                    {
                        CheckEquipment(_sword, KitWeaponKind.Sword, "SwordSlash");
                        Check(_actions.TryDrink(duration: 0.65f), "Right-facing equipped Drink starts");
                        Check(!_equipment.Equipped.gameObject.activeSelf, "Right-facing Drink hides the near-hand Sword immediately");
                        Begin(Phase.RightDrink);
                    }
                    else Limit(elapsed, 1.5f, "Player failed to settle after cancelling equipped Throw.");
                    break;
                case Phase.RightDrink:
                    if (elapsed > 0.8f && !_actions.IsBusy && Ready())
                    {
                        Check(_equipment.Equipped.gameObject.activeSelf, "Completing right-facing Drink restores the Sword");
                        _player.LookAtImmediately(-1);
                        Begin(Phase.LeftDrinkStart);
                    }
                    else Limit(elapsed, 1.8f, "Right-facing equipped Drink failed to complete.");
                    break;
                case Phase.LeftDrinkStart:
                    if (elapsed > 0.15f && Ready())
                    {
                        CheckEquipment(_sword, KitWeaponKind.Sword, "SwordSlash", true);
                        Check(_actions.TryDrink(duration: 0.65f), "Left-facing equipped Drink starts");
                        Check(!_equipment.Equipped.gameObject.activeSelf, "Left-facing Drink hides the near-hand Sword immediately");
                        Begin(Phase.LeftDrink);
                    }
                    else Limit(elapsed, 1.5f, "Player failed to settle while facing left.");
                    break;
                case Phase.LeftDrink:
                    if (elapsed > 0.8f && !_actions.IsBusy && Ready())
                    {
                        Check(_equipment.Equipped.gameObject.activeSelf, "Completing left-facing Drink restores the Sword");
                        _equipment.Unequip();
                        _player.LookAtImmediately(1);
                        Begin(Phase.UnarmedRestore);
                    }
                    else Limit(elapsed, 1.8f, "Left-facing equipped Drink failed to complete.");
                    break;
                case Phase.UnarmedRestore:
                    if (elapsed > 0.15f && Ready())
                    {
                        Check(_equipment.Equipped == null && _animator.runtimeAnimatorController == _unarmedController &&
                            DriverAttackClip() == _unarmedPunch,
                            "Unequip restores the original controller and Punch timing clip");
                        float expected = _unarmedPunch.length / Mathf.Max(0.01f, _player.Attack != null ? _player.Attack.Duration : _player.AttackDuration);
                        Check(Mathf.Abs(_animator.GetFloat("AttackSpeed") - expected) < 0.02f,
                            "Unequip restores AttackSpeed to the original Punch duration");
                        Check(_actions.TryDrink(() => _report.drinkCommits++, () => _report.drinkCompletions++, 0.7f),
                            "TryDrink accepts a grounded idle request");
                        Begin(Phase.Drink);
                    }
                    else Limit(elapsed, 1.5f, "Unequipped player failed to return to Idle.");
                    break;
                case Phase.Drink:
                    _sawDrink |= IsState("Drink");
                    if (elapsed > 0.85f && !_actions.IsBusy && Ready())
                    {
                        Check(_sawDrink && _report.drinkCommits == 1 && _report.drinkCompletions == 1,
                            "Drink enters its state, commits exactly once, completes exactly once and returns to Idle");
                        Check(_actions.TryThrow(() => _report.throwCommits++, () => _report.throwCompletions++, 0.65f),
                            "TryThrow accepts a grounded idle request");
                        Begin(Phase.Throw);
                    }
                    else Limit(elapsed, 2f, "Drink did not complete normally.");
                    break;
                case Phase.Throw:
                    _sawThrow |= IsState("Throw");
                    if (elapsed > 0.8f && !_actions.IsBusy && Ready())
                    {
                        Check(_sawThrow && _report.throwCommits == 1 && _report.throwCompletions == 1,
                            "Throw enters its state, releases exactly once, completes exactly once and returns to Idle");
                        Check(_actions.TryDrink(() => _report.cancellationCommits++, () => _report.cancellationCompletions++, 1.4f),
                            "Second Drink starts the interruption probe");
                        Begin(Phase.StartCancel);
                    }
                    else Limit(elapsed, 2f, "Throw did not complete normally.");
                    break;
                case Phase.StartCancel:
                    if (elapsed > 0.12f && IsState("Drink") && _actions.IsBusy)
                    {
                        _cancelStartX = _player.transform.position.x;
                        QueueKeys(Key.D);
                        Begin(Phase.CancelMove);
                    }
                    else Limit(elapsed, 0.5f, "Interruption probe did not enter Drink before its commit marker.");
                    break;
                case Phase.CancelMove:
                    if (elapsed > 0.15f && !_actions.IsBusy && _player.transform.position.x > _cancelStartX + 0.1f)
                    {
                        Check(_report.cancellations == 1 && _report.cancellationCommits == 0 && _report.cancellationCompletions == 0,
                            "D movement cancels Drink once before commit without invoking consume or completion callbacks");
                        QueueKeys();
                        Begin(Phase.Settle);
                    }
                    else Limit(elapsed, 1.5f, "Movement failed to cancel the utility animation.");
                    break;
                case Phase.Settle:
                    if (elapsed > 0.5f && Ready())
                    {
                        Check(_report.cancellationCommits == 0 && _report.cancellationCompletions == 0 && _report.cancellations == 1,
                            "Cancelled callbacks stay silent after the player returns to Idle");
                        Check(_report.unexpectedDamageMessages == 0, "Animation-only utility requests emit no combat damage");
                        _originalAttack = _player.Attack;
                        StartAttackProbe();
                        Begin(Phase.AttackBeforeContact);
                    }
                    else Limit(elapsed, 1.5f, "Player failed to settle after cancellation.");
                    break;
                case Phase.AttackBeforeContact:
                    if (Time.time - _attackStartedAt >= _attackProbe.Duration * 0.1f)
                    {
                        Check(Time.time - _attackStartedAt < _attackProbe.Duration * _driver.AttackContactNormalized && _attackProbe.executions == 0,
                            "Attack executes no damage request during the first tenth of its duration");
                        Begin(Phase.AttackAfterContact);
                    }
                    else Limit(elapsed, 1f, "Attack never reached the pre-contact timing observation.");
                    break;
                case Phase.AttackAfterContact:
                    if (_attackProbe.executions > 0)
                    {
                        _report.expectedAttackContactSeconds = _attackProbe.Duration * _driver.AttackContactNormalized;
                        _report.actualAttackContactSeconds = _attackProbe.firstExecutedAt - _attackStartedAt;
                        Check(_attackProbe.executions == 1 && _report.actualAttackContactSeconds + 0.002f >= _report.expectedAttackContactSeconds,
                            "Attack commits once at or after its configured visual contact marker");
                        Begin(Phase.AttackSettle);
                    }
                    else Limit(elapsed, 1.5f, "Attack did not execute after its contact marker.");
                    break;
                case Phase.AttackSettle:
                    if (Time.time - _attackStartedAt > _attackProbe.Duration + 0.1f && Ready())
                    {
                        _report.attackProbeExecutions = _attackProbe.executions;
                        Check(_report.attackProbeExecutions == 1, "Completed attack never repeats its execution request");
                        _attackCancelIndex = 0;
                        StartAttackProbe();
                        Begin(Phase.CancelAttackBeforeContact);
                    }
                    else Limit(elapsed, 1.8f, "Attack failed to return to grounded Idle.");
                    break;
                case Phase.CancelAttackBeforeContact:
                    if (Time.time - _attackStartedAt >= _attackProbe.Duration * 0.1f)
                    {
                        Check(_attackProbe.executions == 0 && Time.time - _attackStartedAt < _attackProbe.Duration * _driver.AttackContactNormalized,
                            AttackCancellationStates[_attackCancelIndex] + ": interruption occurs before attack contact");
                        _player.ChangeState(AttackCancellationStates[_attackCancelIndex]);
                        _revivedCancelledDeath = false;
                        Begin(Phase.CancelAttackSettle);
                    }
                    else Limit(elapsed, 1f, "Cancelled attack never reached its interruption observation.");
                    break;
                case Phase.CancelAttackSettle:
                    if (Time.time - _attackStartedAt > _attackProbe.Duration + 0.15f)
                    {
                        Check(_attackProbe.executions == 0,
                            AttackCancellationStates[_attackCancelIndex] + ": cancelled attack stays silent beyond its original contact and end times");
                        if (_player.IsDead && !_revivedCancelledDeath)
                        {
                            _player.Revive();
                            _revivedCancelledDeath = true;
                        }
                        if (Ready())
                        {
                            _report.cancelledAttackProbeExecutions += _attackProbe.executions;
                            _attackCancelIndex++;
                            if (_attackCancelIndex < AttackCancellationStates.Length)
                            {
                                StartAttackProbe();
                                Begin(Phase.CancelAttackBeforeContact);
                            }
                            else
                            {
                                SetValidationAttack(_originalAttack);
                                Check(_report.unexpectedDamageMessages == 0,
                                    "Timing probes use an isolated IPlayerAttack observer and never damage scene dummies");
                                Finish(null);
                            }
                        }
                    }
                    Limit(elapsed, 2.5f, "Cancelled attack failed to settle or revive.");
                    break;
            }
        }
        catch (Exception exception) { Finish(exception.ToString()); }
    }

    private static bool Ready() => _player.IsGrounded && _player.CurrentStateType == PlayerState.Idle && IsState("Idle");
    private static AnimationClip DriverAttackClip() => new SerializedObject(_driver).FindProperty("_punchClip").objectReferenceValue as AnimationClip;
    private static void CheckEquipment(SocketWeapon prefab, KitWeaponKind kind, string attackName, bool facingLeft = false)
    {
        SocketWeapon instance = _equipment.Equipped;
        Check(instance != null && instance != prefab && instance.Kind == kind &&
            instance.transform.parent != null && instance.transform.parent.name == (facingLeft ? "WeaponSocket_Left" : "WeaponSocket") &&
            instance.transform.IsChildOf(_player.transform), kind + ": actual cloned weapon is attached to the player hand socket");
        Check(_equipment.UsesLeftHand == facingLeft && _equipment.MirrorLeft == facingLeft &&
            _equipment.NearHandSocket == instance.transform.parent && _animator.GetBool("FacingLeft") == facingLeft,
            kind + ": " + (facingLeft ? "left" : "right") + " facing matches near-hand socket and mirrored Animator state");
        var source = new SerializedObject(prefab);
        Vector3 euler = source.FindProperty("_gripEuler").vector3Value;
        Vector3 position = source.FindProperty("_gripPosition").vector3Value;
        if (facingLeft) { euler = new Vector3(euler.x, -euler.y, -euler.z); position.x = -position.x; }
        Check(Quaternion.Angle(instance.transform.localRotation, Quaternion.Euler(euler)) < 0.1f &&
            Vector3.Distance(instance.transform.localPosition, position) < 0.001f,
            kind + ": " + (facingLeft ? "left" : "right") + " hand uses the configured symmetric grip");
        Check(_animator.runtimeAnimatorController == prefab.AnimationOverride &&
            _animator.runtimeAnimatorController is AnimatorOverrideController weapon && weapon["Punch"] == prefab.BasicAttackClip &&
            prefab.BasicAttackClip.name == attackName && DriverAttackClip() == prefab.BasicAttackClip,
            kind + ": controller override, Punch motion and animation timing all switch to " + attackName);
        float duration = Mathf.Max(0.01f, _player.Attack != null ? _player.Attack.Duration : _player.AttackDuration);
        Check(Mathf.Abs(_animator.GetFloat("AttackSpeed") - prefab.BasicAttackClip.length / duration) < 0.02f,
            kind + ": AttackSpeed matches the equipped clip and gameplay duration");
    }
    private static bool IsState(string name) => _animator.GetCurrentAnimatorStateInfo(0).IsName(name) ||
        (_animator.IsInTransition(0) && _animator.GetNextAnimatorStateInfo(0).IsName(name));
    private static void QueueKeys(params Key[] keys) => InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));
    private static void StartAttackProbe()
    {
        _attackProbe = new AttackProbe();
        SetValidationAttack(_attackProbe);
        _attackStartedAt = Time.time;
        _player.ChangeState(PlayerState.Attack);
        Check(_attackProbe.executions == 0, "Animation-driven attack defers execution when entering its state");
    }
    private static void SetValidationAttack(IPlayerAttack attack)
    {
        // Develop exposes Attack as read-only. Inject the isolated observer only in
        // this Editor harness instead of adding a test setter to the runtime API.
        FieldInfo field = typeof(PlayerController).GetField("_attack", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) throw new MissingFieldException(typeof(PlayerController).FullName, "_attack");
        field.SetValue(_player, attack);
    }
    private static void OnCancelled(ProtagonistExtraAction action) { if (action == ProtagonistExtraAction.Drink) _report.cancellations++; }
    private static void Begin(Phase phase) { _phase = phase; _phaseStart = Time.realtimeSinceStartup; _report.phase = phase.ToString(); Persist(); }
    private static void Limit(float elapsed, float maximum, string failure) { if (elapsed >= maximum) throw new InvalidOperationException(failure); }

    private static void Check(bool passed, string check)
    {
        if (!passed) throw new InvalidOperationException(check);
        List<string> checks = _report.playModeExecuted ? _report.playChecks : _report.editorChecks;
        if (!checks.Contains(check)) checks.Add(check);
    }

    private static void CaptureLog(string condition, string trace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            _report.errors.Add(condition + "\n" + trace);
            Persist();
        }
        if (condition.StartsWith("[TestDummy]", StringComparison.Ordinal)) _report.unexpectedDamageMessages++;
    }

    private static void Finish(string failure)
    {
        if (_finishing) return;
        _finishing = true;
        _report.failure = failure;
        _report.playPassed = failure == null && _report.errors.Count == 0;
        _report.success = _report.editorPassed && _report.playPassed;
        SessionState.SetString(Prefix + "State", "Stopping");
        Persist();
        Write();
        if (_actions != null) { _actions.Cancelled -= OnCancelled; if (_actions.IsBusy) _actions.Cancel(); }
        if (_equipment != null) _equipment.Unequip();
        if (_player != null && _originalAttack != null) SetValidationAttack(_originalAttack);
        if (_asset != null) _asset.devices = _oldDevices;
        if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
        RestoreSettings(false);
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        else if (!EditorApplication.isPlayingOrWillChangePlaymode) ScheduleExit();
    }

    private static void Persist() => SessionState.SetString(Prefix + "Report", JsonUtility.ToJson(_report));
    private static void Restore() => _report = JsonUtility.FromJson<Report>(SessionState.GetString(Prefix + "Report", "{}")) ?? new Report();
    private static void Write()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
        File.WriteAllText(Path, JsonUtility.ToJson(_report, true));
    }
    private static void RestoreSettings(bool final)
    {
        if (!SessionState.GetBool(Prefix + "SettingsTweaked", false)) return;
        InputSettings settings = InputSystem.settings;
        settings.backgroundBehavior = (InputSettings.BackgroundBehavior)SessionState.GetInt(Prefix + "Background", 0);
        settings.editorInputBehaviorInPlayMode = (InputSettings.EditorInputBehaviorInPlayMode)SessionState.GetInt(Prefix + "EditorInput", 0);
        if (!SessionState.GetBool(Prefix + "SettingsWereDirty", false) && EditorUtility.IsDirty(settings))
            EditorUtility.ClearDirty(settings);
        if (final) SessionState.SetBool(Prefix + "SettingsTweaked", false);
    }

    private static void ScheduleExit()
    {
        if (_exitScheduled) return;
        _exitScheduled = true;
        // All Input System EnteredEditMode callbacks finish before restoration is verified.
        EditorApplication.delayCall += Exit;
    }

    private static void Exit()
    {
        Restore();
        RestoreSettings(true);
        _report.success &= _report.errors.Count == 0;
        Write();
        SessionState.SetBool(Prefix + "Active", false);
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= CaptureLog;
        _logging = false;
        Debug.Log(_report.success ? "PROTAGONIST_EXTENDED_PLAY_VALIDATION_SUCCESS" : "PROTAGONIST_EXTENDED_PLAY_VALIDATION_FAILED " + _report.failure);
        EditorApplication.Exit(_report.success ? 0 : 1);
    }
}
