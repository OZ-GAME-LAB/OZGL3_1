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
    /// [v1.1] 상태창 3번 "상점" 탭: 왼쪽 판매 카드(3열) · 오른쪽 '내 아이템'(가진 것 한눈에) + 고른 아이템 설명.
    ///   판매 목록 = ShopData (가격 · 최대 보유) — 빌더가 기본 목록을 넣고, 게임에서는 GameUI.Status.SetItemShop(data)로 교체.
    ///   소모품은 종류별 최대 N개, 장비(칼·방패 등, maxOwned 1)는 1회 구매·영구 보유.
    ///   재화·보유·구매 = UISources.Shop (IShopSource.Currency = 아이템 포인트, 성장 포인트와 따로).
    ///   조작: 방향키 고르기 · Enter/구매 버튼/더블클릭 1개 구매.
    /// </summary>
    [AddComponentMenu("OZ/UI/Status/Status Item Page")]
    public class StatusItemPage : StatusPage
    {
        [SerializeField] internal ShopData shop;
        [Header("판매")]
        [SerializeField] internal RectTransform list;
        [SerializeField] internal ShopEntryView entryTemplate;
        [SerializeField] internal int columns = 3;
        [SerializeField] internal TMP_Text emptyText;
        [Header("내 아이템")]
        [SerializeField] internal RectTransform ownedList;
        [SerializeField] internal OwnedItemRow ownedTemplate;
        [SerializeField] internal TMP_Text detailName;
        [SerializeField] internal TMP_Text detailDesc;
        [SerializeField] internal TMP_Text detailNext;

        readonly List<ShopEntryView> _cards = new List<ShopEntryView>();
        readonly List<OwnedItemRow> _owned = new List<OwnedItemRow>();
        int _selected;
        InputAction _left, _right, _up, _down;

        internal override string Hint => "[방향키] 고르기   [Enter] 1개 구매";
        internal override bool UsesItemPoints => true;

        IShopSource Source => Owner != null ? Owner.ItemShop : UISources.Shop;
        string CurrencyName => shop != null && !string.IsNullOrEmpty(shop.currencyName) ? shop.currencyName : "아이템 포인트";

        /// <summary>지금 판매 목록 (임시 지갑이 1~4 칸 아이템을 채울 때 읽음)</summary>
        public ShopData ItemShop => shop;

        internal ShopData Shop
        {
            get => shop;
            set { shop = value; _selected = 0; Build(); if (isActiveAndEnabled) { Refresh(); Focus(); } }
        }

        void Awake()
        {
            if (entryTemplate != null) entryTemplate.gameObject.SetActive(false);
            if (ownedTemplate != null) ownedTemplate.gameObject.SetActive(false);
            _left = Key("UI_ItemLeft", "<Keyboard>/leftArrow", "<Keyboard>/a", "<Gamepad>/dpad/left", "<Gamepad>/leftStick/left");
            _right = Key("UI_ItemRight", "<Keyboard>/rightArrow", "<Keyboard>/d", "<Gamepad>/dpad/right", "<Gamepad>/leftStick/right");
            _up = Key("UI_ItemUp", "<Keyboard>/upArrow", "<Keyboard>/w", "<Gamepad>/dpad/up", "<Gamepad>/leftStick/up");
            _down = Key("UI_ItemDown", "<Keyboard>/downArrow", "<Keyboard>/s", "<Gamepad>/dpad/down", "<Gamepad>/leftStick/down");
            // 눌린 순간 처리 (EventSystem 이동은 끔 — 프레임이 느려도 짧게 누른 키를 놓치지 않게)
            _left.performed += _ => Step(-1, 0);
            _right.performed += _ => Step(+1, 0);
            _up.performed += _ => Step(0, -1);
            _down.performed += _ => Step(0, +1);
            Build();
        }

        static InputAction Key(string name, params string[] paths)
        {
            var a = new InputAction(name, InputActionType.Button);
            foreach (var p in paths) a.AddBinding(p);
            return a;
        }

        void OnDestroy() { _left?.Dispose(); _right?.Dispose(); _up?.Dispose(); _down?.Dispose(); }
        internal override void OnShow() { _left.Enable(); _right.Enable(); _up.Enable(); _down.Enable(); }
        internal override void OnHide() { _left.Disable(); _right.Disable(); _up.Disable(); _down.Disable(); }

        internal override void Focus()
        {
            if (_selected < 0 || _selected >= _cards.Count || !_cards[_selected].gameObject.activeSelf) return;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_cards[_selected].gameObject);
        }

        // ───────────── 목록 ─────────────

        void Build()
        {
            int count = shop != null ? shop.Count : 0;
            if (entryTemplate != null && list != null)
            {
                while (_cards.Count < count)
                {
                    var card = Instantiate(entryTemplate, list);
                    card.Index = _cards.Count;
                    card.Picked += r => Select(r.Index);
                    card.QuickBuy += r => { Select(r.Index); Buy(); Focus(); };
                    var sel = card.GetComponent<Selectable>();
                    if (sel != null) sel.navigation = new Navigation { mode = Navigation.Mode.None };
                    if (card.buyButton != null) card.buyButton.navigation = new Navigation { mode = Navigation.Mode.None };
                    _cards.Add(card);
                }
                for (int i = 0; i < _cards.Count; i++) _cards[i].gameObject.SetActive(i < count);
            }
            if (ownedTemplate != null && ownedList != null)
            {
                while (_owned.Count < count) _owned.Add(Instantiate(ownedTemplate, ownedList));
                for (int i = 0; i < _owned.Count; i++) _owned[i].gameObject.SetActive(i < count);
            }
        }

        void Step(int dx, int dy)
        {
            int count = shop != null ? shop.Count : 0;
            if (count <= 0 || !isActiveAndEnabled) return;
            int cols = Mathf.Max(1, columns);
            int next = _selected;
            if (dx != 0) next = ((_selected + dx) % count + count) % count;
            else
            {
                next = _selected + dy * cols;
                if (next < 0 || next >= count) return; // 위·아래 끝에서는 멈춤
            }
            Select(next);
            Focus();
        }

        void Select(int index)
        {
            if (shop == null || index < 0 || index >= shop.Count) return;
            if (index != _selected) { _selected = index; UISfx.Play(UISound.Hover); }
            Refresh();
        }

        int Owned(ShopEntry e)
        {
            var src = Source;
            return e != null && e.item != null && src != null ? src.GetOwned(e.item) : 0;
        }

        void Buy()
        {
            var e = shop != null ? shop.Get(_selected) : null;
            if (e == null || e.item == null || Owner == null) return;
            var src = Source;
            if (src == null) { Owner.Report(ShopBuyResult.Unavailable, "", CurrencyName); return; }
            bool equip = e.item.IsEquipment || e.maxOwned == 1;
            var result = src.TryBuy(e, 1);
            string limit = equip ? "이미 가지고 있다" : $"더 가질 수 없다 (최대 {e.maxOwned}개)";
            if (Owner.Report(result, equip ? $"{e.item.displayName} 구매 — 영구 보유" : $"{e.item.displayName} ×1 구매", CurrencyName, limit))
            {
                if (_selected < _cards.Count) ((RectTransform)_cards[_selected].transform).Punch(0.12f);
                if (_selected < _owned.Count) ((RectTransform)_owned[_selected].transform).Punch(0.2f);
            }
            Refresh();
            Owner.RefreshHeader();
        }

        internal override void Refresh()
        {
            int count = shop != null ? shop.Count : 0;
            if (_cards.Count < count || _owned.Count < count) Build();
            var src = Source;
            if (emptyText != null)
            {
                emptyText.gameObject.SetActive(count == 0 || src == null);
                emptyText.text = count == 0 ? "판매 목록이 비어 있습니다 (ShopData)" : "아이템 소스가 연결되지 않았습니다 (IShopSource)";
            }
            int money = src != null ? src.Currency : 0;
            _selected = Mathf.Clamp(_selected, 0, Mathf.Max(0, count - 1));
            for (int i = 0; i < count; i++)
            {
                var e = shop.Get(i);
                int owned = Owned(e);
                if (i < _cards.Count) _cards[i].Show(e, owned, money, i == _selected, "P");
                if (i < _owned.Count) _owned[i].Show(e, owned, i == _selected);
            }

            // 고른 아이템 설명
            var cur = count > 0 ? shop.Get(_selected) : null;
            var item = cur?.item;
            if (detailName != null) { detailName.text = item != null ? item.displayName : ""; detailName.color = item != null ? item.fxColor : Color.white; }
            if (detailDesc != null)
            {
                string eff = UIText.ItemEffect(item);
                string desc = item != null ? item.description : "";
                detailDesc.text = item == null ? "" : item.IsEquipment || string.IsNullOrEmpty(eff) ? desc : string.IsNullOrEmpty(desc) ? eff : eff + "\n" + desc;
            }
            if (detailNext != null)
            {
                int owned = Owned(cur);
                bool equip = item != null && (item.IsEquipment || cur.maxOwned == 1);
                int room = ShopRules.RoomLeft(cur, owned);
                detailNext.text = cur == null ? "" :
                    equip ? (owned > 0 ? "보유 중 — 영구 효과" : $"{cur.price:N0}P · 1회 구매, 영구 보유") :
                    room <= 0 ? $"최대 보유 ({owned}/{cur.maxOwned})" :
                    $"{cur.price:N0}P · 구매하면 {owned + 1}개" + (cur.maxOwned > 0 ? $" (최대 {cur.maxOwned})" : "");
            }
        }
    }
}
