using System.Collections.Generic;
using UnityEngine;

public class SwordBasicAttack : MonoBehaviour, IPlayerAttack
{
    [SerializeField] private float _duration = 0.35f;
    [SerializeField] private float _damage = 10f;
    [SerializeField] private Vector3 _hitBoxSize = new Vector3(1.6f, 1.2f, 1f);
    [SerializeField] private float _hitBoxForward = 0.9f; // 플레이어 중심에서 앞쪽 거리
    [SerializeField] private float _hitBoxHeight = 1f;    // 발밑 기준 높이
    [SerializeField] private LayerMask _enemyLayer;

    private readonly Collider[] _hits = new Collider[16];
    private readonly List<IDamageable> _damaged = new List<IDamageable>();

    public float Duration { get { return _duration; } }
 
    public void Execute(PlayerController player, Vector3 aimPoint)
    {
        Vector3 center = GetHitBoxCenter(player.FacingX);
        int count = Physics.OverlapBoxNonAlloc(center, _hitBoxSize * 0.5f, _hits, Quaternion.identity, _enemyLayer);
 
        _damaged.Clear();
        for (int i = 0; i < count; i++)
        {
            IDamageable target = _hits[i].GetComponentInParent<IDamageable>();

            // 콜라이더가 여러 개인 적이 두 번 맞지 않게
            if (target == null || _damaged.Contains(target))
            {
                continue;
            }

            _damaged.Add(target);
            target.TakeDamage(_damage);
        }
    }

    private Vector3 GetHitBoxCenter(float facingX)
    {
        return transform.position + Vector3.up * _hitBoxHeight + Vector3.right * facingX * _hitBoxForward;
    }

    //씬에서 판정 범위 확인용
    private void OnDrawGizmosSelected()
    {
        float facingX = transform.forward.x >= 0f ? 1f : -1f;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(GetHitBoxCenter(facingX), _hitBoxSize);
    }
}
