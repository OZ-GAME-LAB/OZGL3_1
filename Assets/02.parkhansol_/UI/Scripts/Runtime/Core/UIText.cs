using OZ.UI.Contracts;

namespace OZ.UI
{
    /// <summary>UI 표시 문자열 모음 (나중에 로컬라이즈 시 여기만 교체)</summary>
    public static class UIText
    {
        public static string Key(SkillSlot slot) => slot.ToString();
        public static string ItemKey(int slotIndex) => (slotIndex + 1).ToString();

        public static string ItemType(ItemEffectType t)
        {
            switch (t)
            {
                case ItemEffectType.Heal: return "회복";
                case ItemEffectType.AttackBuff: return "공격 강화";
                case ItemEffectType.DefenseBuff: return "방어 강화";
                case ItemEffectType.SpeedBuff: return "신속 강화";
                case ItemEffectType.KeyItem: return "중요 물품";
                default: return "";
            }
        }

        public static string ItemEffect(ItemData item)
        {
            if (item == null) return "";
            switch (item.effectType)
            {
                case ItemEffectType.Heal: return $"최대 체력의 {item.effectPercent:0}% 회복";
                case ItemEffectType.AttackBuff: return $"{item.buffDuration:0}초 동안 공격 피해 {item.effectPercent:0}% 증가";
                case ItemEffectType.DefenseBuff: return $"{item.buffDuration:0}초 동안 받는 피해 {item.effectPercent:0}% 감소";
                case ItemEffectType.SpeedBuff: return $"{item.buffDuration:0}초 동안 이동 속도 {item.effectPercent:0}% 증가";
                default: return "";
            }
        }

        public static string ClassName(PlayerClass c) => c == PlayerClass.Sword ? "검술" : "마법";
    }
}
