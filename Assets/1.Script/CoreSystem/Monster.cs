using System;
using UnityEngine;
using OZ.UI.Contracts;

public class Monster : MonoBehaviour, IDamageable
{
    [SerializeField, Min(0f)] private float moveSpeed = 1f;
    public bool IsDead { get; private set; }
    public event Action<Monster> Died;

    private void OnEnable() => IsDead = false;

    public void Die()
    {
        if (IsDead) return;
        IsDead = true;
        Died?.Invoke(this);
    }

    [Header("[테스트] 체력")]
    [SerializeField, Min(1f)] private float maxHP = 30f;
    [SerializeField] private Vector3 damageNumberOffset = new Vector3(0f, 2f, 0f);

    private float hp;

    private void OnEnable()
    {
        hp = maxHP;
    }

    private void Update()
    {
        if (IsDead) return;
        transform.position += transform.forward * Mathf.Max(0f, moveSpeed) * Time.deltaTime;
    }

    public void TakeDamage(float damage)
    {
        if (hp <= 0f || damage <= 0f)
        {
            return;
        }

        hp -= damage;
        bool killed = hp <= 0f;

        GameUI.Damage.Show(transform.position + damageNumberOffset, damage,
            killed ? DamageKind.Finisher : DamageKind.Normal);

        if (killed)
        {
            Destroy(gameObject);
        }
    }
}
