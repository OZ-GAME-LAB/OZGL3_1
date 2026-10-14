using System;
using UnityEngine;
using OZ.UI.Contracts;



[RequireComponent(typeof(PlayerController))]
public class PlayerHealth : MonoBehaviour, IHealthSource, IDamageable
{

    [SerializeField] private float _maxHP = 100f;
    [SerializeField] private float _hitInvincibleTime = 0.5f; // 연속 피격 방지
    [SerializeField] private float _respawnInvincibleTime = 0.5f; // 재시작 직후 잠깐 보호

    private PlayerController _player;
    private float _hp;

    public float HP { get { return _hp; } }
    public float MaxHP { get { return _maxHP; } }
    public bool IsDead { get { return _hp <= 0f; } }

    public event Action<HealthChange> HealthChanged;

    private void Awake()
    {
        _player = GetComponent<PlayerController>();
        _hp = _maxHP;
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
        SetHP(_maxHP);
        _player.Revive();
        _player.SetInvincible(_respawnInvincibleTime); // Revive가 무적을 초기화하므로 그 뒤에 설정
    }
    private void SetHP(float value)
    {
        float prev = _hp;
        _hp = Mathf.Clamp(value, 0f, _maxHP);
        HealthChanged?.Invoke(new HealthChange(prev, _hp, _maxHP));
    }
}
