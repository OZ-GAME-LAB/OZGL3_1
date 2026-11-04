using UnityEngine;


[RequireComponent(typeof(BoxCollider))]
public class Ladder : MonoBehaviour
{
    [Tooltip("꼭대기 도착 시 플레이어가 설 위치 (발판 위, 발 기준)")]
    [SerializeField] private Transform _topExit;

    private Collider _collider;

    // 사다리 길이를 바꿔도 콜라이더 크기만 조절하면 값이 따라오게
    public float CenterX { get { return _collider.bounds.center.x; } }
    public float BottomY { get { return _collider.bounds.min.y; } }

    public float TopY { get { return _topExit.position.y; } }
    public Vector3 TopExitPosition
    {
        get { return _topExit.position; }
    }

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _collider.isTrigger = true; // 인스펙터에서 깜빡해도 막히지 않게

        if (_topExit == null)
        {
            Debug.LogError($"[Ladder:Awake] {name}에 TopExit가 연결되지 않았습니다.");
        }
    }
}
