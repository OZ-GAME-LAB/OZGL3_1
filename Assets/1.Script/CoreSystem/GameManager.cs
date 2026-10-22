using Cysharp.Threading.Tasks;
using UnityEngine;

public class GameManager : BaseManager
{
    [SerializeField] private StageManager stageManager;
    [SerializeField] private GateManager gateManager;
    [SerializeField] private PoolManager poolManager;
    private bool isReady;
    private bool isLoading;

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
    //public ResourceManager ResourceManager { get; private set; }

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
        StageChanging?.Invoke();
        if (!stageManager.SetStage(number)) return false;
        StageStarted?.Invoke();
        return true;
    }

    public void EndStage()
    {
        if (stageManager != null) stageManager.EndStage();
    }

    public async UniTask InitializeManagersAsync()
    {
        await InitializeAsync();
        if (stageManager == null || gateManager == null || poolManager == null)
            throw new System.InvalidOperationException("StageManager, GateManager, PoolManager를 모두 연결하세요.");

        stageManager.EndStage();
        await poolManager.InitializeAsync();
        await gateManager.InitializeAsync(poolManager);
        await stageManager.InitializeAsync(gateManager);
        //await ResourceManager.InitializeAsync();    
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
