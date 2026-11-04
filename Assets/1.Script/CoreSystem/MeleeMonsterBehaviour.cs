using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeleeMonsterBehaviour : MonsterBehaviour
{
    [SerializeField] private Vector3 attackBoxSize = new Vector3(1.2f, 2f, 1.6f);
    [SerializeField, Min(0f)] private float attackForwardOffset = 0.8f;
    private readonly HashSet<PlayerHealth> hitPlayers = new HashSet<PlayerHealth>();
    [Tooltip("공격 시작부터 다음 행동 판단까지의 시간입니다. 실제 타격 시점은 Animation Event로 결정합니다.")]
    [SerializeField, Min(0.01f)] private float attackDuration = 1.1f;
    private bool hitProcessed;

    protected override float AttackDelay => attackDuration;

    protected override void OnEnable()
    {
        base.OnEnable();
        hitProcessed = false;
        if (AnimController != null) AnimController.AttackHit += ApplyAttackHit;
    }

    protected override void OnDisable()
    {
        if (AnimController != null) AnimController.AttackHit -= ApplyAttackHit;
        base.OnDisable();
    }

    protected override void UpdateAttack()
    {
        if (IsAttacking) return;
        hitProcessed = false;
        base.UpdateAttack();
        // 적중 여부와 무관하게 공격 시작 프레임에 한 번 재생을 요청합니다.
        if (IsAttacking) PlayAnimation(EnemyAnimState.Attack);
    }

    protected override IEnumerator ExecuteAttack()
    {
        // 타격은 시간 경과가 아니라 Animation Event에서만 요청합니다.
        yield break;
    }

    public void ApplyAttackHit()
    {
        if (!isActiveAndEnabled || Owner.IsDead || !IsAttacking || hitProcessed) return;
        hitProcessed = true;
        Vector3 center = BodyCenter + transform.forward * attackForwardOffset;
        Collider[] hits = Physics.OverlapBox(center, attackBoxSize * 0.5f,
            transform.rotation, LayerMask.GetMask("Player"), QueryTriggerInteraction.Collide);
        hitPlayers.Clear();
        foreach (Collider hit in hits)
        {
            PlayerHealth player = hit.GetComponentInParent<PlayerHealth>();
            if (player == null || !player.isActiveAndEnabled || player.IsDead || !hitPlayers.Add(player)) continue;
            float previousHP = player.HP;
            player.TakeDamage(SkillDamage);
            Debug.Log($"[근접 공격 적중] 몬스터: {Owner.DataId} ({Owner.MonsterName}), 대상: {player.name}, " +
                $"SkillDamage: {SkillDamage}, 실제 피해: {Mathf.Max(0f, previousHP - player.HP)}, " +
                $"플레이어 HP: {previousHP} → {player.HP}", this);
        }
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Matrix4x4 previous = Gizmos.matrix;
        Collider ownCollider = GetComponent<Collider>();
        Vector3 origin = ownCollider != null ? ownCollider.bounds.center : transform.position;
        Vector3 center = origin + transform.forward * attackForwardOffset;
        Gizmos.matrix = Matrix4x4.TRS(center, transform.rotation, Vector3.one);
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(Vector3.zero, attackBoxSize);
        Gizmos.matrix = previous;
    }
}
