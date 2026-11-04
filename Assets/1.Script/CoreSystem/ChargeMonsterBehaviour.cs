using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChargeMonsterBehaviour : MonsterBehaviour
{
    [SerializeField, Min(0.1f)] private float chargeSpeed = 8f;
    [SerializeField, Min(0.1f)] private float chargeDistance = 5f;
    [SerializeField, Min(0f)] private float recoveryTime = 1.5f;
    [SerializeField, Min(0.01f)] private float groundSampleStep = 0.1f;
    private readonly HashSet<PlayerHealth> hitPlayers = new HashSet<PlayerHealth>();

    public bool IsCharging { get; private set; }
    protected override float AttackDelay => 1.2f;

    protected override IEnumerator ExecuteAttack()
    {
        // 부모가 선딜레이 시작 시 고정한 방향을 그대로 사용합니다.
        float direction = transform.forward.x >= 0f ? 1f : -1f;
        float distance = GetChargeDistance(direction);
        float travelled = 0f;
        hitPlayers.Clear();
        IsCharging = true;
        try
        {
            while (travelled < distance && IsCharging && isActiveAndEnabled && !Owner.IsDead)
            {
                float requestedStep = Mathf.Min(Mathf.Max(0.1f, chargeSpeed) * Time.deltaTime, distance - travelled);
                Bounds bounds = GetBodyBounds();
                Vector3 halfSize = GetCastHalfSize(bounds);
                Vector3 movement = Vector3.right * direction;
                float step = requestedStep;

                // 바닥과 달리 벽은 중간에 있어도 통과하지 않습니다.
                if (Physics.BoxCast(bounds.center, halfSize, movement, out var wall,
                    Quaternion.identity, requestedStep + TerrainPadding, GroundMask, QueryTriggerInteraction.Ignore))
                    step = Mathf.Min(step, Mathf.Max(0f, wall.distance - TerrainPadding));

                // 현재 겹친 대상과 이번 프레임에 지나갈 대상을 함께 검사합니다.
                foreach (Collider hit in Physics.OverlapBox(bounds.center, halfSize, Quaternion.identity,
                    LayerMask.GetMask("Player"), QueryTriggerInteraction.Collide))
                    TryChargeDamage(hit.GetComponentInParent<PlayerHealth>());
                if (!IsCharging || !isActiveAndEnabled || Owner.IsDead) yield break;

                if (step > 0f)
                {
                    foreach (RaycastHit hit in Physics.BoxCastAll(bounds.center, halfSize, movement,
                        Quaternion.identity, step, LayerMask.GetMask("Player"), QueryTriggerInteraction.Collide))
                        TryChargeDamage(hit.collider.GetComponentInParent<PlayerHealth>());
                    if (!IsCharging || !isActiveAndEnabled || Owner.IsDead) yield break;
                    Move(movement * step);
                    travelled += step;
                }

                if (step < requestedStep) break;
                yield return null;
            }
        }
        finally
        {
            StopMove();
            IsCharging = false;
            hitPlayers.Clear();
        }

        if (!Owner.IsDead && isActiveAndEnabled && recoveryTime > 0f)
            yield return new WaitForSeconds(recoveryTime);
    }

    protected override void HandleContactAttack(PlayerHealth player)
    {
        if (IsCharging) TryChargeDamage(player);
        else if (!IsAttacking) base.HandleContactAttack(player);
    }

    private void TryChargeDamage(PlayerHealth player)
    {
        if (!IsCharging || !isActiveAndEnabled || Owner.IsDead || player == null ||
            !player.isActiveAndEnabled || player.IsDead || !hitPlayers.Add(player)) return;

        float previousHP = player.HP;
        player.TakeDamage(SkillDamage);
        Debug.Log($"[돌진 공격 적중] 몬스터: {Owner.DataId}, SkillDamage: {SkillDamage}, " +
            $"실제 피해: {Mathf.Max(0f, previousHP - player.HP)}, 플레이어 HP: {previousHP} → {player.HP}", this);
    }

    protected override void OnAttackInterrupted()
    {
        IsCharging = false;
        hitPlayers.Clear();
        base.OnAttackInterrupted();
    }

    private float GetChargeDistance(float direction)
    {
        float maximum = Mathf.Max(0f, chargeDistance);
        // 도착점에 몸을 지탱할 바닥이 있으면 중간 구멍은 넘어갑니다.
        if (HasLandingGround(direction * maximum)) return maximum;
        if (!HasLandingGround(0f)) return 0f;

        float lastSafe = 0f;
        float sampleStep = Mathf.Max(0.01f, groundSampleStep);
        while (lastSafe < maximum)
        {
            float next = Mathf.Min(lastSafe + sampleStep, maximum);
            if (!HasLandingGround(direction * next))
            {
                // 마지막 안전 지점과 첫 빈 지점 사이에서 발판 끝을 좁혀 찾습니다.
                float unsafeDistance = next;
                for (int i = 0; i < 8; i++)
                {
                    float middle = (lastSafe + unsafeDistance) * 0.5f;
                    if (HasLandingGround(direction * middle)) lastSafe = middle;
                    else unsafeDistance = middle;
                }
                return lastSafe;
            }
            lastSafe = next;
        }
        return lastSafe;
    }

    private bool HasLandingGround(float offsetX)
    {
        Bounds bounds = GetBodyBounds();
        Vector3 origin = new Vector3(bounds.center.x + offsetX,
            bounds.min.y + GroundProbeHeight, bounds.center.z);
        float width = bounds.extents.x + TerrainPadding;
        float distance = GroundProbeHeight + GroundProbeDepth;
        return Physics.Raycast(origin, Vector3.down, distance, GroundMask, QueryTriggerInteraction.Ignore) &&
            Physics.Raycast(origin + Vector3.left * width, Vector3.down, distance, GroundMask, QueryTriggerInteraction.Ignore) &&
            Physics.Raycast(origin + Vector3.right * width, Vector3.down, distance, GroundMask, QueryTriggerInteraction.Ignore);
    }

    private static Vector3 GetCastHalfSize(Bounds bounds)
    {
        // 바닥에 닿아 있는 몸통이 벽 검사에서 바닥 자체를 잡지 않도록 조금 줄입니다.
        Vector3 size = bounds.extents - Vector3.one * 0.02f;
        return new Vector3(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y), Mathf.Max(0.01f, size.z));
    }
}
