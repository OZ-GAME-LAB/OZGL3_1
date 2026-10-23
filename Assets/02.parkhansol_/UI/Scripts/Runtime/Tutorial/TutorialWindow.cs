using System;
using System.Collections.Generic;
using DG.Tweening;
using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// [v1.3 · 기획서 v0.5 3장] 튜토리얼 강조 창. 화면을 어둡게 덮고 한 곳만 뚫어서 밝힌 뒤 설명 칸을 붙인다.
    ///   ┌────────────── 어둡게 ──────────────┐
    ///   │        ┌─────┐  ← 1px 테두리(깜빡)  │
    ///   │        │ 밝음 │                     │
    ///   │        └─────┘                     │
    ///   │   ▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔ (청록 줄)      │
    ///   │   제목                              │
    ///   │   설명 …                            │
    ///   │                  [Space] 다음  2/6 │
    ///   └────────────────────────────────────┘
    /// 열려 있는 동안 게임 정지 + 전투 입력 차단 (UIWindow.pausesGame / blocksGameplayInput).
    /// 직접 쓰지 말고 GameUI.Tutorial (TutorialDirector) 로 부른다. 프리팹 없이 코드로 만든다 (Create).
    /// </summary>
    [AddComponentMenu("OZ/UI/Tutorial/Tutorial Window")]
    public class TutorialWindow : UIWindow
    {
        [SerializeField] internal Image[] dims = new Image[4];   // 위 · 아래 · 왼 · 오
        [SerializeField] internal Image[] frame = new Image[4];  // 강조 칸 테두리
        [SerializeField] internal CanvasGroup frameGroup;
        [SerializeField] internal RectTransform callout;
        [SerializeField] internal CanvasGroup calloutGroup;
        [SerializeField] internal TMP_Text titleText;
        [SerializeField] internal TMP_Text bodyText;
        [SerializeField] internal TMP_Text footerText;
        [SerializeField] internal float calloutWidth = 236f;
        [SerializeField] internal float pad = 8f;
        [SerializeField] internal float gap = 6f;
        [SerializeField] internal float margin = 8f;
        [Tooltip("단계가 바뀐 직후 이 시간 동안은 [Space]를 무시 (연타로 건너뛰기 방지)")]
        [SerializeField] internal float inputGuard = 0.3f;

        const float TitleH = 16f, FooterH = 11f;

        Func<Rect?> _hole;
        float _calloutH = 60f;
        float _shownAt;
        InputAction _next;
        Tween _pulse;

        /// <summary>[Space]/[Enter]/패드 A/설명 칸 클릭</summary>
        internal event Action NextPressed;
        /// <summary>지금 밝힌 칸 (창 로컬 좌표). HasHole false면 의미 없음</summary>
        internal Rect Hole { get; private set; }
        internal bool HasHole { get; private set; }
        internal float CalloutHeight => _calloutH;

        RectTransform Root => (RectTransform)transform;

        // ───────────── 만들기 (코드) ─────────────

        internal static TutorialWindow Create(Transform layer, TMP_FontAsset title, TMP_FontAsset body, TMP_FontAsset small,
            Sprite panel, Color accent, float dimAlpha)
        {
            var go = new GameObject("Tutorial", typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(layer, false);
            Stretch(rt);
            var w = go.AddComponent<TutorialWindow>(); // CanvasGroup은 RequireComponent로 붙음
            w.screenId = ScreenId.Tutorial;
            w.layer = UILayer.Popup;
            w.pausesGame = true;
            w.blocksGameplayInput = true;
            w.closeOnBack = false;
            w.transition = WindowTransition.Fade;
            w.openSound = UISound.None;
            w.closeSound = UISound.None;

            var dimColor = new Color(0f, 0.01f, 0.03f, dimAlpha);
            string[] dimNames = { "DimTop", "DimBottom", "DimLeft", "DimRight" };
            for (int i = 0; i < 4; i++) w.dims[i] = Solid(dimNames[i], rt, dimColor, true); // 주변 클릭 막기, 밝힌 칸은 뚫림

            var frameRt = NewRect("Frame", rt);
            Stretch(frameRt);
            w.frameGroup = frameRt.gameObject.AddComponent<CanvasGroup>();
            w.frameGroup.blocksRaycasts = false;
            for (int i = 0; i < 4; i++) w.frame[i] = Solid("Line" + i, frameRt, accent, false);

            // 설명 칸
            var c = NewRect("Callout", rt);
            Place(c, 0.5f, 0.5f, 0f, 0f, w.calloutWidth, 60f);
            c.pivot = Vector2.zero;
            w.callout = c;
            w.calloutGroup = c.gameObject.AddComponent<CanvasGroup>();
            var bg = c.gameObject.AddComponent<Image>();
            bg.sprite = panel;
            bg.color = panel != null ? Color.white : new Color(0.05f, 0.07f, 0.13f, 0.96f);
            if (panel != null && panel.border != Vector4.zero) bg.type = Image.Type.Sliced;
            bg.raycastTarget = true;
            var btn = c.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            var nav = btn.navigation; nav.mode = Navigation.Mode.None; btn.navigation = nav;
            btn.onClick.AddListener(w.RaiseNext);
            var bar = Solid("AccentBar", c, accent, false);
            var barRt = bar.rectTransform;
            barRt.anchorMin = new Vector2(0f, 1f); barRt.anchorMax = Vector2.one; barRt.pivot = new Vector2(0.5f, 1f);
            barRt.anchoredPosition = Vector2.zero; barRt.sizeDelta = new Vector2(0f, 2f);

            w.titleText = Text("Title", c, title, 15f, accent, TextAlignmentOptions.Left);
            w.bodyText = Text("Body", c, body, 12f, new Color(0.93f, 0.95f, 1f), TextAlignmentOptions.TopLeft);
            w.footerText = Text("Footer", c, small, 10f, new Color(0.65f, 0.7f, 0.82f), TextAlignmentOptions.Right);
            go.SetActive(true);
            return w;
        }

        // ───────────── 단계 ─────────────

        /// <summary>제목·설명·아래 글자 + 밝힐 칸 (매 프레임 다시 계산 → 움직이는 대상도 따라감)</summary>
        internal void SetStep(string title, string body, string footer, Func<Rect?> hole)
        {
            _hole = hole;
            _shownAt = Time.unscaledTime;
            float inner = calloutWidth - pad * 2f;
            bool hasTitle = !string.IsNullOrEmpty(title);
            titleText.gameObject.SetActive(hasTitle);
            titleText.text = title ?? "";
            bodyText.text = body ?? "";
            footerText.text = footer ?? "";
            float bodyH = string.IsNullOrEmpty(body) ? 0f : Mathf.Ceil(bodyText.GetPreferredValues(bodyText.text, inner, 0f).y);
            float top = pad + (hasTitle ? TitleH + 3f : 0f);
            _calloutH = top + bodyH + 5f + FooterH + pad;
            callout.sizeDelta = new Vector2(calloutWidth, _calloutH);
            if (hasTitle) PlaceTL(titleText.rectTransform, pad, pad, inner, TitleH);
            PlaceTL(bodyText.rectTransform, pad, top, inner, Mathf.Max(bodyH, 1f));
            var f = footerText.rectTransform;
            f.anchorMin = f.anchorMax = f.pivot = new Vector2(1f, 0f);
            f.anchoredPosition = new Vector2(-pad, pad - 2f);
            f.sizeDelta = new Vector2(inner, FooterH);

            Layout();
            if (IsOpen)
            {
                calloutGroup.DOKill();
                calloutGroup.alpha = 0f;
                calloutGroup.DOFade(1f, UITweenStyle.Fast).SetUpdate(true).SetLink(gameObject);
                callout.SlideIn(Vector2.down, 4f, UITweenStyle.Fast);
            }
        }

        // ───────────── 좌표 ─────────────

        /// <summary>UI 요소 → 창 로컬 칸 (어느 캔버스든)</summary>
        internal Rect? FromRect(RectTransform target, float padding)
        {
            if (target == null || !target.gameObject.activeInHierarchy) return null;
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            var canvas = target.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, corners[i]);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, screen, null, out var local)) return null;
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            return new Rect(min.x - padding, min.y - padding, max.x - min.x + padding * 2f, max.y - min.y + padding * 2f);
        }

        /// <summary>여러 UI 요소를 감싸는 칸</summary>
        internal Rect? FromRects(IReadOnlyList<RectTransform> targets, float padding)
        {
            Rect? all = null;
            foreach (var t in targets)
            {
                var r = FromRect(t, padding);
                if (!r.HasValue) continue;
                all = all.HasValue ? Rect.MinMaxRect(Mathf.Min(all.Value.xMin, r.Value.xMin), Mathf.Min(all.Value.yMin, r.Value.yMin),
                    Mathf.Max(all.Value.xMax, r.Value.xMax), Mathf.Max(all.Value.yMax, r.Value.yMax)) : r;
            }
            return all;
        }

        /// <summary>월드 위치 → 창 로컬 칸 (size = UI 픽셀)</summary>
        internal Rect? FromWorld(Vector3 world, Vector2 size, float padding)
        {
            var cam = Camera.main;
            if (cam == null) return null;
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0f) return null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, sp, null, out var c)) return null;
            Vector2 s = size + Vector2.one * (padding * 2f);
            return new Rect(c - s * 0.5f, s);
        }

        // ───────────── 배치 ─────────────

        void LateUpdate()
        {
            if (IsOpen) Layout();
        }

        internal void Layout()
        {
            Rect r = Root.rect;
            Rect? want = _hole?.Invoke();
            Rect hole = default;
            bool has = false;
            if (want.HasValue)
            {
                var w = want.Value;
                hole = Rect.MinMaxRect(Mathf.Round(Mathf.Max(w.xMin, r.xMin)), Mathf.Round(Mathf.Max(w.yMin, r.yMin)),
                    Mathf.Round(Mathf.Min(w.xMax, r.xMax)), Mathf.Round(Mathf.Min(w.yMax, r.yMax)));
                has = hole.width >= 1f && hole.height >= 1f;
            }
            if (!has) hole = new Rect(Mathf.Round(r.center.x), Mathf.Round(r.center.y), 0f, 0f);
            Hole = hole;
            HasHole = has;

            Put(dims[0], r.xMin, hole.yMax, r.width, r.yMax - hole.yMax);
            Put(dims[1], r.xMin, r.yMin, r.width, hole.yMin - r.yMin);
            Put(dims[2], r.xMin, hole.yMin, hole.xMin - r.xMin, hole.height);
            Put(dims[3], hole.xMax, hole.yMin, r.xMax - hole.xMax, hole.height);

            if (frameGroup != null) frameGroup.gameObject.SetActive(has);
            if (has)
            {
                Put(frame[0], hole.xMin - 1f, hole.yMax, hole.width + 2f, 1f);
                Put(frame[1], hole.xMin - 1f, hole.yMin - 1f, hole.width + 2f, 1f);
                Put(frame[2], hole.xMin - 1f, hole.yMin, 1f, hole.height);
                Put(frame[3], hole.xMax, hole.yMin, 1f, hole.height);
            }

            // 설명 칸: 밝힌 칸 아래 → 안 들어가면 위 → 그래도 안 되면 옆. 화면 밖으로 안 나가게
            float cw = calloutWidth, ch = _calloutH, x, y;
            if (!has)
            {
                x = r.center.x - cw * 0.5f;
                y = r.center.y - ch * 0.5f;
            }
            else
            {
                x = hole.center.x - cw * 0.5f;
                y = hole.yMin - gap - ch;
                if (y < r.yMin + margin)
                {
                    y = hole.yMax + gap;
                    if (y + ch > r.yMax - margin)
                    {
                        y = hole.center.y - ch * 0.5f;
                        x = hole.xMax + gap;
                        if (x + cw > r.xMax - margin) x = hole.xMin - gap - cw;
                    }
                }
            }
            x = Mathf.Clamp(x, r.xMin + margin, Mathf.Max(r.xMin + margin, r.xMax - margin - cw));
            y = Mathf.Clamp(y, r.yMin + margin, Mathf.Max(r.yMin + margin, r.yMax - margin - ch));
            if (!DOTween.IsTweening(callout)) callout.anchoredPosition = new Vector2(Mathf.Round(x), Mathf.Round(y));
        }

        // ───────────── 입력 ─────────────

        protected override void Awake()
        {
            base.Awake();
            _next = new InputAction("UI_TutorialNext", InputActionType.Button);
            _next.AddBinding("<Keyboard>/space");
            _next.AddBinding("<Keyboard>/enter");
            _next.AddBinding("<Keyboard>/numpadEnter");
            _next.AddBinding("<Gamepad>/buttonSouth");
            _next.performed += _ => RaiseNext();
        }

        void OnDestroy() => _next?.Dispose();

        internal void RaiseNext()
        {
            if (!IsOpen || Time.unscaledTime - _shownAt < inputGuard) return;
            NextPressed?.Invoke();
        }

        protected override void OnOpened()
        {
            _next.Enable();
            _shownAt = Time.unscaledTime;
            _pulse?.Kill();
            if (frameGroup != null)
            {
                frameGroup.alpha = 1f;
                _pulse = frameGroup.DOFade(0.35f, 0.55f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetLink(gameObject);
            }
            Layout();
        }

        protected override void OnClosed()
        {
            _next.Disable();
            _pulse?.Kill();
            _hole = null;
        }

        // ───────────── 작은 도구 ─────────────

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform rt, float ax, float ay, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(ax, ay);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        /// <summary>부모 왼쪽 위 기준 (x 오른쪽, y 아래로)</summary>
        static void PlaceTL(RectTransform rt, float x, float y, float w, float h) => Place(rt, 0f, 1f, x, -y, w, h);

        /// <summary>부모 가운데 기준 로컬 좌표 칸 (왼쪽 아래 + 크기)</summary>
        static void Put(Image img, float x, float y, float w, float h)
        {
            if (img == null) return;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(Mathf.Round(x), Mathf.Round(y));
            rt.sizeDelta = new Vector2(Mathf.Max(0f, Mathf.Round(w)), Mathf.Max(0f, Mathf.Round(h)));
            img.enabled = w >= 1f && h >= 1f;
        }

        internal static Image Solid(string name, Transform parent, Color color, bool raycast)
        {
            var img = NewRect(name, parent).gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        internal static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
        {
            var t = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.extraPadding = false;
            return t;
        }
    }
}
