using System;
using System.Collections.Generic;
using UnityEngine;

public class MonsterSpawner : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;
    private Monster fixedPrefab;
    private BaseMonsterData fixedData;
    private readonly List<MonsterData> monsterData = new List<MonsterData>();
    private PoolManager poolManager;
    private readonly HashSet<Monster> spawned = new HashSet<Monster>();
    private bool spawningCompleted;
    private bool completionNotified;
    public event Action AllMonstersDefeated;

    public void Initialize(PoolManager manager)
    {
        if (manager == null) throw new ArgumentNullException(nameof(manager));
        ReturnAll();
        poolManager = manager;
        monsterData.Clear();
        fixedPrefab = null;
        fixedData = null;
    }

    public void Configure(Monster prefab, Transform point, BaseMonsterData data = null)
    {
        fixedPrefab = prefab;
        fixedData = data;
        if (point != null) spawnPoint = point;
    }

    public void ConfigureMonsters(IEnumerable<MonsterData> settings)
    {
        monsterData.Clear();
        fixedPrefab = null;
        fixedData = null;
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        foreach (MonsterData data in settings)
        {
            if (data == null || !poolManager.TryGetPrefab<Monster>(data.PrefabKey, out var prefab) || prefab is BossMonster)
                throw new ArgumentException($"일반 몬스터 풀 설정을 확인하세요: '{data?.Id}'");
            if (!monsterData.Contains(data)) monsterData.Add(data);
        }
        if (monsterData.Count == 0) throw new ArgumentException("스테이지의 일반 몬스터 목록이 비어 있습니다.");
    }

    public GameObject Spawn()
    {
        if (!isActiveAndEnabled || spawningCompleted) return null;
        if (poolManager == null || (fixedPrefab == null && monsterData.Count == 0))
        {
            Debug.LogError("소환 대상이 없습니다. PoolData에 일반 Monster 프리팹을 등록하거나 보스 프리팹을 지정하세요.", this);
            return null;
        }
        Transform point = spawnPoint != null ? spawnPoint : transform;
        BaseMonsterData data = fixedPrefab != null ? fixedData : monsterData[UnityEngine.Random.Range(0, monsterData.Count)];
        if (data == null)
        {
            Debug.LogError("몬스터 스탯 데이터가 전달되지 않았습니다.", this);
            return null;
        }
        GameObject instance;
        if (fixedPrefab != null)
            instance = poolManager.Rent(fixedPrefab.gameObject, point.position, point.rotation, false);
        else
        {
            Monster summoned = poolManager.Get<Monster>(data.PrefabKey, point.position, point.rotation, false);
            instance = summoned != null ? summoned.gameObject : null;
        }
        if (instance == null) return null;
        Monster monster = instance.GetComponent<Monster>();
        try
        {
            monster.InitializeStats(data);
            spawned.Add(monster);
            monster.Died += OnMonsterDied;
            instance.SetActive(true);
        }
        catch
        {
            monster.Died -= OnMonsterDied;
            spawned.Remove(monster);
            poolManager.Return(instance);
            throw;
        }
        return instance;
    }

    public void NotifySpawningCompleted()
    {
        spawningCompleted = true;
        CheckCompletion();
    }

    private void OnMonsterDied(Monster monster) => ReturnMonster(monster.gameObject);

    public void ReturnMonster(GameObject instance)
    {
        if (instance == null) return;
        Monster monster = instance.GetComponent<Monster>();
        // 강제 비활성화나 반환을 처치로 인정하지 않습니다.
        if (monster == null || !monster.IsDead || !spawned.Remove(monster)) return;
        monster.Died -= OnMonsterDied;
        if (poolManager != null) poolManager.Return(instance);
        CheckCompletion();
    }

    private void CheckCompletion()
    {
        if (!spawningCompleted || spawned.Count != 0 || completionNotified) return;
        completionNotified = true;
        AllMonstersDefeated?.Invoke();
    }

    public void ReturnAll()
    {
        spawningCompleted = false;
        completionNotified = false;
        var remaining = new List<Monster>(spawned);
        spawned.Clear();
        foreach (Monster monster in remaining)
        {
            if (monster == null) continue;
            monster.Died -= OnMonsterDied;
            if (poolManager != null) poolManager.Return(monster.gameObject);
        }
    }

    private void OnDisable() => ReturnAll();
}
