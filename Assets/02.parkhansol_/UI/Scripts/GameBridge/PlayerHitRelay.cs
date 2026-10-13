using UnityEngine;

/// <summary>PlayerHealthBridge가 런타임에 플레이어에 붙이는 충돌 전달용. 직접 붙일 필요 없음.</summary>
[AddComponentMenu("")]
public class PlayerHitRelay : MonoBehaviour
{
    internal PlayerHealthBridge owner;

    void OnEnable() { if (owner != null) owner.OnPlayerSpawned(); }

    void OnCollisionEnter(Collision c) => Forward(c);
    void OnCollisionStay(Collision c) => Forward(c);
    void OnTriggerEnter(Collider other) { if (owner != null) owner.OnMonsterContact(other, transform.position); }
    void OnTriggerStay(Collider other) { if (owner != null) owner.OnMonsterContact(other, transform.position); }

    void Forward(Collision c)
    {
        if (owner == null) return;
        Vector3 p = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
        owner.OnMonsterContact(c.collider, p);
    }
}
