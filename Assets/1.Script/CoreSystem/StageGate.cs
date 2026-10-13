using System;
using UnityEngine;

// 몬스터 종류, 수량, 간격과 실제 소환 동작은 각 게이트에서 구현합니다.
public abstract class StageGate : MonoBehaviour
{
    public enum GateState { Inactive, Active, Closed }

    [SerializeField] private Renderer gateRenderer;
    [SerializeField] private Material gateMaterial;
    private MaterialPropertyBlock colorProperties;

    public GateState State { get; private set; }
    public event Action<StageGate> Closed;

    public void Initialize()
    {
        if (gateRenderer == null)
            gateRenderer = GetComponentInChildren<Renderer>(true);
        if (gateRenderer != null && gateMaterial != null)
            gateRenderer.sharedMaterial = gateMaterial;
        Deactivate();
    }

    public virtual void Initialize(PoolManager poolManager)
    {
        if (poolManager == null) throw new ArgumentNullException(nameof(poolManager));
        Initialize();
    }

    public void Activate()
    {
        if (State != GateState.Inactive || !isActiveAndEnabled) return;
        SetState(GateState.Active);
        StartSpawning();
    }

    public void Deactivate()
    {
        StopSpawning();
        SetState(GateState.Inactive);
    }

    public void Close()
    {
        if (State != GateState.Active) return;
        StopSpawning();
        SetState(GateState.Closed);
        Closed?.Invoke(this);
    }

    private void SetState(GateState state)
    {
        State = state;
        if (gateRenderer == null) return;
        Color color = state == GateState.Active ? Color.green
            : state == GateState.Closed ? Color.red : Color.blue;
        if (colorProperties == null) colorProperties = new MaterialPropertyBlock();
        gateRenderer.GetPropertyBlock(colorProperties);
        colorProperties.SetColor("_BaseColor", color);
        colorProperties.SetColor("_Color", color);
        gateRenderer.SetPropertyBlock(colorProperties);
    }

    protected virtual void OnDisable() => Deactivate();

    public abstract void StartSpawning();
    public abstract void StopSpawning();
}
