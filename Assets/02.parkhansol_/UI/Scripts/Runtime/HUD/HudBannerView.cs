using System;
using DG.Tweening;
using OZ.UI.Contracts;
using TMPro;
using UnityEngine;

namespace OZ.UI
{
    /// <summary>
    /// 화면 가운데 큰 띠 문구 ("게이트 파괴" 등).
    ///   등장: 띠가 가로로 펼쳐지며 페이드 인 → holdSeconds 유지 → 위로 살짝 올라가며 사라짐.
    ///   게이트 봉쇄(IGateSource.GateSealed) 시 자동으로 "게이트 파괴"를 띄운다.
    ///   게임 시간을 멈추지 않으며, 일시정지 중에도 연출은 진행(unscaled).
    /// </summary>
    [AddComponentMenu("OZ/UI/HUD/Hud Banner View")]
    public class HudBannerView : MonoBehaviour
    {
        [SerializeField] internal CanvasGroup group;
        [SerializeField] internal RectTransform band;
        [SerializeField] internal TMP_Text titleText;
        [SerializeField] internal TMP_Text subtitleText;

        [Header("게이트 자동 표시")]
        [SerializeField] internal bool showOnGateSealed = true;
        [SerializeField] internal string gateTitle = "게이트 파괴";
        [SerializeField, Min(0.1f)] internal float gateHold = 2f;

        [Header("연출 시간")]
        [SerializeField, Min(0f)] internal float fadeIn = 0.25f;
        [SerializeField, Min(0f)] internal float fadeOut = 0.45f;

        IGateSource _gate;
        Sequence _seq;
        Action _pending;
        Vector2 _bandPos;
        bool _cached;

        public bool IsShowing => _seq != null && _seq.IsActive();

        void Awake() => Cache();

        void Cache()
        {
            if (_cached) return;
            _cached = true;
            if (band != null) _bandPos = band.anchoredPosition;
            if (group != null) group.alpha = 0f;
        }

        internal void Bind(IGateSource gate)
        {
            if (_gate != null) _gate.GateSealed -= OnGateSealed;
            _gate = gate;
            if (_gate != null) _gate.GateSealed += OnGateSealed;
        }

        void OnDestroy()
        {
            if (_gate != null) _gate.GateSealed -= OnGateSealed;
            _seq?.Kill();
        }

        void OnGateSealed(int sealedCount, int target)
        {
            if (!showOnGateSealed) return;
            string sub = target > 0 && sealedCount >= target ? $"{sealedCount} / {target}  ·  보스 구역 개방" : $"{sealedCount} / {target}";
            Show(gateTitle, sub, gateHold);
        }

        /// <summary>띠 표시. 이미 떠 있으면 내용만 바꿔 처음부터 다시 (이전 콜백은 바로 실행)</summary>
        public void Show(string title, string subtitle, float holdSeconds, Action onFinished = null)
        {
            Cache();
            FlushPending();
            _pending = onFinished;

            if (titleText != null) titleText.text = title ?? "";
            if (subtitleText != null)
            {
                subtitleText.text = subtitle ?? "";
                subtitleText.gameObject.SetActive(!string.IsNullOrEmpty(subtitle));
            }

            _seq?.Kill();
            if (group == null || band == null) { FlushPending(); return; }

            group.alpha = 0f;
            band.anchoredPosition = _bandPos;
            band.localScale = new Vector3(0.4f, 1f, 1f);

            _seq = DOTween.Sequence()
                .Append(group.DOFade(1f, fadeIn))
                .Join(band.DOScaleX(1f, fadeIn + 0.1f).SetEase(Ease.OutBack))
                .AppendCallback(() => { if (titleText != null) ((RectTransform)titleText.transform).Punch(0.15f, 0.25f); })
                .AppendInterval(Mathf.Max(0.1f, holdSeconds))
                .Append(group.DOFade(0f, fadeOut))
                .Join(band.DOAnchorPosY(_bandPos.y + 10f, fadeOut).SetEase(Ease.InQuad))
                .OnComplete(FlushPending)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        public void Hide()
        {
            _seq?.Kill();
            if (group != null) group.alpha = 0f;
            FlushPending();
        }

        void FlushPending()
        {
            var cb = _pending;
            _pending = null;
            cb?.Invoke();
        }
    }
}
