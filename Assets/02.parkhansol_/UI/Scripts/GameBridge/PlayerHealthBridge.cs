using System;
using System.Collections;
using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// [박한솔 UI · v0.8] 플레이어 체력 → 화면 연출만. 체력 값·피격 판정은 갖지 않는다.
///
///   체력의 기준은 PlayerHealth 하나 (IHealthSource 구현 + GameUI.Bind는 플레이어 담당).
///   이 컴포넌트는 플레이어의 IHealthSource.HealthChanged를 듣고 아래만 처리한다:
///     · 체력 감소 → 플레이어 머리 위 빨간 피해 숫자 (HealthChange.IsDamage / Delta)
///     · 체력 증가 → 초록 회복 숫자 (부활·재도전 복구는 숫자 없음)
///     · 체력 0   → deathScreenDelay(기본 1초, 사망 모션이 보이게) 뒤 사망 화면. 그 사이 되살아나면 취소
///   피격·회복·무적·사망 처리(PlayerController.Die)·재도전 복구(ResetHealth)는 전부 PlayerHealth 쪽.
///
/// 씬에 PlayerController가 있으면 Play 시 자동으로 생긴다 (씬·프리팹 수정 없음).
///   플레이어에 IHealthSource 컴포넌트가 아직 없으면(PlayerHealth 병합 전) 아무것도 하지 않는다.
/// 수치를 바꾸려면 Play 중 'PlayerHealthBridge (auto, UI)' 오브젝트에서, 또는 씬에 직접 이 컴포넌트를 놓고 수정.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("OZ/UI/Links/Player Health Bridge")]
public class PlayerHealthBridge : MonoBehaviour
{
    [Header("연결 (비우면 씬에서 자동으로 찾음)")]
    [SerializeField] PlayerController player;

    [Header("숫자")]
    [Tooltip("플레이어 머리 위 빨간 피해 숫자 / 초록 회복 숫자")]
    [SerializeField] bool showNumbers = true;
    [SerializeField] Vector3 numberOffset = new Vector3(0f, 2f, 0f);

    [Header("사망")]
    [Tooltip("체력 0 → 사망 화면까지 기다리는 시간(초). 사망 모션이 보이게")]
    [SerializeField, Min(0f)] float deathScreenDelay = 1.0f;

    IHealthSource _health;
    bool _dead;
    Coroutine _deathRoutine;

    public static PlayerHealthBridge Instance { get; private set; }
    /// <summary>연결된 플레이어 체력 (PlayerHealth). 없으면 null</summary>
    public IHealthSource Health => _health;
    public bool IsDead => _dead;

    /// <summary>체력이 0이 된 순간 (사망 화면 열기 전)</summary>
    public event Action Died;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[OZ UI] PlayerHealthBridge가 두 개 있습니다. 하나만 남깁니다.", this);
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void OnEnable()
    {
        UISources.Changed += Connect; // PlayerHealth가 늦게 켜지거나 바뀌어도 다시 연결
        Connect();
    }

    void OnDisable()
    {
        UISources.Changed -= Connect;
        SetHealth(null);
        StopDeathRoutine();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>플레이어 오브젝트에서 IHealthSource 구현 컴포넌트(PlayerHealth)를 찾아 구독</summary>
    void Connect()
    {
        if (player == null) player = FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
        IHealthSource found = null;
        if (player != null)
            foreach (var c in player.GetComponents<MonoBehaviour>())
                if (c is IHealthSource hs) { found = hs; break; }
        SetHealth(found);
    }

    void SetHealth(IHealthSource h)
    {
        if (ReferenceEquals(h, _health)) return;
        if (_health != null) _health.HealthChanged -= OnHealthChanged;
        _health = h;
        if (_health != null)
        {
            _health.HealthChanged += OnHealthChanged;
            _dead = _health.HP <= 0f;
            Debug.Log($"[OZ UI] PlayerHealthBridge: {((Component)_health).GetType().Name}.HealthChanged 구독 (피해·회복 숫자, 사망 화면)", this);
        }
    }

    void OnHealthChanged(HealthChange c)
    {
        Vector3 pos = player != null ? player.transform.position + numberOffset : Vector3.zero;
        float amount = Mathf.Abs(c.Delta);

        if (c.IsDamage && amount > 0f)
        {
            if (showNumbers && player != null) GameUI.Damage.Show(pos, amount, DamageKind.PlayerHurt);
        }
        else if (c.IsHeal && amount > 0f)
        {
            // 부활(0에서 회복)·꺼진 동안의 복구(재도전)는 숫자 없이
            bool revive = c.Previous <= 0f || player == null || !player.gameObject.activeInHierarchy;
            if (showNumbers && !revive) GameUI.Damage.Show(pos, amount, DamageKind.Heal);
        }

        if (c.Current <= 0f)
        {
            if (!_dead) Die();
        }
        else if (_dead)
        {
            _dead = false;
            StopDeathRoutine(); // 사망 화면 뜨기 전에 되살아남
        }
    }

    void Die()
    {
        _dead = true;
        Died?.Invoke();
        StopDeathRoutine();
        _deathRoutine = StartCoroutine(ShowDeathAfterDelay());
    }

    IEnumerator ShowDeathAfterDelay()
    {
        if (deathScreenDelay > 0f) yield return new WaitForSecondsRealtime(deathScreenDelay);
        _deathRoutine = null;
        if (_dead && !GameUI.Screens.IsOpen(ScreenId.Death)) GameUI.Screens.ShowDeath();
    }

    void StopDeathRoutine()
    {
        if (_deathRoutine == null) return;
        StopCoroutine(_deathRoutine);
        _deathRoutine = null;
    }

    // ── 자동 설치: PlayerController가 있으면 독립 오브젝트로 생성 (플레이어가 꺼져도 사망 화면 대기가 끊기지 않게) ──
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
        if (FindAnyObjectByType<PlayerHealthBridge>(FindObjectsInactive.Include) != null) return;
        var pc = FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (pc == null) return;
        // 지역 이름·상호작용 안내·화면 밖 화살표·플레이어 옆 게이지가 플레이어를 찾을 수 있게 (런타임에만 추가)
        if (pc.GetComponent<UIPlayerTag>() == null) pc.gameObject.AddComponent<UIPlayerTag>();
        new GameObject("PlayerHealthBridge (auto, UI)").AddComponent<PlayerHealthBridge>(); // Play 중에만 생김
    }
}
