using System.Collections.Generic;
using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// [박한솔 UI · v0.7] 활성 게이트를 화면 밖 화살표(GameUI.Indicators)로 표시. 팀원 코드(GateManager 등) 수정 없이 동작한다.
///   GameManager.Instance.GateManager.Gates를 0.25초마다 읽어서
///   게이트가 활성(AreGatesActive)이면 추적, 스테이지가 끝나거나 게이트가 사라지면 해제.
/// 씬에 GameManager가 있으면 Play 시 자동으로 생긴다 (씬 파일 수정 불필요).
/// 나중에 게이트 봉쇄가 생기면: 봉쇄된 게이트는 GameUI.Indicators.Untrack(gate.transform) 한 줄이면 화살표가 사라짐
///   (또는 게이트 오브젝트를 끄면 자동으로 숨김).
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("OZ/UI/Links/Gate Indicator Bridge")]
public class GateIndicatorBridge : MonoBehaviour
{
    [SerializeField, Min(0.05f)] float pollInterval = 0.25f;
    [Tooltip("화살표 옆 글자 (비우면 거리만)")]
    [SerializeField] string label = "";

    readonly HashSet<Transform> _tracked = new HashSet<Transform>();
    readonly List<Transform> _scratch = new List<Transform>();
    float _next;

    void OnDisable() => UntrackAll();

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
        if (gates == null || !gates.AreGatesActive || !GameUI.IsReady) { UntrackAll(); return; }

        _scratch.Clear();
        foreach (var g in gates.Gates)
            if (g != null) _scratch.Add(g.transform);

        // 사라진 게이트 해제
        var stale = new List<Transform>();
        foreach (var t in _tracked) if (t == null || !_scratch.Contains(t)) stale.Add(t);
        foreach (var t in stale) { if (t != null) GameUI.Indicators.Untrack(t); _tracked.Remove(t); }

        // 새 게이트 추적
        foreach (var t in _scratch)
        {
            if (_tracked.Contains(t)) continue;
            GameUI.Indicators.Track(t, IndicatorKind.Gate, label);
            _tracked.Add(t);
        }
    }

    void UntrackAll()
    {
        if (_tracked.Count == 0) return;
        foreach (var t in _tracked) if (t != null) GameUI.Indicators.Untrack(t);
        _tracked.Clear();
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
        if (FindAnyObjectByType<GateIndicatorBridge>(FindObjectsInactive.Include) != null) return;
        if (FindAnyObjectByType<GameManager>(FindObjectsInactive.Include) == null) return;
        new GameObject("GateIndicatorBridge (auto, UI)").AddComponent<GateIndicatorBridge>(); // Play 중에만 생김
    }
}
