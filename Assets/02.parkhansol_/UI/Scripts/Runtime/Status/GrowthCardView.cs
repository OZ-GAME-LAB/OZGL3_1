using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// 상태창 카드 한 장 (상점의 기술·스탯, 스킬 탭의 보유 기술). 시안 B 카드와 같은 모양.
    ///   색 띠 · 아이콘 · 제목 · 부제(유형/설명) · 상세(피해·쿨다운 / 지금→다음) · 단계 칸 · 버튼
    /// 카드 클릭/선택 = Picked, 버튼·Enter·더블클릭 = Action.
    /// </summary>
    [AddComponentMenu("OZ/UI/Status/Growth Card View")]
    public class GrowthCardView : MonoBehaviour,
        IPointerClickHandler, ISelectHandler, ISubmitHandler
    {
        [SerializeField] internal Image background;
        [SerializeField] internal Image selectedMark;
        [SerializeField] internal Image accent;
        [SerializeField] internal Image icon;
        [SerializeField] internal TMP_Text iconLetter;
        [SerializeField] internal TMP_Text titleText;
        [SerializeField] internal TMP_Text subText;
        [SerializeField] internal TMP_Text detailText;
        [SerializeField] internal Image[] pips = new Image[0];
        [SerializeField] internal Button actionButton;
        [SerializeField] internal TMP_Text actionLabel;
        [SerializeField] internal Color normalColor = Color.white;
        [SerializeField] internal Color selectedColor = new Color(0.8f, 0.92f, 1f, 1f);
        [SerializeField] internal Color pipEmptyColor = new Color(1f, 1f, 1f, 0.12f);
        [SerializeField] internal Color actionOffColor = new Color(0.55f, 0.58f, 0.68f, 1f);

        internal int Index { get; set; }
        internal string Key { get; set; }
        internal event Action<GrowthCardView> Picked;
        internal event Action<GrowthCardView> Action;

        void Awake()
        {
            if (actionButton != null) actionButton.onClick.AddListener(() => Action?.Invoke(this));
        }

        /// <summary>
        /// pipCount 0이면 단계 칸 숨김. actionText null이면 버튼 숨김. letter = 아이콘이 없을 때 대신 보일 글자(힘/Q 등)
        /// </summary>
        internal void Set(Sprite sprite, string letter, Color color, string title, string sub, string detail,
            int pipFilled, int pipCount, string actionText, bool actionOn, bool selected)
        {
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }
            if (iconLetter != null)
            {
                iconLetter.text = sprite == null ? letter ?? "" : "";
                iconLetter.color = color;
            }
            if (accent != null) accent.color = color;
            if (titleText != null) { titleText.text = title; titleText.color = color; }
            if (subText != null) subText.text = sub;
            if (detailText != null) detailText.text = detail;
            for (int k = 0; k < pips.Length; k++)
            {
                if (pips[k] == null) continue;
                var back = pips[k].transform.parent;
                bool show = k < pipCount;
                if (back != null && back != transform) back.gameObject.SetActive(show);
                else pips[k].gameObject.SetActive(show);
                pips[k].color = k < pipFilled ? color : pipEmptyColor;
            }
            if (actionButton != null)
            {
                actionButton.gameObject.SetActive(actionText != null);
                if (actionButton.targetGraphic != null) actionButton.targetGraphic.color = actionOn ? Color.white : actionOffColor;
            }
            if (actionLabel != null) actionLabel.text = actionText ?? "";
            if (background != null) background.color = selected ? selectedColor : normalColor;
            if (selectedMark != null) selectedMark.enabled = selected;
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            if (e.clickCount >= 2) Action?.Invoke(this);
            else Picked?.Invoke(this);
        }

        public void OnSelect(BaseEventData e) => Picked?.Invoke(this);
        public void OnSubmit(BaseEventData e) => Action?.Invoke(this);
    }
}
