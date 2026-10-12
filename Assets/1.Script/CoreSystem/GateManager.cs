using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class GateManager : BaseManager
{
    private readonly List<StageGate> gates = new List<StageGate>();
    private int activeGateIndex = -1;
    public event Action AllGatesClosed;

    public bool AreGatesActive { get; private set; }
    public IReadOnlyList<StageGate> Gates => gates.AsReadOnly();

    public override UniTask InitializeAsync()
    {
        ClearGates();
        return UniTask.CompletedTask;
    }

    public void SetGates(IEnumerable<StageGate> stageGates)
    {
        var incomingGates = stageGates == null
            ? new List<StageGate>()
            : new List<StageGate>(stageGates);

        ClearGates();
        foreach (StageGate gate in incomingGates)
        {
            if (gate != null && !gates.Contains(gate))
            {
                gates.Add(gate);
                // 스테이지 게이트 등록/초기화 단계에서 구독합니다.
                gate.Initialize();
                gate.Closed += OnGateClosed;
            }
        }
    }

    public void ActivateGates()
    {
        if (AreGatesActive)
            return;

        if (gates.Count == 0) return;
        foreach (StageGate gate in gates)
            if (gate != null) gate.Initialize();
        AreGatesActive = true;
        activeGateIndex = 0;
        ActivateNextGate();
    }

    private void OnGateClosed(StageGate gate)
    {
        if (!AreGatesActive || activeGateIndex < 0 ||
            activeGateIndex >= gates.Count || gates[activeGateIndex] != gate) return;
        activeGateIndex++;
        ActivateNextGate();
    }

    private void ActivateNextGate()
    {
        while (activeGateIndex < gates.Count && gates[activeGateIndex] == null)
            activeGateIndex++;
        if (activeGateIndex < gates.Count)
        {
            gates[activeGateIndex].Activate();
            return;
        }
        AreGatesActive = false;
        activeGateIndex = -1;
        AllGatesClosed?.Invoke();
    }

    public void DeactivateGates()
    {
        AreGatesActive = false;
        activeGateIndex = -1;
        foreach (StageGate gate in gates.ToArray())
        {
            if (gate != null)
                gate.Deactivate();
        }
    }

    public void ClearGates()
    {
        DeactivateGates();
        foreach (StageGate gate in gates)
            if (gate != null) gate.Closed -= OnGateClosed;
        gates.Clear();
    }

    private void OnDestroy() => ClearGates();
}
