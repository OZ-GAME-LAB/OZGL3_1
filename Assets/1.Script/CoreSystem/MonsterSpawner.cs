using System;
using System.Collections.Generic;
using UnityEngine;

public class MonsterSpawner : MonoBehaviour
{
    [SerializeField, Min(0)] private int monsterPrefabIndex;
    [SerializeField] private Transform spawnPoint;
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
    }

    public void Configure(int prefabIndex, Transform point)
    {
        monsterPrefabIndex = prefabIndex;
        spawnPoint = point;
    }

    public GameObject Spawn()
    {
        if (!isActiveAndEnabled || spawningCompleted) return null;
        if (poolManager == null || monsterPrefabIndex < 0 || monsterPrefabIndex >= poolManager.MonsterPrefabs.Count
            || poolManager.MonsterPrefabs[monsterPrefabIndex] == null
            || poolManager.MonsterPrefabs[monsterPrefabIndex].GetComponent<Monster>() == null)
        {
            Debug.LogError("PoolManager의 프리팹 인덱스와 루트 Monster 컴포넌트를 확인하세요.", this);
            return null;
        }
        Transform point = spawnPoint != null ? spawnPoint : transform;
        GameObject instance = poolManager.Rent(poolManager.MonsterPrefabs[monsterPrefabIndex], point.position, point.rotation);
        if (instance == null) return null;
        Monster monster = instance.GetComponent<Monster>();
        spawned.Add(monster);
        monster.Died += OnMonsterDied;
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
