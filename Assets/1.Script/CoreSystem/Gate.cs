using System.Collections;
using UnityEngine;

public class Gate : StageGate
{
    [SerializeField] private MonsterSpawner spawner;
    // 기존 프리팹의 소환 종류와 위치를 보존합니다.
    [SerializeField, HideInInspector] private Monster monsterPrefab;
    [SerializeField, HideInInspector] private Transform spawnPoint;
    [SerializeField, Min(0.1f)] private float spawnInterval = 3f;
    private Coroutine spawnRoutine;
    // 랭크별 데이터가 마련되면 GetMaxSpawnCount()에서 조회하도록 교체합니다.
    private const int DefaultMaxSpawnCount = 5;
    public int MaxSpawnCount => GetMaxSpawnCount();
    private int GetMaxSpawnCount() => DefaultMaxSpawnCount;

    public override void Initialize(PoolManager manager)
    {
        base.Initialize(manager);
        if (spawner == null) spawner = GetComponent<MonsterSpawner>();
        if (spawner == null)
        {
            spawner = gameObject.AddComponent<MonsterSpawner>();
            int index = 0;
            if (monsterPrefab != null)
            {
                index = -1;
                for (int i = 0; i < manager.MonsterPrefabs.Count; i++)
                    if (manager.MonsterPrefabs[i] == monsterPrefab.gameObject) { index = i; break; }
            }
            spawner.Configure(index, spawnPoint);
        }
        spawner.Initialize(manager);
        spawner.AllMonstersDefeated -= OnAllMonstersDefeated;
        spawner.AllMonstersDefeated += OnAllMonstersDefeated;
    }

    public override void StartSpawning()
    {
        if (spawnRoutine != null || !isActiveAndEnabled || spawner == null) return;
        spawnRoutine = StartCoroutine(SpawnMonsters());
    }

    private IEnumerator SpawnMonsters()
    {
        int targetCount = MaxSpawnCount;
        for (int i = 0; i < targetCount; i++)
        {
            if (spawner.Spawn() == null)
            {
                // 소환 실패를 목표 달성으로 처리하지 않습니다.
                yield return null;
                spawnRoutine = null;
                yield break;
            }
            if (i < targetCount - 1)
                yield return new WaitForSeconds(Mathf.Max(0.1f, spawnInterval));
        }
        spawnRoutine = null;
        spawner.NotifySpawningCompleted();
    }

    private void OnAllMonstersDefeated() => Close();

    private void OnDestroy()
    {
        StopSpawning();
        if (spawner != null) spawner.AllMonstersDefeated -= OnAllMonstersDefeated;
    }

    public override void StopSpawning()
    {
        if (spawnRoutine != null) StopCoroutine(spawnRoutine);
        spawnRoutine = null;
        if (spawner != null) spawner.ReturnAll();
    }
}
