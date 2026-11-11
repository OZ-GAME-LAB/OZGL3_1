using System;
using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// [박한솔 UI · v1.4] 정명진 님 PlayerWeapon(맨손·검·스태프) → HUD 평타 칸. 팀원 코드 수정 없이 동작한다.
///   PlayerWeapon.WeaponChanged를 구독해 IWeaponSource로 바꿔 GameUI에 등록.
///   [v1.5] 상점 무기 구매: IWeaponAcquireSource → PlayerWeapon.CanAcquire / TryAcquire ("무기는 하나만" 규칙은 플레이어 쪽).
///   콤보 점: 지금 무기의 평타 컴포넌트가 IComboAttack이고 플레이어가 Attack 상태일 때 CurrentStep.
/// Play 시 PlayerWeapon이 있는 오브젝트에 자동으로 붙는다 (씬·프리팹 수정 없음).
/// 플레이어 쪽이 IWeaponSource를 직접 구현하면 이 파일은 지워도 된다.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("OZ/UI/Links/Player Weapon Bridge")]
public class PlayerWeaponBridge : MonoBehaviour, IWeaponSource, IWeaponAcquireSource
{
    [Tooltip("검 콤보 타 수 (HUD 점 개수). 콤보가 아닌 무기는 0")]
    [SerializeField, Min(0)] int swordComboLength = 3;
    [SerializeField, Min(0)] int staffComboLength = 0;

    PlayerWeapon _weapon;
    PlayerController _player;
    IComboAttack[] _combos = new IComboAttack[0];
    IAttackStatReceiver[] _receivers = new IAttackStatReceiver[0];

    public event Action WeaponChanged;

    public UIWeapon CurrentWeapon => _weapon != null ? (UIWeapon)(int)_weapon.Current : UIWeapon.None;

    public bool IsMagicAttack
    {
        get
        {
            var atk = CurrentAttack<IAttackStatReceiver>(_receivers);
            return atk != null && atk.DamageType == SkillDamageType.Magic;
        }
    }

    public int ComboLength
    {
        get
        {
            var w = CurrentWeapon;
            if (CurrentAttack<IComboAttack>(_combos) == null) return 0;
            return w == UIWeapon.Sword ? swordComboLength : w == UIWeapon.Staff ? staffComboLength : 0;
        }
    }

    public int ComboStep
    {
        get
        {
            if (_player == null || _player.CurrentStateType != PlayerState.Attack) return -1;
            var c = CurrentAttack<IComboAttack>(_combos);
            return c != null ? c.CurrentStep : -1;
        }
    }

    void Awake()
    {
        _weapon = GetComponent<PlayerWeapon>();
        _player = GetComponent<PlayerController>();
        _combos = GetComponentsInChildren<IComboAttack>(true);
        _receivers = GetComponentsInChildren<IAttackStatReceiver>(true);
    }

    void OnEnable()
    {
        if (_weapon != null) _weapon.WeaponChanged += OnWeaponChanged;
        GameUI.Bind(this);
    }

    void OnDisable()
    {
        if (_weapon != null) _weapon.WeaponChanged -= OnWeaponChanged;
        GameUI.Unbind(this);
    }

    void OnWeaponChanged(WeaponType _) => WeaponChanged?.Invoke();

    // ── v1.5 무기 획득 (상점) → 정명진 님 PlayerWeapon (UIWeapon과 WeaponType 순서 같음) ──
    public bool CanAcquire(UIWeapon weapon) => _weapon != null && _weapon.CanAcquire((WeaponType)(int)weapon);
    public bool TryAcquire(UIWeapon weapon) => _weapon != null && _weapon.TryAcquire((WeaponType)(int)weapon);

    // 무기 종류 이름이 들어간 평타 컴포넌트 (SwordComboAttack / StaffAttack / PunchAttack)
    T CurrentAttack<T>(T[] list) where T : class
    {
        string key = CurrentWeapon == UIWeapon.Sword ? "Sword" : CurrentWeapon == UIWeapon.Staff ? "Staff" : "Punch";
        foreach (var a in list)
            if (a is MonoBehaviour mb && mb != null && mb.GetType().Name.Contains(key)) return a;
        return null;
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
        foreach (var w in FindObjectsByType<PlayerWeapon>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (w.GetComponent<PlayerWeaponBridge>() == null) w.gameObject.AddComponent<PlayerWeaponBridge>(); // Play 중에만 붙음
    }
}
