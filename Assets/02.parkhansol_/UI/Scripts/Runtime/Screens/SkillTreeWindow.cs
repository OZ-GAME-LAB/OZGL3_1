using System.Collections.Generic;
using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// 스킬 트리 창 (K). SkillTreeData를 읽어 노드·연결선·안내 글자를 자동 배치한다.
    ///   노드 클릭 = 해금 (ISkillTreeSource.TryUnlock) · 마우스 올림/선택 = 오른쪽 설명 칸
    ///   초기화 버튼 = 두 번 눌러 확정 (ISkillTreeSource.TryResetTree)
    /// 규칙 판단은 전부 소스 쪽 (UI는 결과만 그림).
    /// </summary>
    [AddComponentMenu("OZ/UI/Screens/Skill Tree Window")]
    public class SkillTreeWindow : UIWindow
    {
        [Header("트리 영역")]
        [SerializeField] internal RectTransform treeArea;
        [SerializeField] internal SkillNodeView nodeTemplate;
        [SerializeField] internal Image linkTemplate;
        [SerializeField] internal TMP_Text labelTemplate;
        [Tooltip("격자 1칸 크기. 영역보다 크면 자동으로 줄인다")]
        [SerializeField] internal Vector2 cellSize = new Vector2(32f, 36f);
        [SerializeField] internal float linkThickness = 2f;

        [Header("상단")]
        [SerializeField] internal TMP_Text titleText;
        [SerializeField] internal TMP_Text levelText;
        [SerializeField] internal TMP_Text pointsText;
        [SerializeField] internal RectTransform pointsBadge;
        [SerializeField] internal TMP_Text emptyText;

        [Header("설명 칸")]
        [SerializeField] internal Image detailIcon;
        [SerializeField] internal TMP_Text detailName;
        [SerializeField] internal TMP_Text detailKind;
        [SerializeField] internal TMP_Text detailDesc;
        [SerializeField] internal TMP_Text detailStatus;

        [Header("초기화")]
        [SerializeField] internal Button resetButton;
        [SerializeField] internal TMP_Text resetLabel;
        [SerializeField] internal float resetConfirmSeconds = 2.5f;

        [Header("노드 테두리 (index = SkillNodeState: Locked, Available, Unlocked, Blocked)")]
        [SerializeField] internal Sprite[] skillFrames = new Sprite[4];
        [SerializeField] internal Sprite[] upgradeFrames = new Sprite[4];
        [SerializeField] internal Sprite[] passiveFrames = new Sprite[4];
        [SerializeField] internal Sprite skillSelector, upgradeSelector, passiveSelector;

        [Header("연결선 색")]
        [SerializeField] internal Color linkUnlocked = new Color(1f, 0.82f, 0.45f);
        [SerializeField] internal Color linkAvailable = new Color(0.35f, 0.7f, 1f);
        [SerializeField] internal Color linkLocked = new Color(0.36f, 0.4f, 0.52f, 0.8f);
        [SerializeField] internal Color linkBlocked = new Color(0.55f, 0.22f, 0.28f, 0.6f);

        [Header("상태 글자 색")]
        [SerializeField] internal Color okColor = new Color(0.55f, 1f, 0.65f);
        [SerializeField] internal Color availableColor = new Color(1f, 0.85f, 0.4f);
        [SerializeField] internal Color deniedColor = new Color(1f, 0.5f, 0.5f);

        struct Link { public string from, to; public Image img; }

        ISkillTreeSource _src;
        IProgressionSource _progress;
        SkillTreeData _built;
        readonly List<SkillNodeView> _views = new List<SkillNodeView>();
        readonly List<Link> _links = new List<Link>();
        readonly List<GameObject> _spawned = new List<GameObject>();
        SkillNodeView _focus;
        string _deniedReason;
        float _resetArmedUntil = -1f;

        internal IReadOnlyList<SkillNodeView> Views => _views;
        internal SkillNodeView Focused => _focus;

        protected override void Awake()
        {
            base.Awake();
            if (nodeTemplate != null) nodeTemplate.gameObject.SetActive(false);
            if (linkTemplate != null) linkTemplate.gameObject.SetActive(false);
            if (labelTemplate != null) labelTemplate.gameObject.SetActive(false);
            if (resetButton != null) resetButton.onClick.AddListener(OnResetClicked);
        }

        protected override void OnOpened()
        {
            _src = UISources.SkillTree;
            _progress = UISources.Progression;
            if (_src != null) _src.TreeChanged += Refresh;
            if (_progress != null) _progress.ProgressionChanged += Refresh;

            var tree = _src?.Tree;
            if (tree != _built) Build(tree);
            _resetArmedUntil = -1f;
            _deniedReason = null;
            Refresh();

            var first = FirstFocus();
            Focus(first);
            if (first != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        protected override void OnClosed()
        {
            if (_src != null) _src.TreeChanged -= Refresh;
            if (_progress != null) _progress.ProgressionChanged -= Refresh;
            _src = null; _progress = null;
        }

        void Update()
        {
            if (!IsOpen) return;
            if (_resetArmedUntil > 0f && Time.unscaledTime > _resetArmedUntil) { _resetArmedUntil = -1f; RefreshReset(); }
        }

        // ───────────── 배치 ─────────────

        void Build(SkillTreeData tree)
        {
            foreach (var go in _spawned) if (go != null) Destroy(go);
            _spawned.Clear(); _views.Clear(); _links.Clear();
            _focus = null;
            _built = tree;

            bool empty = tree == null || tree.nodes == null || tree.nodes.Count == 0;
            if (emptyText != null) emptyText.gameObject.SetActive(empty);
            if (empty || treeArea == null || nodeTemplate == null) return;

            // 격자 범위 (노드 + 글자)
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            void Grow(Vector2 p) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            foreach (var n in tree.nodes) if (n != null) Grow(n.gridPos);
            if (tree.labels != null) foreach (var l in tree.labels) Grow(l.gridPos);

            Vector2 span = max - min;
            Vector2 area = treeArea.rect.size - new Vector2(32f, 32f);
            Vector2 cell = cellSize;
            if (span.x > 0f) cell.x = Mathf.Min(cell.x, Mathf.Floor(area.x / span.x));
            if (span.y > 0f) cell.y = Mathf.Min(cell.y, Mathf.Floor(area.y / span.y));
            Vector2 center = (min + max) * 0.5f;
            Vector2 Pos(Vector2 g) => new Vector2(Mathf.Round((g.x - center.x) * cell.x), Mathf.Round(-(g.y - center.y) * cell.y));

            // 1) 연결선 (노드 뒤)
            if (linkTemplate != null)
                foreach (var n in tree.nodes)
                {
                    if (n == null || n.requires == null) continue;
                    foreach (var req in n.requires)
                    {
                        var from = tree.Find(req);
                        if (from == null) continue;
                        var img = Instantiate(linkTemplate, treeArea);
                        img.gameObject.SetActive(true);
                        img.name = $"Link_{from.id}_{n.id}";
                        Vector2 a = Pos(from.gridPos), b = Pos(n.gridPos);
                        var rt = img.rectTransform;
                        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                        rt.anchoredPosition = (a + b) * 0.5f;
                        rt.sizeDelta = new Vector2((b - a).magnitude, linkThickness);
                        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
                        _links.Add(new Link { from = from.id, to = n.id, img = img });
                        _spawned.Add(img.gameObject);
                    }
                }

            // 2) 안내 글자
            if (labelTemplate != null && tree.labels != null)
                foreach (var l in tree.labels)
                {
                    var t = Instantiate(labelTemplate, treeArea);
                    t.gameObject.SetActive(true);
                    t.name = "Label_" + l.text;
                    t.text = l.text;
                    if (l.small) { t.fontSize = Mathf.Max(8f, t.fontSize * 0.8f); t.color = new Color(t.color.r, t.color.g, t.color.b, 0.7f); }
                    var rt = t.rectTransform;
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = Pos(l.gridPos);
                    _spawned.Add(t.gameObject);
                }

            // 3) 노드
            foreach (var n in tree.nodes)
            {
                if (n == null) continue;
                var v = Instantiate(nodeTemplate, treeArea);
                v.gameObject.SetActive(true);
                float size = n.kind == SkillNodeKind.Skill ? 28f : n.kind == SkillNodeKind.Upgrade ? 21f : 22f;
                v.Bind(n, size, 16f);
                v.Rect.anchorMin = v.Rect.anchorMax = v.Rect.pivot = new Vector2(0.5f, 0.5f);
                v.Rect.anchoredPosition = Pos(n.gridPos);
                if (v.selector != null)
                    v.selector.sprite = n.kind == SkillNodeKind.Skill ? skillSelector : n.kind == SkillNodeKind.Upgrade ? upgradeSelector : passiveSelector;
                v.Hovered += Focus;
                v.Clicked += OnNodeClicked;
                _views.Add(v);
                _spawned.Add(v.gameObject);
            }
        }

        // ───────────── 표시 ─────────────

        int Level => _progress != null ? _progress.Level : 1;
        int Points => _progress != null ? _progress.SkillPoints : 0;

        internal SkillNodeState StateOf(SkillTreeNode n)
        {
            if (_src == null || n == null) return SkillNodeState.Locked;
            if (_src.IsUnlocked(n.id)) return SkillNodeState.Unlocked;
            if (!string.IsNullOrEmpty(n.exclusiveGroup) && _built != null)
                foreach (var o in _built.nodes)
                    if (o != null && o != n && o.exclusiveGroup == n.exclusiveGroup && _src.IsUnlocked(o.id))
                        return SkillNodeState.Blocked;
            return _src.CanUnlock(n.id, out _) ? SkillNodeState.Available : SkillNodeState.Locked;
        }

        Sprite FrameFor(SkillTreeNode n, SkillNodeState s)
        {
            var set = n.kind == SkillNodeKind.Skill ? skillFrames : n.kind == SkillNodeKind.Upgrade ? upgradeFrames : passiveFrames;
            int i = (int)s;
            return set != null && i < set.Length ? set[i] : null;
        }

        void Refresh()
        {
            _deniedReason = null;
            if (titleText != null) titleText.text = _built != null && !string.IsNullOrEmpty(_built.displayName) ? _built.displayName : "스킬 트리";
            if (levelText != null)
                levelText.text = _progress == null ? "" : _progress.ExpToNextLevel <= 0f ? $"Lv.{Level}  (최대)" : $"Lv.{Level}";
            if (pointsText != null) pointsText.text = $"스킬 포인트  {Points}";

            var states = new Dictionary<string, SkillNodeState>();
            foreach (var v in _views)
            {
                var s = StateOf(v.Node);
                states[v.Node.id] = s;
                v.SetState(s, FrameFor(v.Node, s));
            }

            foreach (var l in _links)
            {
                states.TryGetValue(l.from, out var a);
                states.TryGetValue(l.to, out var b);
                l.img.color = a == SkillNodeState.Unlocked && b == SkillNodeState.Unlocked ? linkUnlocked
                            : a == SkillNodeState.Blocked || b == SkillNodeState.Blocked ? linkBlocked
                            : b == SkillNodeState.Available ? linkAvailable : linkLocked;
            }

            RefreshDetail();
            RefreshReset();
        }

        void Focus(SkillNodeView v)
        {
            if (_focus != null) _focus.SetHighlighted(false);
            _focus = v;
            _deniedReason = null;
            if (_focus != null) _focus.SetHighlighted(true);
            RefreshDetail();
        }

        void RefreshDetail()
        {
            var n = _focus != null ? _focus.Node : null;
            if (detailIcon != null) { detailIcon.sprite = n?.Icon; detailIcon.enabled = detailIcon.sprite != null; }
            if (detailName != null) detailName.text = n != null ? n.DisplayName : "";
            if (detailKind != null) detailKind.text = n == null ? "" : KindLine(n);
            if (detailDesc != null) detailDesc.text = n != null ? n.Describe() : "노드에 마우스를 올리면 설명이 나옵니다.";
            if (detailStatus == null) return;
            if (n == null) { detailStatus.text = ""; return; }

            var s = _focus.State;
            string cost = n.cost > 0 ? $"비용 {n.cost}P" : "무료";
            string reason = null;
            if (s == SkillNodeState.Locked && _src != null) _src.CanUnlock(n.id, out reason);
            if (!string.IsNullOrEmpty(_deniedReason)) { detailStatus.text = _deniedReason; detailStatus.color = deniedColor; return; }
            switch (s)
            {
                case SkillNodeState.Unlocked: detailStatus.text = "해금됨"; detailStatus.color = okColor; break;
                case SkillNodeState.Available: detailStatus.text = $"클릭해서 해금  ·  {cost}"; detailStatus.color = availableColor; break;
                case SkillNodeState.Blocked: detailStatus.text = "택1 — 다른 쪽을 골랐음 (초기화로 변경)"; detailStatus.color = deniedColor; break;
                default: detailStatus.text = $"{reason ?? "잠김"}  ·  {cost}"; detailStatus.color = deniedColor; break;
            }
        }

        static string KindLine(SkillTreeNode n)
        {
            switch (n.kind)
            {
                case SkillNodeKind.Skill:
                    string cls = n.skill != null ? UIText.ClassName(n.skill.playerClass) + " " : "";
                    return $"{UIText.Key(n.slot)} 버튼 · {cls}스킬";
                case SkillNodeKind.Upgrade: return $"{UIText.Key(n.slot)} 강화 · {n.grantsRank}단계";
                default: return "패시브";
            }
        }

        void RefreshReset()
        {
            if (resetButton == null) return;
            string reason = null;
            bool can = _src != null && _src.CanResetTree(out reason);
            resetButton.interactable = can;
            if (resetLabel != null) resetLabel.text = !can ? "초기화" : _resetArmedUntil > 0f ? "한 번 더: 초기화" : "초기화";
        }

        SkillNodeView FirstFocus()
        {
            foreach (var v in _views) if (v.State == SkillNodeState.Available) return v;
            return _views.Count > 0 ? _views[0] : null;
        }

        // ───────────── 입력 ─────────────

        void OnNodeClicked(SkillNodeView v)
        {
            Focus(v);
            if (_src == null) return;
            if (_src.IsUnlocked(v.Node.id)) return;
            if (_src.CanUnlock(v.Node.id, out var reason) && _src.TryUnlock(v.Node.id))
            {
                Refresh(); // 이벤트가 없는 소스도 갱신
                v.PlayUnlocked();
                UISfx.Play(UISound.Unlock);
                if (pointsBadge != null) pointsBadge.Punch(0.2f);
            }
            else
            {
                Refresh();
                _deniedReason = reason ?? "해금할 수 없음";
                RefreshDetail();
                v.PlayDenied();
                UISfx.Play(UISound.Error);
            }
        }

        void OnResetClicked()
        {
            if (_src == null || !_src.CanResetTree(out _)) return;
            if (_resetArmedUntil < 0f)
            {
                _resetArmedUntil = Time.unscaledTime + resetConfirmSeconds;
                UISfx.Play(UISound.ToastWarning);
                RefreshReset();
                return;
            }
            _resetArmedUntil = -1f;
            if (_src.TryResetTree())
            {
                UISfx.Play(UISound.Reset);
                Refresh();
                if (pointsBadge != null) pointsBadge.Punch(0.25f);
                foreach (var v in _views) if (v.State != SkillNodeState.Unlocked) v.Rect.Punch(0.1f);
            }
            RefreshReset();
        }
    }
}
