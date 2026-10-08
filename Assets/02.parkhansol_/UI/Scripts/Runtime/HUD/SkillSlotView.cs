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
                icon.color = rank > 0 ? Color.white : new Color(0.45f, 0.45f, 0.45f, 1f);
            }
            if (lockOverlay != null) lockOverlay.SetActive(src != null && rank <= 0);
            if (rankText != null) rankText.text = rank > 0 ? new string('·', rank) : "";

            float remain = src?.GetCooldownRemaining(slot) ?? 0f;
            float dur = src?.GetCooldownDuration(slot) ?? 0f;
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
