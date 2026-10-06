using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI.EditorTools
{
    /// <summary>UIRoot 빌더용 uGUI 생성 헬퍼 (논리 640×360 좌표, 에셋 1px = 1 단위)</summary>
    internal static class UIB
    {
        public static TMP_FontAsset Body, Small, Title, Bold;

        public static readonly Color TextColor = new Color(0.93f, 0.95f, 1f);
        public static readonly Color Dim = new Color(0.65f, 0.7f, 0.82f);
        public static readonly Color Accent = new Color(1f, 0.85f, 0.4f);

        public static void LoadFonts()
        {
            Body = OZFontBuilder.Load("Galmuri11");
            Small = OZFontBuilder.Load("Galmuri9");
            Title = OZFontBuilder.Load("Galmuri14");
            if (Body == null) Body = TMP_Settings.defaultFontAsset;
            if (Small == null) Small = Body;
            if (Title == null) Title = Body;
            Bold = OZFontBuilder.Load("Galmuri11 Bold");
            if (Bold == null) Bold = Body;
        }

        // ── 스프라이트 ──
        static Sprite _fallback;
        public static Sprite Fallback => _fallback != null ? _fallback
            : (_fallback = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"));

        /// <summary>Pixel UI 스프라이트 (예: "Panels/Blue/Panel"). 없으면 null</summary>
        public static Sprite Px(string rel) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{OZPaths.PixelUI}/Sprites/{rel}.png");

        public static Sprite Art(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{OZPaths.UI}/Art/Icons/{name}.png");

        /// <summary>UI/Art/HUD (에셋을 가공해 만든 HUD 전용 스프라이트)</summary>
        public static Sprite HudArt(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{OZPaths.UI}/Art/HUD/{name}.png");

        /// <summary>ThirdParty/SciFiPixelUI (체력바만 사용)</summary>
        public static Sprite Sf(string rel) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{OZPaths.SciFi}/{rel}.png");

        public static Sprite White => Art("UI_White");

        // ── RectTransform ──
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>앵커 = 피벗 = (ax, ay), 위치/크기 지정. ax/ay: 0=왼쪽·아래, 0.5=가운데, 1=오른쪽·위</summary>
        public static RectTransform Place(this RectTransform rt, float ax, float ay, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(ax, ay);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform StretchH(this RectTransform rt, float ay, float y, float h, float left = 0, float right = 0)
        {
            rt.anchorMin = new Vector2(0f, ay);
            rt.anchorMax = new Vector2(1f, ay);
            rt.pivot = new Vector2(0.5f, ay);
            rt.anchoredPosition = new Vector2((left - right) * 0.5f, y);
            rt.sizeDelta = new Vector2(-(left + right), h);
            return rt;
        }

        // ── Image ──
        public static Image Img(string name, Transform parent, Sprite sprite, Color? color = null, bool raycast = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite != null ? sprite : White;
            img.color = color ?? Color.white;
            img.raycastTarget = raycast;
            if (img.sprite != null && img.sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
            return img;
        }

        public static Image Filled(this Image img, Image.FillMethod method, int origin = 0)
        {
            img.type = Image.Type.Filled;
            img.fillMethod = method;
            img.fillOrigin = origin;
            img.fillAmount = 1f;
            return img;
        }

        /// <summary>창/패널 배경 (Pixel UI Panel, 없으면 반투명 사각형)</summary>
        public static Image Panel(string name, Transform parent, string sprite = "Panels/Blue/Panel")
        {
            var s = Px(sprite);
            return Img(name, parent, s ?? Fallback, s != null ? Color.white : new Color(0.08f, 0.1f, 0.18f, 0.92f), true);
        }

        public static Image Solid(string name, Transform parent, Color color, bool raycast = false) =>
            Img(name, parent, White, color, raycast);

        // ── Text ──
        public static TextMeshProUGUI Text(string name, Transform parent, string text, TMP_FontAsset font, float size,
            TextAlignmentOptions align = TextAlignmentOptions.Left, Color? color = null)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font != null ? font : Body;
            t.fontSize = size;
            t.text = text;
            t.alignment = align;
            t.color = color ?? TextColor;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.extraPadding = false;
            return t;
        }

        public static TextMeshProUGUI Body12(string name, Transform parent, string text, TextAlignmentOptions a = TextAlignmentOptions.Left, Color? c = null)
            => Text(name, parent, text, Body, 12, a, c);
        public static TextMeshProUGUI Small10(string name, Transform parent, string text, TextAlignmentOptions a = TextAlignmentOptions.Left, Color? c = null)
            => Text(name, parent, text, Small, 10, a, c);
        public static TextMeshProUGUI Title15(string name, Transform parent, string text, TextAlignmentOptions a = TextAlignmentOptions.Center, Color? c = null)
            => Text(name, parent, text, Title, 15, a, c);

        // ── Button ──
        public static Button Button(string name, Transform parent, string label, out TextMeshProUGUI labelText)
        {
            var bg = Panel(name, parent, "Panels/Blue/Panel");
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var colors = btn.colors;
            colors.normalColor = new Color(0.85f, 0.88f, 1f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = new Color(1f, 0.93f, 0.6f);
            colors.pressedColor = new Color(0.7f, 0.75f, 0.9f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.5f, 0.8f);
            colors.fadeDuration = 0.05f;
            btn.colors = colors;
            labelText = Body12("Label", bg.transform, label, TextAlignmentOptions.Center);
            labelText.rectTransform.Stretch(2, 2, 1, 1);
            return btn;
        }

        public static Button Button(string name, Transform parent, string label) => Button(name, parent, label, out _);

        public static CanvasGroup Group(GameObject go)
        {
            var cg = go.GetComponent<CanvasGroup>();
            return cg != null ? cg : go.AddComponent<CanvasGroup>();
        }

        public static T Add<T>(Component c) where T : Component => c.gameObject.AddComponent<T>();
    }
}
