using System.Collections.Generic;
using DG.Tweening;
using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// 짧은 알림 스택 (GameUI.Notify 구현). template(비활성 자식)을 복제해 아래→위로 쌓는다.
    /// </summary>
    [AddComponentMenu("OZ/UI/HUD/Toast View")]
    public class ToastView : MonoBehaviour, INotifyApi
    {
        [SerializeField] internal RectTransform template;
        [SerializeField] internal float holdSeconds = 2f;
        [SerializeField] internal int maxCount = 4;
        [SerializeField] internal Color infoColor = Color.white;
        [SerializeField] internal Color successColor = new Color(0.55f, 1f, 0.6f);
        [SerializeField] internal Color warningColor = new Color(1f, 0.75f, 0.35f);

        readonly List<RectTransform> _live = new List<RectTransform>();

        void Awake()
        {
            if (template != null) template.gameObject.SetActive(false);
        }

        void OnEnable() => GameUI.Register((INotifyApi)this);
        void OnDisable() => GameUI.Unregister(this);

        public void Toast(string message, ToastType type = ToastType.Info)
        {
            if (template == null) { Debug.Log("[Toast] " + message); return; }

            while (_live.Count >= maxCount) Remove(_live[0]);

            var item = Instantiate(template, template.parent);
            item.gameObject.SetActive(true);
            item.SetAsLastSibling();
            _live.Add(item);

            var text = item.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = message;
                text.color = type == ToastType.Success ? successColor : type == ToastType.Warning ? warningColor : infoColor;
            }

            var cg = item.GetComponent<CanvasGroup>();
            if (cg == null) cg = item.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            // 레이아웃 그룹이 있으면 위치를 먼저 확정
            var layout = template.parent as RectTransform;
            if (layout != null) LayoutRebuilder.ForceRebuildLayoutImmediate(layout);

            DOTween.Sequence()
                .Append(cg.DOFade(1f, UITweenStyle.Fast))
                .AppendInterval(holdSeconds)
                .Append(cg.DOFade(0f, UITweenStyle.Slow))
                .OnComplete(() => Remove(item))
                .SetUpdate(true).SetLink(item.gameObject);
        }

        void Remove(RectTransform item)
        {
            _live.Remove(item);
            if (item != null) Destroy(item.gameObject);
        }
    }
}
