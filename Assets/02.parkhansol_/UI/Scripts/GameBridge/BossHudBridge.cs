using System.Collections.Generic;
using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// [박한솔 UI · v1.0] 실제 보스(BossMonster) → 화면 상단 보스 체력바 + 등장 연출. 팀원 코드(BossGate · BossMonster) 수정 없이 동작한다.
///   0.1초마다 활성화된 BossMonster를 찾아서
///     등장: BossUILink.Begin(MaxHP)  → 레터박스·이름 배너·바 채움 (게임은 멈추지 않음)
///     매 프레임: CurrentHP → 체력바, 0이 되면 "격파" 연출
///     죽기 전에 사라지면 (스테이지 종료·풀 반납): 체력바만 치움
///   이름: 보스 데이터(BossMonsterData.Name), 페이즈 눈금: PatternTriggerHpRatio
///   칭호·바 테마 등을 꾸미려면 presets에 BossData를 넣고 id를 보스 데이터 Id(예: BossMonster_1)와 같게 하면 그걸 쓴다.
/// 씬에 GameManager가 있으면 Play 시 자동으로 생긴다 (씬·프리팹 수정 불필요).
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("OZ/UI/Links/Boss Hud Bridge")]
public class BossHudBridge : MonoBehaviour
{
    [Tooltip("선택: 보스별 표시 설정 (id = 보스 데이터 Id). 없으면 보스 이름만으로 자동 생성")]
    [SerializeField] BossData[] presets;
    [SerializeField, Min(0.05f)] float pollInterval = 0.1f;

    BossUILink _link;
    BossMonster _boss;
    float _lastHp = float.NaN;
    float _next;
    readonly Dictionary<string, BossData> _auto = new Dictionary<string, BossData>();

    internal BossMonster Current => _boss;

    void Awake()
    {
        _link = GetComponent<BossUILink>();
        if (_link == null) _link = gameObject.AddComponent<BossUILink>();
    }

    void Update()
    {
        if (_boss != null || !GameUI.IsReady) return;
        if (Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + pollInterval;
        foreach (var b in FindObjectsByType<BossMonster>(FindObjectsSortMode.None)) // 활성인 것만
        {
            if (b == null || b.IsDead || !(b.MaxHP > 0f)) continue;
            Begin(b);
            break;
        }
    }

    void Begin(BossMonster boss)
    {
        _boss = boss;
        _link.SetData(DataFor(boss));
        _link.Begin(boss.MaxHP);
        _lastHp = boss.MaxHP;
    }

    void LateUpdate()
    {
        if (_boss == null) return;
        if (!_boss.isActiveAndEnabled) // 죽기 전에 사라짐 (스테이지 종료·리소스 정리)
        {
            if (!_boss.IsDead) _link.Hide();
            _boss = null;
            return;
        }
        float hp = _boss.IsDead ? 0f : _boss.CurrentHP;
        if (!Mathf.Approximately(hp, _lastHp)) { _lastHp = hp; _link.SetHP(hp); } // 0이면 BossUILink가 격파 연출
        if (_boss.IsDead) _boss = null; // 같은 개체가 풀에서 다시 나오면 새 보스전으로 다시 잡힘
    }

    BossData DataFor(BossMonster boss)
    {
        string id = string.IsNullOrEmpty(boss.DataId) ? boss.name : boss.DataId;
        if (presets != null)
            foreach (var p in presets) if (p != null && p.id == id) return p;
        if (_auto.TryGetValue(id, out var d) && d != null) return d;
        d = ScriptableObject.CreateInstance<BossData>();
        d.hideFlags = HideFlags.DontSave;
        d.id = id;
        d.displayName = string.IsNullOrEmpty(boss.MonsterName) ? "보스" : boss.MonsterName;
        float r = boss.PatternTriggerHpRatio;
        d.phaseThresholds = r > 0f && r < 1f ? new[] { r } : new float[0];
        _auto[id] = d;
        return d;
    }

    void OnDestroy()
    {
        foreach (var d in _auto.Values) if (d != null) Destroy(d);
        _auto.Clear();
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
        if (FindAnyObjectByType<BossHudBridge>(FindObjectsInactive.Include) != null) return;
        if (FindAnyObjectByType<GameManager>(FindObjectsInactive.Include) == null) return;
        new GameObject("BossHudBridge (auto, UI)").AddComponent<BossHudBridge>(); // Play 중에만 생김
    }
}