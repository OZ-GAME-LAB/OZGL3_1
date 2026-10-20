using System;
using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// 상점 판매 칸 하나 (v1.0 시안 B: 카드).
    ///   [1~4 키] · 색 띠 · 아이콘 · 이름 · 효과 · 보유 칸(■■□□□) · [가격] 구매 버튼
    /// 카드 클릭/←→ 선택 = 고르기, 구매 버튼·Enter·더블클릭 = 1개 구매 요청.
    /// 비어 있는 참조는 건너뛰므로 목록형(시안 A) 줄로 써도 된다.
    /// </summary>
    [AddComponentMenu("OZ/UI/Shop/Shop Entry View")]
    public class ShopEntryView : MonoBehaviour,
        IPointerClickHandler, ISelectHandler, ISubmitHandler
    {
        [SerializeField] internal Image background;
        [SerializeField] internal Image selectedMark;
        [SerializeField] internal Image accent;
        [SerializeField] internal Image icon;
        [SerializeField] internal TMP_Text keyText;
        [SerializeField] internal TMP_Text nameText;
        [SerializeField] internal TMP_Text typeText;
        [SerializeField] internal TMP_Text effectText;
        [Tooltip("보유 칸 (최대 보유 수만큼 켜짐). 비우면 ownedText만 씀")]
        [SerializeField] internal Image[] pips = new Image[0];
        [SerializeField] internal TMP_Text ownedText;
        [SerializeField] internal TMP_Text priceText;
        [Tooltip("카드 아래 구매 버튼 (선택)")]
        [SerializeField] internal Button buyButton;
        [SerializeField] internal TMP_Text buyLabel;

        [SerializeField] internal Color normalColor = Color.white;
        [SerializeField] internal Color selectedColor = new Color(0.75f, 0.9f, 1f, 1f);
        [SerializeField] internal Color priceOkColor = new Color(1f, 0.85f, 0.4f);
        [SerializeField] internal Color priceBadColor = new Color(1f, 0.45f, 0.45f);
        [SerializeField] internal Color maxedColor = new Color(0.55f, 0.6f, 0.72f);
        [SerializeField] internal Color pipEmptyColor = new Color(1f, 1f, 1f, 0.12f);
        [SerializeField] internal Color buyDisabledColor = new Color(0.55f, 0.58f, 0.68f, 1f);

        internal int Index { get; set; }
        internal event Action<ShopEntryView> Picked;
        internal event Action<ShopEntryView> QuickBuy;

        void Awake()
        {
            if (buyButton != null) buyButton.onClick.AddListener(() => QuickBuy?.Invoke(this));
        }

        internal void Show(ShopEntry entry, int owned, int currency, bool selected, string currencyShort)
        {
            var item = entry?.item;
            Color c = item != null ? item.fxColor : Color.white;
            if (icon != null)
            {
                icon.sprite = item != null ? item.icon : null;
                icon.enabled = icon.sprite != null;
            }
            if (accent != null) accent.color = c;
            if (keyText != null) keyText.text = item != null && item.slotIndex >= 0 ? UIText.ItemKey(item.slotIndex) : "";
            if (nameText != null) { nameText.text = item != null ? item.displayName : "-"; nameText.color = c; }
            if (typeText != null) typeText.text = item != null ? UIText.ItemType(item.effectType) : "";
            if (effectText != null) effectText.text = UIText.ItemEffect(item);

            bool maxed = entry != null && ShopRules.RoomLeft(entry, owned) <= 0;
            bool afford = entry != null && entry.price <= currency;
            int limit = entry != null ? entry.maxOwned : 0;

            for (int k = 0; k < pips.Length; k++)
            {
                if (pips[k] == null) continue;
                var back = pips[k].transform.parent; // 칸 바탕(PipBack)이 있으면 같이 끈다
                if (back != null && back != transform) back.gameObject.SetActive(k < limit);
                else pips[k].gameObject.SetActive(k < limit);
                pips[k].color = k < owned ? c : pipEmptyColor;
            }
            if (ownedText != null)
            {
                bool hasPips = limit > 0 && pips.Length >= limit;
                ownedText.text = entry == null || hasPips ? "" : limit > 0 ? $"{owned}/{limit}" : $"보유 {owned}";
                ownedText.color = maxed ? maxedColor : Color.white;
            }

            string price = entry != null ? $"{entry.price:N0}{currencyShort}" : "";
            bool oneTime = limit == 1; // 장비 등 1회 구매
            if (priceText != null)
            {
                priceText.text = maxed ? (oneTime ? "보유" : "최대") : price;
                priceText.color = maxed ? maxedColor : afford ? priceOkColor : priceBadColor;
            }
            if (buyLabel != null) buyLabel.text = maxed ? (oneTime ? "구매 완료" : "최대 보유") : price;
            if (buyButton != null && buyButton.targetGraphic != null)
                buyButton.targetGraphic.color = !maxed && afford ? Color.white : buyDisabledColor;

            if (background != null) background.color = selected ? selectedColor : normalColor;
            if (selectedMark != null) selectedMark.enabled = selected;
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            if (e.clickCount >= 2) QuickBuy?.Invoke(this);
            else Picked?.Invoke(this);
        }

        public void OnSelect(BaseEventData e) => Picked?.Invoke(this);
        public void OnSubmit(BaseEventData e) => QuickBuy?.Invoke(this);
    }
}
