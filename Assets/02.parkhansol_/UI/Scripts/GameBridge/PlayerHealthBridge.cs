using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// [박한솔 UI] 플레이어 체력 ↔ HUD 연결. 팀원 코드(PlayerController 등) 수정 없이 동작한다.
///
///   체력 값의 기준은 PlayerUILink (HUD에 보이는 값 = 실제 체력).
///   PlayerHealthBridge.Instance.TakeDamage(10f);           // 피격 → HUD 감소 + 빨간 숫자 + PlayerController.OnHit()
///   PlayerHealthBridge.Instance.TakeDamage(10f, hitPos);   // 맞은 위치 지정
///   PlayerHealthBridge.Instance.Heal(20f);                 // 회복 → 초록 숫자
///   체력 0 → 사망 화면(GameUI.Screens.ShowDeath) → '재도전'은 SceneGameLink.RestartStage가 체력 가득으로 복구
///
/// 씬에 PlayerUILink와 PlayerController가 있으면 Play 시 자동으로 붙는다 (씬 파일 수정 불필요).
/// 수치를 바꾸고 싶으면 'UI Links' 오브젝트에 직접 Add Component 해서 인스펙터로 조정.
///
/// [임시] 몬스터 접촉 피해: 전투 담당이 몬스터 공격에서 TakeDamage를 직접 부르게 되면 monsterContactDamage 끄기.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("OZ/UI/Links/Player Health Bridge")]
public class PlayerHealthBridge : MonoBehaviour
{
    [Header("연결 (비우면 씬에서 자동으로 찾음)")]
    [SerializeField] PlayerController player;
    [SerializeField] PlayerUILink playerUI;

    [Header("피격")]
    [Tooltip("한 번 맞은 뒤 무적 시간(초)")]
    [SerializeField, Min(0f)] float invincibleTime = 0.8f;
    [Tooltip("플레이어 머리 위 빨간 피해 숫자")]
    [SerializeField] bool showDamageNumber = true;
    [SerializeField] Vector3 numberOffset = new Vector3(0f, 2f, 0f);

    [Header("[임시] 몬스터 접촉 피해")]
    [SerializeField] bool monsterContactDamage = true;
    [SerializeField, Min(0f)] float contactDamage = 10f;

    float _invincibleUntil;
    bool _dead;
    PlayerHitRelay _relay;

    public static PlayerHealthBridge Instance { get; private set; }
    public bool IsDead => _dead;
    public float HP => playerUI != null ? playerUI.HP : 0f;
    public float MaxHP => playerUI != null ? playerUI.MaxHP : 0f;

    /// <summary>체력이 0이 된 순간 (사망 화면 열기 직전)</summary>
    public event System.Action Died;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[OZ UI] PlayerHealthBridge가 두 개 있습니다. 하나만 남깁니다.", this);
            Destroy(this);
            return;
        }
        Instance = this;
        FindRefs();
    }

    void OnEnable()
    {
        if (playerUI != null) playerUI.HealthChanged += OnHealthChanged;
        AttachRelay();
    }

    void OnDisable()
    {
        if (playerUI != null) playerUI.HealthChanged -= OnHealthChanged;
    }

    void OnDestroy()
    {
        if (_relay != null) Destroy(_relay);
        if (Instance == this) Instance = null;
    }

    void FindRefs()
    {
        if (playerUI == null) playerUI = FindAnyObjectByType<PlayerUILink>(FindObjectsInactive.Include);
        if (player == null) player = FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (playerUI == null) Debug.LogWarning("[OZ UI] PlayerHealthBridge: PlayerUILink를 찾지 못했습니다.", this);
        if (player == null) Debug.LogWarning("[OZ UI] PlayerHealthBridge: PlayerController를 찾지 못했습니다.", this);
    }

    void AttachRelay()
    {
        if (player == null || _relay != null) return;
        _relay = player.GetComponent<PlayerHitRelay>();
        if (_relay == null) _relay = player.gameObject.AddComponent<PlayerHitRelay>(); // 런타임에만 추가 (프리팹 수정 없음)
        _relay.owner = this;
        // v0.7: 지역 이름·상호작용 안내·화면 밖 화살표 거리가 플레이어를 찾을 수 있게 (런타임에만 추가)
        if (player.GetComponent<UIPlayerTag>() == null) player.gameObject.AddComponent<UIPlayerTag>();
    }

    // RestartStage 등에서 체력이 다시 채워지면 사망 상태 해제
    void OnHealthChanged(HealthChange change)
    {
        if (_dead && change.Current > 0f) _dead = false;
    }

    /// <summary>피격. 무적 시간·사망·UI 창 열림 중에는 무시. 반환 = 실제로 피해가 들어갔는지</summary>
    public bool TakeDamage(float amount, Vector3? hitPoint = null)
    {
        if (amount <= 0f || _dead || playerUI == null) return false;
        if (player == null || !player.isActiveAndEnabled) return false;
        if (UIState.IsGameplayInputBlocked) return false;
        if (Time.time < _invincibleUntil) return false;

        _invincibleUntil = Time.time + invincibleTime;
        playerUI.SetHP(playerUI.HP - amount);

        if (showDamageNumber)
            GameUI.Damage.Show(hitPoint ?? player.transform.position + numberOffset, amount, DamageKind.PlayerHurt);

        if (playerUI.HP <= 0f) Die();
        else player.OnHit();
        return true;
    }

    public void Heal(float amount)
    {
        if (amount <= 0f || _dead || playerUI == null) return;
        playerUI.SetHP(playerUI.HP + amount);
        if (showDamageNumber && player != null)
            GameUI.Damage.Show(player.transform.position + numberOffset, amount, DamageKind.Heal);
    }

    void Die()
    {
        _dead = true;
        if (player != null) player.Stop();
        Died?.Invoke();
        GameUI.Screens.ShowDeath(); // 입력 차단은 사망 화면이 처리 → SceneGameLink가 플레이어 정지
    }

    internal void OnPlayerSpawned()
    {
        _invincibleUntil = Time.time + 0.5f; // 스폰 직후 잠깐 보호
    }

    internal void OnMonsterContact(Collider other, Vector3 point)
    {
        if (!monsterContactDamage || other == null) return;
        if (other.GetComponentInParent<Monster>() == null) return;
        TakeDamage(contactDamage, point + Vector3.up * 1.5f);
    }

    // ── 자동 설치: 씬에 PlayerUILink + PlayerController가 있고 Bridge가 없으면 PlayerUILink 오브젝트에 붙임 ──
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
        var ui = FindAnyObjectByType<PlayerUILink>(FindObjectsInactive.Include);
        if (ui == null) return;
        if (FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include) == null) return;
        ui.gameObject.AddComponent<PlayerHealthBridge>(); // Play 중에만 붙음 → 씬 파일에 저장 안 됨
    }
}
