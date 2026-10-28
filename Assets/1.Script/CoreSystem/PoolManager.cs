using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using System;

public class PoolManager : BaseManager
{
    // 상위 조정자가 데이터 ID를 실제 주소로 변환한 초기화 입력입니다.
    public readonly struct PoolRequest
    {
        public string PrefabKey { get; }
        public int InitialCount { get; }

        public PoolRequest(string prefabKey, int initialCount)
        {
            PrefabKey = prefabKey;
            InitialCount = initialCount;
        }
    }

    private readonly Dictionary<GameObject, Queue<GameObject>> pools = new Dictionary<GameObject, Queue<GameObject>>();
    private readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>(StringComparer.Ordinal);
    private readonly Dictionary<GameObject, GameObject> owners = new Dictionary<GameObject, GameObject>();
    private readonly HashSet<GameObject> rented = new HashSet<GameObject>();
    private Transform storage;
    private bool ready, initializing, clearing, destroyed;
    private UniTask clearTask;

    public override UniTask InitializeAsync() => InitializeAsync(null, null);

    public async UniTask InitializeAsync(ResourceManager manager, IEnumerable<PoolRequest> settings = null)
    {
        if (destroyed || clearing || initializing)
            throw new InvalidOperationException("종료, 정리 또는 초기화 중인 PoolManager입니다.");
        if (ready) return;
        initializing = true;
        try
        {
            if (storage == null)
            {
                var root = new GameObject("Pool Storage");
                root.transform.SetParent(transform, false);
                root.SetActive(false);
                storage = root.transform;
            }
            // 데이터 테이블 종류와 ID 조회는 상위 조정자가 담당합니다.
            if (settings != null)
            {
                if (manager == null) throw new ArgumentNullException(nameof(manager));
                foreach (PoolRequest data in settings)
                {
                    if (string.IsNullOrWhiteSpace(data.PrefabKey) || data.InitialCount < 0)
                        throw new ArgumentException("풀 요청의 프리팹 주소와 초기 수량을 확인하세요.");
                    if (prefabs.ContainsKey(data.PrefabKey))
                        throw new ArgumentException($"중복 풀 주소: '{data.PrefabKey}'");
                    GameObject prefab = await manager.LoadAssetAsync<GameObject>(data.PrefabKey);
                    if (destroyed || clearing) throw new OperationCanceledException("풀 초기화가 중단되었습니다.");
                    if (prefab == null) throw new InvalidOperationException($"'{data.PrefabKey}' 프리팹 로드 실패");
                    prefabs.Add(data.PrefabKey, prefab);
                    CreatePool(prefab, data.InitialCount);
                }
            }
            ready = true;
        }
        catch
        {
            DestroyInstances();
            await UniTask.NextFrame();
            throw;
        }
        finally { initializing = false; }
    }

    // 보스처럼 직접 지정한 프리팹은 호출자가 명시적으로 풀을 준비합니다.
    public void PreparePool(GameObject prefab, int count = 1)
    {
        if (!ready || destroyed || clearing)
            throw new InvalidOperationException("풀 초기화를 먼저 완료하세요.");
        if (prefab == null) throw new ArgumentNullException(nameof(prefab));
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        CreatePool(prefab, count);
    }

    public bool TryGetPrefab<T>(string key, out T component) where T : Component
    {
        component = null;
        return ready && !clearing && !destroyed && key != null &&
            prefabs.TryGetValue(key, out var prefab) && prefab != null && prefab.TryGetComponent<T>(out component);
    }

    private void CreatePool(GameObject prefab, int count)
    {
        if (pools.ContainsKey(prefab)) return;
        var pool = new Queue<GameObject>();
        pools.Add(prefab, pool);
        for (int i = 0; i < count; i++) pool.Enqueue(Create(prefab));
    }

    private GameObject Create(GameObject prefab)
    {
        GameObject instance = Instantiate(prefab, storage);
        instance.SetActive(false);
        owners.Add(instance, prefab);
        return instance;
    }

    // 주소는 풀을, T는 프리팹 루트의 컴포넌트 타입을 지정합니다.
    public T Get<T>(string key, Vector3 position, Quaternion rotation, bool activate = true) where T : Component
    {
        if (key == null || !prefabs.TryGetValue(key, out var prefab)) return null;
        if (!prefab.TryGetComponent<T>(out _))
        {
            Debug.LogError($"'{key}' 프리팹 루트에 {typeof(T).Name} 컴포넌트가 없습니다.", this);
            return null;
        }
        GameObject instance = Rent(prefab, position, rotation, activate);
        return instance != null ? instance.GetComponent<T>() : null;
    }

    public GameObject Rent(string key, Vector3 position, Quaternion rotation, bool activate = true)
    {
        if (key != null && prefabs.TryGetValue(key, out var prefab))
            return Rent(prefab, position, rotation, activate);
        Debug.LogError($"PoolData에 '{key}' 풀을 먼저 설정하세요.", this);
        return null;
    }

    public GameObject Rent(GameObject prefab, Vector3 position, Quaternion rotation, bool activate = true)
    {
        if (!ready || destroyed || clearing) return null;
        if (prefab == null || !pools.TryGetValue(prefab, out var pool))
        {
            Debug.LogError("초기화한 풀의 프리팹만 대여할 수 있습니다.", this);
            return null;
        }
        GameObject instance = null;
        while (pool.Count > 0 && instance == null) instance = pool.Dequeue();
        if (instance == null) instance = Create(prefab);
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.transform.SetParent(transform, true);
        rented.Add(instance);
        instance.SetActive(activate);
        return instance;
    }

    public void Return(GameObject instance)
    {
        if (instance == null || !owners.TryGetValue(instance, out var prefab) || !rented.Remove(instance)) return;
        instance.SetActive(false);
        if (!owners.ContainsKey(instance) || !pools.TryGetValue(prefab, out var pool)) return;
        instance.transform.SetParent(storage, false);
        pool.Enqueue(instance);
    }

    public bool IsRented(GameObject instance) => instance != null && rented.Contains(instance);

    public UniTask ClearPoolsAsync()
    {
        if (clearing) return clearTask;
        ready = false;
        clearing = true;
        clearTask = ClearPoolsCoreAsync().Preserve();
        return clearTask;
    }

    private async UniTask ClearPoolsCoreAsync()
    {
        try
        {
            // 정리 작업을 먼저 저장해 콜백의 중복 정리 요청도 같은 완료를 기다리게 합니다.
            await UniTask.NextFrame();
            // 초기화 작업이 중단된 후 사용 중인 인스턴스까지 정리합니다.
            await UniTask.WaitUntil(() => !initializing);
            DestroyInstances();
            // Destroy가 실행된 다음에 프리팹 에셋을 해제할 수 있습니다.
            await UniTask.NextFrame();
        }
        finally { clearing = false; }
    }

    private void DestroyInstances()
    {
        ready = false;
        var instances = new List<GameObject>(owners.Keys);
        pools.Clear();
        prefabs.Clear();
        owners.Clear();
        rented.Clear();
        foreach (GameObject instance in instances)
        {
            if (instance == null) continue;
            instance.SetActive(false);
            Destroy(instance);
        }
    }

    private void OnDestroy()
    {
        destroyed = true;
        ClearPoolsAsync().Forget();
    }
}
