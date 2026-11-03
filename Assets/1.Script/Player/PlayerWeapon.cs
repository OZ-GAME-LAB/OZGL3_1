using System;
using UnityEngine;
using UnityEngine.InputSystem;


// 스태프는 StaffAttack(IComboAttack 1타)으로 구현해 목록에 추가 (2026-11-03)
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
        public GameObject prop;      // 무기 모델. 장착 중에만 켬 (검 = WeaponSocket 아래, 스태프 = 왼손 뼈 아래)

        // 무기별 모션(Idle, 공격 등)을 바꿔 끼우는 컨트롤러.비우면 기본 컨트롤러 사용
        // 무기는 한 번 고르면 끝까지 가므로, 상태를 무기마다 만들지 않고 클립만 교체
        // -> 이동·점프·사다리 같은 공통 상태는 기본 컨트롤러 하나만 고치면 모든 무기에 반영됨
        public AnimatorOverrideController animatorOverride;
    }

    [Header("Weapon")]
    [SerializeField] private MonoBehaviour _unarmedAttack; // 맨손 평타 (PunchAttack)
    [SerializeField] private WeaponEntry[] _weapons;
    [SerializeField] private WeaponType _startWeapon = WeaponType.None;

    [Header("Debug (검 획득 방법이 정해지기 전 임시)")]
    [SerializeField] private bool _useDebugKey = true;
    [SerializeField] private Key _debugToggleKey = Key.T; // 맨손 -> 검 -> 스태프 순환

    private static readonly int WeaponHash = Animator.StringToHash("Weapon");

    private PlayerController _player;
    private Animator _animator;
    private bool _hasWeaponParam;
    private RuntimeAnimatorController _baseController; // 맨손, Override 없는 무기일 때 쓸 원래 컨트롤러

    public WeaponType Current { get; private set; }
    public event Action<WeaponType> WeaponChanged; // 나중에 UI(장비 표시)나 저장에서 구독

    private void Awake()
    {
        _player = GetComponent<PlayerController>();
        _animator = GetComponent<Animator>();

        // 무기를 고르면 runtimeAnimatorController가 Override로 바뀌어서
        // 맨손으로 돌아갈 때(새 게임 등) 원래 컨트롤러를 다시 찾을 수 없음
        _baseController = _animator.runtimeAnimatorController;

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

        if(Keyboard.current[_debugToggleKey].wasPressedThisFrame)
        {
            WeaponType next = Current == WeaponType.None ? WeaponType.Sword
                            : Current == WeaponType.Sword ? WeaponType.Staff
                            : WeaponType.None;

            // 실패 시 맨손 -> 스태프가 등록 안 된 프리팹(원본 Player 등)에서
            // 검 다음 칸이 막혀 검에서 영영 못 빠져나오는 것 방지
            // (공격 중이라 실패한 경우는 맨손 장착도 똑같이 막혀서 아무 변화 없음)
            if (!Equip(next))
            {
                Equip(WeaponType.None);
            }
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

        // 무기별 모션 교체. 반드시 Weapon 파라미터 설정보다 먼저
        // 컨트롤러를 바꾸면 애니메이터 파라미터가 기본값(Weapon = 0)으로 초기화됨
        ApplyAnimatorController(type);

        if (_hasWeaponParam)
        {
            _animator.SetInteger(WeaponHash, (int)type);
        }

        WeaponChanged?.Invoke(type);
    }

    // 무기에 맞는 컨트롤러로 교체 (Override가 없으면 기본 컨트롤러)
    private void ApplyAnimatorController(WeaponType type)
    {
        RuntimeAnimatorController next = _baseController;

        foreach (WeaponEntry entry in _weapons)
        {
            if (entry.type == type && entry.animatorOverride != null)
            {
                next = entry.animatorOverride;
            }
        }

        // 같은 컨트롤러를 다시 넣으면 재생 중인 모션이 처음부터 다시 시작됨 -> 바뀔 때만 교체
        if (next != null && _animator.runtimeAnimatorController != next)
        {
            _animator.runtimeAnimatorController = next;
        }
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
