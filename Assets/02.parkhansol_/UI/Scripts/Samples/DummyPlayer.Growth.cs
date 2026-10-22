using System;
using System.Collections.Generic;
using OZ.UI.Contracts;
using UnityEngine;

namespace OZ.UI.Samples
{
    /// <summary>
    /// v1.1 상태창 참고 구현 — 기획데이터 v0.5 수치 + v1.1 (계열 선택 · 기술 단계형 · 아이템 포인트 분리).
    ///   포인트: 각성 1000 (퀘스트 보상으로만 증가) · 스탯 4종 (가격 200/250/300…, 상한 5·5·3·5)
    ///   기술 6종 (한 번 구매·영구 보유, 강화 없음) · Q/E/R 장착 (같은 기술 한 칸, 쿨다운은 기술별로 유지)
    ///   지도 노드 4개 (메인 완료로 다음 노드 해금)
    /// 사망·재도전(ResetAll)으로 포인트·구매·장착이 사라지지 않는다 (기획서 8장).
    /// 실제 게임에서는 PlayerStats / PlayerSkill + 코어 포인트·상점 로직이 같은 인터페이스를 구현하면 된다.
    /// </summary>
    public partial class DummyPlayer : IPointsSource, IStatSource, ISkillLoadoutSource, IGrowthShopSource, IStageMapSource
    {
        [Header("v1.2 계열 · 기술 단계 (기획데이터 v0.5: 계열 자유 · 기술 1회 구매)")]
        [Tooltip("켜면 처음 찍은 쪽(물리/마법)으로 고정, 반대쪽 잠김 (기획서 기본은 자유 → 끔)")]
        [SerializeField] internal bool lockCombatPath = false;
        [Tooltip("계열 고정을 켰을 때, 정한 뒤에도 바꿀 수 있나")]
        [SerializeField] internal bool allowPathChange = false;
        [Tooltip("기술에 단계 목록(ranks)이 비어 있으면 임시 3단계를 채운다 (단계형 확인용. 기획서 기본은 1회 구매 → 끔)")]
        [SerializeField] internal bool placeholderSkillRanks = false;
        CombatPath _path;
        public event Action PathChanged;
        PathRule _pathRule;

        /// <summary>계열 고정 규칙 (lockCombatPath 켤 때만 ICombatPathSource 로 등록)</summary>
        sealed class PathRule : ICombatPathSource
        {
            readonly DummyPlayer _p;
            public PathRule(DummyPlayer p) { _p = p; }
            public CombatPath Path => _p._path;
            public bool CanChangePath => _p.allowPathChange || _p._path == CombatPath.None;
            public ShopBuyResult TrySetPath(CombatPath path) => _p.TrySetPath(path);
            public event Action PathChanged { add => _p.PathChanged += value; remove => _p.PathChanged -= value; }
        }

        void BindPathRule()
        {
            if (!lockCombatPath) return;
            if (_pathRule == null) _pathRule = new PathRule(this);
            GameUI.Bind(_pathRule);
        }

        void UnbindPathRule() { if (_pathRule != null) GameUI.Unbind(_pathRule); }

        [Header("v1.0 기획서 v0.5 성장 (상태창)")]
        [Tooltip("끄면 예전 방식(계열 스킬 트리·레벨)으로 Q/E/R 동작")]
        [SerializeField] internal bool v05Growth = true;
        [Tooltip("기술 6종 (기획데이터 5장). 아이콘은 샌드박스 빌더가 채움")]
        [SerializeField] internal SkillOffer[] skillOffers = DefaultSkills();
        [Tooltip("시작할 때 이미 가진 기술 id (테스트용, 기획서 기본은 없음)")]
        [SerializeField] internal string[] startOwnedSkills = new string[0];

        // 기획데이터 4장: 초기값 · 1회 효과 · 상한
        static readonly float[] StatBase = { 20f, 0f, 0f, 20f };
        static readonly float[] StatPer = { 3f, 12f, 0.03f, 3f };
        static readonly int[] StatMax = { 5, 5, 3, 5 };

        int _gp;
        float _bonusHP;
        readonly int[] _statLv = new int[4];
        readonly Dictionary<string, int> _skillRank = new Dictionary<string, int>(); // 기술 id → 단계 (1 = 배움)
        readonly string[] _equipped = new string[3];
        readonly Dictionary<string, float> _cdEnd = new Dictionary<string, float>();
        readonly List<StageNode> _nodes = new List<StageNode>();
        readonly HashSet<string> _paidQuests = new HashSet<string>(); // 기획데이터 2장: 첫 완료에만 지급
        IQuestApi _questApi;

        public event Action<int, int> PointsChanged;
        public event Action<StatKind> StatChanged;
        public event Action<string> SkillOwned;
        public event Action<SkillSlot> SlotChanged;
        public event Action NodesChanged;

        static SkillOffer[] DefaultSkills() => new[]
        {
            Skill("sword_wave", "검기", "짧은 검기로 방향을 잡아 공격", SkillDamageKind.Physical, 500, 1.8f, 4f),
            Skill("spin_slash", "회전 베기", "가까운 여러 적에게 강한 피해", SkillDamageKind.Physical, 600, 2.1f, 7f),
            Skill("combo_slash", "연속 참격", "빈틈에 여러 참격을 집중", SkillDamageKind.Physical, 800, 3.75f, 12f),
            Skill("mana_bolt", "마력탄", "목표 위치의 적을 폭발로 공격", SkillDamageKind.Magic, 500, 1.65f, 5f),
            Skill("mana_field", "마력장", "적을 유도해 지속 장판 활용", SkillDamageKind.Magic, 600, 4f, 9f),
            Skill("chain_blast", "연쇄 폭발", "넓은 구역에 연속 공격", SkillDamageKind.Magic, 800, 3.45f, 13f),
        };

        static SkillOffer Skill(string id, string name, string desc, SkillDamageKind kind, int price, float coef, float cd) =>
            new SkillOffer { id = id, displayName = name, description = desc, damageKind = kind, price = price, coefficient = coef, cooldown = cd, ranks = new SkillRank[0] }; // 기획데이터 5장: 1회 구매 · 강화 없음

        /// <summary>v1.1 임시 단계: 1단계 = 배우기(기획 가격), 2·3단계 = 기능 추가 (가격·기능은 기획 확정 전 자리)</summary>
        static SkillRank[] PlaceholderRanks(int learnPrice) => new[]
        {
            new SkillRank { price = learnPrice, description = "배우기" },
            new SkillRank { price = 300, description = "[2단계 추가 기능 — 기획 미정]" },
            new SkillRank { price = 450, description = "[3단계 추가 기능 — 기획 미정]" },
        };

        void InitGrowth()
        {
            _gp = startShopPoints;
            _ip = startItemPoints;
            _bonusHP = 0f;
            Array.Clear(_statLv, 0, _statLv.Length);
            _skillRank.Clear();
            _path = CombatPath.None;
            if (placeholderSkillRanks && skillOffers != null)
                for (int i = 0; i < skillOffers.Length; i++)
                    if (skillOffers[i].ranks == null || skillOffers[i].ranks.Length == 0) skillOffers[i].ranks = PlaceholderRanks(skillOffers[i].price);
            if (startOwnedSkills != null) foreach (var id in startOwnedSkills) if (!string.IsNullOrEmpty(id)) _skillRank[id] = 1;
            for (int i = 0; i < 3; i++) _equipped[i] = null;
            _cdEnd.Clear();

            _nodes.Clear();
            _nodes.Add(new StageNode { id = "stage1", displayName = "1스테이지", unlocked = true, current = true, mainGoal = "일반 게이트 3개 직접 봉쇄", lockCondition = "" });
            _nodes.Add(new StageNode { id = "boss1", displayName = "1스테이지 보스", isBoss = true, mainGoal = "보스 처치 후 보스 게이트 봉쇄", lockCondition = "1스테이지 메인 완료" });
            _nodes.Add(new StageNode { id = "stage2", displayName = "2스테이지", mainGoal = "일반 게이트 4개 직접 봉쇄", lockCondition = "1스테이지 보스 완료" });
            _nodes.Add(new StageNode { id = "boss2", displayName = "2스테이지 보스", isBoss = true, mainGoal = "보스 처치 후 보스 게이트 봉쇄", lockCondition = "2스테이지 메인 완료" });
        }

        /// <summary>
        /// 테스트·디버그: 성장 상태를 각성 직후로 (포인트 1000, 스탯·기술·장착·노드 처음). 퀘스트 보상 지급 기록은 유지 (기획서: 첫 완료만 지급).
        /// 실제 게임에는 없음 — 사망·재도전은 ResetAll이고 구매는 그대로 남는다.
        /// </summary>
        public void ResetGrowth()
        {
            UnbindPathRule();
            InitGrowth();
            BindPathRule();
            float prev = _hp;
            _hp = Mathf.Min(_hp, MaxHP);
            PointsChanged?.Invoke(_gp, 0);
            foreach (StatKind s in Enum.GetValues(typeof(StatKind))) StatChanged?.Invoke(s);
            for (int i = 0; i < 3; i++) SlotChanged?.Invoke((SkillSlot)i);
            NodesChanged?.Invoke();
            PathChanged?.Invoke();
            ShopChanged?.Invoke();
            HealthChanged?.Invoke(new HealthChange(prev, _hp, MaxHP));
        }

        void Start()
        {
            // 상태창 퀘스트 탭: 보상·메인/서브 (게이트·보스 줄은 HUD가 자동으로 만든다)
            GameUI.Quest.SetReward("gate", 1200, true);
            GameUI.Quest.SetReward("boss", 800, true);
            GameUI.Quest.Set("sub_melee", "근접형 처치 (1회차)", 0, 6, QuestState.Active, null, 10);
            GameUI.Quest.SetReward("sub_melee", 150, false);
            BossAreaUnlocked += OnMainCleared;
            _questApi = GameUI.Quest;
            _questApi.Changed += PayQuestRewards;
        }

        void OnDestroy()
        {
            BossAreaUnlocked -= OnMainCleared;
            if (_questApi != null) _questApi.Changed -= PayQuestRewards;
        }

        /// <summary>퀘스트가 완료(Done)로 바뀌면 보상 포인트를 한 번만 지급 (재도전·재방문으로 다시 안 줌)</summary>
        void PayQuestRewards()
        {
            var q = _questApi;
            if (q == null) return;
            for (int i = 0; i < q.Count; i++)
            {
                var info = q.GetAt(i);
                if (info.State != QuestState.Done || _paidQuests.Contains(info.Id)) continue;
                if (!q.TryGetReward(info.Id, out int reward, out _) || reward <= 0) continue;
                _paidQuests.Add(info.Id);
                AddPoints(reward);
                GameUI.Notify.System("상태창", info.Title + " 완료", $"+{reward:N0} 포인트", "reward_" + info.Id);
            }
        }

        void OnMainCleared()
        {
            // 일반 메인 완료 → 다음(보스) 노드 해금. 보상 1200은 'gate' 퀘스트 완료로 PayQuestRewards가 지급
            int cur = _nodes.FindIndex(n => n.current);
            if (cur < 0) return;
            var n0 = _nodes[cur];
            if (n0.cleared || n0.isBoss) return; // 재도전으로 다시 봉쇄해도 중복 해금·보상 없음, 보스 노드는 보스 처치로
            n0.cleared = true; _nodes[cur] = n0;
            if (cur + 1 < _nodes.Count) { var n1 = _nodes[cur + 1]; n1.unlocked = true; _nodes[cur + 1] = n1; }
            GameUI.Notify.System("상태창", "일반 게이트 억제 완료", "보스 노드 해금", "main_clear");
            NodesChanged?.Invoke();
        }

        // ───────────── 포인트 ─────────────

        public int Points => _gp;

        /// <summary>퀘스트 보상 등 (] 키 = +500)</summary>
        public void AddPoints(int amount)
        {
            if (amount == 0) return;
            _gp = Mathf.Max(0, _gp + amount);
            PointsChanged?.Invoke(_gp, amount);
            ShopChanged?.Invoke();
        }

        void SpendPoints(int amount) => AddPoints(-amount);

        // ───────────── 스탯 ─────────────

        public int GetStatLevel(StatKind s) => _statLv[(int)s];
        public int GetStatMaxLevel(StatKind s) => StatMax[(int)s];
        public float GetStatValue(StatKind s) => StatBase[(int)s] + StatPer[(int)s] * _statLv[(int)s];
        public float GetStatNextValue(StatKind s) => StatBase[(int)s] + StatPer[(int)s] * Mathf.Min(_statLv[(int)s] + 1, StatMax[(int)s]);
        public int GetStatPrice(StatKind s) => _statLv[(int)s] >= StatMax[(int)s] ? 0 : 200 + 50 * _statLv[(int)s];

        public ShopBuyResult TryBuyStat(StatKind s)
        {
            int i = (int)s;
            if (CombatPathRules.IsLocked(_path, s)) return ShopBuyResult.Unavailable;
            if (_statLv[i] >= StatMax[i]) return ShopBuyResult.LimitReached;
            int price = GetStatPrice(s);
            if (price > _gp) return ShopBuyResult.NotEnoughCurrency;
            SpendPoints(price);
            AutoPath(CombatPathRules.PathOf(s));
            _statLv[i]++;
            if (s == StatKind.Vitality)
            {
                // 기획서: 최대 HP가 늘고 현재 HP에는 늘어난 만큼만 더한다
                float prev = _hp;
                _bonusHP += StatPer[i];
                if (_hp > 0f) _hp += StatPer[i]; // 쓰러진 상태에선 되살리지 않음
                HealthChanged?.Invoke(new HealthChange(prev, _hp, MaxHP));
            }
            StatChanged?.Invoke(s);
            return ShopBuyResult.Success;
        }

        // ───────────── 기술 ─────────────

        public int SkillCount => v05Growth && skillOffers != null ? skillOffers.Length : 0; // 0 = HUD가 예전 방식으로 그림
        public SkillOffer GetSkill(int index) => skillOffers != null && index >= 0 && index < skillOffers.Length ? skillOffers[index] : default;
        public bool IsOwned(string id) => GetRank(id) > 0;
        public int GetRank(string id) => id != null && _skillRank.TryGetValue(id, out int r) ? r : 0;

        SkillOffer Offer(string id)
        {
            if (skillOffers != null) foreach (var o in skillOffers) if (o.id == id) return o;
            return default;
        }

        public int GetDamage(string id)
        {
            var o = Offer(id);
            if (!o.IsValid) return 0;
            float power = o.damageKind == SkillDamageKind.Magic ? GetStatValue(StatKind.Magic) : GetStatValue(StatKind.Strength);
            return Mathf.RoundToInt(power * o.coefficient);
        }

        /// <summary>v1.1: 다음 단계로 (0→1 = 배우기). 단계 수 = SkillOffer.MaxRank (ranks 길이)</summary>
        public ShopBuyResult TryBuySkill(string id)
        {
            var o = Offer(id);
            if (!o.IsValid) return ShopBuyResult.Unavailable;
            if (CombatPathRules.IsLocked(_path, o.damageKind)) return ShopBuyResult.Unavailable;
            int rank = GetRank(id);
            if (rank >= o.MaxRank) return ShopBuyResult.LimitReached;
            int price = o.RankPrice(rank + 1);
            if (price > _gp) return ShopBuyResult.NotEnoughCurrency;
            SpendPoints(price);
            AutoPath(CombatPathRules.PathOf(o.damageKind));
            _skillRank[id] = rank + 1;
            SkillOwned?.Invoke(id);
            // 처음 배우면 빈 칸에 바로 장착 (첫 구매가 바로 손에 잡히게)
            if (rank == 0) for (int i = 0; i < 3; i++) if (_equipped[i] == null) { TryEquip((SkillSlot)i, id); break; }
            return ShopBuyResult.Success;
        }

        // ───────────── 계열 (v1.1) ─────────────

        public CombatPath Path => _path;

        ShopBuyResult TrySetPath(CombatPath path)
        {
            if (path == _path) return ShopBuyResult.Success;
            if (!lockCombatPath) return ShopBuyResult.Unavailable;
            if (!(allowPathChange || _path == CombatPath.None)) return ShopBuyResult.Unavailable;
            _path = path;
            // 다른 계열 기술은 장착에서 뺀다 (그 계열만 작동)
            for (int i = 0; i < 3; i++)
            {
                var o = Offer(_equipped[i]);
                if (o.IsValid && CombatPathRules.IsLocked(_path, o.damageKind)) Unequip((SkillSlot)i);
            }
            PathChanged?.Invoke();
            return ShopBuyResult.Success;
        }

        /// <summary>계열 미선택일 때 힘·물리 기술(또는 마력·마법 기술)에 투자하면 그쪽으로 정해진다</summary>
        void AutoPath(CombatPath p)
        {
            if (lockCombatPath && _path == CombatPath.None && p != CombatPath.None) TrySetPath(p);
        }

        public string GetEquipped(SkillSlot slot) => _equipped[(int)slot];

        public bool TryEquip(SkillSlot slot, string id)
        {
            if (!IsOwned(id)) return false;
            if (CombatPathRules.IsLocked(_path, Offer(id).damageKind)) return false;
            for (int i = 0; i < 3; i++)
                if (_equipped[i] == id && i != (int)slot) { _equipped[i] = null; SlotChanged?.Invoke((SkillSlot)i); }
            _equipped[(int)slot] = id;
            SlotChanged?.Invoke(slot);
            return true;
        }

        public void Unequip(SkillSlot slot)
        {
            _equipped[(int)slot] = null;
            SlotChanged?.Invoke(slot);
        }

        float LoadoutCooldownRemaining(SkillSlot slot)
        {
            var id = _equipped[(int)slot];
            return id != null && _cdEnd.TryGetValue(id, out float end) ? Mathf.Max(0f, end - Time.time) : 0f;
        }

        float LoadoutCooldownDuration(SkillSlot slot)
        {
            var o = Offer(_equipped[(int)slot]);
            return o.IsValid ? o.cooldown : 0f;
        }

        void UseLoadoutSkill(SkillSlot slot)
        {
            var id = _equipped[(int)slot];
            if (id == null) { SkillUseFailed?.Invoke(slot, SkillUseFailReason.NotLearned); return; }
            if (LoadoutCooldownRemaining(slot) > 0f) { SkillUseFailed?.Invoke(slot, SkillUseFailReason.Cooldown); return; }
            float cd = Offer(id).cooldown;
            _cdEnd[id] = Time.time + cd; // 기술별 → 장착을 바꿔도 남은 쿨다운 유지
            CooldownStarted?.Invoke(slot, cd);
        }

        // ───────────── 지도 노드 ─────────────

        public int NodeCount => _nodes.Count;
        public StageNode GetNode(int index) => index >= 0 && index < _nodes.Count ? _nodes[index] : default;

        public bool TryTravel(string id)
        {
            int to = _nodes.FindIndex(n => n.id == id);
            if (to < 0 || !_nodes[to].unlocked || _nodes[to].current) return false;
            var target = _nodes[to];
            GameUI.Flow.Transition(() =>
            {
                for (int i = 0; i < _nodes.Count; i++) { var n = _nodes[i]; n.current = i == to; _nodes[i] = n; }
                NodesChanged?.Invoke();
            }, 0.35f, () => GameUI.Area.Show(target.displayName, target.isBoss ? "보스 구역" : "일반 필드"));
            return true;
        }
    }
}
