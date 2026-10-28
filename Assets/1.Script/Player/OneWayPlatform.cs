using UnityEngine;

// 아래로 내려갈 수 있는 발판 (S + Space).
// 바닥 판정(Ground 레이어)은 일반 바닥과 똑같이 쓰고, 이 컴포넌트가 붙은 것만 "통과 가능"으로 구분한다.
public class OneWayPlatform : MonoBehaviour
{
    private Collider[] _colliders;

    public Collider[] Colliders { get { return _colliders; } }

    private void Awake()
    {
        // 자식에 콜라이더를 나눠 붙인 발판도 한 번에 통과되게 전부 모아 둠
        _colliders = GetComponentsInChildren<Collider>();

        if (_colliders.Length == 0)
        {
            Debug.LogError($"[OneWayPlatform:Awake] {name}에 콜라이더가 없습니다.");
        }
    }
}
