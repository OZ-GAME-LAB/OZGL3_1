using System;
using UnityEngine;

public enum ProtagonistExtraAction { Drink, Throw, SwordSlash, MagicCast, Interact, Pickup }

/// <summary>
/// Optional animation requests and one-shot timing callbacks. The caller owns item
/// consumption, projectiles, gate rules and damage; this component changes none of them.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class ProtagonistActionAnimator : MonoBehaviour
{
    [Serializable]
    public sealed class ActionDefinition
    {
        public ProtagonistExtraAction action;
        public AnimationClip clip;
        [Min(0.05f)] public float duration = 1f;
        [Range(0f, 1f)] public float commitNormalizedTime = 0.5f;
    }

    [SerializeField] private ActionDefinition[] _actions = Array.Empty<ActionDefinition>();
    private static readonly int BusyHash = Animator.StringToHash("ActionBusy");
    private static readonly int SpeedHash = Animator.StringToHash("ActionSpeed");
    private Animator _animator;
    private PlayerController _player;
    private ActionDefinition _active;
    private Action _onCommit;
    private Action _onCompleted;
    private bool _entered;
    private bool _committed;
    private float _lastProgress;
    private float _requestedAt;
    private float _requestedDuration;

    public bool IsBusy => _active != null;
    public ProtagonistExtraAction? ActiveAction => _active != null ? _active.action : (ProtagonistExtraAction?)null;
    public event Action<ProtagonistExtraAction> Started;
    public event Action<ProtagonistExtraAction> Committed;
    public event Action<ProtagonistExtraAction> Completed;
    public event Action<ProtagonistExtraAction> Cancelled;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _player = GetComponent<PlayerController>();
    }

    public void Configure(ActionDefinition[] actions) => _actions = actions ?? Array.Empty<ActionDefinition>();

    public AnimationClip GetClip(ProtagonistExtraAction action)
    {
        ActionDefinition definition = Find(action);
        return definition != null ? definition.clip : null;
    }

    public bool TryDrink(Action consume = null, Action completed = null, float duration = -1f)
        => TryPlay(ProtagonistExtraAction.Drink, consume, completed, duration);

    public bool TryThrow(Action release = null, Action completed = null, float duration = -1f)
        => TryPlay(ProtagonistExtraAction.Throw, release, completed, duration);

    public bool TryInteract(Action interact = null, Action completed = null, float duration = -1f)
        => TryPlay(ProtagonistExtraAction.Interact, interact, completed, duration);

    public bool TryPickup(Action collect = null, Action completed = null, float duration = -1f)
        => TryPlay(ProtagonistExtraAction.Pickup, collect, completed, duration);

    public bool TryPlay(ProtagonistExtraAction action, Action onCommit = null, Action onCompleted = null, float duration = -1f)
    {
        if (_animator == null) Awake();
        ActionDefinition definition = Find(action);
        if (!isActiveAndEnabled || IsBusy || definition == null || definition.clip == null ||
            !_animator.isActiveAndEnabled || _animator.runtimeAnimatorController == null) return false;
        if (_player != null)
        {
            if (_player.IsDead || _player.CurrentStateType == PlayerState.Hit || _player.CurrentStateType == PlayerState.Dash)
                return false;
            bool utility = action != ProtagonistExtraAction.SwordSlash && action != ProtagonistExtraAction.MagicCast;
            if (utility && (!_player.IsGrounded || _player.CurrentStateType != PlayerState.Idle)) return false;
        }
        int trigger = Animator.StringToHash(action.ToString());
        if (!HasParameter(trigger, AnimatorControllerParameterType.Trigger) ||
            !_animator.HasState(0, Animator.StringToHash("Base Layer." + action))) return false;

        _active = definition;
        _onCommit = onCommit;
        _onCompleted = onCompleted;
        _entered = false;
        _committed = false;
        _lastProgress = 0f;
        _requestedAt = Time.time;
        _requestedDuration = Mathf.Max(0.05f, duration > 0f ? duration : definition.duration);
        if (HasParameter(BusyHash, AnimatorControllerParameterType.Bool)) _animator.SetBool(BusyHash, true);
        if (HasParameter(SpeedHash, AnimatorControllerParameterType.Float))
            _animator.SetFloat(SpeedHash, definition.clip.length / _requestedDuration);
        _animator.ResetTrigger(trigger);
        _animator.SetTrigger(trigger);
        Started?.Invoke(action);
        return true;
    }

    public void Cancel()
    {
        if (!IsBusy) return;
        string stateName = _active.action.ToString();
        bool ownsCurrentState = _animator != null && _animator.isActiveAndEnabled &&
            _animator.GetCurrentAnimatorStateInfo(0).IsName(stateName);
        End(false);
        if (ownsCurrentState && (_player == null || !_player.IsDead))
        {
            string state = _player != null && !_player.IsGrounded ? "Air" : "Idle";
            _animator.CrossFadeInFixedTime(state, 0.04f);
        }
    }

    private void Update()
    {
        if (!IsBusy) return;
        if (_player != null && _player.IsDead) { End(false); return; }
        bool utility = _active.action != ProtagonistExtraAction.SwordSlash && _active.action != ProtagonistExtraAction.MagicCast;
        if (_player != null && utility && (_player.CurrentStateType != PlayerState.Idle || !_player.IsGrounded))
        {
            if (_player.CurrentStateType == PlayerState.Walk || _player.CurrentStateType == PlayerState.Air) Cancel();
            else End(false); // Gameplay-owned attack, jump, hurt and dodge transitions keep priority.
            return;
        }
        if (!_entered && Time.time - _requestedAt > 0.75f) { End(false); return; }
        if (Time.time - _requestedAt > _requestedDuration + 1f) Cancel();
    }

    private void OnDisable()
    {
        if (IsBusy) End(false);
    }

    // Called by controller StateMachineBehaviours, not by imported animation events.
    public void NotifyEntered(ProtagonistExtraAction action)
    {
        if (_active != null && _active.action == action) _entered = true;
    }

    public void NotifyProgress(ProtagonistExtraAction action, float normalizedTime)
    {
        if (_active == null || _active.action != action || !_entered) return;
        ActionDefinition active = _active;
        _lastProgress = normalizedTime;
        if (!_committed && normalizedTime >= _active.commitNormalizedTime)
        {
            _committed = true;
            Action callback = _onCommit;
            _onCommit = null;
            callback?.Invoke();
            Committed?.Invoke(action);
        }
        if (ReferenceEquals(_active, active) && normalizedTime >= 0.999f) End(true);
    }

    public void NotifyExited(ProtagonistExtraAction action)
    {
        if (_active != null && _active.action == action && _entered)
        {
            if (_lastProgress >= 0.95f) NotifyProgress(action, 1f);
            else End(false);
        }
    }

    private void End(bool completed)
    {
        ProtagonistExtraAction action = _active.action;
        Action callback = _onCompleted;
        _active = null;
        _onCommit = null;
        _onCompleted = null;
        if (_animator != null && _animator.isActiveAndEnabled && _animator.runtimeAnimatorController != null)
        {
            int trigger = Animator.StringToHash(action.ToString());
            if (HasParameter(trigger, AnimatorControllerParameterType.Trigger)) _animator.ResetTrigger(trigger);
            if (HasParameter(BusyHash, AnimatorControllerParameterType.Bool)) _animator.SetBool(BusyHash, false);
        }
        if (completed) { callback?.Invoke(); Completed?.Invoke(action); }
        else Cancelled?.Invoke(action);
    }

    private ActionDefinition Find(ProtagonistExtraAction action)
    {
        foreach (ActionDefinition definition in _actions)
            if (definition != null && definition.action == action) return definition;
        return null;
    }

    private bool HasParameter(int hash, AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter parameter in _animator.parameters)
            if (parameter.nameHash == hash && parameter.type == type) return true;
        return false;
    }
}
