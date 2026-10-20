using System;
using UnityEngine;
using OZ.UI.Contracts;

public class Monster : MonoBehaviour, IDamageable
{
    [SerializeField, Min(0f)] private float moveSpeed = 1f;
    public bool IsDead { get; private set; }
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

    private void OnEnable()
    {
        hp = maxHP;
        IsDead = false;
    }

    private void Update()
    {
        if (IsDead) return;
        transform.position += transform.forward * Mathf.Max(0f, moveSpeed) * Time.deltaTime;
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
    private void OnCollisionStay(Collision collision)
    {
        TryContactAttack(collision.collider);
    }

    // 몬스터 콜라이더가 Is Trigger여도 똑같이 동작하게
    private void OnTriggerStay(Collider other)
    {
        TryContactAttack(other);
    }

    private void TryContactAttack(Collider other)
    {
        //  Monster 자신도 IDamageable -> IDamageable로 찾으면 몬스터끼리 닿을 때 서로 때림
        //  플레이어 자식(모델·무기) 콜라이더에 닿아도 본체를 찾기 위해
        PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
        if (player == null)
        {
            return;
        }

        Attack(player);
    }
}
