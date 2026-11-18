using System;
using System.Collections.Generic;
using UnityEngine;

public class MonsterSpawner : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;
    [Tooltip("여러 지점을 지정하면 플레이어 및 살아 있는 몬스터와 간격을 두고 순환 소환합니다.")]
    [SerializeField] private Transform[] spawnPoints = Array.Empty<Transform>();
    [Tooltip("한 종류가 연속해서만 나오지 않도록 종류별로 한 번씩 소환하고 섞습니다.")]
    [SerializeField] private bool shuffleMonsterTypes;
    [Tooltip("스폰 지점의 Y를 발바닥 높이로 해석합니다. 기존 단일 스폰 지점은 루트 중심 기준을 유지합니다.")]
    [SerializeField] private bool alignFeetToSpawnPoint;
    private readonly List<MonsterData> monsterBag = new List<MonsterData>();
    private int nextSpawnPoint;
    private Monster fixedPrefab;
    private BaseMonsterData fixedData;
    private readonly List<MonsterData> monsterData = new List<MonsterData>();
    private PoolManager poolManager;
    private Transform playerTarget;
    private readonly HashSet<Monster> spawned = new HashSet<Monster>();
    private bool spawningCompleted;
    private bool completionNotified;
    public event Action AllMonstersDefeated;

    public void Initialize(PoolManager manager, Transform target)
    {
        if (manager == null) throw new ArgumentNullException(nameof(manager));
        if (target == null) throw new ArgumentNullException(nameof(target));
        ReturnAll();
        poolManager = manager;
        playerTarget = target;
        monsterData.Clear();
        fixedPrefab = null;
        fixedData = null;
        monsterBag.Clear();
    }

    public void Configure(Monster prefab, BaseMonsterData data = null)
    {
        fixedPrefab = prefab;
        fixedData = data;
        monsterBag.Clear();
    }

    public void ConfigureSpawnPoints(Transform[] points, bool balancedTypes = true, bool alignFeet = true)
    {
        spawnPoints = points ?? Array.Empty<Transform>();
        shuffleMonsterTypes = balancedTypes;
        alignFeetToSpawnPoint = alignFeet && spawnPoints.Length > 0;
        nextSpawnPoint = 0;
        monsterBag.Clear();
    }

    public void ConfigureMonsters(IEnumerable<MonsterData> settings)
    {
        monsterData.Clear();
        monsterBag.Clear();
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
        if (playerTarget == null)
        {
            Debug.LogError("소환할 몬스터의 플레이어 타깃이 없습니다.", this);
            return null;
        }
        if (poolManager == null || (fixedPrefab == null && monsterData.Count == 0))
        {
            Debug.LogError("소환 대상이 없습니다. PoolData에 일반 Monster 프리팹을 등록하거나 보스 프리팹을 지정하세요.", this);
            return null;
        }
        Transform point = ChooseSpawnPoint();
        BaseMonsterData data = fixedPrefab != null ? fixedData : ChooseMonsterData();
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
            monster.SetTarget(playerTarget);
            if (alignFeetToSpawnPoint) AlignFeet(monster, point.position.y);
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

    // Inactive Collider.bounds is empty. Read the collider shape and transform instead,
    // before OnEnable starts AI ground probes or the first rendered frame.
    private static void AlignFeet(Monster monster, float groundY)
    {
        Collider body = monster.GetComponent<Collider>();
        if (body == null) return;
        Transform pose = body.transform;
        float centerY;
        float halfHeight;
        if (body is CapsuleCollider capsule)
        {
            Vector3 axis = capsule.direction == 0 ? Vector3.right :
                capsule.direction == 2 ? Vector3.forward : Vector3.up;
            Vector3 perpendicularA = capsule.direction == 0 ? Vector3.up : Vector3.right;
            Vector3 perpendicularB = capsule.direction == 2 ? Vector3.up : Vector3.forward;
            Vector3 worldAxis = pose.TransformVector(axis);
            float radius = capsule.radius * Mathf.Max(pose.TransformVector(perpendicularA).magnitude,
                pose.TransformVector(perpendicularB).magnitude);
            float segmentHalfLength = Mathf.Max(0f, capsule.height * worldAxis.magnitude * 0.5f - radius);
            halfHeight = radius + Mathf.Abs(worldAxis.normalized.y) * segmentHalfLength;
            centerY = pose.TransformPoint(capsule.center).y;
        }
        else if (body is BoxCollider box)
        {
            Vector3 half = box.size * 0.5f;
            halfHeight = Mathf.Abs(pose.TransformVector(Vector3.right * half.x).y) +
                Mathf.Abs(pose.TransformVector(Vector3.up * half.y).y) +
                Mathf.Abs(pose.TransformVector(Vector3.forward * half.z).y);
            centerY = pose.TransformPoint(box.center).y;
        }
        else if (body is SphereCollider sphere)
        {
            halfHeight = sphere.radius * Mathf.Max(pose.TransformVector(Vector3.right).magnitude,
                Mathf.Max(pose.TransformVector(Vector3.up).magnitude, pose.TransformVector(Vector3.forward).magnitude));
            centerY = pose.TransformPoint(sphere.center).y;
        }
        else return;

        Vector3 position = monster.transform.position;
        position.y += groundY - (centerY - halfHeight);
        monster.transform.position = position;
    }

    private MonsterData ChooseMonsterData()
    {
        if (!shuffleMonsterTypes) return monsterData[UnityEngine.Random.Range(0, monsterData.Count)];
        if (monsterBag.Count == 0) monsterBag.AddRange(monsterData);
        int index = UnityEngine.Random.Range(0, monsterBag.Count);
        MonsterData data = monsterBag[index];
        monsterBag.RemoveAt(index);
        return data;
    }

    private Transform ChooseSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return spawnPoint != null ? spawnPoint : transform;

        Transform best = null;
        float bestClearance = float.NegativeInfinity;
        int bestIndex = nextSpawnPoint;
        for (int offset = 0; offset < spawnPoints.Length; offset++)
        {
            int index = (nextSpawnPoint + offset) % spawnPoints.Length;
            Transform point = spawnPoints[index];
            if (point == null) continue;
            // X/Y가 실제 전투 평면입니다. Z 방향 배경 장식은 간격에 영향을 주지 않습니다.
            float clearance = playerTarget != null
                ? Mathf.Min(1f, Vector2.Distance(point.position, playerTarget.position) / 2.5f) : 1f;
            foreach (Monster monster in spawned)
                if (monster != null && !monster.IsDead)
                    clearance = Mathf.Min(clearance, Vector2.Distance(point.position, monster.transform.position) / 1.5f);
            if (clearance > bestClearance)
            {
                best = point;
                bestClearance = clearance;
                bestIndex = index;
            }
            if (clearance >= 1f) break;
        }
        nextSpawnPoint = (bestIndex + 1) % spawnPoints.Length;
        return best != null ? best : spawnPoint != null ? spawnPoint : transform;
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
        nextSpawnPoint = 0;
        monsterBag.Clear();
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
