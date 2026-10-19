using System.Collections;
using UnityEngine;


// 연속 참격 (물리, 계수 3.75, 쿨 12초): 앞쪽 좁은 구역에 여러 번 연타
// [주의] 마지막 타 시각(_hitInterval x (타수-1))이 SO의 CastDuration보다 짧아야 시전 중에 다 들어감
//        기본값 0.08 x 4 = 0.32초 < CastDuration 0.4초
// [주의] 이 컴포넌트가 붙은 오브젝트를 꺼두면 코루틴이 돌지 않음
public class FlurrySlashSkill : PlayerSkillBase
{
    [Header("Hit")]
    [SerializeField, Min(1)] private int _hitCount = 5; // 총 타수
    [SerializeField, Min(0.01f)] private float _hitInterval = 0.08f;// 타격 사이 간격 

    [Header("Hit Box")]
    [SerializeField] private Vector3 _hitBoxSize = new Vector3(1.8f, 1.6f, 2f);
    [SerializeField] private float _hitBoxForward = 1f;
    [SerializeField] private float _hitBoxHeight = 1f;
    [SerializeField] private LayerMask _enemyLayer;
    [SerializeField] private SkillAutoRelease _slashVfx; // 매 타 연출 (선택)

    private Coroutine _flurryCoroutine;

    private void OnDisable()
    {
        _flurryCoroutine = null;
    }

    public override void Execute(PlayerController player, Vector3 aimPoint, float damage)
    {
        // 이전 연타가 남아 있으면 멈추고 새로 시작. 코루틴이 겹쳐 피해가 2배로 들어가는 것 방지
        if (_flurryCoroutine != null)
        {
            StopCoroutine(_flurryCoroutine);
        }
        _flurryCoroutine = StartCoroutine(FlurryRoutine(player, damage));
    }

    private IEnumerator FlurryRoutine(PlayerController player, float totalDamage)
    {
        float damagePerHit = SkillUtil.SplitDamage(totalDamage, _hitCount);
        // 시작할 때 방향을 저장: 연타 도중 플레이어가 돌아봐도 처음 방향으로 계속 벰
        float facingX = player.FacingX;

        //  WaitForSeconds를 한 번만 만들어 재사용
        WaitForSeconds wait = new WaitForSeconds(_hitInterval);
        for (int i = 0; i < _hitCount; i++)
        {
            // 피격·사망으로 스킬 상태가 끊기면 남은 타 취소
            if (player.CurrentStateType != PlayerState.Skill)
            {
                break;
            }
            // 매 타마다 위치를 다시 계산: 공중에서 쓰면 떨어지면서 플레이어 위치가 바뀌기 때문
            Vector3 center = GetHitBoxCenter(player.transform.position, facingX);
            SkillUtil.DamageBox(center, _hitBoxSize, _enemyLayer, damagePerHit);
            if (_slashVfx != null)
            {
                SkillSpawner.Spawn(_slashVfx, center, Quaternion.LookRotation(new Vector3(facingX, 0f, 0f)));
            }
            //  마지막 타 뒤에는 기다릴 필요가 없음
            if (i < _hitCount - 1)
            {
                yield return wait;
            }
        }
        _flurryCoroutine = null;
    }

    private Vector3 GetHitBoxCenter(Vector3 origin, float facingX)
    {
        return origin + Vector3.up * _hitBoxHeight + Vector3.right * facingX * _hitBoxForward;
    }
    private void OnDrawGizmosSelected()
    {
        float facingX = transform.forward.x >= 0f ? 1f : -1f;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(GetHitBoxCenter(transform.position, facingX), _hitBoxSize);
    }

}
