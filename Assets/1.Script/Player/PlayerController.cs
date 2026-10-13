using System;
using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Component")]
    [SerializeField] private InputManager _inputManager;

    [Header("Ground Check")]
    [SerializeField] private Transform _groundCheck;
    [SerializeField] private float _groundRadius = 0.2f;
    [SerializeField] private LayerMask _groundLayer;

    [Header("Move")]
    [SerializeField] private float _walkSpeed = 5f;
    [SerializeField] private float _jumpSpeed = 5f;
    [SerializeField] private float _jumpForce = 12f;
    [SerializeField] private float _landDuration = 0.1f;
    [SerializeField] private float _rotationSpeed = 20f;

    [Header("Dash")]
    [SerializeField] private float _dashSpeed = 14f;
    [SerializeField] private float _dashDuration = 0.18f;
    [SerializeField] private float _dashInvincibleTime = 0.15f;
    [SerializeField] private float _dashCooldown = 0.6f;
    [SerializeField] private bool _canAirDash = true;

    [Header("Combat")]
    [SerializeField] private float _attackDuration = 0.4f; // IPlayerAttack이 없을 때 기본값
    [SerializeField] private float _hitStunTime = 0.3f;

    [Header("Debug")]
    [SerializeField] private PlayerState _currentStateType;
    
    private Animator _animator;
    private Rigidbody _rigidbody;

    private IPlayerState _currentState;
    private Dictionary<PlayerState, IPlayerState> _playerStates;
    private IPlayerAttack _attack; // 공격 확장 지점. 없으면 모션만 재생.

    private float _facingX = 1f;

    private float _invincibleEndTime;
    private float _nextDashTime;
    private bool _airDashUsed;

    public event Action OnDead;

    public InputManager Input { get { return _inputManager; } }
    public IPlayerAttack Attack { get { return _attack; } }
    public PlayerState CurrentStateType { get { return _currentStateType; } }


    public bool IsGrounded { get { return Physics.CheckSphere(_groundCheck.position, _groundRadius, _groundLayer); } }
    public bool IsJumpRequested { get; private set; }
    public bool IsDashRequested { get; private set; }
    public bool IsAttackRequested { get; private set; }

    public bool IsInvincible { get { return Time.time < _invincibleEndTime; } }
    public bool IsDead { get { return _currentStateType == PlayerState.Dead; } }
    public float FacingX { get { return _facingX; } }

    public bool CanDash
    {
        get
        {
            if (Time.time < _nextDashTime)
            {
                return false;
            }
            if (IsGrounded)
            {
                return true;
            }
            return _canAirDash && !_airDashUsed;
        }
    }

    public float WalkSpeed { get { return _walkSpeed; } }
    public float JumpSpeed { get { return _jumpSpeed; } }
    public float LandDuration { get { return _landDuration; } }
    public float DashSpeed { get { return _dashSpeed; } }
    public float DashDuration { get { return _dashDuration; } }
    public float AttackDuration { get { return _attackDuration; } }
    public float HitStunTime { get { return _hitStunTime; } }

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _rigidbody = GetComponent<Rigidbody>();

        if (_inputManager == null)
        {
            Debug.LogError("[Player:Awake] InputManager가 연결되지 않았습니다.");
            return;
        }

        if (_groundCheck == null)
        {
            Debug.LogError("[Player:Awake] GroundCheck가 연결되지 않았습니다.");
            return;
        }

        _attack = GetComponent<IPlayerAttack>();
        _rigidbody.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        InitStateDictionary();
    }

    private void OnEnable()
    {
        _inputManager.OnJumpAction += OnJump;
        _inputManager.OnDashAction += OnDash;
        _inputManager.OnAttackAction += OnAttack;

        ChangeState(PlayerState.Idle);
    }

    private void Update()
    {
        if (_currentState == null)
        {
            return;
        }

        _currentState.UpdateState(this);

        // 상태가 이번 프레임에 안 쓴 입력은 버림 (경직 중 누른 점프가 끝나고 발동되는 것 방지)
        ClearRequests();
    }

    private void FixedUpdate()
    {
        if (_currentState == null)
        {
            return;
        }

        _currentState.FixedUpdateState(this);
    }

    private void OnDisable()
    {
        ClearRequests();
        _inputManager.OnJumpAction -= OnJump;
        _inputManager.OnDashAction -= OnDash;
        _inputManager.OnAttackAction -= OnAttack;
    }

    private void InitStateDictionary()
    {
        _playerStates = new Dictionary<PlayerState, IPlayerState>
        {
            {PlayerState.Idle, new PlayerState_Idle() },
            {PlayerState.Walk, new PlayerState_Walk() },
            {PlayerState.Jump, new PlayerState_Jump() },
            {PlayerState.Air, new PlayerState_Air() },
            {PlayerState.Land, new PlayerState_Land() },
            {PlayerState.Dash, new PlayerState_Dash() },
            {PlayerState.Attack, new PlayerState_Attack() },
            {PlayerState.Hit, new PlayerState_Hit() },
            {PlayerState.Dead, new PlayerState_Dead() }
        };
    }

    private void ClearRequests()
    {
        IsJumpRequested = false;
        IsDashRequested = false;
        IsAttackRequested = false;
    }

    private void OnJump()
    {
        IsJumpRequested = true;
    }

    private void OnDash()
    {
        IsDashRequested = true;
    }

    private void OnAttack()
    {
        IsAttackRequested = true;
    }

    public void ChangeState(PlayerState newState)
    {
        if (_playerStates.ContainsKey(newState) == false)
        {
            Debug.LogError("[Player:ChangeState] 플레이어 상태를 찾을 수 없습니다.");
            return;
        }

        if (_currentState != null)
        {
            // 같은 상태 재진입(EnterState 중복 호출) 방지
            if (_currentStateType == newState)
            {
                return;
            }

            if (_currentStateType == PlayerState.Dead)
            {
                return;
            }
            _currentState.ExitState(this);
        }

        _currentStateType = newState;
        _currentState = _playerStates[newState];
        _currentState.EnterState(this);
    }

    // 공격/피격/대시가 끝났을 때 돌아갈 상태
    public void ChangeToDefaultState()
    {
        ChangeState(IsGrounded ? PlayerState.Idle : PlayerState.Air);
    }

    public Animator GetAnimator()
    {
        return _animator;
    }

    public Rigidbody GetRigidbody()
    {
        return _rigidbody;
    }

    // 다른 공격(스킬 등)으로 교체할 때 사용
    public void SetAttack(IPlayerAttack attack)
    {
        _attack = attack;
    }

    //HP 계산은 PlayerHealth로 옮기고, 컨트롤러는 상태 전환만 담당
    public void OnHit()
    {
        ChangeState(PlayerState.Hit);
    }

    public void Die()
    {
        ChangeState(PlayerState.Dead);
        OnDead?.Invoke();
    }

    // 스테이지 재시작 시 (PlayerHealth.ResetHealth에서 호출)
    public void Revive()
    {
        if (_currentState != null)
        {
            _currentState.ExitState(this);
        }

        _currentState = null;
        _invincibleEndTime = 0f;
        _nextDashTime = 0f;
        _airDashUsed = false;
        ClearRequests();
        ChangeState(PlayerState.Idle);
    }

    public void SetInvincible(float duration)
    {
        _invincibleEndTime = Mathf.Max(_invincibleEndTime, Time.time + duration);
    }

    public void StartDash()
    {
        IsDashRequested = false;
        _nextDashTime = Time.time + _dashCooldown;
        SetInvincible(_dashInvincibleTime);

        if (!IsGrounded)
        {
            _airDashUsed = true;
        }
    }

    public void ResetAirDash()
    {
        _airDashUsed = false;
    }

    // AddForce 대신 속도 대입 → 중복 호출돼도 점프력이 누적되지 않음
    public void Jump()
    {
        IsJumpRequested = false;

        Vector3 velocity = _rigidbody.linearVelocity;
        velocity.y = _jumpForce;
        _rigidbody.linearVelocity = velocity;
    }

    // 좌우 이동 + 이동 방향 바라보기
    public void Move(float speed)
    {
        float inputX = _inputManager.MoveInput.x;

        Vector3 velocity = _rigidbody.linearVelocity;
        velocity.x = inputX * speed;
        _rigidbody.linearVelocity = velocity;

        LookAt(inputX);
    }

    public void Stop()
    {
        Vector3 velocity = _rigidbody.linearVelocity;
        velocity.x = 0f;
        _rigidbody.linearVelocity = velocity;
    }
    public void LookAt(float dirX)
    {
        if (Mathf.Abs(dirX) < 0.01f)
        {
            return;
        }

        _facingX = Mathf.Sign(dirX);
        Quaternion targetRotation = Quaternion.LookRotation(_facingX > 0 ? Vector3.right : Vector3.left);
        _rigidbody.MoveRotation(Quaternion.Slerp(transform.rotation, targetRotation, _rotationSpeed * Time.fixedDeltaTime));
    }

    // 공격, 대시처럼 즉시 방향을 돌려야 할 때
    public void LookAtImmediately(float dirX)
    {
        if (Mathf.Abs(dirX) < 0.01f)
        {
            return; // 커서가 플레이어와 겹치면 마지막 방향 유지
        }

        _facingX = Mathf.Sign(dirX);
        _rigidbody.rotation = Quaternion.LookRotation(_facingX > 0 ? Vector3.right : Vector3.left);
    }

}
