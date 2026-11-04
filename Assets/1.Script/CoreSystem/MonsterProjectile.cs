using UnityEngine;

public class MonsterProjectile : MonoBehaviour
{
    [SerializeField, Min(0f)] private float speed = 8f;
    [SerializeField, Min(0.01f)] private float lifetime = 3f;
    [SerializeField, Min(0.01f)] private float hitRadius = 0.15f;
    private readonly Collider[] overlaps = new Collider[8];
    private Vector3 direction;
    private float damage;
    private float remainingTime;
    private int playerMask;
    private bool launched;

    public void Launch(Vector3 launchDirection, float attackDamage)
    {
        if (launchDirection.sqrMagnitude < 0.0001f)
            throw new System.ArgumentException("투사체의 발사 방향이 필요합니다.", nameof(launchDirection));
        direction = launchDirection.normalized;
        damage = Mathf.Max(0f, attackDamage);
        remainingTime = Mathf.Max(0.01f, lifetime);
        playerMask = LayerMask.GetMask("Player");
        launched = true;
    }

    private void Update()
    {
        if (!launched) return;
        // 첫 생성 위치에 이미 겹친 플레이어도 한 번만 처리합니다.
        int count = Physics.OverlapSphereNonAlloc(transform.position, hitRadius, overlaps,
            playerMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
            if (TryHit(overlaps[i])) return;

        float duration = Mathf.Min(Time.deltaTime, remainingTime);
        float distance = Mathf.Max(0f, speed) * duration;
        // 빠른 투사체가 한 프레임 사이에 플레이어를 통과하지 않도록 이동 구간을 검사합니다.
        if (distance > 0f && Physics.SphereCast(transform.position, hitRadius, direction, out var hit,
            distance, playerMask, QueryTriggerInteraction.Collide) && TryHit(hit.collider)) return;

        transform.position += direction * distance;
        remainingTime -= Time.deltaTime;
        if (remainingTime <= 0f) Finish();
    }

    private bool TryHit(Collider other)
    {
        PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
        if (player == null || !player.isActiveAndEnabled || player.IsDead) return false;
        Finish();
        player.TakeDamage(damage);
        return true;
    }

    private void Finish()
    {
        launched = false;
        Destroy(gameObject);
    }

    private void OnDisable()
    {
        launched = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, hitRadius);
    }
}
