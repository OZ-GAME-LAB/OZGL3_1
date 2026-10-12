using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// [박한솔 UI · v0.7] MainScene용: 스테이지가 시작될 때마다(새 게임·재도전) 화면 위쪽에 지역 이름을 띄운다.
///   GameManager.Instance.StageManager.IsStageRunning 이 꺼짐 → 켜짐 으로 바뀌는 순간 GameUI.Area.Show.
///   팀원 코드·씬 수정 없이 동작 (씬에 GameManager가 있으면 Play 시 자동으로 생김).
/// 레벨이 나뉘면: 각 지역 입구에 AreaNameTrigger를 놓으면 들어갈 때마다 그 지역 이름이 뜬다.
/// 이름을 바꾸려면 Play 중 'StageAreaBridge (auto, UI)' 오브젝트에서, 또는 씬에 직접 이 컴포넌트를 놓고 수정.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("OZ/UI/Links/Stage Area Bridge")]
public class StageAreaBridge : MonoBehaviour
{
    [SerializeField] string areaName = "서울 거리";
    [SerializeField] string subtitle = "STAGE 1";
    [Tooltip("스테이지 시작 후 이 시간 뒤에 표시 (화면 전환이 끝난 뒤 보이게)")]
    [SerializeField, Min(0f)] float delay = 0.4f;

    bool _wasRunning;
    float _showAt = -1f;

    void Update()
    {
        var gm = GameManager.Instance;
        bool running = gm != null && gm.StageManager != null && gm.StageManager.IsStageRunning;
        if (running && !_wasRunning) _showAt = Time.unscaledTime + delay;
        _wasRunning = running;
        if (_showAt > 0f && Time.unscaledTime >= _showAt)
        {
            _showAt = -1f;
            if (running) GameUI.Area.Show(areaName, subtitle);
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
