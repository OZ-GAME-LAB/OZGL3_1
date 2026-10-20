using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// 상태창 상점 탭 오른쪽 '내 아이템' 한 줄: [1~4 키] 아이콘 이름 ▮▮▮▯▯ 개수.
    /// 장비(1회 구매)는 막대 대신 "보유 / 없음".
    /// </summary>
    [AddComponentMenu("OZ/UI/Status/Owned Item Row")]
    public class OwnedItemRow : MonoBehaviour
    {
        [SerializeField] internal Image background;
        [SerializeField] internal TMP_Text keyText;
        [SerializeField] internal Image icon;
        [SerializeField] internal TMP_Text nameText;
        [SerializeField] internal Image barBack;
        [SerializeField] internal Image barFill;
        [SerializeField] internal TMP_Text countText;
        [SerializeField] internal Color normalColor = new Color(0.12f, 0.16f, 0.28f, 1f);
        [SerializeField] internal Color selectedColor = new Color(0.22f, 0.32f, 0.52f, 1f);
        [SerializeField] internal Color emptyColor = new Color(0.45f, 0.5f, 0.62f, 1f);
        [SerializeField] internal Color fullColor = new Color(1f, 0.85f, 0.4f);
        [SerializeField] internal Color ownColor = new Color(0.45f, 0.9f, 0.55f);

        internal void Show(ShopEntry entry, int owned, bool selected)
        {
            var item = entry?.item;
            Color c = item != null ? item.fxColor : Color.white;
            bool equip = item != null && (item.IsEquipment || entry.maxOwned == 1);
            int max = entry != null ? entry.maxOwned : 0;

            if (keyText != null) keyText.text = item != null && item.slotIndex >= 0 ? UIText.ItemKey(item.slotIndex) : "";
            if (icon != null) { icon.sprite = item != null ? item.icon : null; icon.enabled = icon.sprite != null; icon.color = owned > 0 ? Color.white : new Color(1f, 1f, 1f, 0.35f); }
            if (nameText != null) { nameText.text = item != null ? item.displayName : "-"; nameText.color = owned > 0 ? c : emptyColor; }

            bool showBar = !equip && max > 0;
            if (barBack != null) barBack.gameObject.SetActive(showBar);
            if (barFill != null) { barFill.fillAmount = showBar ? Mathf.Clamp01((float)owned / max) : 0f; barFill.color = c; }
            if (countText != null)
            {
                if (equip) { countText.text = owned > 0 ? "보유" : "없음"; countText.color = owned > 0 ? ownColor : emptyColor; }
                else { countText.text = max > 0 ? $"{owned}/{max}" : owned.ToString(); countText.color = owned <= 0 ? emptyColor : max > 0 && owned >= max ? fullColor : Color.white; }
            }
            if (background != null) background.color = selected ? selectedColor : normalColor;
        }
    }
}
