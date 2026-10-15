using System;
using System.Collections;
using UnityEngine;
using OZ.UI.Contracts;

public class BossGate : StageGate, IDamageable
{
    [SerializeField, Min(1f)] private float durability = 30f;
    [SerializeField] private BossMonster bossPrefab;
    [SerializeField] private Transform bossSpawnPoint;
    [SerializeField] private MonsterSpawner spawner;
    private const float SummonDelay = 5f;
    private float remainingDurability;
    private Coroutine summonRoutine;
    public bool CanTakeDamage { get; private set; }
    public bool IsOpened { get; private set; }
    public event Action BossDefeated;

    public override void Initialize(PoolManager poolManager)
    {
        base.Initialize(poolManager);
        remainingDurability = Mathf.Max(1f, durability);
        if (spawner == null) spawner = GetComponent<MonsterSpawner>();
        if (spawner == null) spawner = gameObject.AddComponent<MonsterSpawner>();
        spawner.Initialize(poolManager);
        int prefabIndex = -1;
        if (bossPrefab != null)
            for (int i = 0; i < poolManager.MonsterPrefabs.Count; i++)
                if (poolManager.MonsterPrefabs[i] == bossPrefab.gameObject) { prefabIndex = i; break; }
        spawner.Configure(prefabIndex, bossSpawnPoint != null ? bossSpawnPoint : transform);
        spawner.AllMonstersDefeated -= OnBossDefeated;
        spawner.AllMonstersDefeated += OnBossDefeated;
        if (prefabIndex < 0)
            Debug.LogError("BossGate의 Boss Prefab을 지정하고 PoolManager 목록에도 등록하세요.", this);
    }

    // 일반 게이트 목표 달성은 공격 잠금만 해제하며 상태는 비활성을 유지합니다.
    public void Unlock()
    {
        if (State != GateState.Inactive) return;
        CanTakeDamage = true;
    }

    public override void StartSpawning()
    {
        if (IsOpened || spawner == null) return;
        CanTakeDamage = false;
        IsOpened = true;
        GameUI.HUD.ShowBanner("보스 게이트 개방", "5초 후 보스가 출현합니다", SummonDelay);
        summonRoutine = StartCoroutine(SummonBoss());
    }

    public void TakeDamage(float damage)
    {
        if (!isActiveAndEnabled || !CanTakeDamage || State != GateState.Inactive || damage <= 0f) return;
        remainingDurability = Mathf.Max(0f, remainingDurability - damage);
        GameUI.Damage.Show(transform.position, damage, DamageKind.Normal);
        if (remainingDurability > 0f) return;
        Activate();
    }

    private IEnumerator SummonBoss()
    {
        yield return new WaitForSeconds(SummonDelay);
        summonRoutine = null;
        if (spawner.Spawn() != null) spawner.NotifySpawningCompleted();
    }

    private void OnBossDefeated()
    {
        if (State != GateState.Active || !IsOpened) return;
        Close();
        BossDefeated?.Invoke();
    }

    public override void StopSpawning()
    {
        if (summonRoutine != null) StopCoroutine(summonRoutine);
        summonRoutine = null;
        IsOpened = false;
        CanTakeDamage = false;
        remainingDurability = Mathf.Max(1f, durability);
        if (spawner != null) spawner.ReturnAll();
    }

    private void OnDestroy()
    {
        StopSpawning();
        if (spawner != null) spawner.AllMonstersDefeated -= OnBossDefeated;
    }
}
