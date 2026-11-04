using System;
using System.Collections.Generic;
using UnityEngine;


// 검 평타 3타 콤보: 내려베기 -> 가로베기 -> 찌르기.
// 기획 수치가 아직 없어서 피해 계수, 판정 크기, 시간은 전부 임시값 (인스펙터에서 조정)
// 판정 방식은 PunchAttack과 동일 (OverlapBox) -> 피격 쪽(IDamageable)은 아무것도 안 바뀜
public class SwordComboAttack : MonoBehaviour, IComboAttack, IAttackStatReceiver
{
    [Serializable]
    public struct ComboStep
    {
        public string name;                // 인스펙터에서 알아보기용
        public AnimationClip clip;         // ChibiMotion_v007/Clips 쪽 클립을 넣을 것 (Combat/Clips 아님)
        public float baseDuration;         // 민첩 0일 때 이 타의 공격 시간
        [Range(0f, 1f)]
        public float contactNormalized;    // 이 타의 판정 시점 (baseDuration 대비)
        public float damageCoefficient;    // 피해 = 물리 공격력 x 계수
        public Vector3 hitBoxSize;
        public float hitBoxForward;        // 플레이어 중심에서 앞쪽 거리
    }

    [Header("Combo (임시값 - 플레이하면서 조정)")]
    // 클립 길이(0.7 / 0.6 / 0.7초)보다 짧게 잡음 -> 드라이버가 클립을 조금 빠르게 재생
    // 클립 그대로 쓰면 주먹(0.3초)보다 2배 이상 느려서 조작감이 무거워짐
    [SerializeField]
    private ComboStep[] _steps =
    {
        new ComboStep { name = "내려베기", baseDuration = 0.45f, contactNormalized = 0.5f,  damageCoefficient = 1.0f, hitBoxSize = new Vector3(1.4f, 1.4f, 1f), hitBoxForward = 0.8f },
        new ComboStep { name = "가로베기", baseDuration = 0.40f, contactNormalized = 0.45f, damageCoefficient = 1.0f, hitBoxSize = new Vector3(1.6f, 1.0f, 1f), hitBoxForward = 0.8f },
        new ComboStep { name = "찌르기",   baseDuration = 0.50f, contactNormalized = 0.5f,  damageCoefficient = 1.3f, hitBoxSize = new Vector3(1.8f, 0.8f, 1f), hitBoxForward = 1.0f },
    };

    [Header("Hit Box")]
    [SerializeField] private float _hitBoxHeight = 0.65f; // 발밑 기준 높이 (PunchAttack과 같게)
    [SerializeField] private LayerMask _enemyLayer;

    [Header("Base")]
    [SerializeField] private float _attackPower = 20f; // PlayerStats가 힘에 맞춰 덮어씀

    private readonly Collider[] _hits = new Collider[16];
    private readonly List<IDamageable> _damaged = new List<IDamageable>();

    private float _speedBonus; // 0.03 = +3%
    private int _currentStep;
    private PlayerBuffs _buffs; // 없으면 버프 없이 동작

    private ComboStep Step { get { return _steps[_currentStep]; } }

    // 검은 물리 -> 힘을 받음
    public SkillDamageType DamageType { get { return SkillDamageType.Physical; } }

    public int CurrentStep { get { return _currentStep; } }
    public float ContactNormalized { get { return Step.contactNormalized; } }
    public AnimationClip CurrentClip { get { return Step.clip; } }

    // 민첩은 "기본 공격 속도"라 검 평타에도 똑같이 적용
    public float Duration { get { return Step.baseDuration / (1f + _speedBonus); } }

    private void Awake()
    {
        _buffs = GetComponent<PlayerBuffs>();

        if (_steps == null || _steps.Length == 0)
        {
            Debug.LogError("[SwordComboAttack:Awake] 콤보 단계가 비어 있습니다.");
        }
    }

    public void BeginCombo()
    {
        _currentStep = 0;
    }

    public bool TryAdvance()
    {
        if (_currentStep + 1 >= _steps.Length)
        {
            return false;
        }

        _currentStep++;
        return true;
    }

    public void SetAttackPower(float attackPower)
    {
        _attackPower = Mathf.Max(0f, attackPower);
    }

    public void SetSpeedBonus(float bonus)
    {
        _speedBonus = Mathf.Max(0f, bonus);
    }

    public void Execute(PlayerController player, Vector3 aimPoint)
    {
        ComboStep step = Step;
        Vector3 center = GetHitBoxCenter(player.FacingX, step);
        int count = Physics.OverlapBoxNonAlloc(center, step.hitBoxSize * 0.5f, _hits, Quaternion.identity, _enemyLayer);

        // 루프 밖에서 한 번만 계산: 여러 적을 맞혀도 같은 피해
        float buff = _buffs != null ? _buffs.AttackMultiplier : 1f;
        float damage = Mathf.Round(_attackPower * step.damageCoefficient * buff);
        _damaged.Clear();

        for (int i = 0; i < count; i++)
        {
            IDamageable target = _hits[i].GetComponentInParent<IDamageable>();

            // 콜라이더가 여러 개인 적이 한 타에 두 번 맞지 않게
            if (target == null || _damaged.Contains(target))
            {
                continue;
            }

            _damaged.Add(target);
            target.TakeDamage(damage);
        }
    }

    private Vector3 GetHitBoxCenter(float facingX, ComboStep step)
    {
        return transform.position + Vector3.up * _hitBoxHeight + Vector3.right * facingX * step.hitBoxForward;
    }

    // 씬에서 타별 판정 범위 확인용 (1타 빨강 / 2타 주황 / 3타 노랑)
    private void OnDrawGizmosSelected()
    {
        if (_steps == null)
        {
            return;
        }

        Color[] colors = { Color.red, new Color(1f, 0.5f, 0f), Color.yellow };
        float facingX = transform.forward.x >= 0f ? 1f : -1f;

        for (int i = 0; i < _steps.Length; i++)
        {
            Gizmos.color = colors[i % colors.Length];
            Gizmos.DrawWireCube(GetHitBoxCenter(facingX, _steps[i]), _steps[i].hitBoxSize);
        }
    }
}
