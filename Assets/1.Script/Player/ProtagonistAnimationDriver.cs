using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Matches visual locomotion and punch playback to the existing player logic.</summary>
[DefaultExecutionOrder(10)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator), typeof(Rigidbody), typeof(PlayerController))]
public sealed class ProtagonistAnimationDriver : MonoBehaviour
{
    public const float UnarmedAttackContactNormalized = .82f;
    [SerializeField] private AnimationClip _punchClip;
    [SerializeField] private AnimationClip _skillClip;
    [SerializeField, Range(0f, 1f)] private float _attackContactNormalized = UnarmedAttackContactNormalized;
    [SerializeField] private bool _allowControlKeyWalk = true;
    [SerializeField, Range(0.1f, 1f)] private float _slowWalkMultiplier = 0.44f;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackSpeedHash = Animator.StringToHash("AttackSpeed");
    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
    private static readonly int FacingLeftHash = Animator.StringToHash("FacingLeft");
    private static readonly int SkillSpeedHash = Animator.StringToHash("SkillSpeed");

    private Animator _animator;
    private Rigidbody _rigidbody;
    private PlayerController _player;
    private RuntimeAnimatorController _cachedController;
    private bool _hasSpeed;
    private bool _hasAttackSpeed;
    private bool _hasIsDead;
    private bool _hasFacingLeft;
    private bool _hasSkillSpeed;

    public float AttackContactNormalized => _attackContactNormalized;

    public void SetAttackContactNormalized(float normalizedTime)
    {
        _attackContactNormalized = Mathf.Clamp01(normalizedTime);
    }

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _rigidbody = GetComponent<Rigidbody>();
        _player = GetComponent<PlayerController>();
        if (_skillClip == null) _skillClip = _punchClip;
        _animator.applyRootMotion = false;
        CacheParameters();
    }

    public void Configure(AnimationClip punchClip)
    {
        _punchClip = punchClip;
        _skillClip = punchClip;
    }

    private void Update()
    {
        if (_animator == null || _rigidbody == null || _player == null)
        {
            return;
        }

        if (_cachedController != _animator.runtimeAnimatorController)
        {
            CacheParameters();
        }

        Keyboard keyboard = Keyboard.current;
        bool slowWalk = _allowControlKeyWalk && keyboard != null &&
            (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
        _player.SetWalkInputMultiplier(slowWalk ? _slowWalkMultiplier : 1f);

        if (_hasSpeed)
        {
            _animator.SetFloat(SpeedHash, Mathf.Abs(_rigidbody.linearVelocity.x));
        }

        if (_hasAttackSpeed && _punchClip != null)
        {
            IPlayerAttack attack = _player.Attack;
            float duration = attack != null ? attack.Duration : _player.AttackDuration;
            _animator.SetFloat(AttackSpeedHash, _punchClip.length / Mathf.Max(0.01f, duration));
        }

        if (_hasIsDead)
        {
            _animator.SetBool(IsDeadHash, _player.IsDead);
        }

        if (_hasFacingLeft)
        {
            _animator.SetBool(FacingLeftHash, transform.forward.x < 0f);
        }

        AnimationClip skillClip = _skillClip != null ? _skillClip : _punchClip;
        if (_hasSkillSpeed && skillClip != null)
        {
            float duration = 0.5f;
            PlayerSkill skills = _player.Skills;
            PlayerSkillBase skill = skills != null ? skills.GetEquipped(_player.RequestedSkillSlot) : null;
            if (skill != null && skill.Data != null) duration = skill.Data.CastDuration;
            _animator.SetFloat(SkillSpeedHash, skillClip.length / Mathf.Max(0.01f, duration));
        }
    }

    private void OnDisable()
    {
        if (_player != null) _player.SetWalkInputMultiplier(1f);
    }

    private void CacheParameters()
    {
        _cachedController = _animator.runtimeAnimatorController;
        _hasSpeed = false;
        _hasAttackSpeed = false;
        _hasIsDead = false;
        _hasFacingLeft = false;
        _hasSkillSpeed = false;
        if (_cachedController == null)
        {
            return;
        }

        foreach (AnimatorControllerParameter parameter in _animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Bool)
            {
                _hasIsDead |= parameter.nameHash == IsDeadHash;
                _hasFacingLeft |= parameter.nameHash == FacingLeftHash;
            }

            if (parameter.type != AnimatorControllerParameterType.Float)
            {
                continue;
            }

            _hasSpeed |= parameter.nameHash == SpeedHash;
            _hasAttackSpeed |= parameter.nameHash == AttackSpeedHash;
            _hasSkillSpeed |= parameter.nameHash == SkillSpeedHash;
        }
    }
}
