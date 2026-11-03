using UnityEngine;


// 스태프 평타 스태프 끝(EffectTip)에서 마력탄을 쏘는 단발 원거리 공격
// 수치는 기획이 아직 없어서 전부 임시값 (인스펙터에서 조정)
public class StaffAttack : MonoBehaviour, IComboAttack, IAttackStatReceiver
{
    [Header("Motion (임시값 - 플레이하면서 조정)")]
    [SerializeField] private AnimationClip _clip;            
    // 클립(0.9초)보다 짧게 잡음 -> 드라이버가 클립을 1.5배속으로 재생
    // 그대로 쓰면 주먹(0.3초)보다 3배 느려서 조작감이 무거워짐 (검 콤보와 같은 이유)
    [SerializeField] private float _baseDuration = 0.6f;     // 민첩 0일 때 공격 시간
    [SerializeField, Range(0f, 1f)]
    private float _contactNormalized = 0.48f;                // 탄 발사 시점. 0.9초 = 0.48

    [Header("Bolt")]
    [SerializeField] private SkillProjectile _boltPrefab;    // 마력탄과 같은 투사체 스크립트 재사용 (판정은 투사체가 함)
    [SerializeField] private Transform _tip;                 // 스태프 끝 EffectTip. 비우면 아래 기본 위치에서 발사
    [SerializeField] private float _fallbackHeight = 0.8f;   // _tip이 없을 때 발밑 기준 발사 높이
    [SerializeField] private float _fallbackForward = 0.5f;  // _tip이 없을 때 앞쪽 거리
    [SerializeField] private bool _horizontalOnly = false;   // true: 좌우로만 / false: 커서 각도 그대로 (마력탄과 같은 옵션)
    [SerializeField] private float _damageCoefficient = 1f;  // 피해 = 마법 공격력 x 계수

    [Header("Base")]
    [SerializeField] private float _attackPower = 20f;       // PlayerStats가 마력에 맞춰 덮어씀

    private float _speedBonus; // 0.03 = +3%
    private PlayerBuffs _buffs; // 없으면 버프 없이 동작

    // 스태프는 마법 -> 마력을 받음 (힘을 올려도 스태프는 안 세짐)
    public SkillDamageType DamageType { get { return SkillDamageType.Magic; } }

    // 단발이라 항상 1타 -> 애니메이터 ComboIndex도 항상 0
    public int CurrentStep { get { return 0; } }
    public float ContactNormalized { get { return _contactNormalized; } }
    public AnimationClip CurrentClip { get { return _clip; } }

    // 민첩은 "기본 공격 속도"라 스태프 평타에도 똑같이 적용
    public float Duration { get { return _baseDuration / (1f + _speedBonus); } }

    private void Awake()
    {
        _buffs = GetComponent<PlayerBuffs>();

        if (_boltPrefab == null)
        {
            Debug.LogError("[StaffAttack:Awake] 투사체 프리팹(_boltPrefab)이 연결되지 않았습니다.");
        }
    }

    // 단발 공격이라 되돌릴 타가 없음
    public void BeginCombo() { }

    // 다음 타가 없음 -> 공격 중에 다시 눌러도 한 발 쏘고 끝 (연타하면 새 공격으로 다시 시작)
    public bool TryAdvance()
    {
        return false;
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
        if (_boltPrefab == null)
        {
            return;
        }

        Vector3 origin = GetSpawnOrigin(player);

        Vector3 dir = _horizontalOnly
            ? new Vector3(player.FacingX, 0f, 0f)
            : SkillUtil.GetAimDirection(origin, aimPoint, player.FacingX);

        // 버프는 발사 순간 값으로 고정 -> 날아가는 도중 버프가 끝나도 피해가 바뀌지 않게
        float buff = _buffs != null ? _buffs.AttackMultiplier : 1f;
        float damage = Mathf.Round(_attackPower * _damageCoefficient * buff);

        SkillProjectile bolt = SkillSpawner.Spawn(_boltPrefab, origin, Quaternion.LookRotation(dir));
        if (bolt != null)
        {
            bolt.Launch(dir, damage);
        }
    }

    private Vector3 GetSpawnOrigin(PlayerController player)
    {
        Vector3 playerPos = player.transform.position;

        if (_tip != null)
        {
            // 스태프 끝은 애니메이션 때문에 앞뒤(Z)로 살짝 흔들림 -> 전투 평면(플레이어 Z)에 맞춰야 적과 같은 줄에서 날아감
            Vector3 tipPos = _tip.position;
            tipPos.z = playerPos.z;
            return tipPos;
        }

        return playerPos + Vector3.up * _fallbackHeight + Vector3.right * player.FacingX * _fallbackForward;
    }

    // 씬에서 발사 위치 확인용
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 pos = _tip != null
            ? _tip.position
            : transform.position + Vector3.up * _fallbackHeight + transform.forward * _fallbackForward;
        Gizmos.DrawWireSphere(pos, 0.1f);
    }
}
