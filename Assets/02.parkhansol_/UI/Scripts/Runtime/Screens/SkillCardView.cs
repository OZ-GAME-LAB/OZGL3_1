using System;
using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>스킬 창의 스킬 1개 (아이콘 · 이름 · 단계 · 현재/다음 효과 · 비용 · 투자 버튼)</summary>
    [AddComponentMenu("OZ/UI/Screens/Skill Card View")]
    public class SkillCardView : MonoBehaviour
    {
        [SerializeField] internal SkillSlot slot;
        [SerializeField] internal Image icon;
        [SerializeField] internal TMP_Text keyText;
        [SerializeField] internal TMP_Text nameText;
        [SerializeField] internal TMP_Text rankText;
        [SerializeField] internal TMP_Text currentText;
        [SerializeField] internal TMP_Text nextText;
        [SerializeField] internal TMP_Text costText;
        [SerializeField] internal Button investButton;
        [SerializeField] internal TMP_Text investLabel;
        [SerializeField] internal Image[] rankPips;
        [SerializeField] internal Color pipOn = new Color(1f, 0.85f, 0.35f);
        [SerializeField] internal Color pipOff = new Color(1f, 1f, 1f, 0.2f);

        internal SkillSlot Slot => slot;
        internal event Action<SkillCardView> InvestClicked;

        void Awake()
        {
            if (investButton != null) investButton.onClick.AddListener(() => InvestClicked?.Invoke(this));
        }

        internal void Refresh(ISkillSource src)
        {
            var data = src?.GetSkill(slot);
            int rank = src?.GetRank(slot) ?? 0;
            int max = data != null ? data.maxRank : 3;

            if (keyText != null) keyText.text = UIText.Key(slot);
            if (nameText != null) nameText.text = data != null ? data.displayName : "(미지정)";
            if (icon != null) { icon.sprite = data != null ? data.icon : null; icon.enabled = icon.sprite != null; icon.color = rank > 0 ? Color.white : new Color(0.5f, 0.5f, 0.5f); }
            if (rankText != null) rankText.text = rank > 0 ? $"{rank}/{max}단계" : "미습득";

            if (rankPips != null)
                for (int i = 0; i < rankPips.Length; i++)
                    if (rankPips[i] != null) { rankPips[i].gameObject.SetActive(i < max); rankPips[i].color = i < rank ? pipOn : pipOff; }

            if (currentText != null)
                currentText.text = data == null ? "" : rank > 0
                    ? $"현재: {data.GetRank(rank).summary}  (대기 {data.GetCooldown(rank):0.#}초)"
                    : data.description;
            if (nextText != null)
                nextText.text = data == null || rank >= max ? "최대 단계" :
                    $"다음: {data.GetRank(rank + 1).summary}  (대기 {data.GetCooldown(rank + 1):0.#}초)";
            if (costText != null) costText.text = data == null || rank >= max ? "" : $"비용 {data.costPerRank}P";

            string reason = null;
            bool can = src != null && src.CanInvest(slot, out reason);
            if (investButton != null) investButton.interactable = can;
            if (investLabel != null) investLabel.text = can ? (rank > 0 ? "강화" : "습득") : (reason ?? "-");
        }

        internal void PlayInvested()
        {
            ((RectTransform)transform).Punch(0.12f);
            if (icon != null) icon.FlashColor(new Color(1f, 0.9f, 0.5f), 0.3f);
        }

        internal void PlayDenied()
        {
            ((RectTransform)transform).Shake(3f, 0.2f);
        }
    }
}
