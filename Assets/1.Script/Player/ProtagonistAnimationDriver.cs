using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Matches visual locomotion and punch playback to the existing player logic.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator), typeof(Rigidbody), typeof(PlayerController))]
public sealed class ProtagonistAnimationDriver : MonoBehaviour
{
    [SerializeField] private AnimationClip _punchClip;
    [SerializeField] private bool _allowControlKeyWalk = true;
    [SerializeField, Range(0.1f, 1f)] private float _slowWalkMultiplier = 0.44f;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackSpeedHash = Animator.StringToHash("AttackSpeed");
    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");

    private Animator _animator;
    private Rigidbody _rigidbody;
    private PlayerController _player;
    private RuntimeAnimatorController _cachedController;
    private bool _hasSpeed;
    private bool _hasAttackSpeed;
    private bool _hasIsDead;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _rigidbody = GetComponent<Rigidbody>();
        _player = GetComponent<PlayerController>();
        _animator.applyRootMotion = false;
        CacheParameters();
    }

    public void Configure(AnimationClip punchClip)
    {
        _punchClip = punchClip;
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
        if (_cachedController == null)
        {
            return;
        }

        foreach (AnimatorControllerParameter parameter in _animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Bool)
            {
                _hasIsDead |= parameter.nameHash == IsDeadHash;
            }

            if (parameter.type != AnimatorControllerParameterType.Float)
            {
                continue;
            }

            _hasSpeed |= parameter.nameHash == SpeedHash;
            _hasAttackSpeed |= parameter.nameHash == AttackSpeedHash;
        }
    }
}
