using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// 메인 fill + 잔상(trail) fill 바. 체력 통, 경험치 바, 보스 체력바 공용.
    /// 에셋 ValueBar 대신 사용 — 에셋 프리팹의 Fill/FollowFill Image를 그대로 연결하면 된다.
    ///   감소: 메인 즉시 → 잔상이 delay 후 따라 내려감 (데미지 표시)
    ///   증가: 잔상이 먼저 올라가고 메인이 따라 올라감 (회복 표시)
    /// Image.type은 Filled 여야 한다.
    /// </summary>
    [AddComponentMenu("OZ/UI/Tween Fill Bar")]
    public class TweenFillBar : MonoBehaviour
    {
        [SerializeField] internal Image fill;
        [SerializeField] internal Image trail;

        [Header("Timing")]
        [SerializeField] internal float mainDuration = 0.1f;
        [SerializeField] internal float trailDelay = 0.4f;
        [SerializeField] internal float trailDuration = 0.3f;

        [Header("Pixel Step (선택)")]
        [Tooltip("0이면 연속. 9면 Flask처럼 9단계로 끊어서 표시")]
        [SerializeField, Min(0)] internal int steps;

        float _value = 1f;
        Tween _mainTween, _trailTween;

        public float Value => _value;
        public Image Fill => fill;
        public Image Trail => trail;

        /// <summary>normalized: 0~1. animate=false면 즉시 반영(초기화, 재도전 복구)</summary>
        public void SetValue(float normalized, bool animate = true)
        {
            normalized = Mathf.Clamp01(normalized);
            float shown = Quantize(normalized);
            bool decreasing = normalized < _value;
            _value = normalized;

            _mainTween?.Kill();
            _trailTween?.Kill();

            if (!animate)
            {
                if (fill != null) fill.fillAmount = shown;
                if (trail != null) trail.fillAmount = shown;
                return;
            }

            if (decreasing)
            {
                if (fill != null) _mainTween = TweenImage(fill, shown, mainDuration, 0f);
                if (trail != null) _trailTween = TweenImage(trail, shown, trailDuration, trailDelay);
            }
            else
            {
                if (trail != null) _trailTween = TweenImage(trail, shown, mainDuration, 0f);
                if (fill != null) _mainTween = TweenImage(fill, shown, trailDuration, trailDelay * 0.5f);
            }
        }

        /// <summary>보스 등장처럼 0에서 목표까지 채우는 연출</summary>
        public Tween FillFromZero(float normalized, float duration)
        {
            SetValue(0f, false);
            _value = Mathf.Clamp01(normalized);
            float shown = Quantize(_value);
            _mainTween?.Kill();
            _trailTween?.Kill();
            if (trail != null) _trailTween = TweenImage(trail, shown, duration, 0f);
            if (fill != null) _mainTween = TweenImage(fill, shown, duration, 0f);
            return _mainTween;
        }

        float Quantize(float v)
        {
            if (steps <= 0) return v;
            if (v <= 0f) return 0f;
            return Mathf.Ceil(v * steps) / steps; // 조금이라도 남으면 1칸은 보이게
        }

        Tween TweenImage(Image img, float to, float duration, float delay)
        {
            img.DOKill();
            var t = img.DOFillAmount(to, duration).SetEase(Ease.OutQuad).SetDelay(delay);
            if (steps > 0) t.SetEase(Ease.Linear);
            return t.SetUpdate(true).SetLink(gameObject);
        }

        void OnValidate()
        {
            if (fill != null && fill.type != Image.Type.Filled)
                Debug.LogWarning($"[TweenFillBar] {name}: fill Image의 Type을 Filled로 바꿔주세요.", this);
        }
    }
}
