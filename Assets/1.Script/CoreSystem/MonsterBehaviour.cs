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
    [SerializeField, Min(1.1f)] private float detectionRange = 10f;
    private const float BaseAttackRange = 1f;
    private const float AttackRangeVariation = 0.1f;

    protected Monster Owner { get; private set; }
    public MonsterState State { get; private set; } = MonsterState.Idle;
    public Transform Target { get; private set; }
    public float AttackRange { get; private set; }

    protected virtual void Awake()
    {
        Owner = GetComponent<Monster>();
    }

    protected virtual void OnEnable()
    {
        // Monster.OnEnable의 실행 순서에 의존하지 않고 재사용 상태를 초기화합니다.
        State = MonsterState.Idle;
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
        transform.position += transform.forward * Owner.MoveSpeed * Time.deltaTime;
    }
    protected virtual void UpdateChase()
    {
        float deltaX = Target.position.x - transform.position.x;
        // X축이 사거리 안이면 Y축이 맞을 때까지 현재 자리에서 기다립니다.
        if (Mathf.Abs(deltaX) <= AttackRange) return;

        Vector3 position = transform.position;
        float destinationX = Target.position.x - Mathf.Sign(deltaX) * AttackRange;
        position.x = Mathf.MoveTowards(position.x, destinationX, Owner.MoveSpeed * Time.deltaTime);
        transform.position = position;
    }
    protected virtual void UpdateAttack() { }
    protected virtual void HandleDead() { }

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
