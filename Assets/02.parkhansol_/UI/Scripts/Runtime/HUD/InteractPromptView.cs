using System;
using DG.Tweening;
using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// [v0.7] GameUI.Prompt 구현 — 대상 머리 위 "[F] 조사".
    ///   나타날 때 살짝 튀어오르며 페이드 인, 떠 있는 동안 1px 위아래로 숨쉬기, 사라질 때 페이드 아웃.
    ///   대상이 화면 밖이거나 창(인벤토리·일시정지 등)이 열려 있으면 숨김.
    ///   보이는 동안 키(기본 F · 패드 X)를 누르면: 키 칸이 꾹 눌렸다 튀어오르고 번쩍 + 확인음 → Pressed(대상) 이벤트.
    ///   실제 동작(문 열기 등)은 Pressed 또는 InteractPromptLink.onInteract에서 게임 쪽이 처리.
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
        [Header("누름 반응")]
        [SerializeField] internal UISound pressSound = UISound.Confirm;
        [SerializeField] internal Color pressFlash = new Color(1f, 0.85f, 0.4f, 1f);
        [Tooltip("키 글자와 상관없이 항상 받는 패드 버튼")]
        [SerializeField] internal string gamepadBinding = "<Gamepad>/buttonWest";
        [Tooltip("같은 대상 연타 방지 (초)")]
        [SerializeField, Min(0f)] internal float pressCooldown = 0.25f;

        Transform _target;
        Vector3 _offset;
        RectTransform _area;
        Camera _cam;
        float _alpha;
        bool _want;
        Tween _pop;
        Sequence _press;
        InputAction _action;
        string _boundKey;
        float _lastPress = -99f;

        public event Action<Transform> Pressed;
        /// <summary>테스트용: 누른 횟수</summary>
        public int PressCount { get; private set; }

        public Transform Current => _want ? _target : null;
        public bool IsVisible => _alpha > 0.5f;

        void Awake()
        {
            _area = transform.parent as RectTransform;
            if (group != null) group.alpha = 0f;
        }

        void OnEnable() => GameUI.Register((IPromptApi)this);

        void OnDisable()
        {
            GameUI.Unregister(this);
            if (_action != null) _action.Disable();
        }

        void OnDestroy() { _action?.Dispose(); _press?.Kill(); }

        // 표시 중인 키 글자에 맞춰 입력 연결 ("F" → <Keyboard>/f, "Space" → <Keyboard>/space)
        void BindKey(string key)
        {
            key = string.IsNullOrEmpty(key) ? "F" : key;
            if (_action != null && _boundKey == key) { if (!_action.enabled) _action.Enable(); return; }
            _action?.Dispose();
            _boundKey = key;
            _action = new InputAction("UI_Interact", InputActionType.Button);
            _action.AddBinding("<Keyboard>/" + key.Trim().ToLowerInvariant().Replace(" ", ""));
            if (!string.IsNullOrEmpty(gamepadBinding)) _action.AddBinding(gamepadBinding);
            _action.performed += _ => Press();
            _action.Enable();
        }

        /// <summary>안내가 보이는 상태에서 상호작용 키를 눌렀을 때 (입력으로 자동 호출, 테스트·터치 버튼에서 직접 호출 가능)</summary>
        public bool Press()
        {
            if (!_want || _target == null || _alpha < 0.5f || UIState.IsGameplayInputBlocked) return false;
            if (Time.unscaledTime - _lastPress < pressCooldown) return false;
            _lastPress = Time.unscaledTime;
            PressCount++;
            PlayPress();
            UISfx.Play(pressSound);
            Pressed?.Invoke(_target);
            return true;
        }

        void PlayPress()
        {
            _press?.Kill();
            _press = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            if (keyCap != null)
            {
                keyCap.localScale = Vector3.one;
                _press.Append(keyCap.DOScale(0.78f, 0.05f).SetEase(Ease.OutQuad))
                      .Append(keyCap.DOScale(1.12f, 0.1f).SetEase(Ease.OutQuad))
                      .Append(keyCap.DOScale(1f, 0.12f).SetEase(Ease.OutBack));
                var capImg = keyCap.GetComponent<Graphic>();
                if (capImg != null) capImg.FlashColor(pressFlash, 0.25f);
            }
            if (keyText != null) keyText.FlashColor(Color.white, 0.25f);
            if (body != null)
            {
                body.DOKill();
                body.localScale = Vector3.one;
                _press.Join(body.DOPunchPosition(new Vector3(0f, -2f, 0f), 0.2f, 6, 0.5f));
            }
        }

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
            BindKey(key);
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
