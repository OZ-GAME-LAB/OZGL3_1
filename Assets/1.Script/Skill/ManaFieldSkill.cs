using UnityEngine;


// 마력장 (마법, 계수 4, 쿨 9초): 커서 지점(사거리 안) 바닥에 지속 피해 장판 설치
public class ManaFieldSkill : PlayerSkillBase
{
    [Header("Skill")]
    [SerializeField] private SkillField _fieldPrefab;
    [SerializeField, Min(0f)] private float _maxRange = 7f; // 플레이어로부터 좌우 최대 설치 거리

    [Header("Ground")]
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private float _rayHeight = 2f;    // 커서 지점보다 위에서 아래로 쏨
    [SerializeField] private float _rayDistance = 10f;

    public override void Execute(PlayerController player, Vector3 aimPoint, float damage)
    {
        Vector3 origin = player.transform.position;

        // 화면 끝을 찍어도 사거리 끝에 설치
        float offsetX = Mathf.Clamp(aimPoint.x - origin.x, -_maxRange, _maxRange);

        // z는 플레이어 z 사용. 장판이 전투 평면에서 벗어나지 않게
        Vector3 target = new Vector3(origin.x + offsetX, aimPoint.y, origin.z);

        // 공중을 찍으면 그 아래 바닥에 깔림. 바닥이 없으면 플레이어 발 높이에 깔림
        if (!SkillUtil.TrySnapToGround(target, _groundLayer, _rayHeight, _rayDistance, out Vector3 spawnPos))
        {
            spawnPos = new Vector3(target.x, origin.y, origin.z);
        }
        SkillField field = SkillSpawner.Spawn(_fieldPrefab, spawnPos, Quaternion.identity);
        if (field != null)
        {
            field.Init(damage);
        }
    }
}
