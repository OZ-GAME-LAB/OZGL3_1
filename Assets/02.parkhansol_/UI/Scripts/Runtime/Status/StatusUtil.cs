using System;
using System.Collections.Generic;
using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>템플릿 카드를 필요한 만큼 복제하고 격자 이동(←→↑↓)을 이어 주는 도우미</summary>
    internal sealed class CardPool
    {
        readonly GrowthCardView _template;
        readonly Transform _parent;
        readonly List<GrowthCardView> _cards = new List<GrowthCardView>();
        public int Columns = 3;
        public Action<GrowthCardView> OnPicked, OnAction;

        public CardPool(GrowthCardView template, Transform parent)
        {
            _template = template;
            _parent = parent;
            if (_template != null) _template.gameObject.SetActive(false);
        }

        public IReadOnlyList<GrowthCardView> Cards => _cards;

        public void Ensure(int count)
        {
            if (_template == null || _parent == null) return;
            while (_cards.Count < count)
            {
                var c = UnityEngine.Object.Instantiate(_template, _parent);
                c.Index = _cards.Count;
                c.Picked += v => OnPicked?.Invoke(v);
                c.Action += v => OnAction?.Invoke(v);
                _cards.Add(c);
            }
            for (int i = 0; i < _cards.Count; i++) _cards[i].gameObject.SetActive(i < count);
            Wire(count);
        }

        void Wire(int count)
        {
            int cols = Mathf.Max(1, Columns);
            for (int i = 0; i < count; i++)
            {
                var sel = _cards[i].GetComponent<Selectable>();
                if (sel == null) continue;
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                int col = i % cols;
                if (col > 0) nav.selectOnLeft = _cards[i - 1].GetComponent<Selectable>();
                if (col < cols - 1 && i + 1 < count) nav.selectOnRight = _cards[i + 1].GetComponent<Selectable>();
                if (i - cols >= 0) nav.selectOnUp = _cards[i - cols].GetComponent<Selectable>();
                if (i + cols < count) nav.selectOnDown = _cards[i + cols].GetComponent<Selectable>();
                sel.navigation = nav;
                if (_cards[i].actionButton != null) _cards[i].actionButton.navigation = new Navigation { mode = Navigation.Mode.None };
            }
        }

        public void Focus(int index)
        {
            if (index < 0 || index >= _cards.Count || !_cards[index].gameObject.activeSelf) return;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_cards[index].gameObject);
        }

        public void Punch(int index)
        {
            if (index >= 0 && index < _cards.Count) ((RectTransform)_cards[index].transform).Punch(0.12f);
        }

        public void Shake(int index)
        {
            if (index >= 0 && index < _cards.Count) ((RectTransform)_cards[index].transform).Shake(3f, 0.2f);
        }
    }

    internal static class StatusColors
    {
        public static readonly Color Physical = new Color(1f, 0.62f, 0.3f);
        public static readonly Color Magic = new Color(0.55f, 0.72f, 1f);
        public static readonly Color Dim = new Color(0.65f, 0.7f, 0.82f);

        public static Color Stat(StatKind s)
        {
            switch (s)
            {
                case StatKind.Strength: return new Color(1f, 0.5f, 0.45f);
                case StatKind.Vitality: return new Color(0.5f, 1f, 0.6f);
                case StatKind.Agility: return new Color(1f, 0.88f, 0.4f);
                default: return new Color(0.55f, 0.72f, 1f);
            }
        }

        public static Color Skill(SkillDamageKind k) => k == SkillDamageKind.Magic ? Magic : Physical;
    }
}
