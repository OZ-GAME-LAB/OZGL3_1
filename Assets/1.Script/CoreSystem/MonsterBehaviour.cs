using System.Collections;
using UnityEngine;

public enum MonsterState
{
    Idle,
    Chase,
    Attack,
    Dead
}

[RequireComponent(typeof(Monster))]
[DisallowMultipleComponent]
public class MonsterBehaviour : MonoBehaviour
{
    [SerializeField, Min(1.1f)] private float detectionRange = 7f;
    [SerializeField] private EnemyAnimController animController;
    [Header("지형 검사 (Ground 레이어)")]
    [SerializeField, Min(0.01f)] private float checkTravelDistance = 0.1f;
    [SerializeField, Min(0.01f)] private float groundProbeHeight = 0.2f;
    [SerializeField, Min(0.01f)] private float groundProbeDepth = 0.5f;
    [SerializeField, Min(0.01f)] private float terrainPadding = 0.05f;
    private Collider bodyCollider;
    private Collider targetCollider;
    private int groundMask;
    private float moveDirection;
    private float checkedDirection;
    private float nextTerrainCheckTime;
    private bool pathIsSafe;
    private Coroutine attackRoutine;
    private const float AttackRangeVariation = 0.1f;

    protected Monster Owner { get; private set; }
    protected EnemyAnimController AnimController => animController;
    public MonsterState State { get; private set; } = MonsterState.Idle;
    public Transform Target { get; private set; }
    public float AttackRangeX { get; private set; }
    public float AttackRangeY { get; private set; }
    public bool IsAttacking { get; private set; }
    protected virtual float AttackDelay => 0.5f;
    protected float SkillDamage => Owner.SkillDamage;
    protected Vector3 BodyCenter => bodyCollider != null ? bodyCollider.bounds.center : transform.position;
    protected Vector3 TargetCenter => targetCollider != null ? targetCollider.bounds.center : Target.position;
    protected int GroundMask => groundMask;
    protected float GroundProbeHeight => groundProbeHeight;
    protected float GroundProbeDepth => groundProbeDepth;
    protected float TerrainPadding => terrainPadding;

    protected virtual void Awake()
    {
        Owner = GetComponent<Monster>();
        if (animController == null) animController = GetComponentInChildren<EnemyAnimController>(true);
        bodyCollider = GetComponent<Collider>();
        groundMask = LayerMask.GetMask("Ground");
        if (groundMask == 0) Debug.LogError("지형 검사에 필요한 Ground 레이어가 없습니다.", this);
    }

    protected virtual void OnEnable()
    {
        // Monster.OnEnable의 실행 순서에 의존하지 않고 재사용 상태를 초기화합니다.
        State = MonsterState.Idle;
        IsAttacking = false;
        attackRoutine = null;
        moveDirection = Mathf.Abs(transform.forward.x) > 0.01f ? Mathf.Sign(transform.forward.x) : 1f;
        checkedDirection = 0f;
        nextTerrainCheckTime = 0f;
        pathIsSafe = false;
        Target = Owner.Target;
        targetCollider = Target != null ? Target.GetComponent<Collider>() : null;
        AttackRangeX = Mathf.Max(0.01f, Owner.AttackRangeX + Random.Range(-AttackRangeVariation, AttackRangeVariation));
        AttackRangeY = Owner.AttackRangeY;
        Owner.Died += OnMonsterDied;
        Owner.Damaged += OnMonsterDamaged;
        StopMove();
    }

    protected virtual void OnDisable()
    {
        StopAttack();
        StopMove();
        Owner.Died -= OnMonsterDied;
        Owner.Damaged -= OnMonsterDamaged;
        Target = null;
        targetCollider = null;
    }

    protected virtual void Update()
    {
        if (Owner.IsDead)
        {
            ChangeState(MonsterState.Dead);
            return;
        }
        // 선딜레이와 패턴 실행 중에는 거리 판단 및 일반 이동을 중단합니다.
        if (IsAttacking) return;

        if (Target != Owner.Target)
        {
            Target = Owner.Target;
            targetCollider = Target != null ? Target.GetComponent<Collider>() : null;
        }
        if (Target == null || !Target.gameObject.activeInHierarchy)
        {
            ChangeState(MonsterState.Idle);
            UpdateIdle();
            return;
        }

        Vector2 offset = TargetCenter - BodyCenter;
        if (offset.sqrMagnitude > detectionRange * detectionRange)
            ChangeState(MonsterState.Idle);
        else if (Mathf.Abs(offset.x) <= AttackRangeX && Mathf.Abs(offset.y) <= AttackRangeY)
            ChangeState(MonsterState.Attack);
        else
            ChangeState(MonsterState.Chase);

        switch (State)
        {
            case MonsterState.Idle: UpdateIdle(); break;
            case MonsterState.Chase: UpdateChase(); break;
            case MonsterState.Attack: UpdateAttack(); break;
        }
    }

    public void ChangeState(MonsterState next)
    {
        // 사망 판정은 Monster만 담당하며, 행동 상태가 이를 따릅니다.
        if (Owner.IsDead) next = MonsterState.Dead;
        else if (next == MonsterState.Dead) return;
        if (IsAttacking && next != MonsterState.Dead) return;
        if (State == next) return;

        State = next;
        if (State == MonsterState.Dead)
        {
            StopAttack();
            StopMove();
            HandleDead();
        }
    }

    private void OnMonsterDied(Monster monster)
    {
        ChangeState(MonsterState.Dead);
    }

    private void OnMonsterDamaged(Monster monster)
    {
        if (!isActiveAndEnabled || Owner.IsDead) return;
        HandleDamaged();
    }

    protected virtual void HandleDamaged()
    {
        PlayAnimation(EnemyAnimState.Damaged);
    }

    protected void PlayAnimation(EnemyAnimState state)
    {
        if (animController != null) animController.ChangeAnimState(state);
    }

    protected virtual void UpdateIdle()
    {
        if (Owner.MoveSpeed <= 0f)
        {
            StopMove();
            return;
        }
        if (!CanMove(moveDirection))
        {
            moveDirection = -moveDirection;
            if (!CanMove(moveDirection))
            {
                StopMove();
                return;
            }
        }
        FaceDirection(moveDirection);
        Move(Vector3.right * moveDirection * Owner.MoveSpeed * Time.deltaTime);
    }
    protected virtual void UpdateChase()
    {
        float deltaX = TargetCenter.x - BodyCenter.x;
        // X축이 사거리 안이면 Y축이 맞을 때까지 현재 자리에서 기다립니다.
        if (Mathf.Abs(deltaX) <= AttackRangeX)
        {
            StopMove();
            return;
        }

        moveDirection = Mathf.Sign(deltaX);
        FaceDirection(moveDirection);
        // 추격 중에는 위험 지점에서 방향을 바꾸지 않고 대기합니다.
        if (!CanMove(moveDirection))
        {
            StopMove();
            return;
        }

        Vector3 position = transform.position;
        float destinationX = position.x + deltaX - Mathf.Sign(deltaX) * AttackRangeX;
        position.x = Mathf.MoveTowards(position.x, destinationX, Owner.MoveSpeed * Time.deltaTime);
        Move(position - transform.position);
    }

    // displacement는 이번 프레임의 이동량입니다. 속도나 deltaTime을 다시 곱하지 않습니다.
    protected void Move(Vector3 displacement)
    {
        if (!isActiveAndEnabled || Owner == null || Owner.IsDead) displacement = Vector3.zero;
        Vector3 previousPosition = transform.position;
        transform.position += displacement;
        bool moved = (transform.position - previousPosition).sqrMagnitude > Mathf.Epsilon;
        PlayAnimation(moved ? EnemyAnimState.Move : EnemyAnimState.Idle);
    }

    protected void StopMove()
    {
        Move(Vector3.zero);
    }

    protected virtual void UpdateAttack()
    {
        if (IsAttacking) return;
        StopMove();
        // 선딜레이 시작 시 공격 방향을 정하고 패턴이 끝날 때까지 유지합니다.
        if (Target != null)
        {
            float deltaX = TargetCenter.x - BodyCenter.x;
            if (Mathf.Abs(deltaX) > 0.01f) FaceDirection(Mathf.Sign(deltaX));
        }
        IsAttacking = true;
        attackRoutine = StartCoroutine(RunAttack());
    }

    private IEnumerator RunAttack()
    {
        try
        {
            yield return new WaitForSeconds(Mathf.Max(0f, AttackDelay));
            if (Owner.IsDead || !isActiveAndEnabled) yield break;
            yield return ExecuteAttack();
        }
        finally
        {
            StopMove();
            IsAttacking = false;
            attackRoutine = null;
        }
    }

    // 자식은 선딜레이 이후의 공격 내용만 구현합니다.
    protected virtual IEnumerator ExecuteAttack()
    {
        yield break;
    }

    private void StopAttack()
    {
        if (!IsAttacking) return;
        if (attackRoutine != null) StopCoroutine(attackRoutine);
        attackRoutine = null;
        IsAttacking = false;
        OnAttackInterrupted();
    }

    protected virtual void OnAttackInterrupted() { }
    protected virtual void HandleDead() { }

    private void FaceDirection(float direction)
    {
        transform.rotation = Quaternion.LookRotation(Vector3.right * direction, Vector3.up);
    }

    private bool CanMove(float direction)
    {
        if (direction == checkedDirection && Time.time < nextTerrainCheckTime) return pathIsSafe;

        // 검사 사이 이동 거리를 기준으로, 속도가 빠를수록 더 자주 검사합니다.
        float interval = Mathf.Clamp(checkTravelDistance / Mathf.Max(Owner.MoveSpeed, 0.01f), 0.02f, 0.2f);
        float lookAhead = terrainPadding + Owner.MoveSpeed * (interval + Time.deltaTime);
        checkedDirection = direction;
        nextTerrainCheckTime = Time.time + interval;
        pathIsSafe = !CheckWall(direction, lookAhead) && CheckGroundAhead(direction, lookAhead);
        return pathIsSafe;
    }

    protected bool CheckWall(float direction, float lookAhead)
    {
        Bounds bounds = GetBodyBounds();
        return Physics.Raycast(bounds.center, Vector3.right * direction,
            bounds.extents.x + lookAhead, groundMask, QueryTriggerInteraction.Ignore);
    }

    protected bool CheckGroundAhead(float direction, float lookAhead)
    {
        Bounds bounds = GetBodyBounds();
        Vector3 origin = new Vector3(
            bounds.center.x + direction * (bounds.extents.x + lookAhead),
            bounds.min.y + groundProbeHeight, bounds.center.z);
        return Physics.Raycast(origin, Vector3.down, groundProbeHeight + groundProbeDepth,
            groundMask, QueryTriggerInteraction.Ignore);
    }

    protected Bounds GetBodyBounds()
    {
        return bodyCollider != null ? bodyCollider.bounds :
            new Bounds(transform.position + Vector3.up * 0.5f, new Vector3(0.5f, 1f, 0.5f));
    }

    protected virtual void OnDrawGizmosSelected()
    {
        float rangeX = Application.isPlaying ? AttackRangeX : 1f;
        float rangeY = Application.isPlaying ? AttackRangeY : 1f;
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = Color.yellow;
        // 상태 판정과 같은 월드 X/Y 범위입니다. Z축은 판정하지 않습니다.
        Collider ownCollider = bodyCollider != null ? bodyCollider : GetComponent<Collider>();
        Vector3 center = ownCollider != null ? ownCollider.bounds.center : transform.position;
        Gizmos.DrawWireCube(center, new Vector3(rangeX * 2f, rangeY * 2f, 0f));
        if (Application.isPlaying && Target != null)
            Gizmos.DrawWireSphere(TargetCenter, 0.08f);
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

    private void OnCollisionStay(Collision collision)
    {
        TryContactAttack(collision.collider);
    }

    private void OnTriggerStay(Collider other)
    {
        TryContactAttack(other);
    }

    private void TryContactAttack(Collider other)
    {
        if (!isActiveAndEnabled || Owner.IsDead) return;
        PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
        if (player == null || !player.isActiveAndEnabled || player.IsDead) return;

        HandleContactAttack(player);
    }

    protected virtual void HandleContactAttack(PlayerHealth player)
    {
        Owner.Attack(player);
    }
}
