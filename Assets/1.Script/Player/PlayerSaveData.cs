using System;

// 플레이어 파트 저장 데이터 한 묶음.
// 저장하지 않는 것 : HP, 버프, 쿨다운
[Serializable]
public class PlayerSaveData
{
    public int[] statLevels; // StatType 순서 (힘·체력·민첩·마력)
    public PlayerSkillSaveData skills = new PlayerSkillSaveData();

    // 무기는 한 번 고르면 끝까지 가는 선택이라 이어하기 때 반드시 복원해야 함
    public WeaponType weapon = WeaponType.None;
}
