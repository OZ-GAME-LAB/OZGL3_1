using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// Q/E/R 스킬 슬롯 1칸. 쿨타임 원형 오버레이 + 남은 초 + 완료 연출 + 미습득 잠금.
    /// </summary>
    [AddComponentMenu("OZ/UI/HUD/Skill Slot View")]
    public class SkillSlotView : MonoBehaviour
    {
        [SerializeField] internal SkillSlot slot;
        [SerializeField] internal Image icon;
        [SerializeField] internal TMP_Text keyLabel;
        [SerializeField] internal TMP_Text rankText;
        [Tooltip("v1.0: 장착한 기술에 아이콘이 없으면 이름 첫 글자 (선택)")]
        [SerializeField] internal TMP_Text iconLetter;
        [SerializeField] internal CooldownRadial cooldown;
        [SerializeField] internal GameObject lockOverlay;
        [SerializeField] internal RectTransform shakeTarget;
        [SerializeField] internal Graphic tintTarget;
        [SerializeField] internal Color failColor = new Color(1f, 0.35f, 0.35f);

        internal SkillSlot Slot => slot;

        internal void Refresh(ISkillSource src)
        {
            if (keyLabel != null) keyLabel.text = UIText.Key(slot);
            SkillData data = src?.GetSkill(slot);
            int rank = src?.GetRank(slot) ?? 0;

            if (icon != null)
            {
                icon.sprite = data != null ? data.icon : null;
                icon.enabled = icon.sprite != null;
                var rest = rank > 0 ? Color.white : ItemSlotView.UnusableColor; // v0.7 미습득 = 쿨타임처럼 어둡게
                if (cooldown != null && cooldown.dimTarget == icon) cooldown.SetRestColor(rest);
                else icon.color = rest;
            }
            if (lockOverlay != null) lockOverlay.SetActive(src != null && rank <= 0);
            if (iconLetter != null) iconLetter.text = "";
            if (rankText != null) rankText.text = rank > 0 ? new string('·', rank) : "";

            float remain = src?.GetCooldownRemaining(slot) ?? 0f;
            float dur = src?.GetCooldownDuration(slot) ?? 0f;
            if (cooldown != null)
            {
                if (remain > 0f && dur > 0f) cooldown.Play(remain, dur);
                else cooldown.Clear();
            }
        }

        /// <summary>v1.0 (기획서 v0.5): 장착된 기술 아이콘 · 빈 칸은 잠금 표시(미장착) · 쿨다운</summary>
        internal void RefreshLoadout(ISkillLoadoutSource lo)
        {
            if (keyLabel != null) keyLabel.text = UIText.Key(slot);
            string id = lo?.GetEquipped(slot);
            SkillOffer offer = default;
            if (lo != null && !string.IsNullOrEmpty(id))
                for (int i = 0; i < lo.SkillCount; i++) { var o = lo.GetSkill(i); if (o.id == id) { offer = o; break; } }
            bool equipped = offer.IsValid;

            if (icon != null)
            {
                icon.sprite = equipped ? offer.icon : null;
                icon.enabled = icon.sprite != null;
                var rest = equipped ? Color.white : ItemSlotView.UnusableColor;
                if (cooldown != null && cooldown.dimTarget == icon) cooldown.SetRestColor(rest);
                else icon.color = rest;
            }
            if (lockOverlay != null) lockOverlay.SetActive(!equipped);
            if (rankText != null) rankText.text = ""; // 강화 단계 없음
            if (iconLetter != null)
            {
                bool letterOn = equipped && offer.icon == null && !string.IsNullOrEmpty(offer.displayName);
                iconLetter.text = letterOn ? offer.displayName.Substring(0, 1) : "";
                iconLetter.color = offer.damageKind == SkillDamageKind.Magic ? new Color(0.55f, 0.72f, 1f) : new Color(1f, 0.62f, 0.3f);
            }

            float remain = equipped ? lo.GetCooldownRemaining(slot) : 0f;
            float dur = equipped ? lo.GetCooldownDuration(slot) : 0f;
            if (cooldown != null)
            {
                if (remain > 0f && dur > 0f) cooldown.Play(remain, dur);
                else cooldown.Clear();
            }
        }

        internal void OnCooldownStarted(float duration)
        {
            if (cooldown != null) cooldown.Play(duration, duration);
        }

        internal void OnUseFailed(SkillUseFailReason reason)
        {
            if (shakeTarget != null) shakeTarget.Shake(2f, 0.18f);
            UISfx.Play(UISound.Error, 0.7f);
            if (tintTarget != null) tintTarget.FlashColor(failColor, 0.15f);
        }
    }
}
