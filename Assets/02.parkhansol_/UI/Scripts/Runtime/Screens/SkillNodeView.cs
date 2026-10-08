using System;
using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>스킬 트리 노드 1개 (배경 · 아이콘 · 테두리 · 선택 표시 · 단계 숫자). SkillTreeWindow가 템플릿을 복제해 만든다.</summary>
    [AddComponentMenu("OZ/UI/Screens/Skill Node View")]
    public class SkillNodeView : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        [SerializeField] internal Button button;
        [SerializeField] internal Image background;
        [SerializeField] internal Image icon;
        [SerializeField] internal Image frame;
        [SerializeField] internal Image selector;
        [SerializeField] internal TMP_Text rankText;

        [Header("아이콘 색 (상태별)")]
        [SerializeField] internal Color iconUnlocked = Color.white;
        [SerializeField] internal Color iconAvailable = new Color(0.82f, 0.85f, 0.95f);
        [SerializeField] internal Color iconLocked = new Color(0.32f, 0.34f, 0.42f);
        [SerializeField] internal Color iconBlocked = new Color(0.35f, 0.2f, 0.22f, 0.7f);

        internal SkillTreeNode Node { get; private set; }
        internal SkillNodeState State { get; private set; } = (SkillNodeState)(-1);
        internal RectTransform Rect => (RectTransform)transform;

        internal event Action<SkillNodeView> Hovered;
        internal event Action<SkillNodeView> Clicked;

        void Awake()
        {
            if (button != null) button.onClick.AddListener(() => Clicked?.Invoke(this));
            SetHighlighted(false);
        }

        internal void Bind(SkillTreeNode node, float size, float iconSize)
        {
            Node = node;
            name = "Node_" + node.id;
            Rect.sizeDelta = new Vector2(size, size);
            bool showIcon = node.kind != SkillNodeKind.Upgrade && node.Icon != null;
            if (icon != null)
            {
                icon.sprite = showIcon ? node.Icon : null;
                icon.enabled = showIcon;
                icon.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
            }
            if (background != null) background.rectTransform.sizeDelta = Vector2.one * Mathf.Round(size * 0.66f);
            if (rankText != null) rankText.text = node.kind == SkillNodeKind.Upgrade ? node.grantsRank.ToString() : "";
        }

        internal void SetState(SkillNodeState state, Sprite frameSprite)
        {
            State = state;
            if (frame != null) { frame.sprite = frameSprite; frame.enabled = frameSprite != null; }
            Color c = state == SkillNodeState.Unlocked ? iconUnlocked
                    : state == SkillNodeState.Available ? iconAvailable
                    : state == SkillNodeState.Blocked ? iconBlocked : iconLocked;
            if (icon != null) icon.color = c;
            if (rankText != null) rankText.color = c;
        }

        internal void SetHighlighted(bool on)
        {
            if (selector != null) selector.enabled = on;
        }

        public void OnPointerEnter(PointerEventData eventData) => Hovered?.Invoke(this);
        public void OnSelect(BaseEventData eventData) => Hovered?.Invoke(this);

        internal void PlayUnlocked()
        {
            Rect.Punch(0.25f);
            if (frame != null) frame.FlashColor(Color.white, 0.25f);
            if (icon != null && icon.enabled) icon.FlashColor(new Color(1f, 0.95f, 0.6f), 0.3f);
        }

        internal void PlayDenied() => Rect.Shake(2f, 0.18f);
    }
}
