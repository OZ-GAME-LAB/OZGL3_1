using UnityEngine;

// 연쇄 폭발 (마법, 계수 3.45, 쿨 13초): 폭발이 순서대로 이어짐
public class ChainBlastSkill : PlayerSkillBase
{
    [Header("Skill")]
    [SerializeField] private SkillExplosion _explosionPrefab;
    [SerializeField, Min(1)] private int _count = 5;
    [SerializeField, Min(0f)] private float _startDistance = 1.5f; // 첫 폭발 위치 (플레이어 앞)
    [SerializeField, Min(0f)] private float _spacing = 1.5f;       // 폭발 간격 (거리)
    [SerializeField, Min(0f)] private float _interval = 0.12f;     // 폭발 간격 (시간)

    [Header("Ground")]
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private float _rayHeight = 2f;
    [SerializeField] private float _rayDistance = 5f;

    public override void Execute(PlayerController player, Vector3 aimPoint, float damage)
    {
        Vector3 origin = player.transform.position;
        float dirX = player.FacingX; // PlayerState_Skill이 이미 커서 쪽으로 돌아봄
        float damagePerBlast = SkillUtil.SplitDamage(damage, _count);
        for (int i = 0; i < _count; i++)
        {
            Vector3 point = origin + Vector3.right * dirX * (_startDistance + _spacing * i);

            // 단차(계단·옥상)가 있어도 폭발이 공중에 뜨지 않고 바닥에 붙도록
            // 못 찾으면 point(플레이어 발 높이)가 그대로 들어감
            SkillUtil.TrySnapToGround(point, _groundLayer, _rayHeight, _rayDistance, out Vector3 spawnPos);
            SkillExplosion explosion = SkillSpawner.Spawn(_explosionPrefab, spawnPos, Quaternion.identity);
            if (explosion != null)
            {
                // 지연 = 간격 x 순번 → 0초, 0.12초, 0.24초 ... 순서대로 터져서 "연쇄" 로 보임
                explosion.Init(damagePerBlast, _interval * i);
            }
        }
    }
}
