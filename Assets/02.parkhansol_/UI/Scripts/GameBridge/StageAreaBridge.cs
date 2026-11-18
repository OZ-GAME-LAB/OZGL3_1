using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// [박한솔 UI · v0.7] MainScene용: 스테이지가 시작될 때마다(새 게임·재도전) 화면 위쪽에 지역 이름을 띄운다.
///   GameManager.StageStarted 이벤트를 받아 현재 스테이지 번호와 함께 GameUI.Area.Show를 호출한다.
///   팀원 코드·씬 수정 없이 동작 (씬에 GameManager가 있으면 Play 시 자동으로 생김).
/// 레벨이 나뉘면: 각 지역 입구에 AreaNameTrigger를 놓으면 들어갈 때마다 그 지역 이름이 뜬다.
/// 이름을 바꾸려면 Play 중 'StageAreaBridge (auto, UI)' 오브젝트에서, 또는 씬에 직접 이 컴포넌트를 놓고 수정.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("OZ/UI/Links/Stage Area Bridge")]
public class StageAreaBridge : MonoBehaviour
{
    [SerializeField] string areaName = "서울 거리";
    [Tooltip("스테이지 시작 후 이 시간 뒤에 표시 (화면 전환이 끝난 뒤 보이게)")]
    [SerializeField, Min(0f)] float delay = 0.4f;

    GameManager _gameManager;
    float _showAt = -1f;

    void OnEnable() => BindGameManager();

    // 씬에 직접 배치했을 때 GameManager.Awake보다 먼저 활성화되는 경우를 보완합니다.
    void Start() => BindGameManager();

    void BindGameManager()
    {
        if (_gameManager != null) return;
        _gameManager = GameManager.Instance;
        if (_gameManager == null) return;
        _gameManager.StageStarted += OnStageStarted;
        _gameManager.StageChanging += OnStageChanging;
        if (_gameManager.IsStageRunning) OnStageStarted();
    }

    void OnStageStarted() => _showAt = Time.unscaledTime + delay;

    void OnStageChanging() => _showAt = -1f;

    void OnDisable()
    {
        if (_gameManager != null)
        {
            _gameManager.StageStarted -= OnStageStarted;
            _gameManager.StageChanging -= OnStageChanging;
        }
        _gameManager = null;
        _showAt = -1f;
    }

    void Update()
    {
        if (_gameManager != null && _gameManager.StageManager != null &&
            _gameManager.StageManager.CurrentStage != null &&
            _gameManager.StageManager.CurrentStage.GetComponent<StageLayout>() != null)
        {
            // 구역 트리거가 있는 스테이지는 작성된 구역명을 직접 표시한다.
            _showAt = -1f;
            return;
        }
        if (_showAt >= 0f && Time.unscaledTime >= _showAt)
        {
            _showAt = -1f;
            if (_gameManager != null && _gameManager.IsStageRunning)
                GameUI.Area.Show(areaName, $"STAGE {_gameManager.StageNumber}");
        }
    }

    // ── 자동 설치 ──
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
        if (FindAnyObjectByType<StageAreaBridge>(FindObjectsInactive.Include) != null) return;
        if (FindAnyObjectByType<GameManager>(FindObjectsInactive.Include) == null) return;
        new GameObject("StageAreaBridge (auto, UI)").AddComponent<StageAreaBridge>(); // Play 중에만 생김
    }
}
