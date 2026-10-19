using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// [박한솔 UI · v0.9] 게이트 봉쇄 진행 → 좌상단 퀘스트 "게이트 봉쇄 N / M" 숫자. 팀원 코드(GateManager 등) 수정 없이 동작한다.
///   GameManager.Instance.GateManager.Gates 의 상태(StageGate.State)를 0.2초마다 읽어서
///   닫힌 게이트 수 · 전체 수 · 열린 게이트가 있는지가 바뀌면 GateUILink.SetProgress 로 HUD를 맞춘다.
///   "게이트 파괴" 띠는 GateManager가 이미 띄우므로 여기서는 숫자만 맞춘다 (띠 중복 없음).
/// 씬에 GameManager가 있으면 Play 시 자동으로 생긴다 (씬 파일 수정 불필요).
/// 나중에 GateManager가 직접 gateUI.GateSealed()를 부르게 되면 이 컴포넌트는 지워도 된다.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("OZ/UI/Links/Gate Progress Bridge")]
public class GateProgressBridge : MonoBehaviour
{
    [SerializeField, Min(0.05f)] float pollInterval = 0.2f;

    GateUILink _link;
    int _lastClosed = -1, _lastTotal = -1;
    bool _lastActive;
    float _next;

    void Update()
    {
        if (Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + pollInterval;
        Sync();
    }

    void Sync()
    {
        var gm = GameManager.Instance;
        var gates = gm != null ? gm.GateManager : null;
        if (gates == null || gates.Gates.Count == 0) { _lastClosed = _lastTotal = -1; return; }

        if (_link == null) _link = FindAnyObjectByType<GateUILink>(FindObjectsInactive.Include);
        if (_link == null) return;

        int total = 0, closed = 0;
        bool active = false;
        foreach (var g in gates.Gates)
        {
            if (g == null) continue;
            total++;
            if (g.State == StageGate.GateState.Closed) closed++;
            else if (g.State == StageGate.GateState.Active) active = true;
        }
        if (total == 0) return;
        if (closed == _lastClosed && total == _lastTotal && active == _lastActive) return;

        _lastClosed = closed;
        _lastTotal = total;
        _lastActive = active;
        _link.SetProgress(closed, total, active); // GateStateReset → 퀘스트 목록·게이트 상태 숫자 갱신 (띠 없음)
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
        if (FindAnyObjectByType<GateProgressBridge>(FindObjectsInactive.Include) != null) return;
        if (FindAnyObjectByType<GameManager>(FindObjectsInactive.Include) == null) return;
        new GameObject("GateProgressBridge (auto, UI)").AddComponent<GateProgressBridge>(); // Play 중에만 생김
    }
}
