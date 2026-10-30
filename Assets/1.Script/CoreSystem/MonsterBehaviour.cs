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
    [Header("지형 검사 (Ground 레이어)")]
    [SerializeField, Min(0.01f)] private float checkTravelDistance = 0.1f;
    [SerializeField, Min(0.01f)] private float groundProbeHeight = 0.2f;
    [SerializeField, Min(0.01f)] private float groundProbeDepth = 0.5f;
    [SerializeField, Min(0.01f)] private float terrainPadding = 0.05f;
    private Collider bodyCollider;
    private int groundMask;
    private float moveDirection;
    private float checkedDirection;
    private float nextTerrainCheckTime;
    private bool pathIsSafe;
    private const float BaseAttackRange = 1f;
    private const float AttackRangeVariation = 0.1f;

    protected Monster Owner { get; private set; }
    public MonsterState State { get; private set; } = MonsterState.Idle;
    public Transform Target { get; private set; }
    public float AttackRange { get; private set; }

    protected virtual void Awake()
    {
        Owner = GetComponent<Monster>();
        bodyCollider = GetComponent<Collider>();
        groundMask = LayerMask.GetMask("Ground");
        if (groundMask == 0) Debug.LogError("지형 검사에 필요한 Ground 레이어가 없습니다.", this);
    }

    protected virtual void OnEnable()
    {
        // Monster.OnEnable의 실행 순서에 의존하지 않고 재사용 상태를 초기화합니다.
        State = MonsterState.Idle;
        moveDirection = Mathf.Abs(transform.forward.x) > 0.01f ? Mathf.Sign(transform.forward.x) : 1f;
        checkedDirection = 0f;
        nextTerrainCheckTime = 0f;
        pathIsSafe = false;
        Target = Owner.Target;
        AttackRange = Random.Range(BaseAttackRange - AttackRangeVariation, BaseAttackRange + AttackRangeVariation);
        Owner.Died += OnMonsterDied;
    }

    protected virtual void OnDisable()
    {
        Owner.Died -= OnMonsterDied;
        Target = null;
    }

    protected virtual void Update()
    {
        if (Owner.IsDead)
        {
            ChangeState(MonsterState.Dead);
            return;
        }

        Target = Owner.Target;
        if (Target == null || !Target.gameObject.activeInHierarchy)
        {
            ChangeState(MonsterState.Idle);
            UpdateIdle();
            return;
        }

        Vector2 offset = Target.position - transform.position;
        if (offset.sqrMagnitude > detectionRange * detectionRange)
            ChangeState(MonsterState.Idle);
        else if (Mathf.Abs(offset.x) <= AttackRange && Mathf.Abs(offset.y) <= AttackRange)
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
        if (State == next) return;

        State = next;
        if (State == MonsterState.Dead) HandleDead();
    }

    private void OnMonsterDied(Monster monster)
    {
        ChangeState(MonsterState.Dead);
    }

    protected virtual void UpdateIdle()
    {
        if (Owner.MoveSpeed <= 0f) return;
        if (!CanMove(moveDirection))
        {
            moveDirection = -moveDirection;
            if (!CanMove(moveDirection)) return;
        }
        FaceDirection(moveDirection);
        transform.position += Vector3.right * moveDirection * Owner.MoveSpeed * Time.deltaTime;
    }
    protected virtual void UpdateChase()
    {
        float deltaX = Target.position.x - transform.position.x;
        // X축이 사거리 안이면 Y축이 맞을 때까지 현재 자리에서 기다립니다.
        if (Mathf.Abs(deltaX) <= AttackRange) return;

        moveDirection = Mathf.Sign(deltaX);
        FaceDirection(moveDirection);
        // 추격 중에는 위험 지점에서 방향을 바꾸지 않고 대기합니다.
        if (!CanMove(moveDirection)) return;

        Vector3 position = transform.position;
        float destinationX = Target.position.x - Mathf.Sign(deltaX) * AttackRange;
        position.x = Mathf.MoveTowards(position.x, destinationX, Owner.MoveSpeed * Time.deltaTime);
        transform.position = position;
    }
    protected virtual void UpdateAttack() { }
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

    private Bounds GetBodyBounds()
    {
        return bodyCollider != null ? bodyCollider.bounds :
            new Bounds(transform.position + Vector3.up * 0.5f, new Vector3(0.5f, 1f, 0.5f));
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

        Owner.Attack(player);
    }
}
