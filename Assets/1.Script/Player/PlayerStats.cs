using OZ.UI.Contracts;
using System;
using UnityEngine;






// 스탯 1개의 밸런스 값. 최종값 = baseValue + perLevel x 레벨
[Serializable]
public struct StatConfig
{
    public float baseValue;  // 레벨 0일 때 값
    public float perLevel;  // 레벨당 증가량
    public int maxLevel;   // 최대 레벨

    public StatConfig(float baseValue, float perLevel, int maxLevel)
    {
        this.baseValue = baseValue;
        this.perLevel = perLevel;
        this.maxLevel = maxLevel;
    }
}


// 스탯 레벨(투자 단계)만 저장하고, 공격력·최대 HP 같은 실제 값은 레벨로 계산한다.
// 구매(가격, 포인트 차감)는 상점이 담당하고, 스탯은 레벨을 올려 달라는 요청만 받는다.
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(PlayerHealth))]
public class PlayerStats : MonoBehaviour, IStatSource
{
    [Header("Stat Config")]
    [SerializeField] private StatConfig _strength = new StatConfig(20f, 3f, 5);    // 물리 공격력 20, +3, 최대 5
    [SerializeField] private StatConfig _vitality = new StatConfig(0f, 12f, 5);    // 추가 최대 HP 0, +12, 최대 5 (기본 HP는 PlayerHealth)
    [SerializeField] private StatConfig _agility = new StatConfig(0f, 0.03f, 3);   // 추가 속도 0%, +3%, 최대 3
    [SerializeField] private StatConfig _magic = new StatConfig(20f, 3f, 5);       // 마법 공격력 20, +3, 최대 5

    [Header("Apply Target")]
    [SerializeField] private PunchAttack _punchAttack; // 비우면 같은 오브젝트에서 찾음

    private PlayerController _player;
    private PlayerHealth _health;

    // StatType 순서대로 레벨 저장 (enum 값을 인덱스로 사용)
    private readonly int[] _levels = new int[Enum.GetValues(typeof(StatType)).Length];

    // 레벨이 바뀐 스탯 (상태창 갱신용)
    public event Action<StatType> StatChanged;

    // 같은 이름으로 형식만 다른 이벤트 두 개는 못 만듦
    private event Action<StatKind> _uiStatChanged;
    event Action<StatKind> IStatSource.StatChanged
    {
        add { _uiStatChanged += value; }   // UI가 += 하면 여기로
        remove { _uiStatChanged -= value; } // UI가 -= 하면 여기로
    }

    public float PhysicalAttack { get { return GetValue(StatType.Strength); } }
    public float BonusMaxHP { get { return GetValue(StatType.Vitality); } }
    public float AgilityBonus { get { return GetValue(StatType.Agility); } } // 0.09 = +9%
    public float MagicAttack { get { return GetValue(StatType.Magic); } }

    private static StatType ToType(StatKind kind) { return (StatType)(int)kind; }
    private static StatKind ToKind(StatType type) { return (StatKind)(int)type; }

    public int GetStatLevel(StatKind stat) { return GetLevel(ToType(stat)); }
    public int GetStatMaxLevel(StatKind stat) { return GetMaxLevel(ToType(stat)); }
    public float GetStatValue(StatKind stat) { return GetValue(ToType(stat)); }
    public float GetStatNextValue(StatKind stat) { return GetNextValue(ToType(stat)); }

    private void Awake()
    {
        _player = GetComponent<PlayerController>();
        _health = GetComponent<PlayerHealth>();

        if (_punchAttack == null)
        {
            _punchAttack = GetComponent<PunchAttack>();
        }
    }
    private void OnEnable()
    {
        GameUI.Bind(this);
    }

    private void OnDisable()
    {
        GameUI.Unbind(this);
    }


    // 다른 컴포넌트의 Awake(체력 초기화 등)가 끝난 뒤 적용
    private void Start()
    {
        ApplyAll();
    }

    public int GetLevel(StatType type)
    {
        return _levels[(int)type];
    }

    public int GetMaxLevel(StatType type)
    {
        return GetConfig(type).maxLevel;
    }

    public bool IsMaxLevel(StatType type)
    {
        return GetLevel(type) >= GetMaxLevel(type);
    }

    public float GetValue(StatType type)
    {
        StatConfig config = GetConfig(type);
        return config.baseValue + config.perLevel * GetLevel(type);
    }

    // 다음 레벨 값 (상점의 "구매 시 효과" 표시용)
    public float GetNextValue(StatType type)
    {
        if (IsMaxLevel(type))
        {
            return GetValue(type);
        }

        StatConfig config = GetConfig(type);
        return config.baseValue + config.perLevel * (GetLevel(type) + 1);
    }

    // 상점이 IsMaxLevel과 포인트를 먼저 확인·차감한 뒤 호출. 상한이면 false
    public bool TryAddLevel(StatType type)
    {
        if (IsMaxLevel(type))
        {
            return false;
        }

        _levels[(int)type]++;
        Apply(type);
        NotifyStatChanged(type);
        return true;
    }

    // 저장 데이터 불러오기 / 저장용
    public int[] GetLevels()
    {
        return (int[])_levels.Clone();
    }

    public void SetLevels(int[] levels)
    {
        if (levels == null)
        {
            Debug.LogError("[PlayerStats:SetLevels] 레벨 데이터가 없습니다.");
            return;
        }

        for (int i = 0; i < _levels.Length && i < levels.Length; i++)
        {
            StatType type = (StatType)i;
            _levels[i] = Mathf.Clamp(levels[i], 0, GetMaxLevel(type));
        }

        ApplyAll();
        foreach (StatType type in Enum.GetValues(typeof(StatType)))
        {
            NotifyStatChanged(type);
        }
    }

    // 새 게임: 모든 스탯 레벨 0
    public void ResetAll()
    {
        SetLevels(new int[_levels.Length]);
    }

    private void NotifyStatChanged(StatType type)
    {
        StatChanged?.Invoke(type);
        _uiStatChanged?.Invoke(ToKind(type));
    }

    // 상점이 생기기 전 테스트용: 플레이 중 인스펙터의 컴포넌트 ⋮ 메뉴에서 실행
    [ContextMenu("Test/힘 +1")] private void TestStrength() { LogTest(StatType.Strength); }
    [ContextMenu("Test/체력 +1")] private void TestVitality() { LogTest(StatType.Vitality); }
    [ContextMenu("Test/민첩 +1")] private void TestAgility() { LogTest(StatType.Agility); }
    [ContextMenu("Test/마력 +1")] private void TestMagic() { LogTest(StatType.Magic); }

    private void LogTest(StatType type)
    {
        bool added = TryAddLevel(type);
        Debug.Log($"[PlayerStats] {type} Lv.{GetLevel(type)}/{GetMaxLevel(type)} 값 {GetValue(type)} {(added ? "" : "(상한)")}");
    }

    private StatConfig GetConfig(StatType type)
    {
        switch (type)
        {
            case StatType.Strength: return _strength;
            case StatType.Vitality: return _vitality;
            case StatType.Agility: return _agility;
            case StatType.Magic: return _magic;
        }

        Debug.LogError("[PlayerStats:GetConfig] 알 수 없는 스탯입니다.");
        return default;
    }

    private void ApplyAll()
    {
        foreach (StatType type in Enum.GetValues(typeof(StatType)))
        {
            Apply(type);
        }
    }

    // 바뀐 스탯을 실제로 쓰는 컴포넌트에 전달
    private void Apply(StatType type)
    {
        switch (type)
        {
            case StatType.Strength:
                if (_punchAttack != null)
                {
                    _punchAttack.SetAttackPower(PhysicalAttack);
                }
                break;

            case StatType.Vitality:
                _health.SetBonusMaxHP(BonusMaxHP);
                break;

            case StatType.Agility:
                if (_punchAttack != null)
                {
                    _punchAttack.SetSpeedBonus(AgilityBonus);
                }
                _player.SetMoveSpeedBonus(AgilityBonus);
                break;

            case StatType.Magic:
                // 마법 스킬이 사용 시점에 MagicAttack을 읽어 감
                break;
        }
    }
}
