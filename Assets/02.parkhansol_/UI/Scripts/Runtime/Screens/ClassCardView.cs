using System;
using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>계열 선택 카드 1장 (이름 · 설명 · Q/E/R 스킬 이름)</summary>
    [AddComponentMenu("OZ/UI/Screens/Class Card View")]
    public class ClassCardView : MonoBehaviour
    {
        [SerializeField] internal Button button;
        [SerializeField] internal Image icon;
        [SerializeField] internal TMP_Text nameText;
        [SerializeField] internal TMP_Text descriptionText;
        [SerializeField] internal TMP_Text skillsText;

        internal ClassData Data { get; private set; }
        internal event Action<ClassCardView> Chosen;

        void Awake()
        {
            if (button != null) button.onClick.AddListener(() => Chosen?.Invoke(this));
        }

        internal void Show(ClassData data)
        {
            Data = data;
            if (nameText != null) nameText.text = data != null ? data.displayName : "";
            if (descriptionText != null) descriptionText.text = data != null ? data.description : "";
            if (icon != null) { icon.sprite = data != null ? data.icon : null; icon.enabled = icon.sprite != null; }
            if (skillsText != null)
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < 3; i++)
                {
                    var s = data != null ? data.GetSkill((SkillSlot)i) : null;
                    sb.Append((SkillSlot)i).Append("  ").Append(s != null ? s.displayName : "-");
                    if (s != null) sb.Append("  (").Append(s.GetCooldown(1).ToString("0")).Append("초)");
                    if (i < 2) sb.Append('\n');
                }
                skillsText.text = sb.ToString();
            }
        }
    }
}
