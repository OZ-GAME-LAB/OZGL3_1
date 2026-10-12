using DG.Tweening;
using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// [v0.7] 회복 순간 화면 가장자리 초록 비네트가 짧게 번쩍 (저체력 붉은 비네트의 초록 버전).
    ///   많이 회복할수록 조금 더 진하게. 사망 후 재도전(0 → 가득)은 표시 안 함.
    /// HudView가 IHealthSource를 연결해 준다 → 팀원 작업 없음.
    /// </summary>
    [AddComponentMenu("OZ/UI/HUD/Heal Flash View")]
    public class HealFlashView : MonoBehaviour
    {
        [SerializeField] internal Image vignette;
        [SerializeField] internal Color color = new Color(0.25f, 0.95f, 0.45f, 1f);
        [SerializeField] internal Vector2 alphaRange = new Vector2(0.22f, 0.45f);
        [SerializeField, Min(0f)] internal float rise = 0.08f;
        [SerializeField, Min(0f)] internal float fade = 0.45f;

        IHealthSource _source;
        Sequence _seq;

        public int FlashCount { get; private set; }
        public float CurrentAlpha => vignette != null && vignette.enabled ? vignette.color.a : 0f;

        internal void Bind(IHealthSource source)
        {
            if (_source != null) _source.HealthChanged -= OnChanged;
            _source = source;
            if (_source != null) _source.HealthChanged += OnChanged;
        }

        void Awake() => Set(0f);
        void OnDestroy() { if (_source != null) _source.HealthChanged -= OnChanged; _seq?.Kill(); }

        void OnChanged(HealthChange c)
        {
            if (!c.IsHeal || c.Previous <= 0f) return; // 재도전 복구는 제외
            float ratio = c.Max > 0f ? Mathf.Clamp01(c.Delta / c.Max * 3f) : 0.5f;
            Flash(Mathf.Lerp(alphaRange.x, alphaRange.y, ratio));
        }

        public void Flash(float peak)
        {
            if (vignette == null) return;
            FlashCount++;
            _seq?.Kill();
            Set(CurrentAlpha);
            _seq = DOTween.Sequence()
                .Append(DOTween.To(() => CurrentAlpha, Set, peak, rise).SetEase(Ease.OutQuad))
                .Append(DOTween.To(() => CurrentAlpha, Set, 0f, fade).SetEase(Ease.InQuad))
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        void Set(float a)
        {
            if (vignette == null) return;
            var c = color;
            c.a = a;
            vignette.color = c;
            vignette.enabled = a > 0.002f;
        }
    }
}
