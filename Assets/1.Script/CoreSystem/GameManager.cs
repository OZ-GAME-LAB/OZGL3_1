using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : BaseManager
{
    [SerializeField] private StageManager stageManager;
    [SerializeField] private GateManager gateManager;
    private bool isReady;
    private bool isLoading;

    public static GameManager Instance { get; private set; }
    public StageManager StageManager => stageManager;
    public GateManager GateManager => gateManager;
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

    private void Update()
    {
        if (Instance != this || !isReady)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.aKey.wasPressedThisFrame)
            stageManager.StartStage();
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
        //await GameManager.Instance.UIManager.OpenMainMenuUIAsync();
    }

    public async UniTask GameStartAsync()
    {
       // await GameManager.Instance.UIManager.OpenMainUIAsync();
    }

    public async UniTask InitializeManagersAsync()
    {
        await InitializeAsync();
        if (stageManager == null || gateManager == null)
            throw new System.InvalidOperationException("StageManager와 GateManager를 모두 연결하세요.");

        stageManager.EndStage();
        await gateManager.InitializeAsync();
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
