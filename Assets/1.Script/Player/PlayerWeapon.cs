using System;
using UnityEngine;
using UnityEngine.InputSystem;


// 스태프는 공격 방식·모션이 정해지면 StaffAttack(IPlayerAttack)만 만들어 목록에 추가
public enum WeaponType
{
    None,  // 맨손 (주먹)
    Sword,
    Staff
}

// 현재 무기 상태만 관리. 무기가 바뀌면 평타, 무기 모델, 애니메이터를 한 번에 맞춤.
// 왜 따로 뺐나 -> 검을 "어떻게 얻는지"(상점/드롭 등)는 아직 미정
// -> 나중에 획득 쪽 코드는 Equip(WeaponType.Sword)만 부르면 되게
[RequireComponent(typeof(PlayerController))]
public class PlayerWeapon : MonoBehaviour
{
    [Serializable]
    private struct WeaponEntry
    {
        public WeaponType type;
        public MonoBehaviour attack; // IPlayerAttack 구현 컴포넌트 (예: SwordComboAttack)
        public GameObject prop;      // WeaponSocket 아래 무기 모델. 장착 중에만 켬
    }

    [Header("Weapon")]
    [SerializeField] private MonoBehaviour _unarmedAttack; // 맨손 평타 (PunchAttack)
    [SerializeField] private WeaponEntry[] _weapons;
    [SerializeField] private WeaponType _startWeapon = WeaponType.None;

    [Header("Debug (검 획득 방법이 정해지기 전 임시)")]
    [SerializeField] private bool _useDebugKey = true;
    [SerializeField] private Key _debugToggleKey = Key.T; // 맨손 <-> 검 전환

    private static readonly int WeaponHash = Animator.StringToHash("Weapon");

    private PlayerController _player;
    private Animator _animator;
    private bool _hasWeaponParam;

    public WeaponType Current { get; private set; }
    public event Action<WeaponType> WeaponChanged; // 나중에 UI(장비 표시)나 저장에서 구독

    private void Awake()
    {
        _player = GetComponent<PlayerController>();
        _animator = GetComponent<Animator>();

        // 원본 Player.controller(Humanoid)에는 Weapon 파라미터가 없음
        // -> 없는데 SetInteger를 부르면 매번 경고가 뜨므로 미리 확인
        _hasWeaponParam = false;
        foreach (AnimatorControllerParameter p in _animator.parameters)
        {
            if (p.nameHash == WeaponHash && p.type == AnimatorControllerParameterType.Int)
            {
                _hasWeaponParam = true;
            }
        }

        if (_unarmedAttack is not IPlayerAttack)
        {
            Debug.LogError("[PlayerWeapon:Awake] 맨손 공격(_unarmedAttack)이 IPlayerAttack이 아닙니다.");
        }
    }

    // Awake가 아니라 Start: PlayerController.Awake의 GetComponent<IPlayerAttack>()가
    // 검/주먹 중 아무거나 먼저 잡을 수 있음 -> 모든 Awake가 끝난 뒤 여기서 확정
    private void Start()
    {
        ApplyWeapon(_startWeapon);
    }

    private void Update()
    {
        if (!_useDebugKey || Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current[_debugToggleKey].wasPressedThisFrame)
        {
            WeaponType next = Current == WeaponType.None ? WeaponType.Sword : WeaponType.None;
            Equip(next);
        }
    }

    // 획득, 장착 쪽에서 호출. 바꿨으면 true
    public bool Equip(WeaponType type)
    {
        // 휘두르는 도중에 바꾸면 검 모델이 사라지거나 콤보가 꼬임 -> 공격 중엔 막음
        if (_player.CurrentStateType == PlayerState.Attack)
        {
            return false;
        }

        if (type == Current)
        {
            return true;
        }

        if (type != WeaponType.None && FindAttack(type) == null)
        {
            Debug.LogWarning($"[PlayerWeapon:Equip] {type} 공격이 등록되지 않았습니다.");
            return false;
        }

        ApplyWeapon(type);
        return true;
    }

    public void Unequip()
    {
        Equip(WeaponType.None);
    }

    private void ApplyWeapon(WeaponType type)
    {
        IPlayerAttack attack = type == WeaponType.None ? _unarmedAttack as IPlayerAttack : FindAttack(type);
        Current = type;

        // 장착한 무기 모델만 켜고 나머지는 끔
        foreach (WeaponEntry entry in _weapons)
        {
            if (entry.prop != null)
            {
                entry.prop.SetActive(entry.type == type);
            }
        }

        _player.SetAttack(attack);

        if (_hasWeaponParam)
        {
            _animator.SetInteger(WeaponHash, (int)type);
        }

        WeaponChanged?.Invoke(type);
    }

    private IPlayerAttack FindAttack(WeaponType type)
    {
        foreach (WeaponEntry entry in _weapons)
        {
            if (entry.type == type)
            {
                return entry.attack as IPlayerAttack;
            }
        }
        return null;
    }
}
