using System;
using System.Collections.Generic;
using DG.Tweening;
using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// [v1.0] 상점 창 (GameUI.Shop 구현). 기본 레이아웃 = 시안 B: 카드 4장 가로 + 아래 '내 가방' 줄(1~4 칸) · 위쪽 재화.
    ///   조작(가로 카드): ←/→ 고르기 · Enter/구매 버튼/더블클릭 1개 구매 · ESC 닫기
    ///   (세로 목록 + 상세 패널(시안 A)로 만들면: ↑/↓ 고르기 · ←/→ 수량)
    /// 데이터: ShopData (가격·상한) + UISources.Shop (IShopSource: 재화·보유·구매). UI는 TryBuy 요청만 보낸다.
    /// 레이아웃은 OZUIRootBuilder.BuildShop이 만든다 — 시안이 바뀌어도 이 스크립트는 참조 필드만 다시 연결하면 된다.
    /// </summary>
    [AddComponentMenu("OZ/UI/Shop/Shop Window")]
    public class ShopWindow : UIWindow, IShopApi
    {
        [Header("머리")]
        [SerializeField] internal TMP_Text titleText;
        [SerializeField] internal TMP_Text greetingText;
        [SerializeField] internal TMP_Text currencyText;
        [SerializeField] internal RectTransform currencyPunch;

        [Header("목록")]
        [SerializeField] internal RectTransform list;
        [Tooltip("켜면 칸을 ←/→로 고른다 (카드 가로 배치, 수량은 항상 1). 끄면 ↑/↓ 고르기 + ←/→ 수량")]
        [SerializeField] internal bool horizontalList = true;
        [SerializeField] internal ShopEntryView entryTemplate;
        [SerializeField] internal TMP_Text emptyText;

        [Header("상세")]
        [SerializeField] internal Image detailIcon;
        [SerializeField] internal TMP_Text detailName;
        [SerializeField] internal TMP_Text detailType;
        [SerializeField] internal TMP_Text detailEffect;
        [SerializeField] internal TMP_Text detailDesc;
        [SerializeField] internal TMP_Text detailOwned;
        [SerializeField] internal Image detailAccent;
        [SerializeField] internal TMP_Text quantityText;
        [SerializeField] internal TMP_Text totalText;
        [SerializeField] internal Button minusButton;
        [SerializeField] internal Button plusButton;
        [SerializeField] internal Button buyButton;
        [SerializeField] internal TMP_Text messageText;

        [Header("내 가방 (선택)")]
        [Tooltip("1~4 칸 — HUD 아이템 칸과 같은 뷰. 구매하면 해당 칸이 튄다")]
        [SerializeField] internal ItemBarView bagBar;

        [Header("색")]
        [SerializeField] internal Color okColor = new Color(0.55f, 1f, 0.65f);
        [SerializeField] internal Color badColor = new Color(1f, 0.45f, 0.45f);
        [SerializeField] internal Color priceColor = new Color(1f, 0.85f, 0.4f);
        [SerializeField] internal Color dimColor = new Color(0.65f, 0.7f, 0.82f);
        [SerializeField] internal float messageSeconds = 1.6f;

        readonly List<ShopEntryView> _rows = new List<ShopEntryView>();
        ShopData _shop;
        IShopSource _source;
        int _selected;
        int _qty = 1;
        Action _onClosed;
        InputAction _qtyDown, _qtyUp;
        Tween _messageTween;

        // ───────────── IShopApi ─────────────

        public void Open(ShopData shop, Action onClosed = null)
        {
            if (shop == null) { onClosed?.Invoke(); return; }
            var ui = UIManager.Instance;
            if (ui == null) { onClosed?.Invoke(); return; }
            _onClosed = onClosed;
            ui.Open(this, shop);
        }
        // Close() · IsOpen 은 UIWindow 것을 그대로 쓴다

        // ───────────── 수명 ─────────────

        protected override void Awake()
        {
            base.Awake();
            if (entryTemplate != null) entryTemplate.gameObject.SetActive(false);
            // 마우스로 버튼을 눌러도 키보드 포커스는 목록에 남긴다 (↑↓·Enter가 계속 되게)
            if (minusButton != null) minusButton.onClick.AddListener(() => { ChangeQty(-1); FocusRow(_selected); });
            if (plusButton != null) plusButton.onClick.AddListener(() => { ChangeQty(+1); FocusRow(_selected); });
            if (buyButton != null) buyButton.onClick.AddListener(() => { BuySelected(); FocusRow(_selected); });

            _qtyDown = new InputAction("UI_ShopQtyDown", InputActionType.Button);
            _qtyDown.AddBinding("<Keyboard>/leftArrow"); _qtyDown.AddBinding("<Keyboard>/a"); _qtyDown.AddBinding("<Gamepad>/dpad/left"); _qtyDown.AddBinding("<Gamepad>/leftStick/left");
            _qtyUp = new InputAction("UI_ShopQtyUp", InputActionType.Button);
            _qtyUp.AddBinding("<Keyboard>/rightArrow"); _qtyUp.AddBinding("<Keyboard>/d"); _qtyUp.AddBinding("<Gamepad>/dpad/right"); _qtyUp.AddBinding("<Gamepad>/leftStick/right");
            // 가로 목록: ←/→ = 칸 이동 (눌린 순간 처리 → 프레임이 느려도 짧게 누른 키를 놓치지 않음)
            // 세로 목록: ←/→ = 수량
            _qtyDown.performed += _ => { if (!IsOpen) return; if (horizontalList) Step(-1); else ChangeQty(-1); };
            _qtyUp.performed += _ => { if (!IsOpen) return; if (horizontalList) Step(+1); else ChangeQty(+1); };
        }

        // 중복 UIRoot(UIManager가 지우는 쪽)의 창이 등록을 덮어쓰지 않게
        bool IsLiveRoot => UIManager.Instance == null || GetComponentInParent<UIManager>() == UIManager.Instance;
        void OnEnable() { if (IsLiveRoot) GameUI.Register((IShopApi)this); }
        void OnDisable() { GameUI.Unregister(this); _qtyDown?.Disable(); _qtyUp?.Disable(); }
        void OnDestroy()
        {
            UISources.Changed -= OnSourcesChanged;
            Bind(null);
            _qtyDown?.Dispose(); _qtyUp?.Dispose();
        }

        protected override void OnSetup(object args)
        {
            if (args is ShopData shop) _shop = shop;
            _selected = 0;
            _qty = 1;
            BuildRows();
            if (IsOpen) { Refresh(); FocusRow(_selected); } // 열린 채로 다른 상점을 열면 바로 갱신
        }

        protected override void OnOpened()
        {
            UISources.Changed += OnSourcesChanged;
            Bind(UISources.Shop);
            _qtyDown.Enable(); _qtyUp.Enable();
            if (bagBar != null) bagBar.Bind(UISources.Items);
            ShowMessage("", Color.white, true);
            if (titleText != null) titleText.text = _shop != null ? _shop.displayName : "상점";
            if (greetingText != null)
            {
                string who = _shop != null && _shop.keeper != null ? _shop.keeper.displayName : "";
                string line = _shop != null ? _shop.greeting : "";
                greetingText.text = string.IsNullOrEmpty(line) ? "" : string.IsNullOrEmpty(who) ? line : $"{who}  \"{line}\"";
            }
            Refresh();
            FocusRow(_selected);
        }

        protected override void OnClosed()
        {
            UISources.Changed -= OnSourcesChanged;
            Bind(null);
            if (bagBar != null) bagBar.Bind(null);
            _qtyDown.Disable(); _qtyUp.Disable();
            var cb = _onClosed;
            _onClosed = null;
            cb?.Invoke();
        }

        void OnSourcesChanged()
        {
            Bind(UISources.Shop);
            if (bagBar != null) bagBar.Bind(UISources.Items);
        }

        void Bind(IShopSource source)
        {
            if (_source != null) _source.ShopChanged -= Refresh;
            _source = source;
            if (_source != null) _source.ShopChanged += Refresh;
            if (IsOpen) Refresh();
        }

        // ───────────── 목록 ─────────────

        void BuildRows()
        {
            int count = _shop != null ? _shop.Count : 0;
            if (entryTemplate == null || list == null) return;
            while (_rows.Count < count)
            {
                var row = Instantiate(entryTemplate, list);
                row.gameObject.SetActive(true);
                row.Index = _rows.Count;
                row.Picked += r => Select(r.Index);
                row.QuickBuy += r => { Select(r.Index); BuySelected(); FocusRow(_selected); };
                _rows.Add(row);
            }
            for (int i = 0; i < _rows.Count; i++) _rows[i].gameObject.SetActive(i < count);
            WireNavigation(count);
            if (emptyText != null) emptyText.gameObject.SetActive(count == 0);
        }

        /// <summary>칸끼리만 움직이게 (가로: ←/→, 세로: ↑/↓ — 세로일 땐 ←/→가 수량)</summary>
        void WireNavigation(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var sel = _rows[i].GetComponent<Selectable>();
                if (sel == null) continue;
                var prev = _rows[(i - 1 + count) % count].GetComponent<Selectable>();
                var next = _rows[(i + 1) % count].GetComponent<Selectable>();
                // 가로 목록은 ←/→를 Step()이 직접 처리 (EventSystem 이동과 겹치지 않게 None)
                var nav = new Navigation { mode = horizontalList ? Navigation.Mode.None : Navigation.Mode.Explicit };
                if (!horizontalList) { nav.selectOnUp = prev; nav.selectOnDown = next; }
                sel.navigation = nav;
                if (_rows[i].buyButton != null) _rows[i].buyButton.navigation = new Navigation { mode = Navigation.Mode.None };
            }
            foreach (var b in new Selectable[] { minusButton, plusButton, buyButton })
                if (b != null) b.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        void FocusRow(int index)
        {
            if (index < 0 || index >= _rows.Count || !_rows[index].gameObject.activeSelf) return;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_rows[index].gameObject);
        }

        void Step(int delta)
        {
            int count = _shop != null ? _shop.Count : 0;
            if (count <= 0) return;
            Select(((_selected + delta) % count + count) % count);
            FocusRow(_selected);
        }

        void Select(int index)
        {
            if (_shop == null || index < 0 || index >= _shop.Count) return;
            if (index != _selected)
            {
                _selected = index;
                _qty = 1;
                UISfx.Play(UISound.Hover);
            }
            Refresh();
        }

        ShopEntry Current => _shop != null ? _shop.Get(_selected) : null;
        int Currency => _source?.Currency ?? 0;
        int Owned(ShopEntry e) => e != null && e.item != null && _source != null ? _source.GetOwned(e.item) : 0;
        string CurrencyShort => _shop != null && _shop.currencyName == "포인트" ? "P" : "";

        void ChangeQty(int delta)
        {
            var e = Current;
            if (e == null) return;
            int max = Mathf.Max(1, Mathf.Min(ShopRules.RoomLeft(e, Owned(e)), 99));
            int next = Mathf.Clamp(_qty + delta, 1, max);
            if (next == _qty)
            {
                if (quantityText != null) quantityText.rectTransform.Shake(2f, 0.15f);
                return;
            }
            _qty = next;
            UISfx.Play(UISound.Tab);
            if (quantityText != null) quantityText.rectTransform.Punch(0.2f, 0.15f);
            Refresh();
        }

        // ───────────── 구매 ─────────────

        void BuySelected()
        {
            var e = Current;
            if (e == null || e.item == null) return;
            if (_source == null)
            {
                Fail("상점 소스가 연결되지 않았습니다 (IShopSource)");
                return;
            }

            var result = _source.TryBuy(e, _qty);
            switch (result)
            {
                case ShopBuyResult.Success:
                    UISfx.Play(UISound.Confirm);
                    ShowMessage($"{e.item.displayName} ×{_qty} 구매", okColor);
                    if (currencyPunch != null) currencyPunch.Punch(0.25f);
                    if (_selected < _rows.Count) ((RectTransform)_rows[_selected].transform).Punch(0.12f);
                    if (detailIcon != null) detailIcon.rectTransform.Punch(0.3f);
                    PunchBagSlot(e.item.slotIndex);
                    _qty = 1;
                    break;
                case ShopBuyResult.NotEnoughCurrency:
                    Fail($"{(_shop != null ? _shop.currencyName : "재화")}가 부족하다");
                    if (currencyPunch != null) currencyPunch.Shake(3f, 0.2f);
                    break;
                case ShopBuyResult.LimitReached:
                    Fail(e.maxOwned > 0 ? $"더 가질 수 없다 (최대 {e.maxOwned}개)" : "더 가질 수 없다");
                    break;
                case ShopBuyResult.InventoryFull:
                    Fail("가방에 자리가 없다");
                    break;
                default:
                    Fail("지금은 살 수 없다");
                    break;
            }
            Refresh();
        }

        void Fail(string message)
        {
            UISfx.Play(UISound.Error);
            ShowMessage(message, badColor);
            var btn = buyButton != null ? buyButton : _selected < _rows.Count ? _rows[_selected].buyButton : null;
            if (btn != null) ((RectTransform)btn.transform).Shake(3f, 0.2f);
        }

        void PunchBagSlot(int slotIndex)
        {
            if (bagBar == null || bagBar.slots == null) return;
            foreach (var s in bagBar.slots)
                if (s != null && s.SlotIndex == slotIndex && s.punchTarget != null) s.punchTarget.Punch(0.35f, 0.3f);
        }

        void ShowMessage(string text, Color color, bool instant = false)
        {
            if (messageText == null) return;
            _messageTween?.Kill();
            messageText.text = text;
            messageText.color = color;
            messageText.alpha = 1f;
            if (instant || string.IsNullOrEmpty(text)) return;
            _messageTween = messageText.DOFade(0f, 0.4f).SetDelay(messageSeconds).SetUpdate(true).SetLink(messageText.gameObject);
        }

        // ───────────── 그리기 ─────────────

        void Refresh()
        {
            int money = Currency;
            if (currencyText != null)
                currencyText.text = _source != null ? $"{money:N0} {(_shop != null ? _shop.currencyName : "")}" : "-";

            for (int i = 0; i < _rows.Count; i++)
            {
                if (!_rows[i].gameObject.activeSelf) continue;
                var e = _shop != null ? _shop.Get(i) : null;
                _rows[i].Show(e, Owned(e), money, i == _selected, CurrencyShort);
            }

            var cur = Current;
            var item = cur?.item;
            int owned = Owned(cur);
            if (detailIcon != null) { detailIcon.sprite = item != null ? item.icon : null; detailIcon.enabled = detailIcon.sprite != null; }
            if (detailName != null) { detailName.text = item != null ? item.displayName : ""; detailName.color = item != null ? item.fxColor : Color.white; }
            if (detailAccent != null) detailAccent.color = item != null ? item.fxColor : Color.clear;
            if (detailType != null) detailType.text = item == null ? "" :
                UIText.ItemType(item.effectType) + (item.slotIndex >= 0 ? $"  ·  [{UIText.ItemKey(item.slotIndex)}] 사용" : "");
            if (detailEffect != null) detailEffect.text = UIText.ItemEffect(item);
            if (detailDesc != null) detailDesc.text = item != null ? item.description : "";
            if (detailOwned != null) detailOwned.text = cur == null ? "" : cur.maxOwned > 0 ? $"보유 {owned} / {cur.maxOwned}" : $"보유 {owned}";

            int room = ShopRules.RoomLeft(cur, owned);
            if (room > 0 && room != int.MaxValue) _qty = Mathf.Clamp(_qty, 1, room);
            if (quantityText != null) quantityText.text = room <= 0 ? "-" : _qty.ToString();

            long total = cur != null ? (long)cur.price * _qty : 0;
            bool affordable = cur != null && total <= money;
            if (totalText != null)
            {
                totalText.text = cur == null ? "" : room <= 0 ? "보유 최대" : $"합계 {total:N0}{CurrencyShort}";
                totalText.color = room <= 0 ? dimColor : affordable ? priceColor : badColor;
            }
            if (buyButton != null)
            {
                var g = buyButton.targetGraphic;
                if (g != null) g.color = cur != null && room > 0 && affordable ? Color.white : new Color(0.55f, 0.58f, 0.68f, 1f);
            }
            if (minusButton != null) minusButton.gameObject.SetActive(room > 0);
            if (plusButton != null) plusButton.gameObject.SetActive(room > 0);
        }

    }
}
