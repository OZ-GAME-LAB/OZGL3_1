using UnityEngine;


// [신규·임시] 뼈대 확인용 스킬. 실제 스킬(검기 등)을 만들면 삭제
// 판정 없이 로그만 찍어서 입력 → 장착 → 쿨다운 → 피해 계산 흐름이 맞는지만 확인
public class TestLogSkill : PlayerSkillBase
{
    public override void Execute(PlayerController player, Vector3 aimPoint, float damage)
    {
        Debug.Log($"[Skill] {Data.DisplayName} 시전 → 피해 {damage}, 조준 {aimPoint}");
    }
}
