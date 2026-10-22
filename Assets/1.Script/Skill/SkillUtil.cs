using System.Collections.Generic;
using UnityEngine;


// 스킬 공용 기능 모음: 범위 피해(대상당 1회), 조준 방향, 바닥 찾기, 피해 분할
public static class SkillUtil
{
    private static readonly Collider[] _hits = new Collider[32];
    private static readonly List<IDamageable> _damaged = new List<IDamageable>();

    // 박스 범위 안의 적에게 피해. 반환값 = 맞은 대상 수
    public static int DamageBox(Vector3 center, Vector3 size, LayerMask enemyLayer, float damage)
    {
        int count = Physics.OverlapBoxNonAlloc(center, size * 0.5f, _hits, Quaternion.identity, enemyLayer);
        return ApplyDamage(count, damage);
    }

    // 구 범위 안의 적에게 피해. 반환값 = 맞은 대상 수
    public static int DamageSphere(Vector3 center, float radius, LayerMask enemyLayer, float damage)
    {
        int count = Physics.OverlapSphereNonAlloc(center, radius, _hits, enemyLayer);
        return ApplyDamage(count, damage);
    }

    private static int ApplyDamage(int count, float damage)
    {
        if (damage <= 0f)
        {
            return 0;
        }
        _damaged.Clear();

        for (int i = 0; i < count; i++)
        {
            IDamageable target = _hits[i].GetComponentInParent<IDamageable>();
            if (target == null || target is PlayerHealth || _damaged.Contains(target))
            {
                continue;
            }
            _damaged.Add(target);
            target.TakeDamage(damage);
        }
        return _damaged.Count;
    }

    // 플레이어 -> 커서 방향 (전투 평면 기준)
    public static Vector3 GetAimDirection(Vector3 origin, Vector3 aimPoint, float facingX)
    {
        Vector3 dir = aimPoint - origin;
        dir.z = 0f;
        if (dir.sqrMagnitude < 0.01f)
        {
            return new Vector3(facingX, 0f, 0f);
        }
        return dir.normalized;
    }

    // 지점 바로 아래 바닥 찾기 (장판·폭발을 바닥에 붙일 때). 못 찾으면 false
    public static bool TrySnapToGround(Vector3 point, LayerMask groundLayer, float rayHeight, float rayDistance, out Vector3 groundPoint)
    {
        Vector3 origin = point + Vector3.up * rayHeight;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayHeight + rayDistance, groundLayer))
        {
            groundPoint = hit.point;
            return true;
        }
        groundPoint = point;
        return false;
    }

    // 연타·장판처럼 총피해를 나눠 줄 때 사용
    public static float SplitDamage(float totalDamage, int count)
    {
        if (count <= 0)
        {
            return totalDamage;
        }
        return Mathf.Round(totalDamage / count);
    }
}
