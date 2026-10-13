using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// 아이콘 위 쿨타임 오버레이 (Q/E/R 슬롯, 1~4 아이템 칸, 버프 아이콘 공용).
    ///   overlay : Image Type=Filled, Radial360 — 남은 비율만큼 덮음
    ///   label   : 남은 초 (선택)
    /// v0.7: 대기 중 아이콘 어둡게(dimTarget) + 도는 경계에 밝은 바늘(sweepHand)이 돌아서 진행이 잘 보임
    ///       완료 → 아이콘이 밝게 두 번 점멸(readyFlash) + Punch + 테두리 번쩍 + 완료음
    /// </summary>
    [AddComponentMenu("OZ/UI/Cooldown Radial")]
    public class CooldownRadial : MonoBehaviour
    {
        [SerializeField] internal Image overlay;
        [SerializeField] internal TMP_Text label;
        [Tooltip("완료 시 Punch 할 대상 (보통 아이콘)")]
        [SerializeField] internal RectTransform punchTarget;
        [Tooltip("완료 시 번쩍일 테두리 (선택)")]
        [SerializeField] internal Graphic flashTarget;
        [SerializeField] internal Color flashColor = Color.white;
        [Tooltip("남은 시간이 이 값 이하면 소수점 1자리 표시")]
        [SerializeField] internal float decimalBelow = 1f;

        [Header("v0.7 대기 중 / 완료 표시")]
        [Tooltip("대기 중 어둡게 할 아이콘")]
        [SerializeField] internal Graphic dimTarget;
        [SerializeField] internal Color dimColor = new Color(0.32f, 0.34f, 0.42f, 1f);
        [Tooltip("남은 영역 경계를 따라 도는 밝은 바늘 (피벗 = 아래 가운데, 칸 가운데에 배치)")]
        [SerializeField] internal RectTransform sweepHand;
        [Tooltip("완료 시 아이콘 위에서 밝게 점멸할 흰 칸")]
        [SerializeField] internal Image readyFlash;
        [SerializeField, Min(1)] internal int readyBlinks = 2;

        Tween _tween;
        Sequence _ready;
        float _remaining;
        bool _dimmed;
        Color _undimmed = Color.white;
        Color _lastDim;

        public bool IsRunning => _tween != null && _tween.IsActive() && _tween.IsPlaying();
        public bool IsDimmed => _dimmed;
        public bool IsReadyFlashing => _ready != null && _ready.IsActive();
        public event Action Completed;

        void Awake()
        {
            if (readyFlash != null) readyFlash.enabled = false;
            Clear();
        }

        /// <summary>remaining초 남은 상태부터 시작 (duration은 전체 길이)</summary>
        public void Play(float remaining, float duration)
        {
            _tween?.Kill();
            if (remaining <= 0f || duration <= 0f) { Clear(); return; }

            _remaining = Mathf.Min(remaining, duration);
            _ready?.Kill();
            if (readyFlash != null) readyFlash.enabled = false;
            Dim(true);
            if (overlay != null)
            {
                overlay.enabled = true;
                overlay.fillAmount = _remaining / duration;
            }
            if (sweepHand != null) sweepHand.gameObject.SetActive(true);
            Apply(_remaining, duration);

            _tween = DOTween.To(() => _remaining, v =>
                {
                    _remaining = v;
                    Apply(v, duration);
                }, 0f, _remaining)
                .SetEase(Ease.Linear)
                .SetUpdate(false) // 쿨타임은 게임 시간 기준 (일시정지 중 멈춤)
                .SetLink(gameObject)
                .OnComplete(Finish);
        }

        void Apply(float v, float duration)
        {
            float n = duration > 0f ? v / duration : 0f;
            if (overlay != null) overlay.fillAmount = n;
            // 오버레이는 위(12시)에서 반시계로 남은 만큼 덮음 → 경계 = 12시에서 반시계 n*360°
            if (sweepHand != null) sweepHand.localEulerAngles = new Vector3(0f, 0f, n * 360f);
            UpdateLabel(v);
        }

        public void Clear()
        {
            _tween?.Kill();
            _remaining = 0f;
            if (overlay != null) { overlay.fillAmount = 0f; overlay.enabled = false; }
            if (label != null) label.text = string.Empty;
            if (sweepHand != null) sweepHand.gameObject.SetActive(false);
            Dim(false);
        }

        /// <summary>아이콘의 평소 색 지정 (사용 가능=흰색, 사용 불가=어둡게). 대기 중이면 끝난 뒤 이 색으로 돌아감</summary>
        public void SetRestColor(Color c)
        {
            if (dimTarget == null) return;
            if (_dimmed) _undimmed = c;
            else dimTarget.color = c;
        }

        void Dim(bool on)
        {
            if (dimTarget == null) return;
            if (on)
            {
                var dim = dimColor; 
                // 처음 어둡게 하거나, 그 사이 다른 코드가 색을 바꿨으면(스킬 칸 Refresh 등) 그 색을 원래 색으로 기억
                if (!_dimmed || dimTarget.color != _lastDim) _undimmed = dimTarget.color;
                dim.a = _undimmed.a;
                dimTarget.color = dim;
                _lastDim = dim;
                _dimmed = true;
            }
            else if (_dimmed)
            {
                _dimmed = false;
                if (dimTarget.color == _lastDim) dimTarget.color = _undimmed;
            }
        }

        void UpdateLabel(float v)
        {
            if (label == null) return;
            label.text = v <= 0f ? string.Empty
                : v < decimalBelow ? v.ToString("0.0")
                : Mathf.CeilToInt(v).ToString();
        }

        [Tooltip("완료 소리 (스킬 칸: SkillReady, 버프 타이머: None)")]
        [SerializeField] internal OZ.UI.Contracts.UISound completeSound = OZ.UI.Contracts.UISound.None;

        void Finish()
        {
            Clear();
            UISfx.Play(completeSound);
            if (punchTarget != null) punchTarget.Punch(0.25f);
            if (flashTarget != null) flashTarget.FlashColor(flashColor, 0.35f);
            if (readyFlash != null)
            {
                _ready?.Kill();
                readyFlash.enabled = true;
                var c = readyFlash.color; c.a = 0f; readyFlash.color = c;
                _ready = DOTween.Sequence();
                for (int i = 0; i < readyBlinks; i++)
                    _ready.Append(readyFlash.DOFade(0.85f, 0.06f)).Append(readyFlash.DOFade(0f, 0.16f));
                _ready.OnComplete(() => readyFlash.enabled = false).SetUpdate(true).SetLink(gameObject);
            }
            Completed?.Invoke();
        }
    }
}
