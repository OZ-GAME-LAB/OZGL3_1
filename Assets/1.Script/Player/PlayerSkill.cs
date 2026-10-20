using OZ.UI.Contracts;
using System;
using System.Collections.Generic;
using UnityEngine;

// 보유 스킬, Q/E/R 장착, 쿨다운, 피해 계산 담당.
// 구매(가격, 포인트 차감)는 상점이 담당하고, 여기는 보유 추가 요청만 받는다.
[RequireComponent(typeof(PlayerStats))]
public class PlayerSkill : MonoBehaviour, ISkillLoadoutSource
{
    public const int SlotCount = 3;

    private PlayerStats _stats;

    private readonly Dictionary<string, PlayerSkillBase> _allSkills = new Dictionary<string, PlayerSkillBase>();
    // UI는 번호로 읽는데 Dictionary는 순서 보장이 안 됨 -> 등록 순서(하이어라키 순서)를 리스트로 고정
    private readonly List<PlayerSkillBase> _skillList = new List<PlayerSkillBase>();
    private readonly HashSet<string> _owned = new HashSet<string>(); // 중복으로 보유 못하도록
    private readonly PlayerSkillBase[] _equipped = new PlayerSkillBase[SlotCount];

    // 스킬 id 기준 쿨다운 종료 시각. 장착을 바꿔도 남은 쿨다운 유지
    private readonly Dictionary<string, float> _cooldownEndTime = new Dictionary<string, float>();

    public event Action<string> SkillOwned;                            // 상점·스킬 창 갱신
    public event Action<SkillSlot> SlotChanged;                        // HUD 슬롯 아이콘 갱신
    public event Action<SkillSlot, float> CooldownStarted;             // UI에 알림 -> HUD 쿨다운 게이지
    public event Action<SkillSlot, SkillUseFailReason> SkillUseFailed; // UI에 알림 -> HUD 아이콘 흔들기

    public IEnumerable<PlayerSkillBase> AllSkills { get { return _skillList; } }
    public int SkillCount { get { return _skillList.Count; } } // UI 전용

    private void Awake()
    {
        _stats = GetComponent<PlayerStats>();
        RegisterSkills();
    }

    private void OnEnable()
    {
        GameUI.Bind(this);
    }

    private void OnDisable()
    {
        GameUI.Unbind(this);
    }

    private void RegisterSkills()
    {
        foreach (PlayerSkillBase skill in GetComponentsInChildren<PlayerSkillBase>(true))
        {
            if (skill.Data == null)
            {
                Debug.LogError($"[PlayerSkill:RegisterSkills] {skill.name}에 PlayerSkillData가 없습니다.");
                continue;
            }
            if (_allSkills.ContainsKey(skill.Data.Id))
            {
                Debug.LogError($"[PlayerSkill:RegisterSkills] 스킬 id가 중복됩니다: {skill.Data.Id}");
                continue;
            }
            _allSkills.Add(skill.Data.Id, skill);
            _skillList.Add(skill);
        }
    }

    // -보유-
    public bool IsOwned(string id)
    {
        return _owned.Contains(id);
    }

    public int GetRank(string id)
    {
        return _owned.Contains(id) ? 1 : 0;
    }

    // 상점이 포인트를 확인·차감한 뒤 호출. 없는 스킬이거나 이미 보유면 false
    public bool TryAddOwned(string id)
    {
        if (!_allSkills.ContainsKey(id) || _owned.Contains(id))
        {
            return false;
        }
        _owned.Add(id);
        SkillOwned?.Invoke(id);
        return true;
    }

    // -장착-
    private bool IsValidSlot(SkillSlot slot)
    {
        int index = (int)slot;
        if (index < 0 || index >= SlotCount)
        {
            Debug.LogError($"[PlayerSkill] 잘못된 슬롯: {slot}");
            return false;
        }
        return true;
    }

    public PlayerSkillBase GetEquipped(SkillSlot slot)
    {
        if (!IsValidSlot(slot))
        {
            return null; // 호출하는 쪽에서 null = 빈 슬롯으로 처리
        }
        return _equipped[(int)slot];
    }

    // 다른 슬롯에 이미 있으면 그 슬롯에서 빼고 옮김 (같은 스킬 중복 장착 금지)
    public bool TryEquip(SkillSlot slot, string id)
    {
        if (!IsValidSlot(slot) || !_owned.Contains(id))
        {
            return false;
        }

        PlayerSkillBase skill = _allSkills[id];

        for (int i = 0; i < SlotCount; i++)
        {
            if (_equipped[i] == skill && i != (int)slot)
            {
                _equipped[i] = null;
                SlotChanged?.Invoke((SkillSlot)i);
            }
        }

        _equipped[(int)slot] = skill;
        SlotChanged?.Invoke(slot);
        return true;
    }

    public void Unequip(SkillSlot slot)
    {
        if (!IsValidSlot(slot))
        {
            return;
        }

        _equipped[(int)slot] = null;
        SlotChanged?.Invoke(slot);
    }

    // - 쿨다운 -
    public float GetCooldownRemaining(string id)
    {
        if (!_cooldownEndTime.TryGetValue(id, out float endTime))
        {
            return 0f;
        }

        return Mathf.Max(0f, endTime - Time.time);
    }

    public float GetCooldownRemaining(SkillSlot slot)
    {
        PlayerSkillBase skill = GetEquipped(slot);
        return skill == null ? 0f : GetCooldownRemaining(skill.Data.Id);
    }

    //쿨다운 전체 시간. HUD 원형 게이지가 "남은 시간 / 전체 시간" 비율로 그림
    public float GetCooldownDuration(SkillSlot slot)
    {
        PlayerSkillBase skill = GetEquipped(slot);
        return skill == null ? 0f : skill.Data.Cooldown;
    }

    // - 사용 -
    public bool CanUse(SkillSlot slot, out SkillUseFailReason reason)
    {
        reason = SkillUseFailReason.Blocked; // 기본값.

        // 상태창·일시정지 중에 Q를 눌러도 스킬이 안 나가게
        if (UIState.IsGameplayInputBlocked)
        {
            return false;
        }

        PlayerSkillBase skill = GetEquipped(slot);
        if (skill == null)
        {
            reason = SkillUseFailReason.NotLearned; // 빈 슬롯
            return false;
        }

        if (GetCooldownRemaining(skill.Data.Id) > 0f)
        {
            reason = SkillUseFailReason.Cooldown;
            return false;
        }

        return true;
    }

    public void NotifyUseFailed(SkillSlot slot, SkillUseFailReason reason)
    {
        SkillUseFailed?.Invoke(slot, reason);
    }

    // PlayerState_Skill에서 시전 시작 시 호출
    public void StartCooldown(SkillSlot slot)
    {
        PlayerSkillBase skill = GetEquipped(slot);
        if (skill == null)
        {
            return;
        }

        float cooldown = skill.Data.Cooldown;
        _cooldownEndTime[skill.Data.Id] = Time.time + cooldown;
        CooldownStarted?.Invoke(slot, cooldown);
    }

    // 총피해 = 해당 공격력 x 고정 계수, 반올림
    public float GetDamage(PlayerSkillBase skill)
    {
        float power = skill.Data.DamageType == SkillDamageType.Physical ? _stats.PhysicalAttack : _stats.MagicAttack;
        return Mathf.Round(power * skill.Data.DamageCoefficient);
    }

    public SkillOffer GetSkill(int index)
    {
        if (index < 0 || index >= _skillList.Count)
        {
            return default; // IsValid == false → UI가 빈 칸으로 처리
        }

        PlayerSkillData data = _skillList[index].Data;
        return new SkillOffer
        {
            id = data.Id,
            displayName = data.DisplayName,
            description = data.Description,
            icon = data.Icon, // 없으면 UI가 이름 첫 글자로 대신 표시
            // SkillDamageType과 SkillDamageKind는 순서(Physical=0, Magic=1)가 같음
            damageKind = (SkillDamageKind)(int)data.DamageType,
            price = data.Price,
            coefficient = data.DamageCoefficient,
            cooldown = data.Cooldown,
        };
    }

    string ISkillLoadoutSource.GetEquipped(SkillSlot slot)
    {
        PlayerSkillBase skill = GetEquipped(slot);
        return skill == null ? null : skill.Data.Id;
    }

    int ISkillLoadoutSource.GetDamage(string skillId)
    {
        if (!_allSkills.TryGetValue(skillId, out PlayerSkillBase skill))
        {
            return 0;
        }
        return Mathf.RoundToInt(GetDamage(skill));
    }


    // 상점이 생기기 전 테스트용: 전체 보유 + 앞에서부터 Q/E/R 장착
    [ContextMenu("Test/전체 보유 + 장착")]
    private void TestOwnAll()
    {
        int slot = 0;
        foreach (PlayerSkillBase skill in _skillList)
        {
            TryAddOwned(skill.Data.Id);
            if (slot < SlotCount)
            {
                TryEquip((SkillSlot)slot, skill.Data.Id);
                Debug.Log($"[PlayerSkill] {(SkillSlot)slot} ← {skill.Data.DisplayName}");
                slot++;
            }
        }
    }
    
    [ContextMenu("Test/물리 보유 + 장착")]
    private void TestEquipPhysical()
    {
        TestEquipByType(SkillDamageType.Physical);
    }

    [ContextMenu("Test/마법 보유 + 장착")]
    private void TestEquipMagic()
    {
        TestEquipByType(SkillDamageType.Magic);
    }

    private void TestEquipByType(SkillDamageType type)
    {
        int slot = 0;
        foreach (PlayerSkillBase skill in _skillList)
        {
            if (skill.Data.DamageType != type)
            {
                continue;
            }

            TryAddOwned(skill.Data.Id);

            if (slot < SlotCount)
            {
                TryEquip((SkillSlot)slot, skill.Data.Id);
                Debug.Log($"[PlayerSkill] {(SkillSlot)slot} ← {skill.Data.DisplayName}");
                slot++;
            }
        }
    }
}
