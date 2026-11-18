using System.Collections;
using UnityEngine;

public class RangedMonsterBehaviour : MonsterBehaviour
{
    [SerializeField] private Transform firePoint;
    [SerializeField, Min(0f)] private float spawnOffset = 0.6f;

    protected override float AttackDelay => 1.5f;

    protected override IEnumerator ExecuteAttack()
    {
        if (Owner.PoolManager == null || string.IsNullOrWhiteSpace(Owner.ProjectileId))
        {
            Debug.LogError("몬스터의 ProjectileId와 투사체 풀을 설정하세요.", this);
            yield break;
        }

        if (Target == null || !Target.gameObject.activeInHierarchy) yield break;
        PlayAnimation(EnemyAnimState.Attack);

        // 발사 시점의 플레이어 중심을 조준하고, 발사 후에는 방향을 바꾸지 않습니다.
        Vector3 aim = TargetCenter - BodyCenter;
        Vector3 initialDirection = aim.sqrMagnitude > 0.0001f ? aim.normalized : transform.forward;
        Vector3 position = firePoint != null ? firePoint.position : BodyCenter + initialDirection * spawnOffset;
        Vector3 offset = TargetCenter - position;
        Vector3 direction = offset.sqrMagnitude > 0.0001f ? offset.normalized : initialDirection;
        Vector3 up = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
        MonsterProjectile projectile = Owner.PoolManager.Get<MonsterProjectile>(Owner.ProjectileId,
            position, Quaternion.LookRotation(direction, up), false);
        if (projectile == null) { Debug.LogError($"투사체 풀 '{Owner.ProjectileId}'가 없습니다.", this); yield break; }
        projectile.Launch(direction, SkillDamage, Owner.PoolManager);
        projectile.gameObject.SetActive(true);
        yield break;
    }
}
