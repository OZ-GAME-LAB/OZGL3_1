using System.Collections;
using UnityEngine;
using System.Collections.Generic;

public class Gate : StageGate
{
    [SerializeField] private MonsterSpawner spawner;
    [SerializeField, Min(0.1f)] private float spawnInterval = 3f;
    [SerializeField, Min(1)] private int maxSpawnCount = 5;
    [Tooltip("0이면 즉시 소환합니다. 양수이면 플레이어가 이 X 거리 안에 들어올 때 전투를 시작합니다.")]
    [SerializeField, Min(0f)] private float activationDistance;
    [SerializeField, Min(0.1f)] private float activationHeight = 3f;
    [Tooltip("비우면 스테이지의 모든 일반 몬스터를 사용합니다.")]
    [SerializeField] private string[] allowedMonsterIds = System.Array.Empty<string>();
    private Coroutine spawnRoutine;
    private Transform playerTarget;
    public int MaxSpawnCount => Mathf.Max(1, maxSpawnCount);
    public bool IsWaitingForPlayer { get; private set; }

    public override void Initialize(PoolManager manager, Transform playerTarget)
    {
        base.Initialize(manager, playerTarget);
        this.playerTarget = playerTarget;
        if (spawner == null) spawner = GetComponent<MonsterSpawner>();
        if (spawner == null)
            spawner = gameObject.AddComponent<MonsterSpawner>();
        spawner.Initialize(manager, playerTarget);
        spawner.AllMonstersDefeated -= OnAllMonstersDefeated;
        spawner.AllMonstersDefeated += OnAllMonstersDefeated;
    }

    public override void StartSpawning()
    {
        if (spawnRoutine != null || !isActiveAndEnabled || spawner == null) return;
        spawnRoutine = StartCoroutine(SpawnMonsters());
    }

    public void ConfigureEncounter(int count, float interval, float distance, float height = 3f,
        string[] monsterIds = null)
    {
        maxSpawnCount = Mathf.Max(1, count);
        spawnInterval = Mathf.Max(0.1f, interval);
        activationDistance = Mathf.Max(0f, distance);
        activationHeight = Mathf.Max(0.1f, height);
        allowedMonsterIds = monsterIds ?? System.Array.Empty<string>();
    }

    public void ConfigureMonsters(IEnumerable<MonsterData> data)
    {
        if (data == null) throw new System.ArgumentNullException(nameof(data));
        if (allowedMonsterIds == null || allowedMonsterIds.Length == 0)
        {
            spawner.ConfigureMonsters(data);
            return;
        }
        var allowed = new HashSet<string>(allowedMonsterIds, System.StringComparer.Ordinal);
        var filtered = new List<MonsterData>();
        foreach (MonsterData monster in data)
            if (monster != null && allowed.Contains(monster.Id)) filtered.Add(monster);
        spawner.ConfigureMonsters(filtered);
    }

    private IEnumerator SpawnMonsters()
    {
        IsWaitingForPlayer = activationDistance > 0f;
        while (IsWaitingForPlayer)
        {
            if (playerTarget != null && playerTarget.gameObject.activeInHierarchy &&
                Mathf.Abs(playerTarget.position.x - transform.position.x) <= activationDistance &&
                Mathf.Abs(playerTarget.position.y - transform.position.y) <= activationHeight)
                IsWaitingForPlayer = false;
            else
                yield return null;
        }
        int targetCount = MaxSpawnCount;
        for (int i = 0; i < targetCount; i++)
        {
            if (spawner.Spawn() == null)
            {
                // 소환 실패를 목표 달성으로 처리하지 않습니다.
                Debug.LogError($"게이트 '{name}'의 몬스터 소환에 실패했습니다. 풀과 스폰 지점 설정을 확인하세요.", this);
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
        IsWaitingForPlayer = false;
        if (spawner != null) spawner.ReturnAll();
    }
}
