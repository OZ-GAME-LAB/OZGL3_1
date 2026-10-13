using DG.Tweening;
using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// HP 바(Sci-Fi Hud_Lifebar B) + 수치 + 초상화 칸 반응. (flaskFill은 선택 — 비워 두면 안 씀)
    ///   피격: 흔들림 + 빨강 플래시 / 회복: 초록 플래시 / 25% 이하: 맥박
    ///   v0.7 체력 상태 색: 60% 초과 하늘색(기본) → 60% 이하 노랑 → 30% 이하 빨강. 바뀌는 순간 바가 하얗게 번쩍 + 살짝 튐, 수치 글자도 같은 색
    /// </summary>
    [AddComponentMenu("OZ/UI/HUD/Health View")]
    public class HealthView : MonoBehaviour
    {
        [Tooltip("Flask 안쪽 Fill. Image Type이 Filled면 fillAmount로, 아니면 flaskSteps 스프라이트 교체로 표시")]
        [SerializeField] internal Image flaskFill;
        [SerializeField] internal Sprite[] flaskSteps;
        [SerializeField] internal RectTransform flaskRoot;
        [SerializeField] internal TweenFillBar bar;
        [SerializeField] internal TMP_Text valueText;
        [SerializeField] internal Graphic flashTarget;
        [SerializeField, Range(0f, 1f)] internal float lowThreshold = 0.25f;
        [SerializeField] internal Color damageColor = new Color(1f, 0.3f, 0.3f);
        [SerializeField] internal Color healColor = new Color(0.4f, 1f, 0.5f);

        [Header("v0.7 체력 상태 색")]
        [SerializeField] internal Image stateFill;
        [SerializeField] internal Sprite normalSprite;
        [SerializeField] internal Sprite cautionSprite;
        [SerializeField] internal Sprite dangerSprite;
        [SerializeField, Range(0f, 1f)] internal float cautionThreshold = 0.6f;
        [SerializeField, Range(0f, 1f)] internal float dangerThreshold = 0.3f;
        [SerializeField] internal Color normalTextColor = new Color(0.93f, 0.95f, 1f);
        [SerializeField] internal Color cautionTextColor = new Color(1f, 0.85f, 0.35f);
        [SerializeField] internal Color dangerTextColor = new Color(1f, 0.45f, 0.45f);
        [SerializeField] internal RectTransform barRoot;

        /// <summary>0 기본 · 1 주의(노랑) · 2 위험(빨강)</summary>
        public int State { get; private set; }

        IHealthSource _source;
        bool _low;

        internal void Bind(IHealthSource source)
        {
            Unsubscribe();
            _source = source;
            if (_source != null) _source.HealthChanged += OnChanged;
            Refresh(false);
        }

        void Unsubscribe()
        {
            if (_source != null) _source.HealthChanged -= OnChanged;
        }

        void OnDestroy() => Unsubscribe();

        void OnChanged(HealthChange c)
        {
            Refresh(true);
            if (c.IsDamage)
            {
                if (flaskRoot != null)
                    flaskRoot.Shake(2f, 0.2f).OnComplete(() => { if (_low) flaskRoot.Pulse(1.1f, 0.3f); });
                if (flashTarget != null) flashTarget.FlashColor(damageColor, 0.2f);
            }
            else if (c.IsHeal)
            {
                if (flashTarget != null) flashTarget.FlashColor(healColor, 0.3f);
                if (flaskRoot != null && !_low) flaskRoot.Punch(0.15f);
            }
        }

        void ApplyState(int state, bool animate)
        {
            bool changed = state != State;
            State = state;
            if (stateFill != null)
            {
                var sp = state == 2 ? dangerSprite : state == 1 ? cautionSprite : normalSprite;
                if (sp != null && stateFill.sprite != sp) stateFill.sprite = sp;
            }
            if (valueText != null) valueText.color = state == 2 ? dangerTextColor : state == 1 ? cautionTextColor : normalTextColor;
            if (changed && animate)
            {
                if (stateFill != null) stateFill.FlashColor(Color.white, 0.25f);
                if (barRoot != null) barRoot.Punch(0.12f, 0.25f);
            }
        }

        void Refresh(bool animate)
        {
            float hp = _source?.HP ?? 0f;
            float max = _source?.MaxHP ?? 1f;
            float n = max > 0f ? Mathf.Clamp01(hp / max) : 0f;
            if (_source == null) n = 1f;

            if (bar != null) bar.SetValue(n, animate);
            if (valueText != null) valueText.text = _source == null ? "-" : $"{Mathf.CeilToInt(hp)}/{Mathf.CeilToInt(max)}";

            if (flaskFill != null && flaskFill.type == Image.Type.Filled)
            {
                flaskFill.DOKill();
                if (animate) flaskFill.DOFillAmount(n, 0.25f).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(flaskFill.gameObject);
                else flaskFill.fillAmount = n;
            }
            else if (flaskFill != null && flaskSteps != null && flaskSteps.Length > 0)
            {
                int step = n <= 0f ? 0 : Mathf.Clamp(Mathf.CeilToInt(n * flaskSteps.Length), 1, flaskSteps.Length);
                flaskFill.enabled = step > 0;
                if (step > 0) flaskFill.sprite = flaskSteps[step - 1];
            }

            ApplyState(_source == null ? 0 : n <= dangerThreshold ? 2 : n <= cautionThreshold ? 1 : 0, animate);

            bool low = _source != null && n > 0f && n <= lowThreshold;
            if (low != _low && flaskRoot != null)
            {
                _low = low;
                if (low) flaskRoot.Pulse(1.1f, 0.3f);
                else { flaskRoot.DOKill(); flaskRoot.localScale = Vector3.one; }
            }
        }
    }
}
