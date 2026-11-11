using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 돌진의 지형 검사·이동·접촉 피해·중단 처리는 기존 구현을 공유합니다.
public class BossMonsterBehaviour : ChargeMonsterBehaviour
{
    public enum BossAttackPattern { Melee = 0, Ranged = 1, Charge = 2 }

    [Header("보스 공격 선택")]
    [SerializeField, Min(0.1f)] private float rangedAttackRange = 6f;
    [SerializeField, Min(0.01f)] private float meleeAttackDuration = 1.1f;
    [SerializeField, Min(0f)] private float rangedAttackDelay = 0.8f;
    [SerializeField, Min(0f)] private float chargeAttackDelay = 1.2f;
    [Header("일반 공격")]
    [SerializeField] private Vector3 attackBoxSize = new Vector3(1.2f, 2f, 1.6f);
    [SerializeField, Min(0f)] private float attackForwardOffset = 0.8f;
    [Header("원거리 공격")]
    [SerializeField] private MonsterProjectile projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField, Min(0f)] private float spawnOffset = 0.6f;

    private BossMonster boss;
    private bool meleeHitProcessed;
    private readonly HashSet<PlayerHealth> hitPlayers = new HashSet<PlayerHealth>();
    public BossAttackPattern CurrentPattern { get; private set; }
    // 보스 데이터에는 SkillDamage가 없으므로 공통 공격력으로 세 패턴을 실행합니다.
    protected override float SkillDamage => Owner.MeleeDamage;
    protected override bool UseChargePreparation => CurrentPattern == BossAttackPattern.Charge;
    protected override float AttackDelay => CurrentPattern == BossAttackPattern.Melee ? meleeAttackDuration :
        CurrentPattern == BossAttackPattern.Ranged ? rangedAttackDelay : chargeAttackDelay;

    protected override void Awake()
    {
        base.Awake();
        boss = GetComponent<BossMonster>();
        if (boss == null)
        {
            Debug.LogError("BossMonsterBehaviour에는 BossMonster가 필요합니다.", this);
            enabled = false;
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
#if UNITY_EDITOR
        DebugManualControl = false;
#endif
        CurrentPattern = BossAttackPattern.Melee;
        meleeHitProcessed = false;
        if (AnimController != null) AnimController.AttackHit += ApplyMeleeHit;
    }

    protected override void OnDisable()
    {
        if (AnimController != null) AnimController.AttackHit -= ApplyMeleeHit;
        base.OnDisable();
    }

    protected override MonsterState EvaluateState(Vector2 offset)
    {
        if (base.EvaluateState(offset) == MonsterState.Idle) return MonsterState.Idle;
        if (Mathf.Abs(offset.y) <= AttackRangeY &&
            Mathf.Abs(offset.x) <= Mathf.Max(AttackRangeX, rangedAttackRange)) return MonsterState.Attack;
        return MonsterState.Chase;
    }

    protected override void UpdateAttack()
    {
        if (IsAttacking || boss == null || Target == null) return;
        bool close = Mathf.Abs(TargetCenter.x - BodyCenter.x) <= AttackRangeX;
        bool lowHealth = boss.MaxHP > 0f && boss.CurrentHP / boss.MaxHP <= boss.PatternTriggerHpRatio;
        CurrentPattern = !close ? BossAttackPattern.Ranged :
            lowHealth ? BossAttackPattern.Charge : BossAttackPattern.Melee;
        StartPattern();
    }

    private void StartPattern()
    {
        meleeHitProcessed = false;
        if (AnimController != null) AnimController.SetAttackType((int)CurrentPattern);

        // 선택한 패턴과 공격력은 이번 공격 동안 유지합니다.
        base.UpdateAttack();
        if (IsAttacking && CurrentPattern == BossAttackPattern.Melee)
            PlayAnimation(EnemyAnimState.Attack);
    }

#if UNITY_EDITOR
    // 디버그 제어는 에디터에서만 제공하며 빌드에는 포함하지 않습니다.
    public bool DebugManualControl { get; set; }

    protected override void Update()
    {
        if (DebugManualControl) return;
        base.Update();
    }

    public void DebugRunPattern(BossAttackPattern pattern)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || boss == null || boss.IsDead ||
            Target == null || !Target.gameObject.activeInHierarchy || IsAttacking) return;
        DebugManualControl = true;
        ChangeState(MonsterState.Attack);
        CurrentPattern = pattern;
        StartPattern();
    }

    public void DebugStopPattern()
    {
        if (!Application.isPlaying || !isActiveAndEnabled) return;
        StopAttack();
        StopMove();
        ChangeState(MonsterState.Idle);
    }
#endif

    protected override IEnumerator ExecuteAttack()
    {
        switch (CurrentPattern)
        {
            case BossAttackPattern.Melee:
                // 실제 타격은 클립의 OnAttackHit 이벤트에서 처리합니다.
                yield break;
            case BossAttackPattern.Ranged:
                FireProjectile();
                yield break;
            case BossAttackPattern.Charge:
                yield return base.ExecuteAttack();
                break;
        }
    }

    public void ApplyMeleeHit()
    {
        if (!isActiveAndEnabled || Owner.IsDead || !IsAttacking ||
            CurrentPattern != BossAttackPattern.Melee || meleeHitProcessed) return;
        meleeHitProcessed = true;
        hitPlayers.Clear();
        Vector3 center = BodyCenter + transform.forward * attackForwardOffset;
        foreach (Collider hit in Physics.OverlapBox(center, attackBoxSize * 0.5f, transform.rotation,
            LayerMask.GetMask("Player"), QueryTriggerInteraction.Collide))
        {
            PlayerHealth player = hit.GetComponentInParent<PlayerHealth>();
            if (player == null || !player.isActiveAndEnabled || player.IsDead || !hitPlayers.Add(player)) continue;
            player.TakeDamage(SkillDamage);
        }
    }

    private void FireProjectile()
    {
        if (projectilePrefab == null)
        {
            Debug.LogError("BossMonsterBehaviour에 원거리 투사체 프리팹을 연결하세요.", this);
            return;
        }
        if (Target == null || !Target.gameObject.activeInHierarchy) return;
        PlayAnimation(EnemyAnimState.Attack);
        Vector3 aim = TargetCenter - BodyCenter;
        Vector3 initialDirection = aim.sqrMagnitude > 0.0001f ? aim.normalized : transform.forward;
        Vector3 position = firePoint != null ? firePoint.position : BodyCenter + initialDirection * spawnOffset;
        Vector3 offset = TargetCenter - position;
        Vector3 direction = offset.sqrMagnitude > 0.0001f ? offset.normalized : initialDirection;
        Vector3 up = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
        MonsterProjectile projectile = Instantiate(projectilePrefab, position, Quaternion.LookRotation(direction, up));
        projectile.Launch(direction, SkillDamage);
        projectile.gameObject.SetActive(true);
    }

    protected override void OnAttackInterrupted()
    {
        meleeHitProcessed = true;
        hitPlayers.Clear();
        base.OnAttackInterrupted();
    }
}
