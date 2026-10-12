using UnityEngine;

public class TestDummy : MonoBehaviour, IDamageable
{
    public void TakeDamage(float damage)
    {
        Debug.Log($"[TestDummy] {damage} 피해");
    }
}
