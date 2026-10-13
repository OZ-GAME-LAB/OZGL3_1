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

    private void OnEnable() => UIState.GameplayInputBlockedChanged += SetInputBlocked;

    private void OnDisable()
    {
        UIState.GameplayInputBlockedChanged -= SetInputBlocked;
        if (input != null) input.enabled = false;
    }

    private void Start()
    {
        spawnPosition = player.transform.position;
        spawnRotation = player.transform.rotation;
        ReturnToTitle();
    }

    // UI Links의 UIRequestEvents에서 시작·재도전·타이틀 요청을 연결합니다.
    public void SelectClass() => GameUI.Screens.ShowClassSelect(null);

    public void StartGame(int selectedClass)
    {
        playerUI.SetClass((PlayerClass)selectedClass);
        RestartStage();
    }

    public void RestartStage()
    {
        player.gameObject.SetActive(false);
        input.enabled = false;
        gameManager.StageManager.EndStage();
        gameManager.StageManager.StartStage();
        if (!gameManager.StageManager.IsStageRunning) return;

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

        gateUI.BeginStage(1, gameManager.GateManager.Gates.Count);
        if (gameManager.GateManager.AreGatesActive) gateUI.GateOpened();
        // 봉쇄가 확정되는 곳에서 gateUI.GateSealed()를 호출합니다.
    }

    public void ReturnToTitle()
    {
        player.gameObject.SetActive(false);
        input.enabled = false;
        gameManager.StageManager.EndStage();
        GameUI.Screens.CloseAll();
        GameUI.HUD.Visible = false;
        GameUI.Screens.ShowTitle(false);
    }

    private void SetInputBlocked(bool blocked)
    {
        bool canPlay = player.gameObject.activeInHierarchy && !blocked;
        input.enabled = canPlay;
        player.enabled = canPlay;
        if (!canPlay && player.gameObject.activeInHierarchy) player.Stop();
    }
}
