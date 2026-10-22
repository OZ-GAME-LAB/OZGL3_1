using System;
using System.Collections.Generic;
using UnityEngine;

public enum StageState
{
    Idle,
    NormalGates,
    BossGateUnlocked,
    BossBattle,
    Cleared,
    Ended
}

// GateManager 이벤트로 상태를 전환하고 종료 시 구독을 해제합니다.
// 상태의 원본은 Stage이며, 매니저는 생성과 연결을 담당합니다.
public class Stage : MonoBehaviour
{
    [SerializeField] private List<StageGate> gates = new List<StageGate>();
    [SerializeField] private BossGate bossGate;
    [SerializeField] private StageState state = StageState.Idle;
    private GateManager gateManager;

    public BossGate BossGate => bossGate;
    public IReadOnlyList<StageGate> Gates => gates.AsReadOnly();
    public StageState State => state;
    public bool IsRunning => state == StageState.NormalGates ||
        state == StageState.BossGateUnlocked || state == StageState.BossBattle;
    public event Action<StageState> StateChanged;
    public event Action Completed;

    public void Initialize(GateManager manager)
    {
        if (manager == null) throw new ArgumentNullException(nameof(manager));
        Unsubscribe();
        gateManager = manager;
        StageState previous = state;
        state = StageState.Idle;
        if (previous != state) LogTransition(previous, state);
        gateManager.AllGatesClosed += HandleAllGatesClosed;
        gateManager.BossSpawned += HandleBossSpawned;
        gateManager.BossDefeated += HandleBossDefeated;
    }

    public void BeginStage()
    {
        if (gateManager == null || !Transition(StageState.Idle, StageState.NormalGates)) return;
        if (state == StageState.NormalGates) gateManager.ActivateGates();
    }

    public void HandleAllGatesClosed()
    {
        if (!Transition(StageState.NormalGates, StageState.BossGateUnlocked)) return;
        if (state == StageState.BossGateUnlocked) gateManager.UnlockBossGate();
    }

    public void HandleBossSpawned()
    {
        Transition(StageState.BossGateUnlocked, StageState.BossBattle);
    }

    public void HandleBossDefeated()
    {
        if (!Transition(StageState.BossBattle, StageState.Cleared)) return;
        if (state == StageState.Cleared) Completed?.Invoke();
    }

    public void EndStage()
    {
        bool changed = state != StageState.Ended;
        StageState previous = state;
        state = StageState.Ended;
        if (changed) LogTransition(previous, state);
        Unsubscribe();
        gateManager = null;
        if (changed) StateChanged?.Invoke(state);
    }

    private bool Transition(StageState expected, StageState next)
    {
        if (state != expected) return false;
        StageState previous = state;
        state = next;
        LogTransition(previous, state);
        StateChanged?.Invoke(state);
        return true;
    }

    private void LogTransition(StageState previous, StageState next)
    {
        Debug.Log($"[Stage:{name}] 상태 전환: {previous} → {next}", this);
    }

    private void Unsubscribe()
    {
        if (gateManager == null) return;
        gateManager.AllGatesClosed -= HandleAllGatesClosed;
        gateManager.BossSpawned -= HandleBossSpawned;
        gateManager.BossDefeated -= HandleBossDefeated;
    }

    private void OnDisable() => EndStage();
}
