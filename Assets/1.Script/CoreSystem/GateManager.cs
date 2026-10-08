using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class GateManager : BaseManager
{
    private readonly List<StageGate> gates = new List<StageGate>();

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
                gates.Add(gate);
        }
    }

    public void ActivateGates()
    {
        if (AreGatesActive)
            return;

        AreGatesActive = true;
        foreach (StageGate gate in gates.ToArray())
        {
            if (gate != null)
                gate.StartSpawning();
        }
    }

    public void DeactivateGates()
    {
        if (!AreGatesActive)
            return;

        AreGatesActive = false;
        foreach (StageGate gate in gates.ToArray())
        {
            if (gate != null)
                gate.StopSpawning();
        }
    }

    public void ClearGates()
    {
        DeactivateGates();
        gates.Clear();
    }
}
