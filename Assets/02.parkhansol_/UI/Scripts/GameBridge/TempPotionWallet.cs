using System;
using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// [박한솔 UI · v1.1 임시] 상태창 아이템 탭·상점 NPC를 실제 씬(MainScene 등)에서 바로 써 보기 위한 임시 지갑 + 1~4 포션 칸.
///   IShopSource (아이템 포인트·구매) + IItemSource (1~4 수량·사용) 를 구현해서 GameUI.Bind 한다.
///   아이템 포인트는 성장 포인트(스탯·스킬, IPointsSource)와 따로 센다.
///
/// 언제 생기나: 씬에 상태창(아이템 탭) 또는 ShopkeeperLink(상점 NPC)가 있으면 Play 시 자동 생성 (씬·프리팹 수정 없음).
///   다른 쪽이 이미 상점 소스를 등록했으면(UI 샌드박스의 DummyPlayer, 나중에 코어의 실제 구현) 스스로 사라진다.
/// 효과: 회복은 PlayerHealth.Heal (체력이 가득이면 소비 안 함 — 기획데이터 9장).
///   공격·방어·신속은 HUD 버프 표시까지만 (실제 능력치 반영은 플레이어 담당 PlayerStats 연결 후).
/// 포인트·아이템 저장은 하지 않는다 (씬을 다시 열면 초기화). 코어의 포인트 시스템이 생기면 이 파일은 지워도 된다.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("OZ/UI/Links/Temp Potion Wallet")]
public class TempPotionWallet : MonoBehaviour, IShopSource, IItemSource
{
    [Tooltip("시작 아이템 포인트 (임시값 — 기획 확정 전)")]
    [SerializeField] int startPoints = 1000;
    [Tooltip("1~4 칸 시작 수량")]
    [SerializeField] int[] startCounts = { 1, 0, 0, 0 };
    [Tooltip("1~4 칸 아이템. 비우면 씬의 상점 목록에서 slotIndex 기준으로 채움")]
    [SerializeField] ItemData[] quickItems = new ItemData[4];
    [Tooltip("숫자 키 1~4로 직접 사용 (기본 꺼짐 — 플레이어 InputManager가 IItemSource.UseItem 호출. 단독 테스트할 때만 켜기)")]
    [SerializeField] bool useNumberKeys = false;

    readonly int[] _counts = new int[4];
    readonly System.Collections.Generic.Dictionary<ItemData, int> _others = new System.Collections.Generic.Dictionary<ItemData, int>(); // 1~4 칸이 없는 아이템 (장비 등)
    int _points;
    PlayerHealth _health;

    public event Action ShopChanged;
    public event Action<int, int> CountChanged;
    public event Action<int> ItemUsed;
    public event Action<int> ItemUseFailed;
    public event Action<ItemData, float> BuffApplied;
    public event Action<ItemData> BuffEnded;

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
        if (FindAnyObjectByType<TempPotionWallet>() != null) return;
        if (FindAnyObjectByType<ShopkeeperLink>() == null &&
            FindAnyObjectByType<OZ.UI.StatusItemPage>(FindObjectsInactive.Include) == null) return;
        new GameObject("TempPotionWallet (auto, UI)").AddComponent<TempPotionWallet>();
    }

    // ───────────── 수명 ─────────────

    void Awake()
    {
        _points = startPoints;
        FillQuickItemsFromShop();
        for (int i = 0; i < 4; i++) _counts[i] = startCounts != null && i < startCounts.Length ? Mathf.Max(0, startCounts[i]) : 0;
    }

    void Start()
    {
        // 다른 오브젝트가 OnEnable에서 먼저 상점 소스를 등록했으면 양보
        if (UISources.Shop != null && !ReferenceEquals(UISources.Shop, this)) { Destroy(gameObject); return; }
        GameUI.Bind(this);
        UIRequests.NewGame += ResetForNewGame;
    }

    void OnDestroy()
    {
        UIRequests.NewGame -= ResetForNewGame;
        GameUI.Unbind(this);
    }

    // ───────────── 저장 / 불러오기 (저장 담당이 부름 — GameSave) ─────────────

    [Serializable]
    public class SaveData
    {
        public int points;
        public int[] counts = new int[4];
        public System.Collections.Generic.List<string> otherIds = new System.Collections.Generic.List<string>();
        public System.Collections.Generic.List<int> otherCounts = new System.Collections.Generic.List<int>();
    }

    public SaveData GetSaveData()
    {
        var d = new SaveData { points = _points };
        for (int i = 0; i < 4; i++) d.counts[i] = _counts[i];
        foreach (var kv in _others)
        {
            if (kv.Key == null || string.IsNullOrEmpty(kv.Key.id) || kv.Value <= 0) continue;
            d.otherIds.Add(kv.Key.id); d.otherCounts.Add(kv.Value);
        }
        return d;
    }

    /// <summary>저장값으로 덮어씀. 상점 목록에 없는 아이템 id는 무시</summary>
    public void LoadSaveData(SaveData d)
    {
        if (d == null) return;
        _points = Mathf.Max(0, d.points);
        _others.Clear();
        CancelInvoke(nameof(EndBuffs));
        for (int i = 0; i < 4; i++)
        {
            if (_lastBuff[i] > 0f) { _lastBuff[i] = 0f; BuffEnded?.Invoke(quickItems[i]); }
            _counts[i] = d.counts != null && i < d.counts.Length ? Mathf.Max(0, d.counts[i]) : 0;
            CountChanged?.Invoke(i, _counts[i]);
        }
        if (d.otherIds != null && d.otherCounts != null)
            for (int i = 0; i < d.otherIds.Count && i < d.otherCounts.Count; i++)
            {
                var item = FindShopItem(d.otherIds[i]);
                if (item != null && d.otherCounts[i] > 0) _others[item] = d.otherCounts[i];
            }
        ShopChanged?.Invoke();
    }

    ItemData FindShopItem(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var page in FindObjectsByType<OZ.UI.StatusItemPage>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (page.ItemShop != null) foreach (var e in page.ItemShop.entries) if (e?.item != null && e.item.id == id) return e.item;
        foreach (var keeper in FindObjectsByType<ShopkeeperLink>(FindObjectsSortMode.None))
            if (keeper.Shop != null) foreach (var e in keeper.Shop.entries) if (e?.item != null && e.item.id == id) return e.item;
        return null;
    }

    // 타이틀 '새 게임': 아이템 포인트·개수를 시작값으로 (플레이어 쪽 스킬·스탯·버프 초기화와 짝)
    void ResetForNewGame()
    {
        _points = startPoints;
        _others.Clear();
        CancelInvoke(nameof(EndBuffs));
        for (int i = 0; i < 4; i++)
        {
            if (_lastBuff[i] > 0f) { _lastBuff[i] = 0f; BuffEnded?.Invoke(quickItems[i]); }
            _counts[i] = startCounts != null && i < startCounts.Length ? Mathf.Max(0, startCounts[i]) : 0;
            CountChanged?.Invoke(i, _counts[i]);
        }
        ShopChanged?.Invoke();
    }

    void FillQuickItemsFromShop()
    {
        if (quickItems == null || quickItems.Length < 4) Array.Resize(ref quickItems, 4);
        var shops = new System.Collections.Generic.List<ShopData>();
        foreach (var page in FindObjectsByType<OZ.UI.StatusItemPage>(FindObjectsInactive.Include, FindObjectsSortMode.None)) shops.Add(page.ItemShop);
        foreach (var keeper in FindObjectsByType<ShopkeeperLink>(FindObjectsSortMode.None)) shops.Add(keeper.Shop);
        foreach (var shop in shops)
        {
            if (shop == null || shop.entries == null) continue;
            foreach (var e in shop.entries)
            {
                var item = e?.item;
                if (item == null || item.slotIndex < 0 || item.slotIndex > 3) continue;
                if (quickItems[item.slotIndex] == null) quickItems[item.slotIndex] = item;
            }
        }
    }

    // ───────────── IItemSource ─────────────

    public int SlotCount => 4;
    public ItemData GetItem(int slotIndex) => slotIndex >= 0 && slotIndex < 4 ? quickItems[slotIndex] : null;
    public int GetCount(int slotIndex) => slotIndex >= 0 && slotIndex < 4 ? _counts[slotIndex] : 0;

    public void UseItem(int slotIndex)
    {
        var item = GetItem(slotIndex);
        if (item == null || _counts[slotIndex] <= 0) { ItemUseFailed?.Invoke(slotIndex); return; }

        if (item.effectType == ItemEffectType.Heal)
        {
            if (_health == null) _health = FindAnyObjectByType<PlayerHealth>();
            if (_health == null || _health.IsDead || _health.HP >= _health.MaxHP)
            {
                ItemUseFailed?.Invoke(slotIndex); // 기획데이터 9장: 체력이 가득 차면 소비하지 않는다
                if (_health != null && !_health.IsDead) GameUI.Notify.Toast("체력이 가득 차 있다", ToastType.Warning);
                return;
            }
            _health.Heal(_health.MaxHP * item.effectPercent / 100f);
        }
        else if (item.IsBuff)
        {
            BuffApplied?.Invoke(item, item.buffDuration); // 표시만 — 같은 버프는 시간 갱신
            _lastBuff[slotIndex] = Time.time + item.buffDuration;
            CancelInvoke(nameof(EndBuffs));
            EndBuffs(); // 가장 먼저 끝나는 버프에 맞춰 다시 예약
        }

        _counts[slotIndex]--;
        ItemUsed?.Invoke(slotIndex);
        Changed(slotIndex);
    }

    readonly float[] _lastBuff = new float[4];

    void EndBuffs()
    {
        float next = float.MaxValue;
        for (int i = 0; i < 4; i++)
        {
            if (_lastBuff[i] <= 0f) continue;
            if (_lastBuff[i] <= Time.time + 0.01f) { _lastBuff[i] = 0f; BuffEnded?.Invoke(quickItems[i]); }
            else next = Mathf.Min(next, _lastBuff[i]);
        }
        if (next < float.MaxValue) Invoke(nameof(EndBuffs), next - Time.time);
    }

    void Changed(int slotIndex)
    {
        CountChanged?.Invoke(slotIndex, _counts[slotIndex]);
        ShopChanged?.Invoke();
    }

    // ───────────── IShopSource ─────────────

    public int Currency => _points;

    public int GetOwned(ItemData item)
    {
        if (item == null) return 0;
        for (int i = 0; i < 4; i++) if (quickItems[i] == item) return _counts[i];
        return _others.TryGetValue(item, out int n) ? n : 0;
    }

    public ShopBuyResult TryBuy(ShopEntry entry, int amount)
    {
        var check = ShopRules.Check(entry, amount, GetOwned(entry?.item), _points);
        if (check != ShopBuyResult.Success) return check;
        int slot = entry.item.slotIndex;
        if (slot < 0 || slot > 3)
        {
            // 1~4 칸이 없는 아이템 (장비 등): 개수만 센다
            _points -= entry.price * amount;
            _others[entry.item] = GetOwned(entry.item) + amount;
            ShopChanged?.Invoke();
            return ShopBuyResult.Success;
        }
        if (quickItems[slot] == null) quickItems[slot] = entry.item;
        else if (quickItems[slot] != entry.item) return ShopBuyResult.Unavailable;

        _points -= entry.price * amount;
        _counts[slot] += amount;
        Changed(slot);
        return ShopBuyResult.Success;
    }

    /// <summary>임시: 아이템 포인트 지급 확인용</summary>
    public void AddPoints(int amount)
    {
        _points = Mathf.Max(0, _points + amount);
        ShopChanged?.Invoke();
    }

    // ───────────── 입력 ─────────────

    void Update()
    {
        if (!useNumberKeys || UIState.IsGameplayInputBlocked) return;
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.digit1Key.wasPressedThisFrame) UseItem(0);
        if (kb.digit2Key.wasPressedThisFrame) UseItem(1);
        if (kb.digit3Key.wasPressedThisFrame) UseItem(2);
        if (kb.digit4Key.wasPressedThisFrame) UseItem(3);
    }
}
