using TMPro;
using UnityEngine;

namespace OZ.UI
{
    /// <summary>전체 지도 창 (M)</summary>
    [AddComponentMenu("OZ/UI/Map/Map Window")]
    public class MapWindow : UIWindow
    {
        [SerializeField] internal MapController controller;
        [SerializeField] internal MapRenderer fullMap;
        [SerializeField] internal TMP_Text titleText;
        [SerializeField] internal TMP_Text roomText;

        protected override void OnOpened()
        {
            if (controller == null || fullMap == null) return;
            fullMap.Rebuild(controller.State);
            var map = controller.State.Map;
            if (titleText != null) titleText.text = map != null ? map.displayName : "지도 없음";
            var room = map != null ? map.Find(controller.State.CurrentRoom) : null;
            if (roomText != null) roomText.text = room != null ? "현재 위치: " + room.displayName : "";
        }
    }
}
