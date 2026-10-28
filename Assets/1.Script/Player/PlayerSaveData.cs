using System;

// 플레이어 파트 저장 데이터 한 묶음.
// 저장하지 않는 것 : HP, 버프, 쿨다운
[Serializable]
public class PlayerSaveData
{
    public int[] statLevels; // StatType 순서 (힘·체력·민첩·마력)
    public PlayerSkillSaveData skills = new PlayerSkillSaveData(); 
}
