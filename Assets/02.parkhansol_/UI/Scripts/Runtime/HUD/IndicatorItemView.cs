using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>화면 밖 화살표 한 개 (화살표는 방향으로 회전, 아이콘·글자는 똑바로)</summary>
    [AddComponentMenu("OZ/UI/HUD/Indicator Item View")]
    public class IndicatorItemView : MonoBehaviour
    {
        [SerializeField] internal CanvasGroup group;
        [SerializeField] internal RectTransform arrow;
        [SerializeField] internal Image arrowImage;
        [SerializeField] internal Image icon;
        [SerializeField] internal Image iconBack;
        [SerializeField] internal TMP_Text label;
        [Tooltip("아이콘이 화살표 안쪽으로 떨어진 거리")]
        [SerializeField] internal float iconInset = 14f;

        internal void Setup(Sprite iconSprite, Color color)
        {
            if (arrowImage != null) arrowImage.color = color;
            if (icon != null)
            {
                icon.sprite = iconSprite;
                icon.enabled = iconSprite != null;
                icon.color = Color.white;
            }
            if (iconBack != null) { var c = color; c.a = 0.9f; iconBack.color = c; }
            if (label != null) label.color = color;
        }

        internal void SetAlpha(float a)
        {
            if (group != null) group.alpha = a;
        }

        internal void Place(Vector2 edgePos, Vector2 dir, float bob, string text)
        {
            var rt = (RectTransform)transform;
            Vector2 p = edgePos + dir * bob;
            rt.anchoredPosition = new Vector2(Mathf.Round(p.x), Mathf.Round(p.y)); // 픽셀 맞춤
            if (arrow != null) arrow.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            Vector2 inner = -dir * iconInset;
            if (iconBack != null) iconBack.rectTransform.anchoredPosition = new Vector2(Mathf.Round(inner.x), Mathf.Round(inner.y));
            if (label != null)
            {
                label.text = text ?? "";
                // 글자는 아이콘 안쪽(화면 가운데 쪽) 아래
                Vector2 lp = inner + new Vector2(0f, -15f);
                if (dir.y < -0.7f) lp = inner + new Vector2(0f, 15f); // 아래쪽 화살표면 글자는 위로
                label.rectTransform.anchoredPosition = new Vector2(Mathf.Round(lp.x), Mathf.Round(lp.y));
            }
        }
    }
}
