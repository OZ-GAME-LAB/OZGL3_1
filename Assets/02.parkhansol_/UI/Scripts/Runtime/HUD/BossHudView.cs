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
    /// 보스 체력바 + 가벼운 보스전 연출 (GameUI.Boss 구현). Timeline 없이 DOTween Sequence만 사용.
    ///   등장: 레터박스 → 이름 배너 → 바 0→100% 채움 → 배너/레터박스 퇴장 → onIntroFinished
    ///   피격: 잔상 감소 + 미세 흔들림 / 큰 피해(10%↑): 화면 플래시
    ///   페이즈: 눈금 + 강한 흔들림 + 이름 재표시 / 처치: 흰 플래시 → 페이드 + "격파" 배너
    /// </summary>
    [AddComponentMenu("OZ/UI/HUD/Boss Hud View")]
    public class BossHudView : MonoBehaviour, IBossApi
    {
        [SerializeField] internal CanvasGroup barGroup;
        [SerializeField] internal RectTransform barRoot;
        [SerializeField] internal TweenFillBar bar;
        [SerializeField] internal TMP_Text barNameText;
        [SerializeField] internal RectTransform phaseTickTemplate;

        [Header("Intro")]
        [SerializeField] internal RectTransform letterboxTop;
        [SerializeField] internal RectTransform letterboxBottom;
        [SerializeField] internal float letterboxHeight = 36f;
        [SerializeField] internal CanvasGroup bannerGroup;
        [SerializeField] internal RectTransform bannerRoot;
        [SerializeField] internal TMP_Text bannerName;
        [SerializeField] internal TMP_Text bannerTitle;

        [Header("Feedback")]
        [SerializeField] internal Graphic barFlashTarget;
        [SerializeField, Range(0f, 1f)] internal float bigHitRatio = 0.1f;

        IBossSource _boss;
        Sequence _seq;
        float _lastHP;
        readonly List<GameObject> _ticks = new List<GameObject>();

        public bool IsShowing => _boss != null;

        void Awake()
        {
            if (phaseTickTemplate != null) phaseTickTemplate.gameObject.SetActive(false);
            HideImmediate();
        }

        [Header("Layer")]
        [Tooltip("켜면 상시 체력바를 HUD 레이어로 옮긴다. 인벤토리·스킬·지도 창(Screen 레이어)이 열리면 창 아래로 가려진다. 등장 레터박스·배너는 Cinematic 레이어에 그대로 둔다.")]
        [SerializeField] internal bool barOnHudLayer = true;

        void Start() => MoveBarToHudLayer();

        /// <summary>BossHUD 오브젝트는 Cinematic(30) 레이어에 있어 Screen(10) 창 위에 그려진다 → 상시 바만 HUD(0) 레이어로 분리.</summary>
        void MoveBarToHudLayer()
        {
            if (!barOnHudLayer || barRoot == null || UIManager.Instance == null) return;
            Canvas hud = UIManager.Instance.GetLayer(UILayer.HUD);
            if (hud == null || barRoot.parent == hud.transform) return;
            barRoot.SetParent(hud.transform, false);
            barRoot.SetAsLastSibling();
        }

        void OnEnable() => GameUI.Register((IBossApi)this);
        void OnDisable() { GameUI.Unregister(this); Unsubscribe(); }

        public void Show(IBossSource boss, Action onIntroFinished = null)
        {
            if (boss == null) { onIntroFinished?.Invoke(); return; }
            Unsubscribe();
            _boss = boss;
            _boss.HPChanged += OnHPChanged;
            _boss.PhaseChanged += OnPhaseChanged;
            _boss.Defeated += OnDefeated;
            _lastHP = boss.HP;

            BossData data = boss.Data;
            string displayName = data != null ? data.displayName : "BOSS";
            if (barNameText != null) barNameText.text = displayName;
            if (bannerName != null) bannerName.text = displayName;
            if (bannerTitle != null) bannerTitle.text = data != null ? data.title : "";
            BuildTicks(data);

            float ratio = boss.MaxHP > 0f ? boss.HP / boss.MaxHP : 1f;
            bool intro = data == null || data.playIntro;
            bool letterbox = data == null || data.useLetterbox;
            float hold = data != null ? data.bannerHold : 1f;
            float fill = data != null ? data.barFillDuration : 0.8f;

            _seq?.Kill();
            gameObject.SetActive(true);
            if (barGroup != null) barGroup.alpha = 0f;

            if (!intro)
            {
                if (barGroup != null) barGroup.alpha = 1f;
                bar?.SetValue(ratio, false);
                onIntroFinished?.Invoke();
                return;
            }

            _seq = DOTween.Sequence();
            if (letterbox) _seq.Append(Letterbox(true, 0.3f));
            if (bannerGroup != null)
            {
                bannerGroup.alpha = 0f;
                _seq.Insert(0.2f, bannerGroup.DOFade(1f, 0.25f));
                if (bannerRoot != null)
                {
                    Vector2 p = bannerRoot.anchoredPosition;
                    bannerRoot.anchoredPosition = p + Vector2.left * 60f;
                    _seq.Insert(0.2f, bannerRoot.DOAnchorPos(p, 0.35f).SetEase(Ease.OutCubic));
                }
            }
            if (barGroup != null) _seq.Insert(0.8f, barGroup.DOFade(1f, 0.2f));
            _seq.InsertCallback(0.8f, () => bar?.FillFromZero(ratio, fill));
            float end = 0.8f + fill;
            if (barRoot != null) _seq.InsertCallback(end, () => barRoot.Punch(0.12f));
            float outAt = Mathf.Max(end, 0.2f + hold);
            if (bannerGroup != null) _seq.Insert(outAt, bannerGroup.DOFade(0f, 0.25f));
            if (letterbox) _seq.Insert(outAt, Letterbox(false, 0.3f));
            _seq.InsertCallback(outAt + 0.3f, () => onIntroFinished?.Invoke());
            _seq.SetUpdate(true).SetLink(gameObject);
        }

        public void Hide()
        {
            Unsubscribe();
            _seq?.Kill();
            if (barGroup == null) { HideImmediate(); return; }
            _seq = DOTween.Sequence().Append(barGroup.DOFade(0f, 0.3f)).OnComplete(HideImmediate)
                .SetUpdate(true).SetLink(gameObject);
        }

        void HideImmediate()
        {
            if (barGroup != null) barGroup.alpha = 0f;
            if (bannerGroup != null) bannerGroup.alpha = 0f;
            SetLetterbox(0f);
        }

        void Unsubscribe()
        {
            if (_boss == null) return;
            _boss.HPChanged -= OnHPChanged;
            _boss.PhaseChanged -= OnPhaseChanged;
            _boss.Defeated -= OnDefeated;
            _boss = null;
        }

        void OnHPChanged(float hp, float max)
        {
            float n = max > 0f ? hp / max : 0f;
            bar?.SetValue(n, true);
            float dmgRatio = max > 0f ? (_lastHP - hp) / max : 0f;
            _lastHP = hp;
            if (dmgRatio <= 0f) return;
            if (barRoot != null) barRoot.Shake(dmgRatio >= bigHitRatio ? 4f : 1.5f, 0.15f);
            if (dmgRatio >= bigHitRatio) GameUI.HUD.Flash(new Color(1f, 1f, 1f, 0.35f), 0.12f);
        }

        void OnPhaseChanged(int phase)
        {
            if (barRoot != null) barRoot.Shake(6f, 0.4f);
            if (barFlashTarget != null) barFlashTarget.FlashColor(new Color(1f, 0.85f, 0.3f), 0.4f);
            if (bannerGroup != null)
            {
                DOTween.Sequence().Append(bannerGroup.DOFade(1f, 0.2f)).AppendInterval(0.8f)
                    .Append(bannerGroup.DOFade(0f, 0.3f)).SetUpdate(true).SetLink(gameObject);
            }
        }

        void OnDefeated()
        {
            var boss = _boss;
            Unsubscribe();
            if (barFlashTarget != null) barFlashTarget.FlashColor(Color.white, 0.3f);
            GameUI.HUD.Flash(new Color(1f, 1f, 1f, 0.6f), 0.4f);
            if (bannerName != null) bannerName.text = "격파";
            if (bannerTitle != null) bannerTitle.text = boss?.Data != null ? boss.Data.displayName : "";
            _seq?.Kill();
            _seq = DOTween.Sequence();
            if (bannerGroup != null) _seq.Append(bannerGroup.DOFade(1f, 0.25f)).AppendInterval(1.2f).Append(bannerGroup.DOFade(0f, 0.4f));
            if (barGroup != null) _seq.Insert(0.4f, barGroup.DOFade(0f, 0.6f));
            _seq.SetUpdate(true).SetLink(gameObject);
        }

        void BuildTicks(BossData data)
        {
            foreach (var t in _ticks) if (t != null) Destroy(t);
            _ticks.Clear();
            if (phaseTickTemplate == null || data == null || data.phaseThresholds == null) return;
            foreach (float threshold in data.phaseThresholds)
            {
                var tick = Instantiate(phaseTickTemplate, phaseTickTemplate.parent);
                tick.gameObject.SetActive(true);
                tick.anchorMin = tick.anchorMax = new Vector2(Mathf.Clamp01(threshold), 0.5f);
                tick.anchoredPosition = Vector2.zero;
                _ticks.Add(tick.gameObject);
            }
        }

        Tween Letterbox(bool show, float duration)
        {
            float from = show ? 0f : letterboxHeight;
            float to = show ? letterboxHeight : 0f;
            float h = from;
            return DOTween.To(() => h, v => { h = v; SetLetterbox(v); }, to, duration).SetEase(Ease.OutCubic);
        }

        void SetLetterbox(float height)
        {
            if (letterboxTop != null) letterboxTop.sizeDelta = new Vector2(letterboxTop.sizeDelta.x, height);
            if (letterboxBottom != null) letterboxBottom.sizeDelta = new Vector2(letterboxBottom.sizeDelta.x, height);
        }
    }
}
