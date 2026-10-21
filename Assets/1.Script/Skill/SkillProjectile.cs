using System.Collections.Generic;
using UnityEngine;


// 직선 투사체 (검기, 마력탄)
public class SkillProjectile : MonoBehaviour
{
    [Header("Move")]
    [SerializeField, Min(0f)] private float _speed = 15f;
    [SerializeField, Min(0f)] private float _maxDistance = 5f;

    [Header("Hit")]
    [SerializeField, Min(0.01f)] private float _hitRadius = 0.5f;
    [SerializeField] private LayerMask _enemyLayer;
    [SerializeField] private LayerMask _wallLayer;      // 벽에 닿으면 소멸 (비우면 무시)
    [SerializeField] private bool _pierce = true;       // true: 관통 (검기) / false: 첫 적에서 소멸

    [Header("Explosion (0이면 폭발 없음)")]
    [SerializeField, Min(0f)] private float _explosionRadius = 0f; // 맞은 지점 범위 피해
    [SerializeField] private bool _explodeAtEnd = false;           // 사거리 끝/벽에서도 폭발
    [SerializeField] private SkillAutoRelease _explosionVfx;       // 폭발 연출 (선택). 없어도 동작함

    private readonly Collider[] _hits = new Collider[16];
    private readonly HashSet<IDamageable> _hitTargets = new HashSet<IDamageable>();

    private Vector3 _direction;
    private float _damage;
    private float _traveled;
    private bool _launched;

    private bool HasExplosion { get { return _explosionRadius > 0f; } }


    private void OnDisable()
    {
        _launched = false;
    }

    private void Update()
    {
        if (!_launched)
        {
            return;
        }

        float step = _speed * Time.deltaTime;
        transform.position += _direction * step;
        _traveled += step;
        if (CheckEnemyHit())
        {
            return;
        }
        bool hitWall = _wallLayer.value != 0 && Physics.CheckSphere(transform.position, _hitRadius * 0.5f, _wallLayer);
        if (hitWall || _traveled >= _maxDistance)
        {
            End(_explodeAtEnd);
        }
    }

    // 생성 직후 스킬에서 호출
    public void Launch(Vector3 direction, float damage)
    {
        _direction = direction.normalized;
        _damage = damage;
        _traveled = 0f;
        _hitTargets.Clear();
        _launched = true;
    }

    // 소멸했으면 true
    private bool CheckEnemyHit()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, _hitRadius, _hits, _enemyLayer);
        for (int i = 0; i < count; i++)
        {
            IDamageable target = _hits[i].GetComponentInParent<IDamageable>();
            if (target == null || target is PlayerHealth || _hitTargets.Contains(target))
            {
                continue;
            }
            if (HasExplosion)
            {
                End(true);
                return true;
            }
            _hitTargets.Add(target);
            target.TakeDamage(_damage);
            if (!_pierce)
            {
                End(false);
                return true;
            }
        }
        return false;
    }

    private void End(bool explode)
    {
        _launched = false;
        if (explode && HasExplosion)
        {
            SkillUtil.DamageSphere(transform.position, _explosionRadius, _enemyLayer, _damage);
            if (_explosionVfx != null)
            {
                SkillSpawner.Spawn(_explosionVfx, transform.position, Quaternion.identity);
            }
        }
        SkillSpawner.Release(gameObject);
    }

    //씬에서 확인하는 용도
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _hitRadius);
        if (HasExplosion)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(transform.position, _explosionRadius);
        }
    }
}