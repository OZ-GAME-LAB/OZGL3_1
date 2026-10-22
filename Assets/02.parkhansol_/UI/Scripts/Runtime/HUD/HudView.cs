using System;
using System.Collections.Generic;
using DG.Tweening;
using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// 전투 HUD 루트 + GameUI.HUD 구현.
    /// UISources가 바뀔 때마다 자식 뷰를 다시 연결한다 → 플레이어가 늦게 생성돼도 자동 연결.
    /// </summary>
    [AddComponentMenu("OZ/UI/HUD/Hud View")]
    public class HudView : MonoBehaviour, IHudApi
    {
        [SerializeField] internal CanvasGroup group;
        [SerializeField] internal HealthView health;
        [SerializeField] internal ProgressionView progression;
        [Tooltip("v0.6부터 비움 — 게이트 진행은 퀘스트 목록(quests)이 표시")]
        [SerializeField] internal GateStatusView gate;
        [SerializeField] internal QuestListView quests;
        [SerializeField] internal SystemAlarmView systemAlarm;
        [SerializeField] internal SkillBarView skills;
        [SerializeField] internal ItemBarView items;
        [SerializeField] internal BuffTrayView buffs;
        [SerializeField] internal HudBannerView banner;

        [Header("v0.7")]
        [SerializeField] internal AreaNameView areaName;
        [SerializeField] internal LowHealthView lowHealth;
        [SerializeField] internal HealFlashView healFlash;
        [SerializeField] internal BuffEdgeView buffEdges;
        [SerializeField] internal OffscreenIndicatorView indicators;
        [SerializeField] internal InteractPromptView prompt;

        [Header("Portrait")]
        [Tooltip("HP 바 옆 초상화 칸 안의 이미지")]
        [SerializeField] internal Image portrait;
        [SerializeField] internal Sprite defaultPortrait;

        [Header("v1.3 부위별 숨김 (SetParts)")]
        [Tooltip("우상단 미니맵 틀. 비우면 자식 중 이름이 'Minimap'인 것을 찾는다")]
        [SerializeField] internal RectTransform minimapRoot;
        [Tooltip("부위가 나타날 때 하나씩 띄우는 간격 (초)")]
        [SerializeField, Min(0f)] internal float partStagger = 0.12f;

        [Header("Guide / Flash")]
        [SerializeField] internal CanvasGroup guideGroup;
        [SerializeField] internal TMP_Text guideText;
        [SerializeField] internal Image flashOverlay;

        Tween _guideTween;
        bool _visible = true;
        HudPart _shown = HudPart.All | HudPart.Alarm;
        // 부위 → 덮개 CanvasGroup (부위 루트를 같은 크기의 빈 부모로 감싸서 만든다 — 부위 자체 CanvasGroup과 안 겹치게)
        readonly Dictionary<HudPart, CanvasGroup> _gates = new Dictionary<HudPart, CanvasGroup>();
        static readonly HudPart[] PartOrder =
            { HudPart.Health, HudPart.Minimap, HudPart.Quests, HudPart.Items, HudPart.Skills, HudPart.Buffs, HudPart.Alarm };

        void OnEnable()
        {
            GameUI.Register((IHudApi)this);
            UISources.Changed += Rebind;
            UIState.GameplayInputBlockedChanged += OnBlockedChanged;
            Rebind();
            if (guideGroup != null) guideGroup.alpha = 0f;
            if (flashOverlay != null) { flashOverlay.gameObject.SetActive(false); }
        }

        void OnDisable()
        {
            UISources.Changed -= Rebind;
            UIState.GameplayInputBlockedChanged -= OnBlockedChanged;
            GameUI.Unregister(this);
        }

        void Rebind()
        {
            if (health != null) health.Bind(UISources.Health);
            if (progression != null) progression.Bind(UISources.Progression);
            if (gate != null) gate.Bind(UISources.Gate);
            if (quests != null) quests.BindGate(UISources.Gate);
            if (skills != null) { skills.Bind(UISources.Skills); skills.BindLoadout(UISources.Loadout); }
            if (items != null) items.Bind(UISources.Items);
            if (buffs != null) buffs.Bind(UISources.Items);
            if (banner != null) banner.Bind(UISources.Gate);
            if (lowHealth != null) lowHealth.Bind(UISources.Health);
            if (healFlash != null) healFlash.Bind(UISources.Health);
            if (buffEdges != null) buffEdges.Bind(UISources.Items);
        }

        // 메뉴가 열리면 HUD를 살짝 흐리게
        void OnBlockedChanged(bool blocked)
        {
            if (group == null || !_visible) return;
            group.DOKill();
            group.DOFade(blocked ? 0.35f : 1f, UITweenStyle.Fast).SetUpdate(true).SetLink(gameObject);
        }

        // ── IHudApi ──
        public bool Visible
        {
            get => _visible;
            set
            {
                _visible = value;
                if (group == null) { gameObject.SetActive(value); return; }
                group.DOKill();
                group.DOFade(value ? 1f : 0f, UITweenStyle.Normal).SetUpdate(true).SetLink(gameObject);
            }
        }

        public void ShowGuide(string message, float duration = 3f)
        {
            if (guideText == null || guideGroup == null) return;
            guideText.text = message;
            _guideTween?.Kill();
            guideGroup.alpha = 0f;
            var seq = DOTween.Sequence()
                .Append(guideGroup.DOFade(1f, UITweenStyle.Normal))
                .Join(((RectTransform)guideText.transform).SlideIn(Vector2.up, 8f));
            if (duration > 0f)
                seq.AppendInterval(duration).Append(guideGroup.DOFade(0f, UITweenStyle.Slow));
            _guideTween = seq.SetUpdate(true).SetLink(gameObject);
        }

        public void HideGuide()
        {
            if (guideGroup == null) return;
            _guideTween?.Kill();
            _guideTween = guideGroup.FadeOut();
        }

        public void ShowBanner(string title, string subtitle = null, float holdSeconds = 2f, Action onFinished = null)
        {
            if (banner == null) { onFinished?.Invoke(); return; }
            banner.Show(title, subtitle, holdSeconds, onFinished);
        }

        public void SetPortrait(Sprite sprite)
        {
            if (portrait == null) return;
            portrait.sprite = sprite != null ? sprite : defaultPortrait;
            portrait.enabled = portrait.sprite != null;
            if (portrait.sprite != null) portrait.SetNativeSize(); // 픽셀 1:1 유지
        }

        // ── v1.3 부위별 숨김 ──

        public HudPart VisibleParts => _shown;

        public void SetParts(HudPart parts, bool visible, bool animate = true)
        {
            int order = 0;
            foreach (var part in PartOrder)
            {
                if ((parts & part) == 0) continue;
                bool was = (_shown & part) != 0;
                if (visible) _shown |= part; else _shown &= ~part;
                var gate = Gate(part);
                if (gate == null) continue;
                gate.DOKill();
                float target = visible ? 1f : 0f;
                if (!animate || !isActiveAndEnabled) { gate.alpha = target; continue; }
                if (was == visible && Mathf.Approximately(gate.alpha, target)) continue;
                var t = gate.DOFade(target, visible ? UITweenStyle.Slow : UITweenStyle.Normal).SetUpdate(true).SetLink(gate.gameObject);
                if (visible)
                {
                    t.SetDelay(order * partStagger);
                    if (gate.transform.childCount > 0 && gate.transform.GetChild(0) is RectTransform child)
                        child.SlideIn(Vector2.down, 6f, UITweenStyle.Slow).SetDelay(order * partStagger); // 살짝 내려앉으며 등장
                    order++;
                }
            }
        }

        internal RectTransform PartRoot(HudPart part)
        {
            switch (part)
            {
                case HudPart.Health: return health != null ? health.transform as RectTransform : null;
                case HudPart.Minimap: return minimapRoot != null ? minimapRoot : (minimapRoot = transform.Find("Minimap") as RectTransform);
                case HudPart.Items: return items != null ? items.transform as RectTransform : null;
                case HudPart.Quests: return quests != null ? quests.transform as RectTransform : null;
                case HudPart.Skills: return skills != null ? skills.transform as RectTransform : null;
                case HudPart.Buffs: return buffs != null ? buffs.transform as RectTransform : null;
                case HudPart.Alarm: return systemAlarm != null ? systemAlarm.transform as RectTransform : null;
                default: return null;
            }
        }

        /// <summary>부위를 감싼 덮개 (처음 부를 때 만든다). 덮개는 부모를 꽉 채우므로 부위 배치는 그대로</summary>
        internal CanvasGroup Gate(HudPart part)
        {
            if (_gates.TryGetValue(part, out var g) && g != null) return g;
            var root = PartRoot(part);
            if (root == null || root.parent == null) return null;
            var go = new GameObject("PartGate_" + part, typeof(RectTransform));
            go.layer = root.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(root.parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.SetSiblingIndex(root.GetSiblingIndex());
            root.SetParent(rt, false);
            g = go.AddComponent<CanvasGroup>();
            g.interactable = false;
            g.blocksRaycasts = false;
            g.alpha = (_shown & part) != 0 ? 1f : 0f;
            _gates[part] = g;
            return g;
        }

        public void Flash(Color color, float duration = 0.15f)
        {
            if (flashOverlay == null) return;
            flashOverlay.DOKill();
            flashOverlay.gameObject.SetActive(true);
            color.a = Mathf.Clamp01(color.a <= 0f ? 0.6f : color.a);
            flashOverlay.color = color;
            flashOverlay.DOFade(0f, duration).SetUpdate(true).SetLink(flashOverlay.gameObject)
                .OnComplete(() => flashOverlay.gameObject.SetActive(false));
        }
    }
}
