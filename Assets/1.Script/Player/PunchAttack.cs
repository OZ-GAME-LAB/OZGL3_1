using System.Collections.Generic;
using UnityEngine;




// 기본 공격: 주먹 (물리). 기획 v0.5 - 최초 피해 물리 20, 민첩 구매 1회당 공격 속도 +3%.
// 피해/속도 값은 나중에 PlayerStat(추가예정) 에서 추가해준다
public class PunchAttack : MonoBehaviour, IPlayerAttack
{
    [Header("Base")]
    [SerializeField] private float _baseDuration = 0.3f; // 민첩 보너스 0일 때 공격 상태 유지 시간
    [SerializeField] private float _attackPower = 20f;     // 물리 공격력 (20 + 3 x 힘 구매 횟수)
    [SerializeField] private float _damageCoefficient = 1f; // 주먹 피해 = 물리 공격력 x 계수

    [Header("Hit Box")]
    [SerializeField] private Vector3 _hitBoxSize = new Vector3(1.0f, 1.0f, 1f);
    [SerializeField] private float _hitBoxForward = 0.7f; // 플레이어 중심에서 앞쪽 거리
    [SerializeField] private float _hitBoxHeight = 1f;    // 발밑 기준 높이
    [SerializeField] private LayerMask _enemyLayer;


    private readonly Collider[] _hits = new Collider[16];
    private readonly List<IDamageable> _damaged = new List<IDamageable>();

    private float _speedBonus; // 0.03 = +3%

    public float Duration { get { return _baseDuration / (1f + _speedBonus); } }
    public float Damage { get { return Mathf.Round(_attackPower * _damageCoefficient); } }

    // PlayerStats에서 호출 (힘 구매 시)
    public void SetAttackPower(float attackPower)
    {
        _attackPower = Mathf.Max(0f, attackPower);
    }

    // PlayerStats에서 호출 (민첩 구매 시). 0.09 = +9%
    public void SetSpeedBonus(float bonus)
    {
        _speedBonus = Mathf.Max(0f, bonus);
    }

    public void Execute(PlayerController player, Vector3 aimPoint)
    {
        Vector3 center = GetHitBoxCenter(player.FacingX);
        int count = Physics.OverlapBoxNonAlloc(center, _hitBoxSize * 0.5f, _hits, Quaternion.identity, _enemyLayer);

        //루프 밖에서 한 번만 계산: 여러 적을 맞혀도 같은 피해, 계산도 1회
        float damage = Damage;
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
            target.TakeDamage(damage);
        }
    }

    private Vector3 GetHitBoxCenter(float facingX)
    {
        return transform.position + Vector3.up * _hitBoxHeight + Vector3.right * facingX * _hitBoxForward;
    }

    // 씬에서 판정 범위 확인용
    private void OnDrawGizmosSelected()
    {
        float facingX = transform.forward.x >= 0f ? 1f : -1f;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(GetHitBoxCenter(facingX), _hitBoxSize);
    }
}
