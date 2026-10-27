using OZ.UI.Contracts;
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

    [Header("Ladder")]
    [SerializeField] private float _climbSpeed = 3f;
    [SerializeField] private float _ladderRegrabDelay = 0.3f;
    [SerializeField] private float _ladderTopOffset = 0.6f; // 발이 꼭대기보다 이 수치만큼 아래면 바로 올라감

    [Header("Drop Through")]
    [SerializeField] private float _dropMinTime = 0.25f; // 최소 이 시간은 충돌을 꺼 둠 (한 프레임 만에 다시 올라타는 것 방지)
    [SerializeField] private float _dropStartSpeed = 2f; // 내려가기 시작할 때 아래로 주는 속도 (발판에 걸려 멈칫하지 않게)

    [Header("Debug")]
    [SerializeField] private PlayerState _currentStateType;
    [SerializeField] private float _debugWalkSpeed; // 인스펙터 확인용. 민첩·신속 버프가 곱해진 최종 걷기 속도

    private Animator _animator;
    private Rigidbody _rigidbody;

    private IPlayerState _currentState;
    private Dictionary<PlayerState, IPlayerState> _playerStates;
    private IPlayerAttack _attack; // 공격 확장 지점. 없으면 모션만 재생.
    private PlayerSkill _skills; //없으면 스킬 입력 무시 -> 스킬 없는 테스트 씬에서도 플레이어가 그대로 동작
    private PlayerBuffs _buffs;  //없으면 신속 버프 없이 동작
    private PlayerStats _stats;  // 새 게임 초기화용

    private float _facingX = 1f;

    private float _invincibleEndTime;
    private float _nextDashTime;
    private bool _airDashUsed;

    private float _moveSpeedBonus; // 민첩 스탯 보너스. 0.03 = +3%
    private float _walkInputMultiplier = 1f;

    private Ladder _currentLadder;
    private float _nextLadderGrabTime;

    // 아랫점프
    // 모델,무기 등 자식 콜라이더도 있을 수 있음 -> 루트 하나만 끄면 자식이 발판에 걸림
    private Collider[] _bodyColliders;
    private readonly Collider[] _groundHits = new Collider[8];
    private OneWayPlatform _droppingPlatform; // 지금 통과 중인 발판 (null이면 통과 중 아님)
    private float _dropMinEndTime;

    public InputManager Input { get { return _inputManager; } }
    public IPlayerAttack Attack { get { return _attack; } }
    public PlayerSkill Skills { get { return _skills; } } //PlayerState_Skill이 장착 스킬·쿨다운을 읽기 위해
    public PlayerState CurrentStateType { get { return _currentStateType; } }
    public Ladder CurrentLadder { get { return _currentLadder; } }
    public float ClimbSpeed { get { return _climbSpeed; } }
    public float LadderTopOffset { get { return _ladderTopOffset; } }

    // 통과 중인 발판은 바닥으로 치지 않음 -> 내려가는 도중 바로 Land로 돌아가지 않게
    public bool IsGrounded { get { return CheckGround(out _); } }
    public bool IsJumpRequested { get; private set; }
    public bool IsDashRequested { get; private set; }
    public bool IsAttackRequested { get; private set; }
    public bool IsSkillRequested { get; private set; }
    public SkillSlot RequestedSkillSlot { get; private set; } //어느 키(Q/E/R)를 눌렀는지 상태에 전달

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

    public bool CanGrabLadder
    {
        get
        {
            if (_currentLadder == null || Time.time < _nextLadderGrabTime)
            {
                return false;
            }
            float inputY = _inputManager.MoveInput.y;
            float feetY = transform.position.y;
            if (inputY > 0.5f) return feetY < _currentLadder.TopY - 0.05f;
            if (inputY < -0.5f) return feetY > _currentLadder.BottomY + 0.3f;
            return false;
        }
    }

    // 수평 이동 속도에만 배율 적용 (대시는 회피 거리가 바뀌지 않도록 제외)
    // 민첩 스탯 x 신속 버프
    public float MoveSpeedMultiplier { get { return (1f + _moveSpeedBonus) * (_buffs != null ? _buffs.MoveSpeedMultiplier : 1f); } }
    public float WalkSpeed { get { return _walkSpeed * MoveSpeedMultiplier * _walkInputMultiplier; } }
    public float JumpSpeed { get { return _jumpSpeed * MoveSpeedMultiplier; } }
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
        _skills = GetComponent<PlayerSkill>();
        _buffs = GetComponent<PlayerBuffs>();
        _bodyColliders = CollectBodyColliders();
        _rigidbody.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        InitStateDictionary();
        _stats = GetComponent<PlayerStats>();
        UIRequests.NewGame += ResetForNewGame;
    }

    private void OnEnable()
    {
        _inputManager.OnJumpAction += OnJump;
        _inputManager.OnDashAction += OnDash;
        _inputManager.OnAttackAction += OnAttack;
        _inputManager.OnSkillAction += OnSkill;
        _inputManager.OnItemAction += OnItem;

        ChangeState(PlayerState.Idle);
    }

    private void Update()
    {
        if (_currentState == null)
        {
            return;
        }

        // 상태보다 먼저 -> 발판을 빠져나간 프레임에 바로 충돌이 복구돼야 착지 판정이 맞음
        UpdateDropThrough();

        _debugWalkSpeed = WalkSpeed;
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
        EndDropThrough(); // 통과 중에 꺼지면 충돌이 꺼진 채로 남지 않게
        _inputManager.OnJumpAction -= OnJump;
        _inputManager.OnDashAction -= OnDash;
        _inputManager.OnAttackAction -= OnAttack;
        _inputManager.OnSkillAction -= OnSkill;
        _inputManager.OnItemAction -= OnItem;
    }

    private void OnDestroy()
    {
        UIRequests.NewGame -= ResetForNewGame;
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
            {PlayerState.Skill, new PlayerState_Skill() },
            {PlayerState.Ladder, new PlayerState_Ladder() },
            {PlayerState.Hit, new PlayerState_Hit() },
            {PlayerState.Dead, new PlayerState_Dead() }
        };
    }

    private void ClearRequests()
    {
        IsJumpRequested = false;
        IsDashRequested = false;
        IsAttackRequested = false;
        IsSkillRequested = false;
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

    private void OnSkill(SkillSlot slot)
    {
        if (_skills == null)
        {
            return;
        }
        if (!_skills.CanUse(slot, out SkillUseFailReason reason))
        {
            _skills.NotifyUseFailed(slot, reason);
            return;
        }
        IsSkillRequested = true;
        RequestedSkillSlot = slot;
    }

    private void OnItem(int slotIndex)
    {
        if (IsDead)
        {
            return;
        }
        if (UIState.IsGameplayInputBlocked)
        {
            return;
        }
        UISources.Items?.UseItem(slotIndex);
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

    // 공격/스킬/피격/대시가 끝났을 때 돌아갈 상태 (스킬도 여기로 복귀)
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

    // PlayerStats에서 호출 (민첩 스탯 변경 시). 0.09 = +9%
    public void SetMoveSpeedBonus(float bonus)
    {
        _moveSpeedBonus = Mathf.Max(0f, bonus);
    }

    // Optional gait input; defaults to the existing full movement speed.
    public void SetWalkInputMultiplier(float multiplier)
    {
        _walkInputMultiplier = Mathf.Clamp(multiplier, 0.1f, 1f);
    }

    //HP 계산은 PlayerHealth로 옮기고, 컨트롤러는 상태 전환만 담당
    public void OnHit()
    {
        ChangeState(PlayerState.Hit);
    }

    public void Die()
    {
        ChangeState(PlayerState.Dead);
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
        EndDropThrough(); // 통과 중 사망 -> 재시작 위치에서 발판을 뚫고 떨어지지 않게
        ClearRequests();
        ChangeState(PlayerState.Idle);
    }

    // 타이틀 '새 게임' 때 이전 판 성장(버프·스킬·스탯)을 지움
    private void ResetForNewGame()
    {
        if (_buffs != null) _buffs.ClearAll();
        if (_skills != null) _skills.ResetAll();
        if (_stats != null) _stats.ResetAll(); // 최대 HP도 100으로 돌아감
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

    public void BlockLadderGrab()
    {
        _nextLadderGrabTime = Time.time + _ladderRegrabDelay;
    }

    public void FaceLadder()
    {
        _rigidbody.rotation = Quaternion.LookRotation(Vector3.forward);
    }

    // 지상 상태에서 점프 입력이 들어왔을 때 호출. 내려갔으면 true -> 상태는 Air로 바꿔야 함
    public bool TryDropThrough()
    {
        if (_inputManager.MoveInput.y > -0.5f)
        {
            return false;
        }

        if (!CheckGround(out OneWayPlatform platform) || platform == null)
        {
            return false;
        }

        IsJumpRequested = false; // 같은 입력으로 점프까지 나가지 않게
        SetPlatformCollision(platform, false);
        _droppingPlatform = platform;
        _dropMinEndTime = Time.time + _dropMinTime;

        // 발판 위에 가만히 서 있으면 중력만으로는 한 프레임 늦게 떨어져 멈칫해 보임
        Vector3 velocity = _rigidbody.linearVelocity;
        velocity.y = -_dropStartSpeed;
        _rigidbody.linearVelocity = velocity;
        return true;
    }

    // 발밑 바닥 검사. 통과 중인 발판은 제외.
    // 발밑이 통과 발판"만"이면 그 발판, 일반 바닥이 하나라도 있으면 null
    private bool CheckGround(out OneWayPlatform onlyPlatform)
    {
        onlyPlatform = null;
        int count = Physics.OverlapSphereNonAlloc(_groundCheck.position, _groundRadius, _groundHits, _groundLayer);

        bool grounded = false;
        bool onSolid = false;

        for (int i = 0; i < count; i++)
        {
            OneWayPlatform platform = _groundHits[i].GetComponentInParent<OneWayPlatform>();

            if (platform != null && platform == _droppingPlatform)
            {
                continue;
            }

            grounded = true;

            if (platform == null)
            {
                onSolid = true;
            }
            else
            {
                onlyPlatform = platform;
            }
        }

        if (onSolid)
        {
            onlyPlatform = null;
        }

        return grounded;
    }

    private void UpdateDropThrough()
    {
        if (_droppingPlatform == null)
        {
            return;
        }

        if (Time.time < _dropMinEndTime)
        {
            return;
        }

        // 아직 몸이 발판과 겹쳐 있을 때 충돌을 켜면 물리 엔진이 밀어내면서 위로 튕겨 올라감
        // -> 완전히 빠져나간 뒤에 복구
        if (IsOverlapping(_droppingPlatform))
        {
            return;
        }

        EndDropThrough();
    }

    private void EndDropThrough()
    {
        if (_droppingPlatform == null)
        {
            return;
        }

        SetPlatformCollision(_droppingPlatform, true);
        _droppingPlatform = null;
    }

    private void SetPlatformCollision(OneWayPlatform platform, bool enable)
    {
        foreach (Collider platformCol in platform.Colliders)
        {
            if (platformCol == null)
            {
                continue;
            }

            foreach (Collider bodyCol in _bodyColliders)
            {
                Physics.IgnoreCollision(bodyCol, platformCol, !enable);
            }
        }
    }

    private bool IsOverlapping(OneWayPlatform platform)
    {
        foreach (Collider platformCol in platform.Colliders)
        {
            if (platformCol == null)
            {
                continue;
            }

            foreach (Collider bodyCol in _bodyColliders)
            {
                if (bodyCol.bounds.Intersects(platformCol.bounds))
                {
                    return true;
                }
            }
        }
        return false;
    }

    // 트리거(사다리 감지 등)는 원래 발판을 막지 않으므로 제외하고 실제 몸 콜라이더만
    private Collider[] CollectBodyColliders()
    {
        List<Collider> result = new List<Collider>();

        foreach (Collider col in GetComponentsInChildren<Collider>(true))
        {
            if (!col.isTrigger)
            {
                result.Add(col);
            }
        }

        if (result.Count == 0)
        {
            Debug.LogError("[Player:CollectBodyColliders] 플레이어 몸 콜라이더가 없습니다. 아랫점프가 동작하지 않습니다.");
        }

        return result.ToArray();
    }


    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out Ladder ladder))
        {
            _currentLadder = ladder;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // 사다리 두 개가 붙어 있을 때 먼저 나간 쪽이 지금 사다리를 지우지 않게
        if (other.TryGetComponent(out Ladder ladder) && ladder == _currentLadder)
        {
            _currentLadder = null;
        }
    }
}
