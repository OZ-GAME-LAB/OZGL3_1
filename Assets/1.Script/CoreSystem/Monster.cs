using System;
using UnityEngine;
using OZ.UI.Contracts;

public class Monster : MonoBehaviour, IDamageable
{
    [SerializeField, Min(0f)] private float moveSpeed = 1f;
    public bool IsDead { get; private set; }
    public Transform Target { get; private set; }
    public event Action<Monster> Died;


    public void Die()
    {
        if (IsDead) return;
        IsDead = true;
        Died?.Invoke(this);
    }

    [Header("[테스트] 체력")]
    [SerializeField, Min(1f)] private float maxHP = 30f;
    [SerializeField] private Vector3 damageNumberOffset = new Vector3(0f, 2f, 0f);

    [Header("[테스트] 공격")]
    [SerializeField, Min(0f)] private float attackDamage = 10f;

    private float hp;

    public string DataId { get; private set; }
    public string MonsterName { get; private set; }
    public MonsterType MonsterType { get; private set; }
    public float MaxHP => maxHP;
    public float CurrentHP => hp;
    public float MeleeDamage => attackDamage;
    public float MoveSpeed => Mathf.Max(0f, moveSpeed);
    public float SkillDamage { get; private set; }

    public void SetTarget(Transform target)
    {
        Target = target;
    }

    protected virtual void OnDisable()
    {
        Target = null;
    }

    // 풀에서 비활성 상태로 꺼낸 뒤, 활성화 전에 호출합니다.
    public virtual void InitializeStats(BaseMonsterData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        float skillDamage = data is MonsterData normal ? normal.SkillDamage : 0f;
        if (!(data.HP > 0f) || float.IsInfinity(data.HP) ||
            !(data.MeleeDamage >= 0f) || float.IsInfinity(data.MeleeDamage) ||
            !(data.MoveSpeed >= 0f) || float.IsInfinity(data.MoveSpeed) ||
            !(skillDamage >= 0f) || float.IsInfinity(skillDamage))
            throw new ArgumentException($"몬스터 '{data.Id}'의 HP, 데미지, 이동속도를 확인하세요.");

        DataId = data.Id;
        MonsterName = data.Name;
        MonsterType = data.MonsterType;
        maxHP = data.HP;
        attackDamage = data.MeleeDamage;
        moveSpeed = data.MoveSpeed;
        SkillDamage = skillDamage;
        hp = maxHP;
        IsDead = false;
    }

    protected virtual void OnEnable()
    {
        hp = maxHP;
        IsDead = false;
    }

    public void TakeDamage(float damage)
    {
        if (hp <= 0f || damage <= 0f ||IsDead)
        {
            return;
        }

        hp -= damage;
        bool killed = hp <= 0f;

        GameUI.Damage.Show(transform.position + damageNumberOffset, damage,
            killed ? DamageKind.Finisher : DamageKind.Normal);

        if (killed)
        {
            Die();
        }
    }

    public void Attack(IDamageable target)
    {
        if (IsDead || target == null)
        {
            return;
        }

        // 무적·사망·UI 창 열림 체크는 PlayerHealth.TakeDamage 안에서 처리하므로 여기서는 부르기만 함
        target.TakeDamage(attackDamage);
    }
}
