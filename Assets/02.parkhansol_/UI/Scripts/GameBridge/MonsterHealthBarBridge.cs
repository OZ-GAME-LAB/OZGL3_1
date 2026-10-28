using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// [박한솔 UI · v0.9] 몬스터 머리 위 체력바. 팀원 코드(Monster · MonsterSpawner · PoolManager) 수정 없이 동작한다.
///   0.25초마다 씬의 Monster(풀에 들어 있는 것 포함)를 찾아 EnemyUILink + MonsterHealthBarSync를 붙인다.
///   Sync는 Monster의 체력 값(MaxHP · CurrentHP)을 읽기만 해서 체력바에 넘긴다 (값을 바꾸지 않음).
///   피해 숫자는 Monster.TakeDamage가 이미 띄우므로 여기서는 체력바만 맞춘다.
///   풀에 반납(비활성) → 체력바 자동 해제, 다시 꺼내면 가득 찬 상태로 다시 표시.
/// 씬에 GameManager가 있으면 Play 시 자동으로 생긴다 (씬·프리팹 수정 불필요).
/// 나중에 Monster가 IEnemyHealthSource를 직접 구현하거나 EnemyUILink를 프리팹에 붙이면 그 몬스터는 건너뛴다.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("OZ/UI/Links/Monster Health Bar Bridge")]
public class MonsterHealthBarBridge : MonoBehaviour
{
    [SerializeField, Min(0.05f)] float pollInterval = 0.25f;

    float _next;

    void Update()
    {
        if (Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + pollInterval;
        if (!GameUI.IsReady) return;

        foreach (var m in FindObjectsByType<Monster>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (m == null || m is IEnemyHealthSource) continue;
            if (m is BossMonster) continue; // 보스는 머리 위 바 대신 화면 상단 보스 체력바 (BossHudBridge)
            if (m.GetComponent<MonsterHealthBarSync>() != null) continue;
            if (m.GetComponent<EnemyUILink>() != null) continue; // 팀원이 직접 붙였으면 그쪽 사용
            m.gameObject.AddComponent<MonsterHealthBarSync>().Setup();
        }
    }

    // ── 자동 설치 ──
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInstall()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryInstall();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryInstall();

    static void TryInstall()
    {
        if (FindAnyObjectByType<MonsterHealthBarBridge>(FindObjectsInactive.Include) != null) return;
        if (FindAnyObjectByType<GameManager>(FindObjectsInactive.Include) == null) return;
        new GameObject("MonsterHealthBarBridge (auto, UI)").AddComponent<MonsterHealthBarBridge>(); // Play 중에만 생김
    }
}
