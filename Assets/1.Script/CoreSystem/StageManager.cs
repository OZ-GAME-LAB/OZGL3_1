using Cysharp.Threading.Tasks;
using UnityEngine;
using System.Collections.Generic;
using OZ.UI.Contracts;

// 생성·의존성 연결·종료를 담당하며 진행 상태는 Stage에서 조회합니다.
public class StageManager : BaseManager
{
    [SerializeField] private List<GameObject> stagePrefabs = new List<GameObject>();
    // 기존 씬의 단일 프리팹 설정은 초기화 시 목록으로 옮깁니다.
    [SerializeField, HideInInspector] private GameObject stagePrefab;
    [SerializeField] private Transform stageParent;
    [SerializeField, Min(1)] private int stageNumber = 1;
    [SerializeField] private HunterRank clearRank = HunterRank.E;
    private GateManager gateManager;
    private DataManager dataManager;
    private ResourceManager resourceManager;
    private Stage currentStage;
    private Transform playerTarget;

    public bool IsStageRunning => currentStage != null && currentStage.IsRunning;
    public int StageNumber => stageNumber;
    public bool IsStageCleared => currentStage != null && currentStage.State == StageState.Cleared;
    public event System.Action StageCleared;
    public GameObject CurrentStage => currentStage != null ? currentStage.gameObject : null;

    public override async UniTask InitializeAsync()
    {
        if (gateManager == null)
            throw new System.InvalidOperationException("GameManager에서 GateManager를 주입해야 합니다.");
        EndStage();
        if (stagePrefabs.Count == 0 && stagePrefab != null)
            stagePrefabs.Add(stagePrefab);
        if (dataManager == null || resourceManager == null)
            throw new System.InvalidOperationException("GameManager에서 DataManager와 ResourceManager를 주입해야 합니다.");
        // 스테이지 시작 API는 동기이므로 보스 에셋을 초기화 시 캐시에 준비합니다.
        if (dataManager.TryGetDataTable<StageMonsterData>(out var table))
        {
            foreach (StageMonsterData data in table.Values)
            {
                BossMonsterData bossData = GetMonsterData<BossMonsterData>(data.BossMonsterId);
                GameObject prefab = await resourceManager.LoadAssetAsync<GameObject>(bossData.PrefabKey);
                if (prefab == null || prefab.GetComponent<BossMonster>() == null)
                    throw new System.InvalidOperationException($"'{data.Id}'의 보스 프리팹 루트에 BossMonster가 필요합니다.");
            }
        }
    }

    public UniTask InitializeAsync(GateManager manager, DataManager data, ResourceManager resources)
    {
        if (manager == null) throw new System.ArgumentNullException(nameof(manager));
        EndStage();
        gateManager = manager;
        dataManager = data;
        resourceManager = resources;
        return InitializeAsync();
    }

    public int StageCount => stagePrefabs.Count;

    // 목록의 0번 항목이 1스테이지입니다. 진행 대상 선택은 GameManager가 담당합니다.
    public bool HasStage(int number) => number >= 1 && number <= stagePrefabs.Count;

    public bool CanSetStage(int number)
    {
        if (!HasStage(number) || gateManager == null) return false;
        GameObject prefab = stagePrefabs[number - 1];
        return prefab != null && prefab.GetComponent<Stage>() != null &&
            TryGetStageMonsters(number, out _, out _, out _);
    }

    private T GetMonsterData<T>(string id) where T : BaseMonsterData
    {
        if (string.IsNullOrWhiteSpace(id) || !dataManager.TryGetData<T>(id, out var data))
            throw new System.InvalidOperationException($"{typeof(T).Name} '{id}'가 없습니다.");
        if (string.IsNullOrWhiteSpace(data.PrefabKey))
            throw new System.InvalidOperationException($"{typeof(T).Name} '{id}'의 PrefabKey가 비어 있습니다.");
        return data;
    }

    private bool TryGetStageMonsters(int number, out List<MonsterData> monsters,
        out BossMonsterData bossData, out BossMonster boss)
    {
        monsters = new List<MonsterData>();
        bossData = null;
        boss = null;
        string id = $"Stage_{number:D2}";
        try
        {
            if (dataManager == null || !dataManager.TryGetData<StageMonsterData>(id, out var data))
                throw new System.InvalidOperationException($"StageMonsterData에 '{id}'가 없습니다.");
            if (data.MonsterIds == null || data.MonsterIds.Length == 0)
                throw new System.InvalidOperationException($"'{id}'의 MonsterIds가 비어 있습니다.");
            foreach (string monsterId in data.MonsterIds)
            {
                MonsterData monsterData = GetMonsterData<MonsterData>(monsterId);
                if (!gateManager.PoolManager.TryGetPrefab<Monster>(monsterData.PrefabKey, out var monster) ||
                    monster is BossMonster)
                    throw new System.InvalidOperationException($"일반 몬스터 '{monsterId}'의 PoolData 등록과 루트 Monster 컴포넌트를 확인하세요.");
                if (!monsters.Contains(monsterData)) monsters.Add(monsterData);
            }
            bossData = GetMonsterData<BossMonsterData>(data.BossMonsterId);
            if (resourceManager == null || !resourceManager.TryGetAsset<GameObject>(bossData.PrefabKey, out var prefab) ||
                !prefab.TryGetComponent(out boss))
                throw new System.InvalidOperationException($"보스 '{data.BossMonsterId}'가 준비되지 않았습니다.");
            return true;
        }
        catch (System.InvalidOperationException exception)
        {
            Debug.LogError($"[{id}] {exception.Message}", this);
            return false;
        }
    }

    public bool SetStage(int number, Transform target = null)
    {
        if (target != null) playerTarget = target;
        if (!CanSetStage(number))
        {
            Debug.LogError($"{number}스테이지의 프리팹과 매니저 설정을 확인하세요.", this);
            return false;
        }
        int previousNumber = stageNumber;
        if (!TryGetStageMonsters(number, out var monsterData, out var bossData, out var boss)) return false;
        try
        {
            stageNumber = number;
            LoadStage(stagePrefabs[number - 1], monsterData, bossData, boss);
            if (IsStageRunning)
            {
                stageNumber = number;
                return true;
            }
            stageNumber = previousNumber;
            return false;
        }
        catch
        {
            stageNumber = previousNumber;
            throw;
        }
    }

    public void StartStage() => SetStage(stageNumber);

    public void StartStage(GameObject prefab)
    {
        if (IsStageRunning) return;
        if (TryGetStageMonsters(stageNumber, out var data, out var bossData, out var boss))
            LoadStage(prefab, data, bossData, boss);
    }

    private void LoadStage(GameObject prefab, IReadOnlyList<MonsterData> monsterData, BossMonsterData bossData, BossMonster boss)
    {
        if (playerTarget == null)
            throw new System.InvalidOperationException("GameManager에서 플레이어 Transform을 전달해야 합니다.");
        if (prefab == null || prefab.GetComponent<Stage>() == null)
        {
            Debug.LogError("Stage 컴포넌트가 있는 스테이지 프리팹을 지정하세요.", this);
            return;
        }
        if (gateManager == null)
        {
            Debug.LogError("GateManager가 없습니다.", this);
            return;
        }
        EndStage();
        GameObject instance = Instantiate(prefab, Vector3.zero, prefab.transform.rotation, stageParent);
        instance.SetActive(true);
        currentStage = instance.GetComponent<Stage>();
        try
        {
            // 등록 → 상태 이벤트 구독 → 진행 시작 순서를 지킵니다.
            gateManager.SetGates(currentStage.Gates, currentStage.BossGate, monsterData, bossData, boss, playerTarget);
            currentStage.Initialize(gateManager);
            currentStage.StateChanged += OnStageStateChanged;
            currentStage.Completed += OnStageCompleted;
            currentStage.BeginStage();
        }
        catch
        {
            EndStage();
            throw;
        }
    }

    public void EndStage()
    {
        Stage stage = currentStage;
        if (stage != null)
        {
            stage.StateChanged -= OnStageStateChanged;
            stage.Completed -= OnStageCompleted;
            stage.EndStage();
        }
        if (gateManager != null) gateManager.ClearGates();
        currentStage = null;
        if (stage != null)
        {
            stage.gameObject.SetActive(false);
            Destroy(stage.gameObject);
        }
    }

    private void OnStageStateChanged(StageState state)
    {
        if (state == StageState.BossGateUnlocked)
            GameUI.HUD.ShowGuide("보스 게이트를 공격해 개방하세요");
    }

    private void OnStageCompleted()
    {
        if (!IsStageCleared) return;
        GameUI.Screens.ShowStageClear(stageNumber, clearRank);
        StageCleared?.Invoke();
    }

    private void OnDestroy() => EndStage();
}
