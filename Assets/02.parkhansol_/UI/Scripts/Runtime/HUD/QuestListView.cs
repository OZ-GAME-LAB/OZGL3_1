using System.Collections.Generic;
using DG.Tweening;
using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// GameUI.Quest 구현: 좌상단 체력 블록 아래 퀘스트 목록 (최대 maxRows줄).
    ///   - IGateSource가 연결되면 "게이트 봉쇄 n / 목표" + "보스 처치 (게이트 후)" 두 줄을 자동으로 관리
    ///   - 완료 줄은 초록 체크·취소선 후 doneLinger초 뒤 사라짐
    ///   - 줄이 하나도 없으면 바탕째 숨김
    /// </summary>
    [AddComponentMenu("OZ/UI/HUD/Quest List View")]
    public class QuestListView : MonoBehaviour, IQuestApi
    {
        public const string GateId = "gate", BossId = "boss";

        [SerializeField] internal CanvasGroup group;
        [SerializeField] internal RectTransform rowsRoot;
        [SerializeField] internal QuestRowView rowTemplate;
        [SerializeField] internal Image backdrop;
        [SerializeField] internal float rowHeight = 16f;
        [SerializeField] internal float headerHeight = 14f;
        [SerializeField] internal int maxRows = 4;
        [SerializeField] internal float doneLinger = 3f;

        [Header("게이트 자동 퀘스트")]
        [SerializeField] internal bool autoGateQuests = true;
        [SerializeField] internal string gateTitle = "게이트 봉쇄";
        [SerializeField] internal string bossTitle = "보스 처치";
        [SerializeField] internal string bossLockedHint = "게이트 후";

        readonly List<QuestRowView> _rows = new List<QuestRowView>();
        readonly Stack<QuestRowView> _free = new Stack<QuestRowView>();
        IGateSource _gate;
        int _seq;
        readonly Dictionary<string, int> _insertOrder = new Dictionary<string, int>();

        public IReadOnlyList<QuestRowView> Rows => _rows;

        void Awake()
        {
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
            Layout(false);
        }

        void OnEnable() => GameUI.Register((IQuestApi)this);
        void OnDisable() => GameUI.Unregister(this);
        void OnDestroy() => UnbindGate();

        // ───────────── 게이트 자동 ─────────────

        internal void BindGate(IGateSource gate)
        {
            if (ReferenceEquals(gate, _gate)) return;
            UnbindGate();
            _gate = gate;
            if (!autoGateQuests) return;
            if (_gate == null) { Remove(GateId); Remove(BossId); return; }
            _gate.GateStateReset += SyncGate;
            _gate.GateSealed += OnSealed;
            _gate.BossAreaUnlocked += SyncGate;
            _gate.GateOpened += SyncGate;
            SyncGate();
        }

        void UnbindGate()
        {
            if (_gate == null) return;
            _gate.GateStateReset -= SyncGate;
            _gate.GateSealed -= OnSealed;
            _gate.BossAreaUnlocked -= SyncGate;
            _gate.GateOpened -= SyncGate;
            _gate = null;
        }

        void OnSealed(int sealedCount, int target) => SyncGate();

        void SyncGate()
        {
            if (_gate == null) return;
            bool unlocked = _gate.IsBossAreaUnlocked;
            if (unlocked)
            {
                if (TryGet(GateId, out var g) && g.State != QuestState.Done) Set(GateId, gateTitle, _gate.SealedCount, _gate.TargetCount, QuestState.Done, null, -20);
                Set(BossId, bossTitle, 0, 0, QuestState.Active, null, -10);
            }
            else
            {
                Set(GateId, gateTitle, _gate.SealedCount, _gate.TargetCount, QuestState.Active, null, -20);
                Set(BossId, bossTitle, 0, 0, QuestState.Locked, bossLockedHint, -10);
            }
        }

        // ───────────── IQuestApi ─────────────

        public void Set(string id, string title, int progress = 0, int target = 0, QuestState state = QuestState.Active, string hint = null, int order = 0)
        {
            if (string.IsNullOrEmpty(id) || rowTemplate == null) return;
            var row = Find(id);
            bool isNew = row == null;
            if (isNew)
            {
                row = _free.Count > 0 ? _free.Pop() : Instantiate(rowTemplate, rowsRoot != null ? rowsRoot : transform);
                row.name = "Quest_" + id;
                row.Id = id;
                row.gameObject.SetActive(true);
                _rows.Add(row);
                _insertOrder[id] = _seq++;
                row.fresh = true;
            }
            else { row.group.DOKill(); row.group.alpha = 1f; }
            row.doneAt = state == QuestState.Done ? (row.Info.State == QuestState.Done && !isNew ? row.doneAt : Time.unscaledTime) : -1f;
            var before = row.Info;
            row.Apply(new QuestInfo(id, title, progress, target, state, hint, order), !isNew);
            if (state == QuestState.Done && (isNew || before.State != QuestState.Done)) UISfx.Play(UISound.QuestComplete);
            else if (!isNew && state == QuestState.Active && (progress != before.Progress || before.State != QuestState.Active)) UISfx.Play(UISound.QuestUpdate);
            Sort();
            Layout(true);
            if (isNew) Appear(row);
        }

        public void SetProgress(string id, int progress, int target = -1)
        {
            var row = Find(id);
            if (row == null) return;
            var q = row.Info;
            int t = target >= 0 ? target : q.Target;
            var state = q.State;
            if (t > 0 && progress >= t && state == QuestState.Active) state = QuestState.Done;
            Set(id, q.Title, progress, t, state, q.Hint, q.Order);
        }

        public void SetState(string id, QuestState state, string hint = null)
        {
            var row = Find(id);
            if (row == null) return;
            var q = row.Info;
            Set(id, q.Title, q.Progress, q.Target, state, hint ?? q.Hint, q.Order);
        }

        public void Complete(string id) => SetState(id, QuestState.Done);

        public void Remove(string id)
        {
            var row = Find(id);
            if (row == null) return;
            _rows.Remove(row);
            row.group.DOKill();
            row.gameObject.SetActive(false);
            _free.Push(row);
            Layout(true);
        }

        public void Clear()
        {
            foreach (var r in _rows) { r.group.DOKill(); r.gameObject.SetActive(false); _free.Push(r); }
            _rows.Clear();
            Layout(false);
        }

        public bool TryGet(string id, out QuestInfo info)
        {
            var row = Find(id);
            info = row != null ? row.Info : default;
            return row != null;
        }

        // ───────────── 표시 ─────────────

        QuestRowView Find(string id)
        {
            foreach (var r in _rows) if (r.Id == id) return r;
            return null;
        }

        void Sort()
        {
            _rows.Sort((a, b) =>
            {
                int c = a.Info.Order.CompareTo(b.Info.Order);
                return c != 0 ? c : _insertOrder[a.Id].CompareTo(_insertOrder[b.Id]);
            });
        }

        void Layout(bool animate)
        {
            int shown = 0;
            for (int i = 0; i < _rows.Count; i++)
            {
                var r = _rows[i];
                bool visible = i < maxRows;
                r.gameObject.SetActive(visible);
                if (!visible) continue;
                var target = new Vector2(0f, -headerHeight - shown * rowHeight);
                if (r.fresh || !Mathf.Approximately(r.slotY, target.y))
                {
                    r.Rect.DOKill();
                    if (animate && !r.fresh) r.Rect.DOAnchorPos(target, 0.18f).SetUpdate(true).SetLink(r.gameObject);
                    else r.Rect.anchoredPosition = target;
                    r.slotY = target.y;
                }
                shown++;
            }
            if (backdrop != null) backdrop.rectTransform.sizeDelta = new Vector2(backdrop.rectTransform.sizeDelta.x, headerHeight + shown * rowHeight + 4f);
            if (group != null)
            {
                float a = shown > 0 ? 1f : 0f;
                group.DOKill();
                if (animate) group.DOFade(a, 0.2f).SetUpdate(true).SetLink(gameObject);
                else group.alpha = a;
            }
        }

        void Appear(QuestRowView row)
        {
            row.fresh = false;
            row.group.DOKill();
            row.group.alpha = 0f;
            row.group.DOFade(1f, 0.25f).SetUpdate(true).SetLink(row.gameObject);
            var target = row.Rect.anchoredPosition;
            row.Rect.anchoredPosition = target + new Vector2(-8f, 0f);
            row.Rect.DOAnchorPos(target, 0.25f).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(row.gameObject);
        }

        void Update()
        {
            if (doneLinger <= 0f) return;
            for (int i = _rows.Count - 1; i >= 0; i--)
            {
                var r = _rows[i];
                if (r.Info.State != QuestState.Done || r.doneAt < 0f) continue;
                if (Time.unscaledTime - r.doneAt < doneLinger) continue;
                r.doneAt = -1f;
                string id = r.Id;
                r.group.DOFade(0f, 0.3f).SetUpdate(true).SetLink(r.gameObject)
                    .OnComplete(() => { if (r.Info.State == QuestState.Done) Remove(id); });
            }
        }
    }
}
