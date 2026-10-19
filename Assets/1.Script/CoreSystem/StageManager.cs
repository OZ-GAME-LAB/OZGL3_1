using Cysharp.Threading.Tasks;
using UnityEngine;
using OZ.UI.Contracts;

// 생성·의존성 연결·종료를 담당하며 진행 상태는 Stage에서 조회합니다.
public class StageManager : BaseManager
{
    [SerializeField] private GameObject stagePrefab;
    [SerializeField] private Transform stageParent;
    [SerializeField, Min(1)] private int stageNumber = 1;
    [SerializeField] private HunterRank clearRank = HunterRank.E;
    private GateManager gateManager;
    private Stage currentStage;

    public bool IsStageRunning => currentStage != null && currentStage.IsRunning;
    public int StageNumber => stageNumber;
    public bool IsStageCleared => currentStage != null && currentStage.State == StageState.Cleared;
    public event System.Action StageCleared;
    public GameObject CurrentStage => currentStage != null ? currentStage.gameObject : null;

    public override UniTask InitializeAsync()
    {
        if (gateManager == null)
            throw new System.InvalidOperationException("GameManager에서 GateManager를 주입해야 합니다.");
        EndStage();
        return UniTask.CompletedTask;
    }

    public UniTask InitializeAsync(GateManager manager)
    {
        if (manager == null) throw new System.ArgumentNullException(nameof(manager));
        EndStage();
        gateManager = manager;
        return InitializeAsync();
    }

    public void StartStage() => StartStage(stagePrefab);

    public void StartStage(GameObject prefab)
    {
        if (IsStageRunning) return;
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
            gateManager.SetGates(currentStage.Gates, currentStage.BossGate);
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
