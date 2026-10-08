using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// 아이콘 위 쿨타임 오버레이 (Q/E/R 슬롯, 버프 아이콘 공용).
    ///   overlay : Image Type=Filled, Radial360, 검정 50% — 남은 비율만큼 덮음
    ///   label   : 남은 초 (선택)
    /// 완료 시 Completed 이벤트 + 아이콘 Punch(쿨다운 완료 애니메이션).
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

        Tween _tween;
        float _remaining;

        public bool IsRunning => _tween != null && _tween.IsActive() && _tween.IsPlaying();
        public event Action Completed;

        void Awake() => Clear();

        /// <summary>remaining초 남은 상태부터 시작 (duration은 전체 길이)</summary>
        public void Play(float remaining, float duration)
        {
            _tween?.Kill();
            if (remaining <= 0f || duration <= 0f) { Clear(); return; }

            _remaining = Mathf.Min(remaining, duration);
            if (overlay != null)
            {
                overlay.enabled = true;
                overlay.fillAmount = _remaining / duration;
            }

            _tween = DOTween.To(() => _remaining, v =>
                {
                    _remaining = v;
                    if (overlay != null) overlay.fillAmount = v / duration;
                    UpdateLabel(v);
                }, 0f, _remaining)
                .SetEase(Ease.Linear)
                .SetUpdate(false) // 쿨타임은 게임 시간 기준 (일시정지 중 멈춤)
                .SetLink(gameObject)
                .OnComplete(Finish);
        }

        public void Clear()
        {
            _tween?.Kill();
            _remaining = 0f;
            if (overlay != null) { overlay.fillAmount = 0f; overlay.enabled = false; }
            if (label != null) label.text = string.Empty;
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
            if (flashTarget != null) flashTarget.FlashColor(flashColor, 0.2f);
            Completed?.Invoke();
        }
    }
}
