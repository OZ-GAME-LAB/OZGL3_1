using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class PoolManager : BaseManager
{
    [SerializeField] private List<GameObject> monsterPrefabs = new List<GameObject>();
    [SerializeField, Min(0)] private int initialCount = 5;
    private readonly Dictionary<GameObject, Queue<GameObject>> pools = new Dictionary<GameObject, Queue<GameObject>>();
    private readonly Dictionary<GameObject, GameObject> owners = new Dictionary<GameObject, GameObject>();
    private readonly HashSet<GameObject> rented = new HashSet<GameObject>();
    private Transform storage;
    public IReadOnlyList<GameObject> MonsterPrefabs => monsterPrefabs.AsReadOnly();

    public override UniTask InitializeAsync()
    {
        if (storage == null)
        {
            var root = new GameObject("Pool Storage");
            root.transform.SetParent(transform, false);
            root.SetActive(false);
            storage = root.transform;
        }
        foreach (GameObject prefab in monsterPrefabs)
        {
            if (prefab == null || pools.ContainsKey(prefab)) continue;
            var pool = new Queue<GameObject>();
            pools.Add(prefab, pool);
            for (int i = 0; i < initialCount; i++) pool.Enqueue(Create(prefab));
        }
        return UniTask.CompletedTask;
    }

    private GameObject Create(GameObject prefab)
    {
        GameObject instance = Instantiate(prefab, storage);
        instance.SetActive(false);
        owners.Add(instance, prefab);
        return instance;
    }

    public GameObject Rent(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null || !pools.TryGetValue(prefab, out var pool))
        {
            Debug.LogError("PoolManager에 등록하고 초기화한 프리팹만 대여할 수 있습니다.", this);
            return null;
        }
        GameObject instance = null;
        while (pool.Count > 0 && instance == null) instance = pool.Dequeue();
        if (instance == null) instance = Create(prefab);
        instance.transform.SetPositionAndRotation(position, rotation);
        rented.Add(instance);
        instance.transform.SetParent(transform, true);
        instance.SetActive(true);
        return instance;
    }

    public void Return(GameObject instance)
    {
        if (instance == null || !owners.TryGetValue(instance, out var prefab) || !rented.Remove(instance)) return;
        instance.SetActive(false);
        instance.transform.SetParent(storage, false);
        pools[prefab].Enqueue(instance);
    }

    public bool IsRented(GameObject instance) => instance != null && rented.Contains(instance);
}
