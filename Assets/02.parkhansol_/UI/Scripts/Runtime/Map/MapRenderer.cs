using System.Collections.Generic;
using DG.Tweening;
using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// MapData를 방 단위 사각형으로 그린다. 미니맵(현재 방 따라가기)과 전체 지도 공용.
    /// 상태: 미발견(안 그림) / 발견(어둡게) / 방문(밝게) / 현재(강조 + 마커 깜빡임)
    /// </summary>
    [AddComponentMenu("OZ/UI/Map/Map Renderer")]
    public class MapRenderer : MonoBehaviour
    {
        [SerializeField] internal RectTransform content;
        [SerializeField] internal Image roomTemplate;
        [SerializeField] internal Image linkTemplate;
        [SerializeField] internal Image iconTemplate;
        [SerializeField] internal RectTransform playerMarker;

        [SerializeField] internal float cellSize = 10f;
        [SerializeField] internal float gap = 1f;
        [Tooltip("현재 방이 가운데 오도록 이동 (미니맵)")]
        [SerializeField] internal bool followCurrent = true;

        [SerializeField] internal Color revealedColor = new Color(0.35f, 0.4f, 0.55f, 1f);
        [SerializeField] internal Color visitedColor = new Color(0.55f, 0.75f, 1f, 1f);
        [SerializeField] internal Color currentColor = new Color(1f, 0.95f, 0.6f, 1f);
        [SerializeField] internal Color linkColor = new Color(0.55f, 0.75f, 1f, 0.6f);

        [Tooltip("MapRoomIcon 순서대로 (None, Start, Gate, Boss, Save, Item)")]
        [SerializeField] internal Sprite[] iconSprites = new Sprite[6];
        [SerializeField] internal Color[] iconColors =
        {
            Color.clear, Color.white, new Color(0.7f, 0.5f, 1f), new Color(1f, 0.35f, 0.35f), new Color(0.4f, 1f, 0.6f), new Color(1f, 0.85f, 0.3f),
        };

        readonly Dictionary<string, Image> _rooms = new Dictionary<string, Image>();
        readonly List<GameObject> _spawned = new List<GameObject>();
        Vector2 _center;

        void Awake()
        {
            if (roomTemplate != null) roomTemplate.gameObject.SetActive(false);
            if (linkTemplate != null) linkTemplate.gameObject.SetActive(false);
            if (iconTemplate != null) iconTemplate.gameObject.SetActive(false);
            if (playerMarker != null) playerMarker.gameObject.SetActive(false);
        }

        public void Rebuild(MapState state)
        {
            Clear();
            var map = state?.Map;
            if (map == null || roomTemplate == null || content == null) return;

            RectInt b = map.GetBounds();
            _center = new Vector2(b.x + b.width * 0.5f, b.y + b.height * 0.5f);

            // 연결선 먼저 (방 아래에 깔리도록)
            if (linkTemplate != null && map.links != null)
            {
                foreach (var link in map.links)
                {
                    MapRoom a = map.Find(link.fromRoom), c = map.Find(link.toRoom);
                    if (a == null || c == null || !state.IsRevealed(a.id) || !state.IsRevealed(c.id)) continue;
                    var img = Spawn(linkTemplate);
                    img.color = linkColor;
                    Vector2 pa = RoomCenter(a), pc = RoomCenter(c);
                    var rt = img.rectTransform;
                    rt.anchoredPosition = (pa + pc) * 0.5f;
                    rt.sizeDelta = new Vector2(Vector2.Distance(pa, pc), Mathf.Max(1f, gap * 2f));
                    rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(pc.y - pa.y, pc.x - pa.x) * Mathf.Rad2Deg);
                }
            }

            foreach (var room in map.rooms)
            {
                if (!state.IsRevealed(room.id)) continue;
                var img = Spawn(roomTemplate);
                var rt = img.rectTransform;
                Vector2Int size = new Vector2Int(Mathf.Max(1, room.size.x), Mathf.Max(1, room.size.y));
                rt.sizeDelta = new Vector2(size.x * cellSize - gap, size.y * cellSize - gap);
                rt.anchoredPosition = RoomCenter(room);
                img.name = "Room_" + room.id;
                _rooms[room.id] = img;

                MapRoomIcon icon = state.GetIcon(room);
                if (icon != MapRoomIcon.None && iconTemplate != null)
                {
                    var ic = Spawn(iconTemplate);
                    int i = (int)icon;
                    ic.sprite = iconSprites != null && i < iconSprites.Length ? iconSprites[i] : null;
                    ic.color = iconColors != null && i < iconColors.Length ? iconColors[i] : Color.white;
                    ic.rectTransform.anchoredPosition = rt.anchoredPosition;
                    float s = Mathf.Min(rt.sizeDelta.x, rt.sizeDelta.y) * 0.5f;
                    ic.rectTransform.sizeDelta = new Vector2(s, s);
                }
            }

            if (playerMarker != null) playerMarker.SetAsLastSibling();
            Refresh(state, false);
        }

        public void Refresh(MapState state, bool animate = true)
        {
            var map = state?.Map;
            if (map == null) return;
            foreach (var kv in _rooms)
            {
                bool current = kv.Key == state.CurrentRoom;
                kv.Value.color = current ? currentColor : state.IsVisited(kv.Key) ? visitedColor : revealedColor;
            }

            MapRoom cur = map.Find(state.CurrentRoom);
            if (playerMarker != null)
            {
                bool show = cur != null && _rooms.ContainsKey(cur.id);
                if (show && !playerMarker.gameObject.activeSelf)
                {
                    playerMarker.gameObject.SetActive(true);
                    var g = playerMarker.GetComponent<Graphic>();
                    if (g != null)
                    {
                        g.DOKill();
                        g.DOFade(0.25f, 0.4f).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(playerMarker.gameObject);
                    }
                }
                else if (!show) playerMarker.gameObject.SetActive(false);
                if (show) playerMarker.anchoredPosition = RoomCenter(cur);
            }

            if (followCurrent && cur != null && content != null)
            {
                Vector2 target = -RoomCenter(cur);
                content.DOKill();
                if (animate) content.DOAnchorPos(target, 0.2f).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(content.gameObject);
                else content.anchoredPosition = target;
            }
            else if (!followCurrent && content != null)
            {
                content.anchoredPosition = Vector2.zero;
            }
        }

        Vector2 RoomCenter(MapRoom r)
        {
            Vector2 c = new Vector2(r.position.x + Mathf.Max(1, r.size.x) * 0.5f, r.position.y + Mathf.Max(1, r.size.y) * 0.5f);
            return (c - _center) * cellSize;
        }

        Image Spawn(Image template)
        {
            var img = Instantiate(template, content);
            img.gameObject.SetActive(true);
            _spawned.Add(img.gameObject);
            return img;
        }

        void Clear()
        {
            foreach (var go in _spawned) if (go != null) Destroy(go);
            _spawned.Clear();
            _rooms.Clear();
        }
    }
}
