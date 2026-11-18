using UnityEngine;

// 아래에서 뛰어오르거나 S + Space로 내려갈 수 있는 발판.
// 적과 일반 지형의 충돌은 유지하고 플레이어 몸과의 충돌만 전환한다.
public class OneWayPlatform : MonoBehaviour
{
    private const float SurfaceTolerance = 0.03f;
    private Collider[] _colliders;
    private PlayerController _player;
    private Rigidbody _playerBody;
    private Collider[] _playerColliders;
    private bool[] _landingPermitted;

    public Collider[] Colliders { get { return _colliders; } }

    public bool CanSupportPlayer(Collider surface)
    {
        if (_landingPermitted == null || _player == null || _player.IsDroppingThrough(this)) return false;
        int index = System.Array.IndexOf(_colliders, surface);
        return index >= 0 && _landingPermitted[index];
    }

    private void Awake()
    {
        // 사다리나 감지용 트리거는 물리 발판에 포함하지 않는다.
        _colliders = System.Array.FindAll(GetComponentsInChildren<Collider>(true), collider => !collider.isTrigger);
        _landingPermitted = new bool[_colliders.Length];

        if (_colliders.Length == 0)
        {
            Debug.LogError($"[OneWayPlatform:Awake] {name}에 콜라이더가 없습니다.");
        }
    }

    private void Start()
    {
        // 스테이지 생성 시 타이틀/전환 때문에 플레이어가 꺼져 있을 수 있다.
        _player = FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (_player == null) return;
        _playerBody = _player.GetComponent<Rigidbody>();
        _playerColliders = System.Array.FindAll(_player.GetComponentsInChildren<Collider>(true),
            collider => !collider.isTrigger && collider.attachedRigidbody == _playerBody);
        UpdatePlayerCollision();
    }

    private void FixedUpdate() => UpdatePlayerCollision();

    private void UpdatePlayerCollision()
    {
        if (_player == null || _playerBody == null || !_player.gameObject.activeInHierarchy) return;
        // 수동 하강의 최소 시간/겹침 해소는 PlayerController가 끝까지 관리한다.
        if (_player.IsDroppingThrough(this))
        {
            System.Array.Clear(_landingPermitted, 0, _landingPermitted.Length);
            return;
        }

        float feetY = float.PositiveInfinity;
        float headY = float.NegativeInfinity;
        Bounds playerBounds = default;
        foreach (Collider bodyCollider in _playerColliders)
        {
            if (bodyCollider != null && bodyCollider.enabled && bodyCollider.gameObject.activeInHierarchy)
            {
                if (float.IsPositiveInfinity(feetY)) playerBounds = bodyCollider.bounds;
                else playerBounds.Encapsulate(bodyCollider.bounds);
                feetY = Mathf.Min(feetY, bodyCollider.bounds.min.y);
                headY = Mathf.Max(headY, bodyCollider.bounds.max.y);
            }
        }
        if (float.IsPositiveInfinity(feetY)) return;

        Vector3 velocity = _playerBody.linearVelocity;
        float nextX = velocity.x * Time.fixedDeltaTime;
        for (int i = 0; i < _colliders.Length; i++)
        {
            Collider platformCollider = _colliders[i];
            if (platformCollider == null || !platformCollider.enabled || !platformCollider.gameObject.activeInHierarchy) continue;
            Bounds surface = platformCollider.bounds;
            bool crossesFootprint = playerBounds.max.x + Mathf.Max(0f, nextX) >= surface.min.x &&
                playerBounds.min.x + Mathf.Min(0f, nextX) <= surface.max.x &&
                playerBounds.max.z >= surface.min.z && playerBounds.min.z <= surface.max.z;
            if (velocity.y > 0.01f || headY < surface.min.y - SurfaceTolerance || !crossesFootprint)
                _landingPermitted[i] = false;
            else if (feetY >= surface.max.y - SurfaceTolerance)
                _landingPermitted[i] = true;

            // 낙하 접촉 직전 한 프레임의 침투로 충돌을 다시 끄면 바닥을 뚫는다.
            // 위에서 내려온 몸은 완전히 아래로 빠져나갈 때까지 착지를 유지한다.
            bool ignore = !_landingPermitted[i];
            foreach (Collider bodyCollider in _playerColliders)
            {
                if (bodyCollider == null || !bodyCollider.enabled || !bodyCollider.gameObject.activeInHierarchy) continue;
                if (Physics.GetIgnoreCollision(bodyCollider, platformCollider) != ignore)
                    Physics.IgnoreCollision(bodyCollider, platformCollider, ignore);
            }
        }
    }

    private void OnDisable()
    {
        if (_landingPermitted != null) System.Array.Clear(_landingPermitted, 0, _landingPermitted.Length);
        if (_colliders == null || _playerColliders == null) return;
        foreach (Collider platformCollider in _colliders)
        {
            if (platformCollider == null) continue;
            foreach (Collider bodyCollider in _playerColliders)
            {
                if (bodyCollider != null) Physics.IgnoreCollision(bodyCollider, platformCollider, false);
            }
        }
    }
}
