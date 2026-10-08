using DG.Tweening;
using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>아이템 정보창 (이름 / 종류 / 효과 / 설명 / 보유 수량 / 조작 안내)</summary>
    [AddComponentMenu("OZ/UI/Inventory/Item Tooltip View")]
    public class ItemTooltipView : MonoBehaviour
    {
        [SerializeField] internal CanvasGroup group;
        [SerializeField] internal Image icon;
        [SerializeField] internal TMP_Text nameText;
        [SerializeField] internal TMP_Text typeText;
        [SerializeField] internal TMP_Text effectText;
        [SerializeField] internal TMP_Text descriptionText;
        [SerializeField] internal TMP_Text footerText;
        [Tooltip("아이템 색 띠 (선택)")]
        [SerializeField] internal Image accent;
        [SerializeField] internal float showDelay = 0.1f;

        ItemData _current;

        void Awake() => HideImmediate();

        internal void Show(ItemStack stack)
        {
            if (stack.IsEmpty) { Hide(); return; }
            var item = stack.Item;
            bool changed = item != _current;
            _current = item;

            if (icon != null) { icon.sprite = item.icon; icon.enabled = item.icon != null; }
            if (nameText != null) { nameText.text = item.displayName; nameText.color = item.fxColor; }
            if (accent != null) accent.color = item.fxColor;
            if (typeText != null) typeText.text = UIText.ItemType(item.effectType)
                + (item.slotIndex >= 0 ? $"  ·  퀵슬롯 {item.slotIndex + 1}" : "");
            if (effectText != null) effectText.text = UIText.ItemEffect(item);
            if (descriptionText != null) descriptionText.text = item.description;
            if (footerText != null) footerText.text = $"보유 {stack.Count}   " + (item.IsUsable ? "[우클릭] 사용" : "") + "   [좌클릭] 이동";

            if (group == null) return;
            if (changed)
            {
                group.DOKill();
                group.alpha = 0f;
                group.DOFade(1f, UITweenStyle.Fast).SetDelay(showDelay).SetUpdate(true).SetLink(gameObject);
            }
        }

        internal void Hide()
        {
            _current = null;
            if (group == null) return;
            group.DOKill();
            group.DOFade(0f, UITweenStyle.Fast).SetUpdate(true).SetLink(gameObject);
        }

        void HideImmediate()
        {
            _current = null;
            if (group != null) group.alpha = 0f;
        }
    }
}
