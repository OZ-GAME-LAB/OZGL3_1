using System;
using UnityEngine;
using OZ.UI.Contracts;



[RequireComponent(typeof(PlayerController))]
public class PlayerHealth : MonoBehaviour, IHealthSource, IDamageable
{
    [SerializeField] private float _baseMaxHP = 100f;
    [SerializeField] private float _hitInvincibleTime = 1.0f; // 연속 피격 방지
    [SerializeField] private float _respawnInvincibleTime = 0.5f; // 재시작 직후 잠깐 보호

    private PlayerController _player;
    private PlayerBuffs _buffs; // 없으면 방어 버프 없이 동작
    private float _hp;
    private float _bonusMaxHP; // 체력 스탯으로 늘어난 최대 HP (PlayerStats가 설정)

    public float HP { get { return _hp; } }
    public float MaxHP { get { return _baseMaxHP + _bonusMaxHP; } }
    public bool IsDead { get { return _hp <= 0f; } }

    public event Action<HealthChange> HealthChanged;

    private void Awake()
    {
        _player = GetComponent<PlayerController>();
        _buffs = GetComponent<PlayerBuffs>();
        _hp = MaxHP;
    }

    private void OnEnable()
    {
        GameUI.Bind(this);
    }

    private void OnDisable()
    {
        GameUI.Unbind(this);
    }

    public void TakeDamage(float damage)
    {
        //무적 체크를 여기 한곳에서 → 대시 무적과 피격 후 무적이 모든 공격에 똑같이 적용됨.
        if (IsDead || _player.IsInvincible || damage <= 0f)
        {
            return;
        }

        // UI 창(일시정지, 스킬 창 등)이 열려 있을 때는 피격 무시 (Bridge에 있던 판정을 옮겨옴)
        if (UIState.IsGameplayInputBlocked)
        {
            return;
        }

        // 방어 강화: 받는 피해 x 0.75
        if (_buffs != null)
        {
            damage = Mathf.Round(damage * _buffs.DamageTakenMultiplier);
        }

        SetHP(_hp - damage);

        if (IsDead)
        {
            _player.Die();
            return;
        }

        //맞은 직후 잠깐 무적 → 몬스터 여러 마리에게 같은 프레임에 연속으로 맞아 죽는것을 방지
        _player.SetInvincible(_hitInvincibleTime);
        _player.OnHit();
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f)
        {
            return;
        }

        SetHP(_hp + amount);
    }

    // 스테이지 재시작 시 코어 쪽에서 호출 (플레이어를 SetActive(true) 한 다음에 호출)
    public void ResetHealth()
    {
        _hp = MaxHP;
        HealthChanged?.Invoke(new HealthChange(0f, _hp, MaxHP));
        _player.Revive();
        _player.SetInvincible(_respawnInvincibleTime); // Revive가 무적을 초기화하므로 그 뒤에 설정
    }
    private void SetHP(float value)
    {
        float prev = _hp;
        _hp = Mathf.Clamp(value, 0f, MaxHP);
        HealthChanged?.Invoke(new HealthChange(prev, _hp, MaxHP));
    }

    // PlayerStats에서 호출 (체력 스탯 변경 시). bonus = 늘어난 최대 HP 총량 (예: 2회 구매 → 24)
    // 기획: 최대 HP가 늘면 현재 HP에는 늘어난 만큼만 더한다 (가득 회복 X)
    public void SetBonusMaxHP(float bonus)
    {
        float prevMaxHP = MaxHP;
        _bonusMaxHP = Mathf.Max(0f, bonus);
        float delta = MaxHP - prevMaxHP;

        // 죽은 상태에서 HP가 더해지면 살아난 것처럼 보이므로 최대치만 갱신
        if (IsDead)
        {
            SetHP(_hp);
            return;
        }

        SetHP(_hp + Mathf.Max(0f, delta));
    }
}
