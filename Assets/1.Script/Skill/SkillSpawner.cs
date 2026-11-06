using System.Collections.Generic;
using UnityEngine;


// 스킬 오브젝트(투사체·장판·폭발·이펙트) 생성/회수 창구
//   Spawn   = Instantiate 대신 사용
//   Release = Destroy 대신 사용
//  스킬 프리팹은 PoolData에 없어서 처음 Spawn할 때 PreparePool로 직접 등록함 (BossGate와 같은 방식)
public static class SkillSpawner
{
    // false -> true
    // 스태프 평타 연타 시 Instantiate/Destroy 반복 -> 메모리 쓰레기가 쌓여 GC 끊김 발생
    private static readonly bool UsePool = true;

    //  처음 만들어 둘 개수 (모자라면 PoolManager.Rent가 새로 만듦)
    //  왜 -> 스태프 평타는 연타가 빨라서 1개만 만들면 바로 또 새로 만들게 됨
    private const int StartCount = 5;

    // 풀에 넣은 프리팹 목록
    // PoolManager.Rent(prefab) 는 PreparePool로 넣은 프리팹만 빌려줌
    // 매번 PreparePool을 부르지 않도록 한 번 넣은 건 기억해 둠
    private static readonly HashSet<GameObject> _pooled = new HashSet<GameObject>();

    // 넣을 때 쓴 PoolManager
    // PoolManager가 파괴되고 새로 생기면 예전 기록은 쓸모없음 -> 비워야 함
    private static PoolManager _pool;

    public static T Spawn<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component
    {
        if (prefab == null)
        {
            Debug.LogError("[SkillSpawner:Spawn] 프리팹이 연결되지 않았습니다.");
            return null;
        }

        GameObject instance = null;
        PoolManager pool = GetPool();
        if (pool != null)
        {
            // 빌리기 전에 풀에 넣기부터
            // 안 넣은 프리팹을 Rent하면 "초기화한 풀의 프리팹만 대여할 수 있습니다" 에러
            EnsurePool(pool, prefab.gameObject);
            instance = pool.Rent(prefab.gameObject, position, rotation);
        }
        // 테스트 씬에 GameManager가 없어도 스킬 테스트가 되도록
        if (instance == null)
        {
            instance = Object.Instantiate(prefab.gameObject, position, rotation);
        }
        return instance.GetComponent<T>();
    }

    public static void Release(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }
 
        PoolManager pool = GetPool();
        if (pool != null && pool.IsRented(instance))
        {
            pool.Return(instance);
            return;
        }
        Object.Destroy(instance);
    }

    private static void EnsurePool(PoolManager pool, GameObject prefab)
    {
        // PoolManager가 바뀌었으면 기록 초기화
        // 새 PoolManager에는 아무것도 없음
        if (_pool != pool)
        {
            _pooled.Clear();
            _pool = pool;
        }

        // 이미 넣었으면 건너뜀
        if (_pooled.Contains(prefab))
        {
            return;
        }

        pool.PreparePool(prefab, StartCount);
        _pooled.Add(prefab);
    }

    private static PoolManager GetPool()
    {
        if (!UsePool || GameManager.Instance == null)
        {
            return null;
        }
        return GameManager.Instance.PoolManager;
    }
}
