using UnityEngine;


// 스킬 오브젝트(투사체·장판·폭발·이펙트) 생성/회수 창구
//   Spawn   = Instantiate 대신 사용
//   Release = Destroy 대신 사용
//  PoolManager에 스킬 프리팹 등록이 추가되면 UsePool만 true로 바꾸면 됨
public static class SkillSpawner
{
    private static readonly bool UsePool = false;

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

    private static PoolManager GetPool()
    {
        if (!UsePool || GameManager.Instance == null)
        {
            return null;
        }
        return GameManager.Instance.PoolManager;
    }
}
