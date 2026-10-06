using DG.Tweening;
using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// 체력 통(Flask) + 수치 + 가로 바.
    ///   피격: 흔들림 + 빨강 플래시 / 회복: 초록 플래시 / 25% 이하: 맥박
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
