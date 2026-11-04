using System;
using System.Collections.Generic;
using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// [박한솔 UI · v1.2] 상태창 캐릭터 탭의 "구매 처리" — 성장 포인트 지갑 + 스탯/스킬 구매. 기획데이터 v0.5 기준.
///   IPointsSource      : 성장 포인트. 시작 = 각성 1000 (1장), 이후는 퀘스트 보상으로만 (1장·2장·3장, 일반 적 처치 지급 없음)
///   IGrowthShopSource  : 가격 확인 → 포인트 차감 → PlayerStats.TryAddLevel / PlayerSkill.TryAddOwned (팀원 코드 그대로 호출)
///                        스탯 가격 200 / 250 / 300 / 350 / 400 (= 기본 200 + 레벨 × 50, 4장) · 상한은 PlayerStats 설정 (힘·체력·마력 5, 민첩 3)
///                        스킬 = 한 번 구매로 영구 보유 (5장). 가격은 스킬 데이터(PlayerSkillData.Price)
///   계열 : 기획서대로 물리·마법 자유 (섞어서 Q/E/R 최대 3개). 한쪽 고정 규칙이 다시 필요하면 lockCombatPath 를 켜면
///          CombatPathLock 컴포넌트가 붙어 ICombatPathSource 로 등록되고, 상태창이 [물리][마법] 버튼과 잠금을 보여 준다.
///
/// 언제 생기나: 씬에 PlayerStats 또는 PlayerSkill이 있으면 (꺼져 있어도) Play 시 자동 생성 (씬·프리팹·팀원 파일 수정 없음).
///   다른 쪽이 이미 성장 상점을 등록했으면(UI 샌드박스의 DummyPlayer, 나중에 코어 구현) 스스로 사라진다.
///
/// 포인트 들어오는 곳:
///   1) 시작 포인트 (각성 1000)
///   2) 퀘스트 보상: GameUI.Quest.SetReward(id, 포인트)로 걸어 둔 퀘스트가 '완료'되면 첫 1회만 지급 (재방문·사망·이어하기 재지급 없음)
///   3) 다른 코드에서 직접: GrowthShopBridge.Give(500, "첫 몬스터 처치")
/// 저장은 하지 않는다 (씬을 다시 열면 초기화) — 세이브가 생기면 Points (+ 지급한 퀘스트 id) 만 저장하면 된다.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("OZ/UI/Links/Growth Shop Bridge")]
public class GrowthShopBridge : MonoBehaviour, IPointsSource, IGrowthShopSource
{
    [Header("포인트 (기획데이터 1장)")]
    [Tooltip("시작 성장 포인트 (각성 1000)")]
    [SerializeField] int startPoints = 1000;

    [Header("스탯 가격 = 기본 + 레벨 × 증가 (기획데이터 4장: 200/250/300/350/400)")]
    [SerializeField] int statBasePrice = 200;
    [SerializeField] int statPricePerLevel = 50;

    [Header("계열")]
    [Tooltip("켜면 처음 찍은 쪽(물리/마법)으로 고정되고 반대쪽이 잠긴다. 기획데이터 v0.5는 자유 → 끔")]
    [SerializeField] bool lockCombatPath = false;

    static GrowthShopBridge _instance;

    int _points;
    PlayerStats _stats;
    PlayerSkill _skills;
    IQuestApi _quest;
    readonly HashSet<string> _paidQuests = new HashSet<string>();

    public event Action<int, int> PointsChanged;

    // ───────────── 외부 API ─────────────

    /// <summary>성장 포인트 지급 (음수 = 차감). 지갑이 없으면 false</summary>
    public static bool Give(int amount, string reason = null)
    {
        if (_instance == null) return false;
        _instance.AddPoints(amount);
        if (amount > 0 && !string.IsNullOrEmpty(reason))
            GameUI.Notify.System("상태창", reason, $"+{amount:N0} 성장 포인트", "growth_" + reason);
        return true;
    }

    // ───────────── 자동 설치 ─────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInstall()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryInstall();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryInstall();

    static void TryInstall()
    {
        if (FindAnyObjectByType<GrowthShopBridge>() != null) return;
        // MainScene의 Player는 꺼진 채로 시작해서 '새 게임' 때 켜진다 → 꺼진 것도 찾는다
        if (FindAnyObjectByType<PlayerStats>(FindObjectsInactive.Include) == null &&
            FindAnyObjectByType<PlayerSkill>(FindObjectsInactive.Include) == null) return;
        new GameObject("GrowthShopBridge (auto, UI)").AddComponent<GrowthShopBridge>();
    }

    // ───────────── 수명 ─────────────

    void Awake() => _points = Mathf.Max(0, startPoints);

    void Start()
    {
        // 다른 오브젝트가 먼저 성장 상점을 등록했으면 양보 (샌드박스 DummyPlayer 등)
        if (UISources.GrowthShop != null && !ReferenceEquals(UISources.GrowthShop, this)) { Destroy(gameObject); return; }
        _instance = this;
        FindPlayer();
        GameUI.Bind(this);
        if (lockCombatPath && GetComponent<CombatPathLock>() == null) gameObject.AddComponent<CombatPathLock>();
        _quest = GameUI.Quest;
        if (_quest != null) _quest.Changed += PayQuestRewards;
        UIRequests.NewGame += ResetForNewGame;
    }

    // ───────────── 저장 / 불러오기 (저장 담당이 부름 — GameSave) ─────────────

    [Serializable]
    public class SaveData
    {
        public int points;
        public List<string> paidQuests = new List<string>();
    }

    public SaveData GetSaveData() => new SaveData { points = _points, paidQuests = new List<string>(_paidQuests) };

    public void LoadSaveData(SaveData d)
    {
        if (d == null) return;
        _paidQuests.Clear();
        if (d.paidQuests != null) foreach (var id in d.paidQuests) if (!string.IsNullOrEmpty(id)) _paidQuests.Add(id);
        int before = _points;
        _points = Mathf.Max(0, d.points);
        PointsChanged?.Invoke(_points, _points - before);
    }

    // 타이틀 '새 게임': 성장 포인트를 시작값으로 (스탯·스킬은 플레이어 쪽 ResetAll이 비움)
    // 받은 퀘스트 보상 기록(_paidQuests)은 유지 — 퀘스트 진행이 초기화되지 않는 동안 중복 지급 방지
    void ResetForNewGame()
    {
        int before = _points;
        _points = Mathf.Max(0, startPoints);
        PointsChanged?.Invoke(_points, _points - before);
    }

    void OnDestroy()
    {
        UIRequests.NewGame -= ResetForNewGame;
        if (_instance == this) _instance = null;
        if (_quest != null) _quest.Changed -= PayQuestRewards;
        GameUI.Unbind(this);
    }

    void FindPlayer()
    {
        if (_stats == null) _stats = FindAnyObjectByType<PlayerStats>(FindObjectsInactive.Include);
        if (_skills == null) _skills = FindAnyObjectByType<PlayerSkill>(FindObjectsInactive.Include);
    }

    void PayQuestRewards()
    {
        var q = _quest;
        if (q == null) return;
        for (int i = 0; i < q.Count; i++)
        {
            var info = q.GetAt(i);
            if (info.State != QuestState.Done || _paidQuests.Contains(info.Id)) continue;
            if (!q.TryGetReward(info.Id, out int reward, out _) || reward <= 0) continue;
            _paidQuests.Add(info.Id);
            AddPoints(reward);
            GameUI.Notify.System("상태창", info.Title + " 완료", $"+{reward:N0} 성장 포인트", "reward_" + info.Id);
        }
    }

    // ───────────── 포인트 ─────────────

    public int Points => _points;

    public void AddPoints(int amount)
    {
        if (amount == 0) return;
        int before = _points;
        _points = Mathf.Max(0, _points + amount);
        PointsChanged?.Invoke(_points, _points - before);
    }

    // ───────────── 스탯 ─────────────

    public int GetStatPrice(StatKind stat)
    {
        FindPlayer();
        if (_stats == null) return 0;
        var t = (StatType)(int)stat; // StatKind와 StatType 순서 같음 (힘·체력·민첩·마력)
        return _stats.IsMaxLevel(t) ? 0 : statBasePrice + statPricePerLevel * _stats.GetLevel(t);
    }

    public ShopBuyResult TryBuyStat(StatKind stat)
    {
        FindPlayer();
        if (_stats == null) return ShopBuyResult.Unavailable;
        if (PathLocked(stat)) return ShopBuyResult.Unavailable;
        var t = (StatType)(int)stat;
        if (_stats.IsMaxLevel(t)) return ShopBuyResult.LimitReached;
        int price = GetStatPrice(stat);
        if (price > _points) return ShopBuyResult.NotEnoughCurrency;
        if (!_stats.TryAddLevel(t)) return ShopBuyResult.LimitReached; // 실패하면 포인트 그대로
        AddPoints(-price);
        CombatPathLock.AutoPick(CombatPathRules.PathOf(stat));
        return ShopBuyResult.Success;
    }

    // ───────────── 스킬 ─────────────

    internal static SkillOffer Offer(string id)
    {
        var src = UISources.Loadout;
        if (src == null || string.IsNullOrEmpty(id)) return default;
        for (int i = 0; i < src.SkillCount; i++)
        {
            var o = src.GetSkill(i);
            if (o.id == id) return o;
        }
        return default;
    }

    /// <summary>배우기 (기획서: 한 번 구매로 영구 보유). 데이터에 단계(ranks)가 생기면 다음 단계 구매로 이어진다</summary>
    public ShopBuyResult TryBuySkill(string skillId)
    {
        FindPlayer();
        var src = UISources.Loadout;
        var o = Offer(skillId);
        if (src == null || _skills == null || !o.IsValid) return ShopBuyResult.Unavailable;
        if (PathLocked(o.damageKind)) return ShopBuyResult.Unavailable;
        int rank = src.GetRank(skillId);
        if (rank >= o.MaxRank) return ShopBuyResult.LimitReached;
        int price = o.RankPrice(rank + 1);
        if (price > _points) return ShopBuyResult.NotEnoughCurrency;

        if (rank == 0)
        {
            if (!_skills.TryAddOwned(skillId)) return ShopBuyResult.Unavailable;
        }
        else
        {
            // 2단계 이상: PlayerSkill에 단계 올리기 함수가 생기면 여기서 호출 (기획서 v0.5는 단계 없음)
            return ShopBuyResult.Unavailable;
        }

        AddPoints(-price);
        CombatPathLock.AutoPick(CombatPathRules.PathOf(o.damageKind));
        // 처음 배우면 빈 칸에 바로 장착 (같은 기술 중복 장착 없음 — 기획서 5장)
        if (rank == 0)
            for (int i = 0; i < 3; i++)
                if (string.IsNullOrEmpty(src.GetEquipped((SkillSlot)i))) { src.TryEquip((SkillSlot)i, skillId); break; }
        return ShopBuyResult.Success;
    }

    // ───────────── 계열 (선택 규칙) ─────────────

    static bool PathLocked(StatKind s) => UISources.Path != null && CombatPathRules.IsLocked(UISources.Path.Path, s);
    static bool PathLocked(SkillDamageKind k) => UISources.Path != null && CombatPathRules.IsLocked(UISources.Path.Path, k);
}
