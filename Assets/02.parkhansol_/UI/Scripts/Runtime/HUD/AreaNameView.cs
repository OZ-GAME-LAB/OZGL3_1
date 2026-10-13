using DG.Tweening;
using OZ.UI.Contracts;
using TMPro;
using UnityEngine;

namespace OZ.UI
{
    /// <summary>
    /// [v0.7] GameUI.Area 구현 — 화면 위쪽 가운데 지역 이름.
    ///   등장: 위에서 살짝 내려오며 페이드 인 + 글자 간격이 좁혀지고 양옆 선이 펼쳐짐
    ///   유지: holdSeconds
    ///   퇴장: 글자 간격이 살짝 벌어지며 천천히 페이드 아웃
    /// 게임을 멈추지 않고, 입력도 막지 않는다.
    /// </summary>
    [AddComponentMenu("OZ/UI/HUD/Area Name View")]
    public class AreaNameView : MonoBehaviour, IAreaApi
    {
        [SerializeField] internal CanvasGroup group;
        [SerializeField] internal RectTransform content;
        [SerializeField] internal TMP_Text nameText;
        [SerializeField] internal TMP_Text subtitleText;
        [SerializeField] internal RectTransform lineLeft;
        [SerializeField] internal RectTransform lineRight;

        [Header("연출")]
        [SerializeField, Min(0f)] internal float fadeIn = 0.7f;
        [SerializeField, Min(0f)] internal float fadeOut = 0.9f;
        [SerializeField] internal float dropDistance = 6f;
        [SerializeField] internal float spacingFrom = 14f;
        [SerializeField] internal float spacingTo = 3f;
        [Tooltip("같은 이름을 이 시간 안에 다시 부르면 무시 (경계 왕복 깜빡임 방지)")]
        [SerializeField, Min(0f)] internal float repeatGuard = 4f;
        [SerializeField] internal UISound sound = UISound.AreaEnter;

        Sequence _seq;
        Vector2 _contentPos;
        bool _cached;
        string _current;
        float _lastShownAt = -999f;

        public string Current => _current;
        public bool IsShowing => _seq != null && _seq.IsActive();

        void Awake() => Cache();
        void OnEnable() => GameUI.Register((IAreaApi)this);
        void OnDisable() => GameUI.Unregister(this);
        void OnDestroy() => _seq?.Kill();

        void Cache()
        {
            if (_cached) return;
            _cached = true;
            if (content != null) _contentPos = content.anchoredPosition;
            if (group != null) group.alpha = 0f;
        }

        public void Show(string areaName, string subtitle = null, float holdSeconds = 2.2f)
        {
            Cache();
            if (string.IsNullOrEmpty(areaName)) return;
            if (areaName == _current && Time.unscaledTime - _lastShownAt < repeatGuard) return;
            _current = areaName;
            _lastShownAt = Time.unscaledTime;

            if (nameText != null) nameText.text = areaName;
            bool hasSub = !string.IsNullOrEmpty(subtitle);
            if (subtitleText != null)
            {
                subtitleText.text = hasSub ? subtitle : "";
                subtitleText.gameObject.SetActive(hasSub);
            }
            if (group == null || content == null || nameText == null) return;

            UISfx.Play(sound);
            _seq?.Kill();
            group.alpha = 0f;
            content.anchoredPosition = _contentPos + new Vector2(0f, dropDistance);
            nameText.characterSpacing = spacingFrom;
            if (subtitleText != null) subtitleText.alpha = 0f;
            if (lineLeft != null) lineLeft.localScale = new Vector3(0f, 1f, 1f);
            if (lineRight != null) lineRight.localScale = new Vector3(0f, 1f, 1f);

            float hold = Mathf.Max(0.3f, holdSeconds);
            _seq = DOTween.Sequence()
                .Append(group.DOFade(1f, fadeIn).SetEase(Ease.OutSine))
                .Join(content.DOAnchorPos(_contentPos, fadeIn).SetEase(Ease.OutCubic))
                .Join(DOTween.To(() => nameText.characterSpacing, v => nameText.characterSpacing = v, spacingTo, fadeIn + 0.2f).SetEase(Ease.OutCubic));
            if (lineLeft != null) _seq.Join(lineLeft.DOScaleX(1f, fadeIn + 0.15f).SetEase(Ease.OutCubic).SetDelay(0.1f));
            if (lineRight != null) _seq.Join(lineRight.DOScaleX(1f, fadeIn + 0.15f).SetEase(Ease.OutCubic).SetDelay(0.1f));
            if (subtitleText != null && hasSub) _seq.Insert(fadeIn * 0.6f, subtitleText.DOFade(1f, 0.4f));
            _seq.AppendInterval(hold)
                .Append(group.DOFade(0f, fadeOut).SetEase(Ease.InSine))
                .Join(DOTween.To(() => nameText.characterSpacing, v => nameText.characterSpacing = v, spacingTo + 3f, fadeOut).SetEase(Ease.InSine))
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        public void Hide()
        {
            _seq?.Kill();
            if (group == null) return;
            _seq = DOTween.Sequence().Append(group.DOFade(0f, 0.3f)).SetUpdate(true).SetLink(gameObject);
        }
    }
}
