using System;
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
        [SerializeField] internal GateStatusView gate;
        [SerializeField] internal SkillBarView skills;
        [SerializeField] internal ItemBarView items;
        [SerializeField] internal BuffTrayView buffs;
        [SerializeField] internal HudBannerView banner;

        [Header("Portrait")]
        [Tooltip("HP 바 옆 초상화 칸 안의 이미지")]
        [SerializeField] internal Image portrait;
        [SerializeField] internal Sprite defaultPortrait;

        [Header("Guide / Flash")]
        [SerializeField] internal CanvasGroup guideGroup;
        [SerializeField] internal TMP_Text guideText;
        [SerializeField] internal Image flashOverlay;

        Tween _guideTween;
        bool _visible = true;

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
            if (skills != null) skills.Bind(UISources.Skills);
            if (items != null) items.Bind(UISources.Items);
            if (buffs != null) buffs.Bind(UISources.Items);
            if (banner != null) banner.Bind(UISources.Gate);
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
