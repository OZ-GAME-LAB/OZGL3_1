using OZ.UI.Contracts;
using UnityEngine;

public class SceneGameLink : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private PlayerController player;
    [SerializeField] private InputManager input;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerUILink playerUI;
    [SerializeField] private GateUILink gateUI;

    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private bool initialized;

    private void OnEnable()
    {
        UIState.GameplayInputBlockedChanged += SetInputBlocked;
        if (gameManager == null)
        {
            Debug.LogError("SceneGameLink에 GameManager를 연결하세요.", this);
            return;
        }
        if (player == null)
        {
            Debug.LogError("SceneGameLink에 Player를 연결하세요.", this);
            return;
        }
        gameManager.SetPlayerTarget(player.transform);
        gameManager.InitializationCompleted += Initialize;
        gameManager.StageChanging += SuspendPlayer;
        gameManager.StageStarted += RestorePlayer;
        gameManager.GameCompleted += ShowDemoEnd;
        if (gameManager.IsReady) Initialize();
        if (initialized) SetInputBlocked(UIState.IsGameplayInputBlocked);
    }

    private void OnDisable()
    {
        UIState.GameplayInputBlockedChanged -= SetInputBlocked;
        if (gameManager != null)
        {
            gameManager.InitializationCompleted -= Initialize;
            gameManager.StageChanging -= SuspendPlayer;
            gameManager.StageStarted -= RestorePlayer;
            gameManager.GameCompleted -= ShowDemoEnd;
        }
        if (input != null) input.enabled = false;
    }

    private void Initialize()
    {
        if (initialized) return;
        spawnPosition = player.transform.position;
        spawnRotation = player.transform.rotation;
        initialized = true;
        ReturnToTitle();
    }

    // UI Links의 UIRequestEvents에서 시작·재도전·타이틀 요청을 연결합니다.
    public void SelectClass()
    {
        if (!initialized) return;
        GameUI.Screens.ShowClassSelect(null);
    }

    public void StartGame(int selectedClass)
    {
        if (!initialized) return;
        playerUI.SetClass((PlayerClass)selectedClass);
        NewGame();
    }

    public void NewGame()
    {
        if (initialized) gameManager.NewGame();
    }

    public void RestartStage()
    {
        if (initialized) gameManager.RestartStage();
    }

    public void NextStage()
    {
        if (initialized) gameManager.NextStage();
    }

    private void SuspendPlayer()
    {
        player.gameObject.SetActive(false);
        input.enabled = false;
    }

    private void RestorePlayer()
    {
        GameUI.Screens.CloseAll();
        GameUI.HUD.Visible = true;
        player.gameObject.SetActive(true);
        playerHealth.ResetHealth(); // 체력 가득 + Dead 상태 해제.
        var body = player.GetComponent<Rigidbody>();
        body.position = spawnPosition;
        body.rotation = spawnRotation;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        SetInputBlocked(UIState.IsGameplayInputBlocked);

        gateUI.BeginStage(gameManager.StageNumber, gameManager.GateCount);
        if (gameManager.AreGatesActive) gateUI.GateOpened();
        // 봉쇄가 확정되는 곳에서 gateUI.GateSealed()를 호출합니다.
    }

    private void ShowDemoEnd()
    {
        GameUI.Screens.CloseAll();
        GameUI.HUD.Visible = false;
        GameUI.Screens.ShowDemoEnd();
    }

    public void ReturnToTitle()
    {
        if (!initialized) return;
        player.gameObject.SetActive(false);
        input.enabled = false;
        gameManager.EndStage();
        GameUI.Screens.CloseAll();
        GameUI.HUD.Visible = false;
        GameUI.Screens.ShowTitle(false);
    }

    private void SetInputBlocked(bool blocked)
    {
        bool canPlay = initialized && player.gameObject.activeInHierarchy && !blocked;
        input.enabled = canPlay;
        player.enabled = canPlay;
        if (!canPlay && player.gameObject.activeInHierarchy) player.Stop();
    }
}
