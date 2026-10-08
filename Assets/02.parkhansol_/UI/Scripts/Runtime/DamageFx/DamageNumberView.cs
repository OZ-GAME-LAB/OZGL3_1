using DG.Tweening;
using TMPro;
using UnityEngine;

namespace OZ.UI
{
    /// <summary>피해 숫자 한 개 (풀링). 월드 위치를 기억하고 DamageFxController가 매 프레임 화면 위치로 옮긴다.</summary>
    [AddComponentMenu("OZ/UI/Damage/Damage Number View")]
    public class DamageNumberView : MonoBehaviour
    {
        [SerializeField] internal TMP_Text text;
        [SerializeField] internal TMP_Text shadow;
        [SerializeField] internal TMP_Text label;
        [SerializeField] internal CanvasGroup group;

        internal Vector3 worldPos;
        internal Vector2 offset;   // 연출용 화면 오프셋 (떠오름·흩어짐)
        internal float shakeX;     // 치명타 흔들림
        internal float spawnTime;
        internal bool active;
        internal OZ.UI.Contracts.DamageKind kind;
        internal float amount;
        internal bool critLane;

        RectTransform _rt;
        Sequence _seq;
        public RectTransform Rect => _rt != null ? _rt : (_rt = (RectTransform)transform);

        internal void Play(string value, DamageNumberStyle s, Vector2 startOffset, int sizeMul, System.Action<DamageNumberView> onDone)
        {
            _seq?.Kill();
            active = true;
            spawnTime = Time.unscaledTime;
            offset = startOffset;
            gameObject.SetActive(true);

            if (s.font != null) { text.font = s.font; if (shadow != null) shadow.font = s.font; }
            kind = s.kind;
            critLane = s.IsCritLane;
            float size = s.fontSize * Mathf.Max(1, sizeMul); // 정수배 → 픽셀 유지
            text.text = value; text.color = s.color; text.fontSize = size;
            if (shadow != null) { shadow.text = value; shadow.fontSize = size; shadow.rectTransform.anchoredPosition = new Vector2(s.shadowOffset * sizeMul, -s.shadowOffset * sizeMul); }
            if (label != null) label.fontSize = 10f * Mathf.Max(1, sizeMul);
            bool hasLabel = label != null && !string.IsNullOrEmpty(s.label);
            if (label != null)
            {
                label.gameObject.SetActive(hasLabel);
                if (hasLabel) { label.text = s.label; label.color = s.labelColor; }
            }

            group.alpha = 1f;
            Rect.localScale = Vector3.one * s.popScale;
            float popT = Mathf.Min(0.14f, s.duration * 0.25f);
            Vector2 end = startOffset + new Vector2(0f, s.rise);

            _seq = DOTween.Sequence()
                .Append(Rect.DOScale(1f, popT).SetEase(Ease.OutBack))
                .Join(DOTween.To(() => offset, v => offset = v, end, s.duration).SetEase(Ease.OutCubic))
                .Insert(s.duration * 0.6f, group.DOFade(0f, s.duration * 0.4f).SetEase(Ease.InQuad));

            shakeX = 0f;
            if (s.shake > 0f)
                _seq.Insert(popT, DOVirtual.Float(s.shake, 0f, 0.22f, amp => shakeX = Mathf.Round(Random.Range(-amp, amp))));
            if (s.IsCritLane)
                _seq.Insert(0f, text.DOColor(Color.white, 0.04f).From()); // 등장 순간 하얗게 번쩍

            _seq.OnComplete(() => { active = false; gameObject.SetActive(false); onDone?.Invoke(this); })
                .SetUpdate(true).SetLink(gameObject);
        }

        internal void Stop()
        {
            _seq?.Kill();
            active = false;
            gameObject.SetActive(false);
        }
    }
}
