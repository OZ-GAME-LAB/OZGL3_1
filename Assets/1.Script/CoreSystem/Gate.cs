using System.Collections;
using UnityEngine;

public class Gate : StageGate
{
    [SerializeField] private Monster monsterPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField, Min(0.1f)] private float spawnInterval = 3f;

    private Coroutine spawnRoutine;

    public override void StartSpawning()
    {
        if (spawnRoutine != null)
            return;

        if (!isActiveAndEnabled)
        {
            Debug.LogWarning("소환을 시작하려면 Gate 컴포넌트와 오브젝트를 활성화하세요.", this);
            return;
        }

        if (monsterPrefab == null)
        {
            Debug.LogError("Gate에 Monster 프리팹을 연결하세요.", this);
            return;
        }

        spawnRoutine = StartCoroutine(SpawnMonsters());
    }

    public override void StopSpawning()
    {
        if (spawnRoutine == null)
            return;

        StopCoroutine(spawnRoutine);
        spawnRoutine = null;
    }

    private IEnumerator SpawnMonsters()
    {
        while (true)
        {
            Transform point = spawnPoint != null ? spawnPoint : transform;
            Monster monster = Instantiate(monsterPrefab, point.position, point.rotation);

            // 스테이지가 제거될 때 소환된 몬스터도 함께 정리합니다.
            monster.transform.SetParent(transform, true);
            monster.gameObject.SetActive(true);

            yield return new WaitForSeconds(Mathf.Max(0.1f, spawnInterval));
        }
    }

    private void OnDisable()
    {
        StopSpawning();
    }
}
