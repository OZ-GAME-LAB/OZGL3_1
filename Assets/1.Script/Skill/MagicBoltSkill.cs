using UnityEngine;

// 마력탄 (마법, 계수 1.65, 쿨 5초): 커서 방향으로 탄 발사 → 맞은 지점 범위 폭발
// 기획서는 "사거리 안 목표 지점에 즉시 범위 피해" → 현재(날아가는 탄)와 다름. 상의 후 수정 예정
public class MagicBoltSkill : PlayerSkillBase
{
    [Header("Skill")]
    [SerializeField] private SkillProjectile _projectilePrefab; // _explosionRadius > 0, _explodeAtEnd = true로 세팅
    [SerializeField] private float _spawnHeight = 1.2f;
    [SerializeField] private float _spawnForward = 0.5f;
    [SerializeField] private bool _horizontalOnly = false; // true: 좌우로만 발사 / false: 커서 각도 그대로

    public override void Execute(PlayerController player, Vector3 aimPoint, float damage)
    {
        Vector3 origin = player.transform.position + Vector3.up * _spawnHeight;

        Vector3 dir = _horizontalOnly
            ? new Vector3(player.FacingX, 0f, 0f)
            : SkillUtil.GetAimDirection(origin, aimPoint, player.FacingX);

        // 몸 안에서 생성되면 겹쳐서 즉시 터지므로 조금 앞에서 생성
        Vector3 spawnPos = origin + dir * _spawnForward;

        SkillProjectile projectile = SkillSpawner.Spawn(_projectilePrefab, spawnPos, Quaternion.LookRotation(dir));
        if (projectile != null)
        {
            projectile.Launch(dir, damage);
        }
    }
}
