using System.Collections.Generic;
using DG.Tweening;
using OZ.UI.Contracts;
using TMPro;
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

        [Header("v1.5 지도 없음 표시")]
        [Tooltip("지도 데이터가 없을 때 칸 가운데에 띄울 흐린 문구 (비우면 표시 안 함)")]
        [SerializeField] internal string emptyMessage = "지도 정보 없음";
        [Tooltip("문구 글자 (비우면 실행 중에 만든다)")]
        [SerializeField] internal TMP_Text emptyLabel;
        [SerializeField] internal Color emptyColor = new Color(0.55f, 0.62f, 0.78f, 0.75f);

        [Tooltip("MapRoomIcon 순서대로 (None, Start, Gate, Boss, Save, Item)")]
        [SerializeField] internal Sprite[] iconSprites = new Sprite[6];
        [SerializeField] internal Color[] iconColors =
        {
            Color.clear, Color.white, new Color(0.7f, 0.5f, 1f), new Color(1f, 0.35f, 0.35f), new Color(0.4f, 1f, 0.6f), new Color(1f, 0.85f, 0.3f),
        };

        readonly Dictionary<string, Image> _rooms = new Dictionary<string, Image>();
        readonly List<GameObject> _spawned = new List<GameObject>();
        Vector2 _center;
        bool _hasMap;

        /// <summary>지금 그린 지도가 있는지 (없으면 '지도 정보 없음' 표시 중)</summary>
        internal bool HasMap => _hasMap;

        void Awake()
        {
            if (roomTemplate != null) roomTemplate.gameObject.SetActive(false);
            if (linkTemplate != null) linkTemplate.gameObject.SetActive(false);
            if (iconTemplate != null) iconTemplate.gameObject.SetActive(false);
            if (playerMarker != null) playerMarker.gameObject.SetActive(false);
        }

        // 아무도 SetMap을 안 부른 씬(지도 데이터 없음)에서도 빈 검은 칸 대신 안내가 보이도록
        void Start() { if (!_hasMap) ShowEmpty(true); }

        public void Rebuild(MapState state)
        {
            Clear();
            var map = state?.Map;
            _hasMap = map != null && map.rooms != null && map.rooms.Count > 0;
            ShowEmpty(!_hasMap);
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

        void ShowEmpty(bool show)
        {
            if (string.IsNullOrEmpty(emptyMessage)) show = false;
            if (show && emptyLabel == null) emptyLabel = CreateEmptyLabel();
            if (emptyLabel == null) return;
            emptyLabel.text = emptyMessage;
            emptyLabel.gameObject.SetActive(show);
        }

        // 프리팹을 다시 만들지 않아도 되게 실행 중에 만든다. 글꼴은 같은 화면의 다른 글자에서 빌림
        TMP_Text CreateEmptyLabel()
        {
            var go = new GameObject("EmptyLabel", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            var canvas = GetComponentInParent<Canvas>();
            var donor = canvas != null ? canvas.GetComponentInChildren<TMP_Text>(true) : null;
            if (donor != null) { t.font = donor.font; t.fontSharedMaterial = donor.fontSharedMaterial; }
            t.fontSize = 9f;
            t.alignment = TextAlignmentOptions.Center;
            t.color = emptyColor;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
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
