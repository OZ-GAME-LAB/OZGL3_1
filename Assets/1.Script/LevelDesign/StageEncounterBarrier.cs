using UnityEngine;

/// <summary>A visible exit seal keeps the next encounter close until this gate is cleared.</summary>
[RequireComponent(typeof(BoxCollider))]
public sealed class StageEncounterBarrier : MonoBehaviour
{
    [SerializeField] private StageGate encounter;
    [SerializeField] private GameObject sealVisual;
    private BoxCollider barrier;

    public void Configure(StageGate gate, GameObject visual)
    {
        encounter = gate;
        sealVisual = visual;
    }

    private void Awake() => barrier = GetComponent<BoxCollider>();

    private void LateUpdate()
    {
        bool locked = encounter != null && encounter.State != StageGate.GateState.Closed;
        if (barrier != null) barrier.enabled = locked;
        if (sealVisual != null && sealVisual.activeSelf != locked) sealVisual.SetActive(locked);
    }
}
