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
///     · 체력 0   → 플레이어 Animator의 사망 상태("Dead")가 끝날 때까지 기다린 뒤 사망 화면 (v1.4)
///                  모션 길이가 바뀌어도(Dead.anim 3.5초 → Death_Fall 1초 등) 자동으로 맞음.
///                  Animator·사망 상태를 못 찾으면 deathScreenDelay(1초) 고정. 그 사이 되살아나면 취소
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
    [Tooltip("켜면 사망 모션이 끝날 때까지 기다린 뒤 사망 화면 (플레이어 Animator의 사망 상태 길이에 맞춤)")]
    [SerializeField] bool waitForDeathAnimation = true;
    [Tooltip("기다릴 Animator 상태 이름 (PlayerState_Dead가 SetTrigger(\"Dead\")로 들어가는 상태)")]
    [SerializeField] string deathStateName = "Dead";
    [Tooltip("사망 모션이 끝난 뒤 사망 화면까지 조금 더 보여 주는 시간(초)")]
    [SerializeField, Min(0f)] float holdAfterAnimation = 0.3f;
    [Tooltip("모션이 길거나 멈춰도 이 시간(초)이 지나면 무조건 사망 화면")]
    [SerializeField, Min(0.1f)] float maxWait = 5f;
    [Tooltip("Animator나 사망 상태를 못 찾을 때 쓰는 고정 대기 시간(초)")]
    [SerializeField, Min(0f)] float deathScreenDelay = 1.0f;

    // 사망 상태로 넘어가기까지(트리거 → 전환) 기다리는 최대 시간. 넘기면 모션이 없다고 보고 고정 대기로
    const float EnterTimeout = 0.5f;

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
        float waited = 0f;
        bool played = false; // 사망 상태에 실제로 들어갔는지
        Animator anim = waitForDeathAnimation ? FindPlayerAnimator() : null;

        if (anim != null)
        {
            int hash = Animator.StringToHash(deathStateName);
            int layer = -1;
            while (waited < maxWait)
            {
                if (anim == null || !anim.isActiveAndEnabled) break; // 플레이어가 꺼짐 → 바로 사망 화면

                bool entering = false;
                if (layer < 0) layer = FindLayerInState(anim, hash, out entering);
                if (layer >= 0)
                {
                    played = true;
                    var st = anim.GetCurrentAnimatorStateInfo(layer);
                    if (st.shortNameHash != hash) break;                                 // 모션 끝나고 다른 상태로 넘어감
                    if (!anim.IsInTransition(layer) && st.normalizedTime >= 1f) break;   // 모션 끝까지 재생됨
                }
                else if (!entering && waited >= EnterTimeout) break;                    // 사망 상태가 없음

                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        if (played)
        {
            if (holdAfterAnimation > 0f) yield return new WaitForSecondsRealtime(holdAfterAnimation);
            Debug.Log($"[OZ UI] 사망 화면: 사망 모션 '{deathStateName}' {waited:0.00}초 + {holdAfterAnimation:0.##}초 뒤 표시", this);
        }
        else
        {
            // Animator·사망 상태를 못 찾음 → 예전처럼 고정 대기 (이미 기다린 시간은 빼고)
            float rest = deathScreenDelay - waited;
            if (rest > 0f) yield return new WaitForSecondsRealtime(rest);
        }

        _deathRoutine = null;
        if (_dead && !GameUI.Screens.IsOpen(ScreenId.Death)) GameUI.Screens.ShowDeath();
    }

    /// <summary>플레이어의 Animator (루트 우선, 없으면 자식 중 컨트롤러가 있는 것)</summary>
    Animator FindPlayerAnimator()
    {
        if (player == null) return null;
        var a = player.GetComponent<Animator>();
        if (a != null && a.runtimeAnimatorController != null) return a;
        foreach (var c in player.GetComponentsInChildren<Animator>())
            if (c.runtimeAnimatorController != null) return c;
        return null;
    }

    /// <summary>지금 사망 상태에 있는 레이어 번호. 없으면 -1 (entering = 그쪽으로 넘어가는 중)</summary>
    static int FindLayerInState(Animator anim, int hash, out bool entering)
    {
        entering = false;
        for (int i = 0; i < anim.layerCount; i++)
        {
            if (anim.GetCurrentAnimatorStateInfo(i).shortNameHash == hash) return i;
            if (anim.IsInTransition(i) && anim.GetNextAnimatorStateInfo(i).shortNameHash == hash) entering = true;
        }
        return -1;
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
