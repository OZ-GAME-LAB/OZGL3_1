using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// v1.5: 픽셀 패널(Panels/Blue/Panel) 안쪽이 반투명이라, 다른 창이나 HUD 위에 뜨면 뒤가 비쳐 글자를 읽기 어려움 →
    /// 패널 안쪽에 어두운 바탕을 한 장 깔아 준다. 프리팹을 다시 만들지 않아도 되게 실행 중에 만든다.
    /// </summary>
    internal static class PanelBacking
    {
        internal static readonly Color Default = new Color(0.015f, 0.02f, 0.05f, 0.94f);
        internal static readonly Color Opaque = new Color(0.015f, 0.02f, 0.05f, 1f);

        internal static Image Ensure(RectTransform panel, Color? color = null, float inset = 2f)
        {
            if (panel == null) return null;
            var found = panel.Find("Backing (auto)");
            if (found != null) return found.GetComponent<Image>();
            var go = new GameObject("Backing (auto)", typeof(RectTransform), typeof(Image));
            go.layer = panel.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(panel, false);
            rt.SetAsFirstSibling(); // 패널 글자·버튼보다 아래 (패널 테두리 그림은 부모라 그대로 보임)
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
            var img = go.GetComponent<Image>();
            img.color = color ?? Default;
            img.raycastTarget = false;
            return img;
        }
    }
}
