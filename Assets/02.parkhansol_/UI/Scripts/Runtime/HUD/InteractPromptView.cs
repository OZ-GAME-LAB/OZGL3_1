using DG.Tweening;
using OZ.UI.Contracts;
using TMPro;
using UnityEngine;

namespace OZ.UI
{
    /// <summary>
    /// [v0.7] GameUI.Prompt 구현 — 대상 머리 위 "[F] 조사".
    ///   나타날 때 살짝 튀어오르며 페이드 인, 떠 있는 동안 1px 위아래로 숨쉬기, 사라질 때 페이드 아웃.
    ///   대상이 화면 밖이거나 창(인벤토리·일시정지 등)이 열려 있으면 숨김.
    /// </summary>
    [AddComponentMenu("OZ/UI/HUD/Interact Prompt View")]
    public class InteractPromptView : MonoBehaviour, IPromptApi
    {
        [SerializeField] internal CanvasGroup group;
        [SerializeField] internal RectTransform body;
        [SerializeField] internal RectTransform keyCap;
        [Tooltip("글자 뒤 반투명 바탕 (선택) — 폭은 글자에 맞춰 자동")]
        [SerializeField] internal RectTransform back;
        [SerializeField] internal TMP_Text keyText;
        [SerializeField] internal TMP_Text actionText;
        [SerializeField] internal Vector3 defaultOffset = new Vector3(0f, 2.2f, 0f);
        [SerializeField] internal float gap = 4f;
        [SerializeField] internal float minKeyWidth = 15f;
        [SerializeField] internal UISound sound = UISound.PromptShow;

        Transform _target;
        Vector3 _offset;
        RectTransform _area;
        Camera _cam;
        float _alpha;
        bool _want;
        Tween _pop;

        public Transform Current => _want ? _target : null;
        public bool IsVisible => _alpha > 0.5f;

        void Awake()
        {
            _area = transform.parent as RectTransform;
            if (group != null) group.alpha = 0f;
        }

        void OnEnable() => GameUI.Register((IPromptApi)this);
        void OnDisable() => GameUI.Unregister(this);

        public void Show(Transform target, string action, string key = "F", Vector3? worldOffset = null)
        {
            if (target == null) { Hide(); return; }
            bool changed = !_want || _target != target;
            _target = target;
            _offset = worldOffset ?? defaultOffset;
            _want = true;
            if (keyText != null) keyText.text = string.IsNullOrEmpty(key) ? "F" : key;
            if (actionText != null) actionText.text = action ?? "";
            Layout();
            if (changed)
            {
                UISfx.Play(sound);
                if (body != null)
                {
                    _pop?.Kill();
                    body.localScale = Vector3.one * 0.85f;
                    _pop = body.DOScale(1f, UITweenStyle.Normal).SetEase(Ease.OutBack).SetUpdate(true).SetLink(gameObject);
                }
            }
        }

        public void Hide(Transform target = null)
        {
            if (target != null && target != _target) return;
            _want = false;
        }

        // [키] 간격 [행동] 을 가운데 정렬
        void Layout()
        {
            if (keyCap == null || keyText == null || actionText == null) return;
            keyText.ForceMeshUpdate();
            actionText.ForceMeshUpdate();
            float kw = Mathf.Max(minKeyWidth, Mathf.Ceil(keyText.preferredWidth) + 7f);
            float aw = Mathf.Ceil(actionText.preferredWidth);
            float total = kw + gap + aw;
            float left = -Mathf.Round(total * 0.5f);
            keyCap.sizeDelta = new Vector2(kw, keyCap.sizeDelta.y);
            keyCap.anchoredPosition = new Vector2(left + kw * 0.5f, keyCap.anchoredPosition.y);
            var at = actionText.rectTransform;
            at.sizeDelta = new Vector2(aw + 2f, at.sizeDelta.y);
            at.anchoredPosition = new Vector2(left + kw + gap + aw * 0.5f, at.anchoredPosition.y);
            if (back != null) back.sizeDelta = new Vector2(total + 8f, back.sizeDelta.y);
        }

        void LateUpdate()
        {
            bool visible = _want && _target != null && _target.gameObject.activeInHierarchy && !UIState.IsGameplayInputBlocked;
            if (visible)
            {
                if (_cam == null || !_cam.isActiveAndEnabled) _cam = Camera.main;
                if (_cam == null || _area == null) visible = false;
                else
                {
                    Vector3 sp = _cam.WorldToScreenPoint(_target.position + _offset);
                    if (sp.z < 0f) visible = false;
                    else
                    {
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, sp, null, out var local);
                        var r = _area.rect;
                        if (local.x < r.xMin || local.x > r.xMax || local.y < r.yMin || local.y > r.yMax) visible = false;
                        float bob = Mathf.Round(Mathf.Sin(Time.unscaledTime * 3f) * 1f);
                        ((RectTransform)transform).anchoredPosition = new Vector2(Mathf.Round(local.x), Mathf.Round(local.y) + bob);
                    }
                }
            }
            _alpha = Mathf.MoveTowards(_alpha, visible ? 1f : 0f, Time.unscaledDeltaTime * (visible ? 7f : 5f));
            if (group != null) group.alpha = _alpha;
            if (!_want && _alpha <= 0f) _target = null;
        }
    }
}
