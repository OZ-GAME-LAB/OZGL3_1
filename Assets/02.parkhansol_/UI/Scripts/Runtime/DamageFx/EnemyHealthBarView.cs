using DG.Tweening;
using OZ.UI.Contracts;
using UnityEngine;

namespace OZ.UI
{
    /// <summary>
    /// 적 머리 위 체력바 (풀링). 맞으면 나타나고 hideDelay초 동안 안 맞으면 사라진다.
    /// 엘리트는 항상 보이고 조금 더 넓다. 0이 되면 짧게 깜빡이고 사라짐.
    /// </summary>
    [AddComponentMenu("OZ/UI/Damage/Enemy Health Bar View")]
    public class EnemyHealthBarView : MonoBehaviour
    {
        [SerializeField] internal TweenFillBar bar;
        [SerializeField] internal CanvasGroup group;
        [SerializeField] internal float normalWidth = 28f;
        [SerializeField] internal float eliteWidth = 44f;
        [SerializeField] internal float hideDelay = 2.5f;

        internal IEnemyHealthSource source;
        float _lastHit = -99f;
        bool _dead;
        Tween _fade;
        RectTransform _rt;
        public RectTransform Rect => _rt != null ? _rt : (_rt = (RectTransform)transform);

        internal void Bind(IEnemyHealthSource s)
        {
            Unbind();
            source = s;
            _dead = false;
            gameObject.SetActive(true);
            var size = Rect.sizeDelta;
            Rect.sizeDelta = new Vector2(s.IsElite ? eliteWidth : normalWidth, size.y);
            source.HealthChanged += OnChanged;
            float n = s.MaxHP > 0f ? Mathf.Clamp01(s.HP / s.MaxHP) : 0f;
            if (bar != null) bar.SetValue(n, false);
            SetVisible(s.IsElite, true);
        }

        internal void Unbind()
        {
            if (source != null) source.HealthChanged -= OnChanged;
            source = null;
            _fade?.Kill();
            gameObject.SetActive(false);
        }

        void OnDestroy() { if (source != null) source.HealthChanged -= OnChanged; }

        void OnChanged(HealthChange c)
        {
            if (bar != null) bar.SetValue(c.Normalized, true);
            _lastHit = Time.unscaledTime;
            if (c.Current <= 0f)
            {
                _dead = true;
                _fade?.Kill();
                _fade = DOTween.Sequence()
                    .Append(group.DOFade(0.2f, 0.06f)).Append(group.DOFade(1f, 0.06f)).SetLoops(2)
                    .OnComplete(() => SetVisible(false, false))
                    .SetUpdate(true).SetLink(gameObject);
                return;
            }
            if (_dead) _dead = false; // 부활/리스폰
            if (c.IsDamage) SetVisible(true, false);              // 맞았을 때만 나타남
            else if (c.Current >= c.Max && !source.IsElite) SetVisible(false, true); // 리스폰·완전 회복 → 숨김
        }

        void Update()
        {
            if (source == null || _dead || source.IsElite) return;
            if (group.alpha > 0.99f && Time.unscaledTime - _lastHit > hideDelay) SetVisible(false, false);
        }

        void SetVisible(bool on, bool instant)
        {
            _fade?.Kill();
            if (instant) { group.alpha = on ? 1f : 0f; return; }
            _fade = group.DOFade(on ? 1f : 0f, on ? 0.08f : 0.35f).SetUpdate(true).SetLink(gameObject);
        }
    }
}
