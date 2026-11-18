using OZ.UI.Contracts;
using UnityEngine;

/// <summary>Updates exploration on every entry; announces the area only on the first visit.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class StageRoomTrigger : MonoBehaviour
{
    [SerializeField] private string roomId;
    [SerializeField] private string areaName;
    [SerializeField] private string subtitle;
    [SerializeField, Min(0.5f)] private float holdSeconds = 2.2f;
    private bool announced;

    public string RoomId => roomId;

    public void Configure(string id, string displayName, string areaSubtitle = null)
    {
        roomId = id;
        areaName = displayName;
        subtitle = areaSubtitle;
    }

    private void Awake() => GetComponent<BoxCollider>().isTrigger = true;

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || other.isTrigger || other.GetComponentInParent<PlayerController>() == null) return;
        Enter();
    }

    public void Enter()
    {
        if (GameUI.Map.Current != null && !string.IsNullOrEmpty(roomId) && GameUI.Map.Current.Find(roomId) != null)
            GameUI.Map.SetPlayerRoom(roomId);
        if (announced || string.IsNullOrEmpty(areaName)) return;
        announced = true;
        StageLayout layout = GetComponentInParent<StageLayout>();
        if (layout != null && !layout.TryAnnounceRoom(roomId)) return;
        GameUI.Area.Show(areaName, subtitle, holdSeconds);
    }

    private void Reset() => GetComponent<BoxCollider>().isTrigger = true;
}
