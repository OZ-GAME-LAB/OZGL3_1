using Cysharp.Threading.Tasks;
using UnityEngine;

public class GameManager : BaseManager
{
    [SerializeField] private StageManager stageManager;
    [SerializeField] private GateManager gateManager;
    [SerializeField] private PoolManager poolManager;
    [SerializeField] private ResourceManager resourceManager;
    [SerializeField] private DataManager dataManager;
    [Tooltip("선택 사항: PreloadData JSON 주소. 비어 있으면 해당 테이블을 등록하지 않습니다.")]
    [SerializeField] private string preloadDataKey;
    private bool isReady;
    private bool isLoading;
    private Transform playerTarget;
    private UniTaskCompletionSource resourceCleanup;

    public static GameManager Instance { get; private set; }
    public bool IsReady => isReady;
    public event System.Action InitializationCompleted;
    public event System.Action StageChanging;
    public event System.Action StageStarted;
    public event System.Action GameCompleted;
    private bool gameCompleted;
    public StageManager StageManager => stageManager;
    public GateManager GateManager => gateManager;
    public PoolManager PoolManager => poolManager;
    public bool IsStageRunning => stageManager != null && stageManager.IsStageRunning;
    public int StageNumber => stageManager != null ? stageManager.StageNumber : 0;
    public int GateCount => gateManager != null ? gateManager.Gates.Count : 0;
    public bool AreGatesActive => gateManager != null && gateManager.AreGatesActive;
    public ResourceManager ResourceManager => resourceManager;
    public DataManager DataManager => dataManager;

    public void SetPlayerTarget(Transform target)
    {
        if (target == null) throw new System.ArgumentNullException(nameof(target));
        playerTarget = target;
    }

    private void Awake()
    {
        if (!EnsureSingleton())
            return;
        SetupManagers();
    }

    private void Start()
    {
        if (Instance == this)
            FirstGameLoadingAsync().Forget();
    }

    private void InitService()
    {
        //서비스를 등록하려면 여기에 추가!    
    }
    public async UniTask FirstGameLoadingAsync()
    {
        if (isReady || isLoading)
            return;

        isLoading = true;
        try
        {
            await InitializeManagersAsync();
            InitService();
            isReady = true;
        }
        finally
        {
            isLoading = false;
        }
        InitializationCompleted?.Invoke();
        //await GameManager.Instance.UIManager.OpenMainMenuUIAsync();
    }

    public async UniTask GameStartAsync()
    {
       // await GameManager.Instance.UIManager.OpenMainUIAsync();
    }

    public bool StartStage()
    {
        return SetStage(StageNumber);
    }

    public bool NewGame()
    {
        bool started = SetStage(1);
        if (started) gameCompleted = false;
        return started;
    }

    public bool RestartStage() => SetStage(StageNumber);

    public bool NextStage()
    {
        if (!isReady || gameCompleted || !stageManager.IsStageCleared) return false;
        int nextNumber = stageManager.StageNumber + 1;
        if (stageManager.HasStage(nextNumber)) return SetStage(nextNumber);
        StageChanging?.Invoke();
        stageManager.EndStage();
        gameCompleted = true;
        GameCompleted?.Invoke();
        return true;
    }

    private bool SetStage(int number)
    {
        if (!isReady)
        {
            Debug.LogWarning("게임 시스템 초기화가 완료되지 않았습니다.", this);
            return false;
        }
        if (!stageManager.CanSetStage(number))
        {
            Debug.LogError($"{number}스테이지를 목록에 올바르게 등록하세요.", this);
            return false;
        }
        if (playerTarget == null)
        {
            Debug.LogError("SceneGameLink를 통해 플레이어 Transform을 연결하세요.", this);
            return false;
        }
        StageChanging?.Invoke();
        if (!stageManager.SetStage(number, playerTarget)) return false;
        StageStarted?.Invoke();
        return true;
    }

    public void EndStage()
    {
        if (stageManager != null) stageManager.EndStage();
    }

    // 스테이지 종료와 전체 리소스 정리는 구분합니다. 타이틀 복귀/재도전은 캐시를 유지합니다.
    public UniTask ReleaseResourcesAsync()
    {
        if (resourceCleanup != null) return resourceCleanup.Task;
        if (isLoading)
            throw new System.InvalidOperationException("시스템 초기화 완료 후 리소스를 정리하세요.");
        var completion = new UniTaskCompletionSource();
        resourceCleanup = completion;
        ReleaseResourcesCoreAsync(completion).Forget();
        return completion.Task;
    }

    private async UniTask ReleaseResourcesCoreAsync(UniTaskCompletionSource completion)
    {
        try
        {
            isReady = false; // 정리 중 새 스테이지 요청 차단
            StageChanging?.Invoke();
            EndStage();
            await poolManager.ClearPoolsAsync();
            // 오디오 등 추가 사용 시스템도 이 지점 전에 정리해야 합니다.
            resourceManager.ReleaseAll();
            resourceCleanup = null;
            completion.TrySetResult();
        }
        catch (System.Exception exception)
        {
            resourceCleanup = null;
            completion.TrySetException(exception);
        }
    }

    public async UniTask InitializeManagersAsync()
    {
        if (resourceCleanup != null)
            throw new System.InvalidOperationException("리소스 정리 완료 후 초기화하세요.");
        await InitializeAsync();
        if (stageManager == null || gateManager == null || poolManager == null)
            throw new System.InvalidOperationException("StageManager, GateManager, PoolManager를 모두 연결하세요.");

        stageManager.EndStage();
        if (resourceManager == null)
            throw new System.InvalidOperationException("ResourceManager를 연결하세요.");
        await resourceManager.InitializeAsync();
        if (dataManager == null)
            throw new System.InvalidOperationException("DataManager를 연결하세요.");
        await dataManager.InitializeAsync(resourceManager, preloadDataKey);
        if (dataManager.TryGetDataTable<PreloadData>(out var preloadTable))
        {
            var requests = new System.Collections.Generic.List<ResourceManager.PreloadRequest>();
            foreach (PreloadData row in preloadTable.Values) requests.Add(CreatePreloadRequest(row));
            await resourceManager.PreloadAsync(requests);
        }
        await InitializePoolsAsync();
        await gateManager.InitializeAsync(poolManager);
        await stageManager.InitializeAsync(gateManager, dataManager, resourceManager);
        //await ResourceManager.InitializeAsync();    
    }
    private async UniTask InitializePoolsAsync()
    {
        var requests = new System.Collections.Generic.List<PoolManager.PoolRequest>();
        var keys = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        if (dataManager.TryGetDataTable<PoolData>(out var table))
        {
            foreach (PoolData data in table.Values)
            {
                if (string.IsNullOrWhiteSpace(data.DataId) || data.InitialCount < 0)
                    throw new System.InvalidOperationException($"풀 '{data.Id}'의 DataId와 InitialCount를 확인하세요.");

                string prefabKey;
                switch (data.PoolType)
                {
                    case PoolType.Monster:
                        if (!dataManager.TryGetData<MonsterData>(data.DataId, out var monster))
                            throw new System.InvalidOperationException($"풀 '{data.Id}'가 참조하는 MonsterData '{data.DataId}'가 없습니다.");
                        prefabKey = monster.PrefabKey;
                        break;
                    case PoolType.BossMonster:
                        if (!dataManager.TryGetData<BossMonsterData>(data.DataId, out var boss))
                            throw new System.InvalidOperationException($"풀 '{data.Id}'가 참조하는 BossMonsterData '{data.DataId}'가 없습니다.");
                        prefabKey = boss.PrefabKey;
                        break;
                    default:
                        throw new System.InvalidOperationException($"풀 '{data.Id}'의 PoolType '{data.PoolType}'은 지원하지 않습니다.");
                }

                if (string.IsNullOrWhiteSpace(prefabKey))
                    throw new System.InvalidOperationException($"풀 '{data.Id}'의 참조 데이터 '{data.DataId}'에 프리팹 주소가 없습니다.");
                if (!keys.Add(prefabKey))
                    throw new System.InvalidOperationException($"동일 프리팹 주소의 풀 설정이 중복되었습니다: '{prefabKey}'");
                requests.Add(new PoolManager.PoolRequest(prefabKey, data.InitialCount));
            }
        }
        await poolManager.InitializeAsync(resourceManager, requests);
    }

    private static ResourceManager.PreloadRequest CreatePreloadRequest(PreloadData data)
    {
        if (string.IsNullOrWhiteSpace(data.Key))
            throw new System.InvalidOperationException($"프리로드 '{data.Id}'의 주소가 비어 있습니다.");
        switch (data.AssetType)
        {
            case PreloadAssetKind.GameObject: return ResourceManager.PreloadRequest.For<GameObject>(data.Key);
            case PreloadAssetKind.TextAsset: return ResourceManager.PreloadRequest.For<TextAsset>(data.Key);
            case PreloadAssetKind.AudioClip: return ResourceManager.PreloadRequest.For<AudioClip>(data.Key);
            case PreloadAssetKind.Sprite: return ResourceManager.PreloadRequest.For<Sprite>(data.Key);
            case PreloadAssetKind.Material: return ResourceManager.PreloadRequest.For<Material>(data.Key);
            case PreloadAssetKind.Texture2D: return ResourceManager.PreloadRequest.For<Texture2D>(data.Key);
            default: throw new System.InvalidOperationException($"프리로드 '{data.Id}'의 에셋 타입이 잘못되었습니다.");
        }
    }

    public override UniTask InitializeAsync()
    {
        return UniTask.CompletedTask;
    }
    private bool EnsureSingleton()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[{nameof(GameManager)}:{nameof(EnsureSingleton)}] 중복된 인스턴스가 발견되어 {gameObject.name} 오브젝트를 파괴합니다.");
            Destroy(gameObject);
            return false;
        }

        Instance = this;
        return true;
    }

    private void SetupManagers()
    {
        if (resourceManager == null) resourceManager = GetComponent<ResourceManager>();
        // 기존 씬에 새 컴포넌트가 없으면 Composition Root에서 생성합니다.
        if (resourceManager == null) resourceManager = gameObject.AddComponent<ResourceManager>();
        if (dataManager == null) dataManager = GetComponent<DataManager>();
        if (dataManager == null) dataManager = gameObject.AddComponent<DataManager>();
        if (poolManager == null) poolManager = GetComponent<PoolManager>();
        if (poolManager == null)
            Debug.LogError("PoolManager를 인스펙터에 연결하거나 같은 오브젝트에 추가하세요.", this);
        if (gateManager == null)
            gateManager = GetComponent<GateManager>();

        if (gateManager == null)
            Debug.LogError("GateManager를 인스펙터에 연결하거나 같은 오브젝트에 추가하세요.", this);

        if (stageManager == null)
            stageManager = GetComponent<StageManager>();

        if (stageManager == null)
            Debug.LogError("StageManager를 인스펙터에 연결하거나 같은 오브젝트에 추가하세요.", this);
     //   ResourceManager = this.GetComponent<ResourceManager>();       
    }
}
