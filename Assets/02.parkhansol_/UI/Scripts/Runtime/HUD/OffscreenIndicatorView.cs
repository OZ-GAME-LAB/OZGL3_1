using System.Collections.Generic;
using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// [v0.7] GameUI.Indicators 구현 — 추적 대상이 화면 밖이면 화면 가장자리에 화살표 + 아이콘 + 거리(m).
    ///   화면 안으로 들어오면 부드럽게 사라지고, 다시 나가면 나타남.
    ///   HUD 모서리(체력·지도·단축키)를 피하도록 안쪽 여백(edgePadding) 사각형 테두리를 따라 움직인다.
    ///   카메라는 Camera.main. 거리는 UIPlayerTag.Current 기준 (없으면 거리 숨김).
    /// </summary>
    [AddComponentMenu("OZ/UI/HUD/Offscreen Indicator View")]
    public class OffscreenIndicatorView : MonoBehaviour, IIndicatorApi
    {
        [SerializeField] internal RectTransform area;
        [SerializeField] internal IndicatorItemView template;
        [Tooltip("화면 가장자리 안쪽 여백 (왼, 오, 위, 아래) — 논리 px")]
        [SerializeField] internal Vector4 edgePadding = new Vector4(18f, 18f, 30f, 30f);
        [Tooltip("대상 기준점 위로 올릴 높이 (월드) — 발밑 대신 몸통을 가리키게")]
        [SerializeField] internal float worldHeight = 1f;
        [SerializeField] internal Sprite gateIcon;
        [SerializeField] internal Sprite bossIcon;
        [SerializeField] internal Sprite objectiveIcon;
        [SerializeField] internal Color gateColor = new Color(0.4f, 0.86f, 1f);
        [SerializeField] internal Color bossColor = new Color(1f, 0.4f, 0.4f);
        [SerializeField] internal Color objectiveColor = new Color(1f, 0.85f, 0.4f);
        [SerializeField] internal bool showDistance = true;

        class Entry
        {
            public Transform target;
            public IndicatorKind kind;
            public string label;
            public IndicatorItemView view;
            public float alpha;
        }

        readonly List<Entry> _entries = new List<Entry>();
        readonly Stack<IndicatorItemView> _pool = new Stack<IndicatorItemView>();
        Camera _cam;

        public int Count => _entries.Count;
        /// <summary>테스트용: 화면 밖이라 화살표가 보이는 개수</summary>
        public int VisibleCount
        {
            get { int n = 0; foreach (var e in _entries) if (e.alpha > 0.5f) n++; return n; }
        }

        void Awake()
        {
            if (template != null) template.gameObject.SetActive(false);
            if (area == null) area = (RectTransform)transform;
        }

        void OnEnable() => GameUI.Register((IIndicatorApi)this);
        void OnDisable() => GameUI.Unregister(this);

        public void Track(Transform target, IndicatorKind kind = IndicatorKind.Objective, string label = null)
        {
            if (target == null || template == null) return;
            var e = Find(target);
            if (e == null)
            {
                e = new Entry { target = target, view = Rent() };
                _entries.Add(e);
            }
            e.kind = kind;
            e.label = label;
            var col = ColorOf(kind);
            e.view.Setup(IconOf(kind), col);
            e.alpha = 0f;
            e.view.SetAlpha(0f);
        }

        public void Untrack(Transform target)
        {
            for (int i = _entries.Count - 1; i >= 0; i--)
                if (_entries[i].target == target) Remove(i);
        }

        public void Clear()
        {
            for (int i = _entries.Count - 1; i >= 0; i--) Remove(i);
        }

        public bool IsTracking(Transform target) => Find(target) != null;

        Entry Find(Transform t)
        {
            foreach (var e in _entries) if (e.target == t) return e;
            return null;
        }

        void Remove(int i)
        {
            var v = _entries[i].view;
            _entries.RemoveAt(i);
            if (v == null) return;
            v.gameObject.SetActive(false);
            _pool.Push(v);
        }

        IndicatorItemView Rent()
        {
            var v = _pool.Count > 0 ? _pool.Pop() : Instantiate(template, template.transform.parent);
            v.gameObject.SetActive(true);
            return v;
        }

        Sprite IconOf(IndicatorKind k) => k == IndicatorKind.Gate ? gateIcon : k == IndicatorKind.Boss ? bossIcon : objectiveIcon;
        Color ColorOf(IndicatorKind k) => k == IndicatorKind.Gate ? gateColor : k == IndicatorKind.Boss ? bossColor : objectiveColor;

        void LateUpdate()
        {
            if (_entries.Count == 0) return;
            if (_cam == null || !_cam.isActiveAndEnabled) _cam = Camera.main;
            var player = UIPlayerTag.Current;
            Rect r = area.rect;
            float minX = r.xMin + edgePadding.x, maxX = r.xMax - edgePadding.y;
            float minY = r.yMin + edgePadding.w, maxY = r.yMax - edgePadding.z;
            Vector2 center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            float halfW = (maxX - minX) * 0.5f, halfH = (maxY - minY) * 0.5f;
            float dt = Time.unscaledDeltaTime;

            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                var e = _entries[i];
                if (e.target == null) { Remove(i); continue; }

                bool show = false;
                Vector2 pos = center, dir = Vector2.right;
                if (_cam != null && e.target.gameObject.activeInHierarchy)
                {
                    Vector3 sp = _cam.WorldToScreenPoint(e.target.position + Vector3.up * worldHeight);
                    bool behind = sp.z < 0f;
                    if (behind) { sp.x = Screen.width - sp.x; sp.y = Screen.height - sp.y; }
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(area, sp, null, out var local);
                    bool inside = !behind && local.x >= minX && local.x <= maxX && local.y >= minY && local.y <= maxY;
                    if (!inside)
                    {
                        show = true;
                        dir = local - center;
                        if (behind && dir.sqrMagnitude < 1f) dir = Vector2.down;
                        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
                        dir.Normalize();
                        float tx = Mathf.Abs(dir.x) > 0.0001f ? halfW / Mathf.Abs(dir.x) : float.MaxValue;
                        float ty = Mathf.Abs(dir.y) > 0.0001f ? halfH / Mathf.Abs(dir.y) : float.MaxValue;
                        pos = center + dir * Mathf.Min(tx, ty);
                    }
                }

                e.alpha = Mathf.MoveTowards(e.alpha, show ? 1f : 0f, dt * (show ? 5f : 7f));
                e.view.SetAlpha(e.alpha);
                if (e.alpha <= 0f) continue;

                string text = e.label;
                if (showDistance && player != null)
                {
                    float d = Vector3.Distance(player.position, e.target.position);
                    text = string.IsNullOrEmpty(e.label) ? $"{d:0}m" : $"{e.label} {d:0}m";
                }
                float bob = Mathf.Sin(Time.unscaledTime * 5f) * 1.5f;
                e.view.Place(pos, dir, bob, text);
            }
        }
    }
}
