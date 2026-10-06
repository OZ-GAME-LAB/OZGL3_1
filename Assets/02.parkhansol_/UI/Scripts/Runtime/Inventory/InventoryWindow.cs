using System.Collections.Generic;
using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// 인벤토리 창 (I). 1×1 슬롯 그리드, 디아블로식 집기→놓기, 우클릭 사용, 툴팁.
    /// 데이터는 IInventorySource (UISources.Inventory)에서만 읽고, 변경은 TryMove/TryUse 요청만 보낸다.
    /// </summary>
    [AddComponentMenu("OZ/UI/Inventory/Inventory Window")]
    public class InventoryWindow : UIWindow
    {
        [SerializeField] internal RectTransform grid;
        [SerializeField] internal InventorySlotView slotTemplate;
        [SerializeField] internal ItemTooltipView tooltip;
        [Tooltip("집은 아이템을 커서에 붙여 보여줄 아이콘")]
        [SerializeField] internal Image cursorIcon;
        [SerializeField] internal TMP_Text emptyText;
        [SerializeField] internal Color hoverColor = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] internal Color dropOkColor = new Color(0.4f, 1f, 0.5f, 0.5f);

        readonly List<InventorySlotView> _slots = new List<InventorySlotView>();
        IInventorySource _source;
        int _held = -1;
        int _hover = -1;

        protected override void Awake()
        {
            base.Awake();
            if (slotTemplate != null) slotTemplate.gameObject.SetActive(false);
            if (cursorIcon != null) cursorIcon.enabled = false;
        }

        protected override void OnOpened()
        {
            Bind(UISources.Inventory);
            UISources.Changed += OnSourcesChanged;
            if (_slots.Count > 0 && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_slots[0].gameObject);
        }

        protected override void OnClosed()
        {
            UISources.Changed -= OnSourcesChanged;
            Bind(null);
            DropHeld();
            if (tooltip != null) tooltip.Hide();
        }

        void OnSourcesChanged() => Bind(UISources.Inventory);

        void Bind(IInventorySource source)
        {
            if (_source != null) _source.Changed -= Refresh;
            _source = source;
            if (_source != null) _source.Changed += Refresh;
            BuildSlots();
            Refresh();
        }

        void BuildSlots()
        {
            int capacity = _source?.Capacity ?? 0;
            if (slotTemplate == null || grid == null) return;
            while (_slots.Count < capacity)
            {
                var s = Instantiate(slotTemplate, grid);
                s.gameObject.SetActive(true);
                s.Index = _slots.Count;
                s.Clicked += OnSlotClicked;
                s.UseRequested += OnSlotUse;
                s.Hovered += OnSlotHover;
                s.Unhovered += OnSlotUnhover;
                _slots.Add(s);
            }
            for (int i = 0; i < _slots.Count; i++) _slots[i].gameObject.SetActive(i < capacity);
            if (emptyText != null) emptyText.gameObject.SetActive(_source == null);
        }

        void Refresh()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                var stack = _source != null && i < _source.Capacity ? _source.GetSlot(i) : ItemStack.Empty;
                _slots[i].Show(stack, i == _held);
                bool hover = i == _hover;
                _slots[i].SetHighlight(hover, _held >= 0 ? dropOkColor : hoverColor);
            }
            if (cursorIcon != null)
            {
                var held = _held >= 0 && _source != null ? _source.GetSlot(_held) : ItemStack.Empty;
                cursorIcon.enabled = !held.IsEmpty && held.Item.icon != null;
                if (cursorIcon.enabled) cursorIcon.sprite = held.Item.icon;
            }
            if (tooltip != null && _hover >= 0 && _source != null) tooltip.Show(_source.GetSlot(_hover));
        }

        void OnSlotClicked(InventorySlotView slot)
        {
            if (_source == null) return;
            if (_held < 0)
            {
                if (_source.GetSlot(slot.Index).IsEmpty) return;
                _held = slot.Index; // 집기
            }
            else
            {
                if (slot.Index != _held && !_source.TryMove(_held, slot.Index))
                {
                    ((RectTransform)slot.transform).Shake();
                    return;
                }
                ((RectTransform)slot.transform).Punch(0.15f);
                _held = -1; // 놓기
            }
            Refresh();
        }

        void OnSlotUse(InventorySlotView slot)
        {
            if (_source == null) return;
            var stack = _source.GetSlot(slot.Index);
            if (stack.IsEmpty || !stack.Item.IsUsable || !_source.TryUse(slot.Index))
            {
                ((RectTransform)slot.transform).Shake();
                return;
            }
            ((RectTransform)slot.transform).Punch(0.25f);
            if (_held == slot.Index && _source.GetSlot(slot.Index).IsEmpty) _held = -1;
            Refresh();
        }

        void OnSlotHover(InventorySlotView slot)
        {
            _hover = slot.Index;
            Refresh();
        }

        void OnSlotUnhover(InventorySlotView slot)
        {
            if (_hover != slot.Index) return;
            _hover = -1;
            if (tooltip != null) tooltip.Hide();
            Refresh();
        }

        void DropHeld()
        {
            _held = -1;
            if (cursorIcon != null) cursorIcon.enabled = false;
        }

        void Update()
        {
            if (!IsOpen || cursorIcon == null || !cursorIcon.enabled) return;
            var mouse = Mouse.current;
            if (mouse == null) return;
            var parent = cursorIcon.rectTransform.parent as RectTransform;
            if (parent != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, mouse.position.ReadValue(), null, out var local))
                cursorIcon.rectTransform.anchoredPosition = local + new Vector2(6f, -6f);
        }
    }
}
