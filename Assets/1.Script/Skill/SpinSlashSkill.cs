using UnityEngine;


// 회전 베기 (물리, 계수 2.1, 쿨 7초): 플레이어 주변 근접 범위 즉시 피해
public class SpinSlashSkill : PlayerSkillBase
{
    [Header("Skill")]
    [SerializeField] private Vector3 _hitBoxSize = new Vector3(3.5f, 2f, 2f); // 플레이어 중심 좌우 범위
    [SerializeField] private float _hitBoxHeight = 1f;
    [SerializeField] private LayerMask _enemyLayer;
    [SerializeField] private SkillAutoRelease _slashVfx; // 회전 궤적 연출 (선택)
    public override void Execute(PlayerController player, Vector3 aimPoint, float damage)
    {
        Vector3 center = player.transform.position + Vector3.up * _hitBoxHeight;
        SkillUtil.DamageBox(center, _hitBoxSize, _enemyLayer, damage);
        if (_slashVfx != null)
        {
            SkillSpawner.Spawn(_slashVfx, center, Quaternion.identity);
        }
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position + Vector3.up * _hitBoxHeight, _hitBoxSize);
    }
}
