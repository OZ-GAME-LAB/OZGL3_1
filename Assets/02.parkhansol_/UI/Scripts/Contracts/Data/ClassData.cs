using UnityEngine;

namespace OZ.UI.Contracts
{
    /// <summary>계열 선택 화면에 보일 계열 정보 + Q/E/R 스킬 구성.</summary>
    [CreateAssetMenu(menuName = "OZ/UI/Class Data", fileName = "Class_")]
    public class ClassData : ScriptableObject
    {
        public PlayerClass playerClass;
        public string displayName;
        [TextArea(2, 5)] public string description;
        public Sprite icon;

        [Tooltip("index 0=Q, 1=E, 2=R")]
        public SkillData[] skills = new SkillData[3];

        public SkillData GetSkill(SkillSlot slot)
        {
            int i = (int)slot;
            return skills != null && i < skills.Length ? skills[i] : null;
        }
    }
}
