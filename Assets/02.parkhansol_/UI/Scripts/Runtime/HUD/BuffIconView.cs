using System.Collections.Generic;
using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>버프 아이콘 1개 (아이콘 + 남은 시간 원형)</summary>
    [AddComponentMenu("OZ/UI/HUD/Buff Icon View")]
    public class BuffIconView : MonoBehaviour
    {
        [SerializeField] internal Image icon;
        [SerializeField] internal Image tint;
        [SerializeField] internal CooldownRadial timer;

        internal void Show(ItemData item, float duration)
        {
            if (icon != null)
            {
                icon.sprite = item != null ? item.icon : null;
                icon.enabled = icon.sprite != null;
            }
            if (tint != null && item != null) tint.color = item.fxColor;
            if (timer != null) timer.Play(duration, duration);
            ((RectTransform)transform).PopIn();
        }
    }
}
