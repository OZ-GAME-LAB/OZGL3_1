using System;
using System.Collections.Generic;
using UnityEngine;


[Serializable]
public class PlayerSkillSaveData
{
    public List<string> owned = new List<string>();               // 보유 스킬 id
    public string[] equipped = new string[PlayerSkill.SlotCount]; // Q/E/R 장착 id (빈 칸은 null)
}
