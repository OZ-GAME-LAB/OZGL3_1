using UnityEngine;

public class Monster : MonoBehaviour
{
    [SerializeField, Min(0f)] private float moveSpeed = 1f;

    private void Update()
    {
        // 로컬 +Z 방향으로 이동하며 프레임 속도와 무관하게 속도를 유지합니다.
        transform.position += transform.forward * Mathf.Max(0f, moveSpeed) * Time.deltaTime;
    }
}
