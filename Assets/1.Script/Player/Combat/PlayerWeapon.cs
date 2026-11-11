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
// -> 획득 쪽 코드는 CanAcquire로 확인하고 TryAcquire만 부르면 되게
// (Equip은 "무기 하나만" 규칙을 검사하지 않아서 private -> 내부·디버그용)
[RequireComponent(typeof(PlayerController))]
public class PlayerWeapon : MonoBehaviour
{
    [Serializable]
    private struct WeaponEntry
    {
        public WeaponType type;
        public MonoBehaviour attack; // IPlayerAttack 구현 컴포넌트 (예: SwordComboAttack)
        public GameObject prop;      // 무기 모델. 장착 중에만 켬 (검 = WeaponSocket 아래, 스태프 = 왼손 뼈 아래)

        // 무기별 모션(Idle, 공격 등)을 바꿔 끼우는 컨트롤러. 비우면 기본 컨트롤러 사용
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

    // 불러오기가 Start보다 먼저 오면 Start의 ApplyWeapon(_startWeapon)이 맨손으로 덮어씀
    // -> Start 전이면 받아만 두고 Start에서 적용
    private bool _started;
    private bool _hasPendingWeapon;
    private WeaponType _pendingWeapon;

    public WeaponType Current { get; private set; }
    public event Action<WeaponType> WeaponChanged; // UI(PlayerWeaponBridge → HUD 평타 칸)가 구독

    // 무기를 이미 골랐는지
    //     Start 전에 불러오기(LoadSaveData)가 오면 아직 Current에 반영 전
    //     -> 그 사이에 물어봐도 불러온 무기 기준으로 답하게
    public bool HasWeapon { get { return GetSaveData() != WeaponType.None; } }

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
        // 불러온 무기가 있으면 그걸로, 없으면 시작 무기로
        _started = true;
        ApplyWeapon(_hasPendingWeapon ? _pendingWeapon : _startWeapon);
        _hasPendingWeapon = false;
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

    // 무기 교체 (내부·디버그용). 바꿨으면 true
    // 획득은 TryAcquire를 쓸 것 -> Equip은 "무기 하나만" 규칙을 검사하지 않음
    private bool Equip(WeaponType type)
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

    // 무기 획득 (상점 구매 / 필드 습득 / 보상 공용)
    // 획득 방식은 아직 미정이지만 결국 "무기 하나를 얻는다"는 건 같음
    //     -> 얻게 해주는 쪽(상점, 아이템)이 CanAcquire로 확인하고 TryAcquire만 부르면 끝
    //     -> "무기는 하나만" 규칙이 여기 한 곳에 있어서, 어디서 불러도 똑같이 지켜짐
    public bool CanAcquire(WeaponType type) // 얻을 수 있는지 (UI 상점 버튼 잠금용)
    {
        // 맨손은 얻는 대상이 아님
        if (type == WeaponType.None)
        {
            return false;
        }

        // 이미 무기를 골랐으면 다른 무기든 같은 무기든 거절
        if (HasWeapon)
        {
            return false;
        }

        // 프리팹에 등록 안 된 무기(원본 Player 프리팹의 스태프 등)는 장착해도 평타가 없음
        return FindAttack(type) != null;
    }

    // 획득. 성공하면 true
    // 실패했을 때 상점은 포인트를 깎지 않고, 필드 아이템은 사라지지 않아야 함
    // -> 호출한 쪽이 결과를 보고 처리할 수 있게
    public bool TryAcquire(WeaponType type)
    {
        if (!CanAcquire(type))
        {
            return false;
        }

        // Equip은 공격 중이면 false (휘두르는 도중 무기가 바뀌면 콤보·모델이 꼬임)
        // -> 공격 중 줍기는 실패 처리, 다시 닿으면 획득됨
        return Equip(type);
    }

    // 저장용. Start 전에 불러온 값이 있으면 아직 Current에 반영 전이라 그 값을 돌려줌
    public WeaponType GetSaveData()
    {
        return _hasPendingWeapon ? _pendingWeapon : Current;
    }

    // 불러오기용. Equip과 달리 공격 중 체크를 하지 않음
    // 불러오기는 입력이 아니라 "저장된 상태로 맞추는" 것이라 막히면 안 됨
    public void LoadSaveData(WeaponType type)
    {
        // 등록 안 된 무기(원본 Player 프리팹에서 스태프 등)면 평타가 null이 됨 -> 맨손으로
        if (type != WeaponType.None && FindAttack(type) == null)
        {
            Debug.LogWarning($"[PlayerWeapon:LoadSaveData] {type} 공격이 등록되지 않아 맨손으로 불러옵니다.");
            type = WeaponType.None;
        }

        if (!_started)
        {
            _pendingWeapon = type;
            _hasPendingWeapon = true;
            return;
        }

        ApplyWeapon(type);
    }

    // 새 게임. 시작 무기로 되돌림 (이전 판에서 고른 무기가 남지 않게)
    public void ResetAll()
    {
        LoadSaveData(_startWeapon);
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

    // 테스트용: 플레이 중 인스펙터 PlayerWeapon 컴포넌트 ⋮ 메뉴에서 실행
    // 획득 방식(상점/습득)이 연결되기 전 확인용 -> 연결되면 삭제
    [ContextMenu("Test/Acquire Sword")]
    private void TestAcquireSword()
    {
        Debug.Log($"[PlayerWeapon] 검 획득: {TryAcquire(WeaponType.Sword)} (현재 {Current})");
    }

    [ContextMenu("Test/Acquire Staff")]
    private void TestAcquireStaff()
    {
        Debug.Log($"[PlayerWeapon] 스태프 획득: {TryAcquire(WeaponType.Staff)} (현재 {Current})");
    }
}
