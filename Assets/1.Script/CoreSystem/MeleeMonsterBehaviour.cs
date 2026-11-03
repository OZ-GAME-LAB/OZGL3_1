using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeleeMonsterBehaviour : MonsterBehaviour
{
    [SerializeField] private Vector3 attackBoxSize = new Vector3(1.2f, 2f, 1.6f);
    [SerializeField, Min(0f)] private float attackForwardOffset = 0.8f;
    private readonly HashSet<PlayerHealth> hitPlayers = new HashSet<PlayerHealth>();

    protected override float AttackDelay => 0.4f;

    protected override IEnumerator ExecuteAttack()
    {
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
        yield break;
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
