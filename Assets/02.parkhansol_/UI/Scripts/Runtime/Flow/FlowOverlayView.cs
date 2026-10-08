using System;
using System.Threading.Tasks;
using DG.Tweening;
using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// GameUI.Flow 구현: 로딩 화면 · 화면 페이드 · 스테이지 시작 띠. Overlay 레이어(맨 위)에 있다.
    ///   그리는 순서 (아래 → 위): 스테이지 띠 → 페이드 → 로딩
    ///   로딩·페이드 중에는 UIState.IsGameplayInputBlocked = true, 마우스 클릭도 막음.
    /// </summary>
    [AddComponentMenu("OZ/UI/Flow Overlay View")]
    public class FlowOverlayView : MonoBehaviour, IFlowApi
    {
        [Header("페이드")]
        [SerializeField] internal CanvasGroup fadeGroup;

        [Header("로딩")]
        [SerializeField] internal CanvasGroup loadingGroup;
        [SerializeField] internal TMP_Text loadingTitle;
        [SerializeField] internal TMP_Text loadingMessage;
        [SerializeField] internal TMP_Text loadingPercent;
        [SerializeField] internal Image loadingFill;
        [SerializeField] internal RectTransform loadingRunner;
        [SerializeField] internal TMP_Text loadingTip;
        [SerializeField] internal string[] tips =
        {
            "게이트를 모두 봉쇄하면 보스 구역이 열린다.",
            "K 키로 스킬 트리를 열 수 있다. Q·E·R마다 검술과 마법 중 하나를 고른다.",
            "체력이 낮을 때는 1~4 키로 회복약을 쓰자.",
            "치명타는 금색 섬광과 큰 숫자로 표시된다.",
            "Tab 키로 지도를 열어 다음 게이트 위치를 확인하자.",
        };
        [SerializeField, Min(0.01f)] internal float progressSpeed = 1.6f; // 초당 채워지는 최대 비율 (갑자기 튀지 않게)

        [Header("스테이지 시작 띠")]
        [SerializeField] internal CanvasGroup introGroup;
        [SerializeField] internal RectTransform introBand;
        [SerializeField] internal TMP_Text introStage;
        [SerializeField] internal TMP_Text introTitle;
        [SerializeField] internal TMP_Text introSubtitle;
        [SerializeField] internal RectTransform introLineLeft;
        [SerializeField] internal RectTransform introLineRight;
        [SerializeField, Min(0.1f)] internal float introHold = 1.6f;

        // 상태
        bool _loading, _hidingLoading, _closingLoading;
        float _shownProgress, _targetProgress;
        Action _onLoadingHidden;
        Tween _fadeTween, _loadingTween;
        Action _pendingFade;
        Sequence _introSeq;
        Action _introPending;
        (int stage, string title, string sub, Action done)? _queuedIntro;
        float _dotsTimer;
        int _dots, _tipIndex = -1;
        float _lineWidth;

        public bool IsLoading => _loading;
        public bool IsFaded => fadeGroup != null && fadeGroup.alpha > 0.01f;
        public bool IsIntroPlaying => _introSeq != null && _introSeq.IsActive();
        public float ShownProgress => _shownProgress;

        void Awake()
        {
            if (fadeGroup != null) SetGroup(fadeGroup, 0f);
            if (loadingGroup != null) SetGroup(loadingGroup, 0f);
            if (introGroup != null) { introGroup.alpha = 0f; introGroup.blocksRaycasts = false; introGroup.interactable = false; }
            if (introLineLeft != null) _lineWidth = introLineLeft.sizeDelta.x;
        }

        void OnEnable() => GameUI.Register((IFlowApi)this);

        void OnDisable()
        {
            GameUI.Unregister(this);
            UIState.SetFlowBlocked(false);
        }

        void OnDestroy()
        {
            _fadeTween?.Kill();
            _loadingTween?.Kill();
            _introSeq?.Kill();
        }

        static void SetGroup(CanvasGroup g, float a)
        {
            g.alpha = a;
            g.blocksRaycasts = a > 0.01f;
            g.interactable = false;
        }

        // ───────────── 로딩 ─────────────

        public void ShowLoading(string message = null)
        {
            if (loadingGroup == null) return;
            bool wasHidden = !_loading || _hidingLoading || _closingLoading;
            _loading = true;
            _hidingLoading = false;
            _closingLoading = false;
            FlushLoadingHidden();
            if (wasHidden)
            {
                _shownProgress = _targetProgress = 0f;
                PickTip();
            }
            if (message != null && loadingMessage != null) loadingMessage.text = message;
            else if (wasHidden && loadingMessage != null) loadingMessage.text = "";
            RefreshProgress();

            _loadingTween?.Kill();
            loadingGroup.blocksRaycasts = true;
            _loadingTween = loadingGroup.DOFade(1f, 0.2f).SetUpdate(true).SetLink(gameObject);
            UpdateBlock();
        }

        public void SetLoadingProgress(float progress01, string message = null)
        {
            if (!_loading) ShowLoading(message);
            _targetProgress = Mathf.Max(_targetProgress, Mathf.Clamp01(progress01)); // 뒤로 가지 않음
            if (message != null && loadingMessage != null) loadingMessage.text = message;
        }

        public void HideLoading(Action onHidden = null)
        {
            if (!_loading || loadingGroup == null) { onHidden?.Invoke(); return; }
            FlushLoadingHidden();
            _onLoadingHidden = onHidden;
            _targetProgress = 1f;
            _hidingLoading = true; // 100%가 화면에 보인 뒤 Update에서 사라짐
        }

        void FinishHideLoading()
        {
            _hidingLoading = false;
            _closingLoading = true;
            UISfx.Play(UISound.LoadDone);
            _loadingTween?.Kill();
            _loadingTween = loadingGroup.DOFade(0f, 0.3f).SetDelay(0.12f).SetUpdate(true).SetLink(gameObject)
                .OnComplete(() =>
                {
                    _loading = false;
                    _closingLoading = false;
                    loadingGroup.blocksRaycasts = false;
                    UpdateBlock();
                    FlushLoadingHidden();
                    TryPlayQueuedIntro();
                });
        }

        void FlushLoadingHidden()
        {
            var cb = _onLoadingHidden;
            _onLoadingHidden = null;
            cb?.Invoke();
        }

        void PickTip()
        {
            if (loadingTip == null) return;
            if (tips == null || tips.Length == 0) { loadingTip.text = ""; return; }
            int next = UnityEngine.Random.Range(0, tips.Length);
            if (tips.Length > 1 && next == _tipIndex) next = (next + 1) % tips.Length;
            _tipIndex = next;
            loadingTip.text = "TIP  " + tips[next];
        }

        void RefreshProgress()
        {
            if (loadingFill != null) loadingFill.fillAmount = _shownProgress;
            if (loadingPercent != null) loadingPercent.text = Mathf.FloorToInt(_shownProgress * 100f + 0.001f) + "%";
            if (loadingRunner != null && loadingFill != null)
            {
                var bar = loadingFill.rectTransform;
                float w = bar.rect.width;
                loadingRunner.anchoredPosition = new Vector2(Mathf.Round(bar.anchoredPosition.x - w * 0.5f + w * _shownProgress), loadingRunner.anchoredPosition.y);
            }
        }

        void Update()
        {
            if (!_loading) return;
            float dt = Time.unscaledDeltaTime;
            if (_shownProgress < _targetProgress)
            {
                _shownProgress = Mathf.MoveTowards(_shownProgress, _targetProgress, progressSpeed * dt);
                RefreshProgress();
            }
            if (_hidingLoading && _shownProgress >= 1f) FinishHideLoading();

            // "LOADING..." 점 애니메이션
            _dotsTimer += dt;
            if (_dotsTimer >= 0.35f && loadingTitle != null)
            {
                _dotsTimer = 0f;
                _dots = (_dots + 1) % 4;
                loadingTitle.text = "LOADING" + new string('.', _dots);
            }
            if (loadingRunner != null)
                loadingRunner.localEulerAngles = new Vector3(0, 0, (Mathf.Floor(Time.unscaledTime * 8f) % 4) * -90f);
        }

        // ───────────── 페이드 ─────────────

        public void FadeOut(float duration = 0.35f, Action onBlack = null) => FadeTo(1f, duration, onBlack);
        public void FadeIn(float duration = 0.35f, Action onClear = null) => FadeTo(0f, duration, () => { onClear?.Invoke(); TryPlayQueuedIntro(); });

        void FadeTo(float alpha, float duration, Action done)
        {
            if (fadeGroup == null) { done?.Invoke(); return; }
            _fadeTween?.Kill();
            FlushFade(); // 이전 페이드가 끊기면 그 콜백은 바로 실행 (교체 작업이 빠지지 않게)
            _pendingFade = done;
            fadeGroup.blocksRaycasts = true;
            UpdateBlock(true);
            if (duration <= 0f || Mathf.Approximately(fadeGroup.alpha, alpha))
            {
                fadeGroup.alpha = alpha;
                OnFadeDone(alpha);
                return;
            }
            _fadeTween = fadeGroup.DOFade(alpha, duration).SetEase(Ease.Linear).SetUpdate(true).SetLink(gameObject)
                .OnComplete(() => OnFadeDone(alpha));
        }

        void OnFadeDone(float alpha)
        {
            _fadeTween = null;
            fadeGroup.blocksRaycasts = alpha > 0.01f;
            UpdateBlock();
            FlushFade();
        }

        void FlushFade()
        {
            var cb = _pendingFade;
            _pendingFade = null;
            cb?.Invoke();
        }

        public void Transition(Action whileBlack, float duration = 0.35f, Action onFinished = null)
        {
            FadeOut(duration, () =>
            {
                try { whileBlack?.Invoke(); }
                catch (Exception e) { Debug.LogException(e); } // 교체 중 예외가 나도 화면이 검은 채로 남지 않게
                FadeIn(duration, onFinished);
            });
        }

        public Task FadeOutAsync(float duration = 0.35f)
        {
            var tcs = new TaskCompletionSource<bool>();
            FadeOut(duration, () => tcs.TrySetResult(true));
            return tcs.Task;
        }

        public Task FadeInAsync(float duration = 0.35f)
        {
            var tcs = new TaskCompletionSource<bool>();
            FadeIn(duration, () => tcs.TrySetResult(true));
            return tcs.Task;
        }

        public async Task TransitionAsync(Func<Task> whileBlack, float duration = 0.35f)
        {
            await FadeOutAsync(duration);
            try { if (whileBlack != null) await whileBlack(); }
            finally { await FadeInAsync(duration); }
        }

        void UpdateBlock(bool forceOn = false)
        {
            bool fading = _fadeTween != null && _fadeTween.IsActive() && _fadeTween.IsPlaying();
            UIState.SetFlowBlocked(forceOn || _loading || fading || IsFaded);
        }

        // ───────────── 스테이지 시작 띠 ─────────────

        public void StageIntro(int stageNumber, string title, string subtitle = null, Action onFinished = null)
        {
            if (introGroup == null || introBand == null) { onFinished?.Invoke(); return; }
            // 화면이 덮여 있으면 걷힌 뒤에 (페이드 뒤에 깔려서 안 보이는 일 없게)
            if (_loading || (fadeGroup != null && fadeGroup.alpha > 0.01f) || _pendingFade != null)
            {
                if (_queuedIntro.HasValue) _queuedIntro.Value.done?.Invoke();
                _queuedIntro = (stageNumber, title, subtitle, onFinished);
                return;
            }
            PlayIntro(stageNumber, title, subtitle, onFinished);
        }

        void TryPlayQueuedIntro()
        {
            if (!_queuedIntro.HasValue || _loading || IsFaded) return;
            var q = _queuedIntro.Value;
            _queuedIntro = null;
            PlayIntro(q.stage, q.title, q.sub, q.done);
        }

        void PlayIntro(int stageNumber, string title, string subtitle, Action onFinished)
        {
            FlushIntro();
            _introPending = onFinished;
            UISfx.Play(UISound.StageIntro);
            if (introStage != null) introStage.text = stageNumber > 0 ? $"STAGE {stageNumber}" : "";
            if (introTitle != null) introTitle.text = title ?? "";
            if (introSubtitle != null)
            {
                introSubtitle.text = subtitle ?? "";
                introSubtitle.gameObject.SetActive(!string.IsNullOrEmpty(subtitle));
            }

            _introSeq?.Kill();
            introGroup.alpha = 0f;
            introBand.localScale = new Vector3(1f, 0.2f, 1f);
            if (introLineLeft != null) introLineLeft.sizeDelta = new Vector2(0f, introLineLeft.sizeDelta.y);
            if (introLineRight != null) introLineRight.sizeDelta = new Vector2(0f, introLineRight.sizeDelta.y);
            var stageRt = introStage != null ? (RectTransform)introStage.transform : null;
            Vector2 stagePos = stageRt != null ? stageRt.anchoredPosition : Vector2.zero;
            if (stageRt != null) stageRt.anchoredPosition = stagePos + new Vector2(0f, 6f);

            var seq = DOTween.Sequence()
                .Append(introGroup.DOFade(1f, 0.2f))
                .Join(introBand.DOScaleY(1f, 0.25f).SetEase(Ease.OutBack));
            if (introLineLeft != null) seq.Join(introLineLeft.DOSizeDelta(new Vector2(_lineWidth, introLineLeft.sizeDelta.y), 0.4f).SetEase(Ease.OutCubic));
            if (introLineRight != null) seq.Join(introLineRight.DOSizeDelta(new Vector2(_lineWidth, introLineRight.sizeDelta.y), 0.4f).SetEase(Ease.OutCubic));
            if (stageRt != null) seq.Join(stageRt.DOAnchorPos(stagePos, 0.3f).SetEase(Ease.OutQuad));
            seq.AppendCallback(() => { if (introTitle != null) ((RectTransform)introTitle.transform).Punch(0.12f, 0.25f); })
                .AppendInterval(introHold)
                .Append(introGroup.DOFade(0f, 0.4f))
                .Join(introBand.DOScaleY(0.2f, 0.4f).SetEase(Ease.InQuad))
                .OnComplete(FlushIntro)
                .OnKill(() => { if (stageRt != null) stageRt.anchoredPosition = stagePos; })
                .SetUpdate(true)
                .SetLink(gameObject);
            _introSeq = seq;
        }

        void FlushIntro()
        {
            var cb = _introPending;
            _introPending = null;
            cb?.Invoke();
        }

        /// <summary>진행 중인 모든 흐름 연출을 즉시 끝냄 (테스트·씬 리셋용). 남은 콜백은 모두 실행.</summary>
        public void ResetAll()
        {
            _fadeTween?.Kill();
            _loadingTween?.Kill();
            _introSeq?.Kill();
            if (fadeGroup != null) SetGroup(fadeGroup, 0f);
            if (loadingGroup != null) SetGroup(loadingGroup, 0f);
            if (introGroup != null) introGroup.alpha = 0f;
            _loading = _hidingLoading = _closingLoading = false;
            _fadeTween = null;
            FlushFade();
            FlushLoadingHidden();
            FlushIntro();
            if (_queuedIntro.HasValue) { var q = _queuedIntro.Value; _queuedIntro = null; q.done?.Invoke(); }
            UpdateBlock();
        }
    }
}
