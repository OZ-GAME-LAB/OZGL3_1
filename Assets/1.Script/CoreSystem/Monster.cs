using System;
using UnityEngine;

public class Monster : MonoBehaviour
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

    private void Update()
    {
        if (IsDead) return;
        transform.position += transform.forward * Mathf.Max(0f, moveSpeed) * Time.deltaTime;
    }
}
