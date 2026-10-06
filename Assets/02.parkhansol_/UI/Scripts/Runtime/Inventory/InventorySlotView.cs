using System;
using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// 인벤토리 1×1 칸. 좌클릭/Submit = 집기·놓기, 우클릭 = 사용, 마우스 오버·선택 = 툴팁.
    /// </summary>
    [AddComponentMenu("OZ/UI/Inventory/Inventory Slot View")]
    public class InventorySlotView : MonoBehaviour,
        IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        [SerializeField] internal Image icon;
        [SerializeField] internal TMP_Text countText;
        [SerializeField] internal Image highlight;
        [SerializeField] internal Image heldMark;

        internal int Index { get; set; }
        internal event Action<InventorySlotView> Clicked;
        internal event Action<InventorySlotView> UseRequested;
        internal event Action<InventorySlotView> Hovered;
        internal event Action<InventorySlotView> Unhovered;

        internal void Show(ItemStack stack, bool held)
        {
            bool has = !stack.IsEmpty;
            if (icon != null)
            {
                icon.sprite = has ? stack.Item.icon : null;
                icon.enabled = has && icon.sprite != null;
                icon.color = held ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
            }
            if (countText != null) countText.text = has && stack.Count > 1 ? stack.Count.ToString() : "";
            if (heldMark != null) heldMark.enabled = held;
        }

        internal void SetHighlight(bool on, Color color)
        {
            if (highlight == null) return;
            highlight.enabled = on;
            highlight.color = color;
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) Clicked?.Invoke(this);
            else if (e.button == PointerEventData.InputButton.Right) UseRequested?.Invoke(this);
        }

        public void OnSubmit(BaseEventData e) => Clicked?.Invoke(this);
        public void OnPointerEnter(PointerEventData e) => Hovered?.Invoke(this);
        public void OnPointerExit(PointerEventData e) => Unhovered?.Invoke(this);
        public void OnSelect(BaseEventData e) => Hovered?.Invoke(this);
        public void OnDeselect(BaseEventData e) => Unhovered?.Invoke(this);
    }
}
