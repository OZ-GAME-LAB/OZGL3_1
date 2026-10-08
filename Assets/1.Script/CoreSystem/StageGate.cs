using UnityEngine;

// 몬스터 종류, 수량, 간격과 실제 소환 동작은 각 게이트에서 구현합니다.
public abstract class StageGate : MonoBehaviour
{
    public abstract void StartSpawning();
    public abstract void StopSpawning();
}
