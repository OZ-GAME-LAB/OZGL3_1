using Cysharp.Threading.Tasks;
using UnityEngine;

public class StageManager : BaseManager
{
    [SerializeField] private GameObject stagePrefab;
    [SerializeField] private Transform stageParent;
    private GateManager gateManager;

    public bool IsStageRunning { get; private set; }
    public bool IsStageCleared { get; private set; }
    public event System.Action StageCleared;
    public GameObject CurrentStage { get; private set; }

    public override UniTask InitializeAsync()
    {
        if (gateManager == null)
            throw new System.InvalidOperationException("GameManager에서 GateManager를 주입해야 합니다.");

        EndStage();
        gateManager.AllGatesClosed -= OnAllGatesClosed;
        gateManager.AllGatesClosed += OnAllGatesClosed;
        return UniTask.CompletedTask;
    }

    public UniTask InitializeAsync(GateManager manager)
    {
        if (manager == null)
            throw new System.ArgumentNullException(nameof(manager));

        // 이전 매니저가 관리하던 게이트를 정리한 뒤 새 참조를 받습니다.
        EndStage();
        if (gateManager != null)
            gateManager.AllGatesClosed -= OnAllGatesClosed;
        gateManager = manager;
        return InitializeAsync();
    }

    public void StartStage()
    {
        StartStage(stagePrefab);
    }

    public void StartStage(GameObject prefab)
    {
        if (IsStageRunning)
            return;

        if (prefab == null)
        {
            Debug.LogError("스테이지 프리팹을 지정하세요.", this);
            return;
        }

        if (gateManager == null)
        {
            Debug.LogError("GateManager가 없습니다.", this);
            return;
        }

        if (prefab.GetComponent<Stage>() == null)
        {
            Debug.LogError("Stage 컴포넌트를 추가하세요.", this);
            return;
        }

        if (CurrentStage != null)
            EndStage();
        CurrentStage = Instantiate(prefab, Vector3.zero, prefab.transform.rotation, stageParent);
        // 씬에 둔 비활성 스테이지 원본도 사용할 수 있습니다.
        CurrentStage.SetActive(true);
        Stage stage = CurrentStage.GetComponent<Stage>();
        gateManager.SetGates(stage.Gates);
        IsStageCleared = false;
        IsStageRunning = true;
        gateManager.ActivateGates();
    }

    public void EndStage()
    {
        IsStageRunning = false;
        IsStageCleared = false;

        if (gateManager != null)
            gateManager.ClearGates();

        if (CurrentStage != null)
        {
            // Destroy는 프레임 끝에 처리되므로 기존 스테이지를 먼저 비활성화합니다.
            CurrentStage.SetActive(false);
            Destroy(CurrentStage);
            CurrentStage = null;
        }
    }

    private void OnAllGatesClosed()
    {
        if (!IsStageRunning || IsStageCleared) return;
        IsStageCleared = true;
        IsStageRunning = false;
        // 폐쇄된 게이트의 빨간색을 확인할 수 있도록 스테이지는 유지합니다.
        StageCleared?.Invoke();
    }

    private void OnDestroy()
    {
        if (gateManager != null)
            gateManager.AllGatesClosed -= OnAllGatesClosed;
        EndStage();
    }

}
