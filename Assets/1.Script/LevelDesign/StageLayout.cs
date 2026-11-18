using OZ.UI.Contracts;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Stage-authored spawn, map and bounded side-scrolling camera.</summary>
[DisallowMultipleComponent]
public sealed class StageLayout : MonoBehaviour
{
    [SerializeField] private Transform playerSpawn;
    [SerializeField] private MapData map;
    [SerializeField] private string startRoom = "entry";
    [SerializeField] private Vector2 horizontalBounds = new Vector2(-12f, 114f);
    [SerializeField] private float cameraSize = 6.8f;
    [SerializeField] private float cameraHeight = 3.6f;
    [SerializeField] private float cameraPitch = 8f;
    private Transform player;
    private Camera view;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private float originalSize;
    private Vector3 cameraVelocity;
    private bool bound;
    private readonly HashSet<string> announcedRooms = new HashSet<string>();

    public Transform PlayerSpawn => playerSpawn;
    public MapData Map => map;
    public Vector2 HorizontalBounds => horizontalBounds;

    public bool TryAnnounceRoom(string roomId) => !string.IsNullOrEmpty(roomId) && announcedRooms.Add(roomId);

    public void Configure(Transform spawn, MapData stageMap, Vector2 bounds)
    {
        playerSpawn = spawn;
        map = stageMap;
        horizontalBounds = bounds;
    }

    public void Begin(Transform target)
    {
        player = target;
        view = Camera.main;
        if (view != null && !bound)
        {
            originalPosition = view.transform.position;
            originalRotation = view.transform.rotation;
            originalSize = view.orthographicSize;
            bound = true;
            view.orthographicSize = cameraSize;
            view.transform.rotation = Quaternion.Euler(cameraPitch, 0f, 0f);
            view.transform.position = DesiredPosition();
            cameraVelocity = Vector3.zero;
        }
        if (map != null)
        {
            GameUI.Map.SetMap(map);
            GameUI.Map.SetPlayerRoom(startRoom);
        }
    }

    private Vector3 DesiredPosition()
    {
        float halfWidth = view.orthographicSize * view.aspect;
        float min = horizontalBounds.x + halfWidth;
        float max = horizontalBounds.y - halfWidth;
        float x = min <= max ? Mathf.Clamp(player.position.x + 2f, min, max)
            : (horizontalBounds.x + horizontalBounds.y) * 0.5f;
        float y = cameraHeight - originalPosition.z * Mathf.Tan(cameraPitch * Mathf.Deg2Rad)
            + Mathf.Max(0f, player.position.y - 2.5f) * 0.6f;
        return new Vector3(x, y, originalPosition.z);
    }

    private void LateUpdate()
    {
        if (!bound || view == null || player == null || !player.gameObject.activeInHierarchy) return;
        view.transform.position = Vector3.SmoothDamp(view.transform.position, DesiredPosition(),
            ref cameraVelocity, 0.16f, Mathf.Infinity, Time.deltaTime);
    }

    private void OnDisable()
    {
        if (bound && view != null)
        {
            view.transform.position = originalPosition;
            view.transform.rotation = originalRotation;
            view.orthographicSize = originalSize;
        }
        bound = false;
        player = null;
    }
}
