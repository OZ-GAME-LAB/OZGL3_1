using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// v1.5: 글자 뒤에 어두운 판을 깔아 밝은 배경(하늘·건물·포션 그림) 위에서도 읽히게 한다.
    /// 글자 크기·위치·투명도(페이드)를 매 프레임 따라가고, 글자가 비거나 꺼지면 판도 숨는다.
    /// 프리팹을 다시 만들지 않아도 되게 실행 중에 붙인다: TextPlate.Attach(text, ...)
    /// </summary>
    [AddComponentMenu("")]
    public sealed class TextPlate : MonoBehaviour
    {
        [SerializeField] TMP_Text target;
        [SerializeField] Vector2 padding = new Vector2(4f, 1f);
        [SerializeField] float baseAlpha = 0.8f;
        Image _img;
        RectTransform _rt;

        internal static readonly Color DefaultColor = new Color(0.02f, 0.03f, 0.07f, 0.8f);

        /// <summary>text 바로 아래(형제 순서 앞)에 판을 만든다. 이미 있으면 그걸 돌려줌</summary>
        internal static TextPlate Attach(TMP_Text text, Color? color = null, Vector2? padding = null, Transform drawBelow = null)
        {
            if (text == null || text.transform.parent == null) return null;
            var parent = text.transform.parent;
            string n = text.name + " Plate (auto)";
            var found = parent.Find(n);
            if (found != null) return found.GetComponent<TextPlate>();

            var go = new GameObject(n, typeof(RectTransform), typeof(Image));
            go.layer = text.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            int below = text.transform.GetSiblingIndex();
            if (drawBelow != null && drawBelow.parent == parent) below = Mathf.Min(below, drawBelow.GetSiblingIndex());
            rt.SetSiblingIndex(below); // 글자(와 그림자 글자)보다 먼저 그려짐 = 아래

            var img = go.GetComponent<Image>();
            var c = color ?? DefaultColor;
            img.color = c;
            img.raycastTarget = false;
            img.enabled = false;

            var p = go.AddComponent<TextPlate>();
            p.target = text;
            p.baseAlpha = c.a;
            if (padding.HasValue) p.padding = padding.Value;
            return p;
        }

        void Awake() { _img = GetComponent<Image>(); _rt = (RectTransform)transform; }

        void LateUpdate()
        {
            if (_img == null) return;
            bool show = target != null && target.isActiveAndEnabled && !string.IsNullOrEmpty(target.text);
            if (!show) { _img.enabled = false; return; }

            var b = target.textBounds; // 글자 칸 기준(피벗 원점) 실제 글자 범위
            if (float.IsNaN(b.size.x) || b.size.x <= 0.01f) { _img.enabled = false; return; }

            // 글자 가운데 → 판의 위치 (부모 가운데 기준 anchoredPosition, 픽셀 반올림)
            var trt = target.rectTransform;
            var prt = (RectTransform)_rt.parent;
            Vector3 local = prt.InverseTransformPoint(trt.TransformPoint(b.center));
            Vector2 toCenter = Vector2.Scale(prt.rect.size, prt.pivot - new Vector2(0.5f, 0.5f));
            _rt.anchoredPosition = new Vector2(Mathf.Round(local.x + toCenter.x), Mathf.Round(local.y + toCenter.y));
            Vector3 s = trt.localScale;
            _rt.sizeDelta = new Vector2(Mathf.Ceil(b.size.x * s.x + padding.x * 2f), Mathf.Ceil(b.size.y * s.y + padding.y * 2f));

            var c = _img.color;
            c.a = baseAlpha * target.alpha; // 글자 페이드(DOFade·alpha)와 같이
            _img.color = c;
            _img.enabled = c.a > 0.01f;
        }
    }
}
