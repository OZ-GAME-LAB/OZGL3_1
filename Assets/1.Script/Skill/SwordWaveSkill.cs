using UnityEngine;

// 검기 (물리, 계수 1.8, 쿨 4초): 짧은 관통 검기 발사
public class SwordWaveSkill : PlayerSkillBase
{
    [Header("Skill")]
    [SerializeField] private SkillProjectile _projectilePrefab; // _pierce = true, 짧은 사거리로 세팅
    [SerializeField] private Vector3 _spawnOffset = new Vector3(0.6f, 1f, 0f); // x는 바라보는 방향 기준
    [SerializeField] private bool _horizontalOnly = false; // true: 좌우로만 발사 / false: 커서 각도 그대로

    public override void Execute(PlayerController player, Vector3 aimPoint, float damage)
    {
        Vector3 origin = player.transform.position;
        Vector3 dir = _horizontalOnly
            ? new Vector3(player.FacingX, 0f, 0f)
            : SkillUtil.GetAimDirection(origin + Vector3.up * _spawnOffset.y, aimPoint, player.FacingX);

        // offset.x에 FacingX를 곱함 -> 왼쪽을 볼 때는 몸 왼쪽 앞에서 생성
        Vector3 spawnPos = origin + new Vector3(_spawnOffset.x * player.FacingX, _spawnOffset.y, 0f);
        SkillProjectile projectile = SkillSpawner.Spawn(_projectilePrefab, spawnPos, Quaternion.LookRotation(dir));
        if (projectile != null)
        {
            projectile.Launch(dir, damage);
        }
    }
}
