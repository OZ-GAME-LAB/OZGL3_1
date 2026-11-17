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
                case ItemEffectType.Equipment: return "장비";
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
                case ItemEffectType.Equipment: return item.description ?? "";
                default: return "";
            }
        }

        /// <summary>v1.5: 좁은 칸(상점 카드)용 짧은 효과. 지속시간 등 자세한 내용은 ItemEffect</summary>
        public static string ItemEffectShort(ItemData item)
        {
            if (item == null) return "";
            switch (item.effectType)
            {
                case ItemEffectType.Heal: return $"체력 {item.effectPercent:0}% 회복";
                case ItemEffectType.AttackBuff: return $"공격 +{item.effectPercent:0}%";
                case ItemEffectType.DefenseBuff: return $"피해 -{item.effectPercent:0}%";
                case ItemEffectType.SpeedBuff: return $"이동 +{item.effectPercent:0}%";
                case ItemEffectType.Equipment: return item.GrantsWeapon ? "평타 변경" : "영구 장비";
                default: return "";
            }
        }

        /// <summary>
        /// v1.5: 한글 줄바꿈을 띄어쓰기에서만 (TMP는 한글을 글자 단위로 끊어서 마지막 한두 글자만 다음 줄로 가기 쉬움).
        /// 단어마다 &lt;nobr&gt;로 감싼다. 이미 태그가 있는 문장은 그대로 둔다.
        /// </summary>
        public static string KeepWords(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('<') >= 0) return text;
            var lines = text.Split('\n');
            var sb = new System.Text.StringBuilder(text.Length * 2);
            for (int l = 0; l < lines.Length; l++)
            {
                if (l > 0) sb.Append('\n');
                var words = lines[l].Split(' ');
                for (int w = 0; w < words.Length; w++)
                {
                    if (w > 0) sb.Append(' ');
                    if (words[w].Length == 0) continue;
                    sb.Append("<nobr>").Append(words[w]).Append("</nobr>");
                }
            }
            return sb.ToString();
        }

        public static string ClassName(PlayerClass c) => c == PlayerClass.Sword ? "검술" : "마법";
    }
}
