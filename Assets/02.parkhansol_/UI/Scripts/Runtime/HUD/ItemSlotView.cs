using DG.Tweening;
using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>1~4 고정 소모품 슬롯 1칸 (아이콘 + 수량 + 키)</summary>
    [AddComponentMenu("OZ/UI/HUD/Item Slot View")]
    public class ItemSlotView : MonoBehaviour
    {
        [SerializeField, Range(0, 3)] internal int slotIndex;
        [SerializeField] internal Image icon;
        [SerializeField] internal TMP_Text countText;
        [Tooltip("수량 그림자 (선택)")]
        [SerializeField] internal TMP_Text countShadow;
        [SerializeField] internal TMP_Text keyLabel;
        [SerializeField] internal RectTransform punchTarget;
        [SerializeField] internal Image useFx;

        internal int SlotIndex => slotIndex;

        internal void Refresh(IItemSource src)
        {
            if (keyLabel != null) keyLabel.text = UIText.ItemKey(slotIndex);
            ItemData item = src?.GetItem(slotIndex);
            int count = src?.GetCount(slotIndex) ?? 0;

            if (icon != null)
            {
                icon.sprite = item != null ? item.icon : null;
                icon.enabled = icon.sprite != null;
                icon.color = count > 0 ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            }
            if (countText != null) countText.text = src == null ? "" : count.ToString();
            if (countShadow != null) countShadow.text = countText != null ? countText.text : "";
        }

        internal void PlayUsed(ItemData item)
        {
            if (punchTarget != null) punchTarget.Punch(0.3f, 0.3f);
            if (useFx == null) return;

            // 사용 효과: 아이템 색 링이 커지며 사라짐
            var rt = useFx.rectTransform;
            useFx.DOKill(); rt.DOKill();
            useFx.gameObject.SetActive(true);
            Color c = item != null ? item.fxColor : Color.white;
            c.a = 0.9f;
            useFx.color = c;
            rt.localScale = Vector3.one * 0.6f;
            DOTween.Sequence()
                .Append(rt.DOScale(1.8f, 0.35f).SetEase(Ease.OutQuad))
                .Join(useFx.DOFade(0f, 0.35f))
                .OnComplete(() => useFx.gameObject.SetActive(false))
                .SetUpdate(true).SetLink(useFx.gameObject);
        }

        internal void PlayFailed()
        {
            if (punchTarget != null) punchTarget.Shake(2f, 0.18f);
        }
    }
}
