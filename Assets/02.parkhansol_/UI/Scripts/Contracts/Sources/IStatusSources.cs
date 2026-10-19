using System;
using UnityEngine;

namespace OZ.UI.Contracts
{
    // ─────────────────────────────────────────────────────────────────────────────
    // [v1.0 · 기획서 v0.5 상태창] 포인트 · 스탯 · 기술(구매·장착) · 지도 노드.
    // 팀원 코드와 1:1로 맞춘 계약 (UI는 읽고 요청만 보낸다):
    //   IPointsSource      ← 포인트 지갑 (코어: 각성 1000 + 퀘스트 보상)
    //   IStatSource        ← PlayerStats  (GetLevel / GetMaxLevel / GetValue / GetNextValue / StatChanged)
    //   ISkillLoadoutSource← PlayerSkill  (보유 · Q/E/R 장착 · 쿨다운 · 사용 실패)
    //   IGrowthShopSource  ← 상점 로직    (가격 확인 → 포인트 차감 → TryAddLevel / TryAddOwned)
    //   IStageMapSource    ← 코어         (노드 4개 해금·이동)
    // 한 오브젝트가 여러 개를 구현해도 된다. 등록은 다른 소스와 똑같이 GameUI.Bind(this).
    // 참고 구현: Samples/DummyPlayer (기획데이터 v0.5 수치)
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>스탯 4종 — 순서는 팀원 StatType(Strength, Vitality, Agility, Magic)과 같다 → (StatKind)(int)type 로 변환</summary>
    public enum StatKind
    {
        Strength = 0, // 힘: 주먹·검술 물리 피해
        Vitality = 1, // 체력: 최대 HP
        Agility = 2,  // 민첩: 이동·주먹 공격 속도
        Magic = 3,    // 마력: 마법 피해
    }

    /// <summary>기술 피해 유형 — 팀원 SkillDamageType(Physical, Magic)과 같은 순서</summary>
    public enum SkillDamageKind
    {
        Physical = 0,
        Magic = 1,
    }

    /// <summary>상태창 탭 (기획서 4장 순서)</summary>
    public enum StatusTab
    {
        Stats = 0,
        Quests = 1,
        Shop = 2,
        Skills = 3,
        Map = 4,
    }

    /// <summary>기술 한 개의 표시 정보 (PlayerSkillData에서 옮겨 담으면 된다)</summary>
    [Serializable]
    public struct SkillOffer
    {
        public string id;
        public string displayName;
        [TextArea(1, 3)] public string description;
        public Sprite icon;
        public SkillDamageKind damageKind;
        public int price;
        public float coefficient;
        public float cooldown;

        public bool IsValid => !string.IsNullOrEmpty(id);
    }

    /// <summary>포인트 지갑 (기획데이터 1장: 각성 1000, 이후 퀘스트 보상만)</summary>
    public interface IPointsSource
    {
        int Points { get; }
        /// <summary>(새 값, 변화량) — 보상 +500 연출용</summary>
        event Action<int, int> PointsChanged;
    }

    /// <summary>스탯 단계 (PlayerStats). 레벨 = 구매 횟수</summary>
    public interface IStatSource
    {
        int GetStatLevel(StatKind stat);
        int GetStatMaxLevel(StatKind stat);
        /// <summary>지금 효과값 (힘 20 → 23 …, 민첩은 0.03 = +3%)</summary>
        float GetStatValue(StatKind stat);
        /// <summary>한 번 더 사면 될 값 (상한이면 지금 값)</summary>
        float GetStatNextValue(StatKind stat);
        event Action<StatKind> StatChanged;
    }

    /// <summary>기술 보유 · Q/E/R 장착 · 쿨다운 (PlayerSkill)</summary>
    public interface ISkillLoadoutSource
    {
        /// <summary>상점에 나오는 기술 전체 (기획서: 6종)</summary>
        int SkillCount { get; }
        SkillOffer GetSkill(int index);
        bool IsOwned(string skillId);
        /// <summary>그 기술의 지금 총피해 (공격력 × 계수, 반올림)</summary>
        int GetDamage(string skillId);

        /// <summary>장착된 기술 id (빈 칸이면 null)</summary>
        string GetEquipped(SkillSlot slot);
        /// <summary>다른 칸에 있던 같은 기술은 빠진다. 보유하지 않았으면 false</summary>
        bool TryEquip(SkillSlot slot, string skillId);
        void Unequip(SkillSlot slot);

        float GetCooldownRemaining(SkillSlot slot);
        float GetCooldownDuration(SkillSlot slot);

        event Action<string> SkillOwned;
        event Action<SkillSlot> SlotChanged;
        event Action<SkillSlot, float> CooldownStarted;
        event Action<SkillSlot, SkillUseFailReason> SkillUseFailed;
    }

    /// <summary>상태창 상점: 스탯·기술 구매 (가격 확인 → 포인트 차감 → 단계/보유 추가)</summary>
    public interface IGrowthShopSource
    {
        /// <summary>다음 1회 구매 가격 (기획데이터 4장: 200 / 250 / 300 …). 상한이면 0</summary>
        int GetStatPrice(StatKind stat);
        ShopBuyResult TryBuyStat(StatKind stat);
        ShopBuyResult TryBuySkill(string skillId);
    }

    /// <summary>지도 탭의 노드 하나 (기획서 5장: 1스테이지 → 1보스 → 2스테이지 → 2보스)</summary>
    [Serializable]
    public struct StageNode
    {
        public string id;
        public string displayName;
        public bool isBoss;
        public bool unlocked;
        public bool cleared;
        public bool current;
        [Tooltip("잠겨 있을 때 보일 조건 (예: 1스테이지 메인 완료)")]
        public string lockCondition;
        [Tooltip("메인 목표 한 줄 (예: 일반 게이트 3개 직접 봉쇄)")]
        public string mainGoal;
    }

    /// <summary>지도 탭 — 해금된 노드를 골라 이동 (코어가 씬 이동)</summary>
    public interface IStageMapSource
    {
        int NodeCount { get; }
        StageNode GetNode(int index);
        /// <summary>이동 요청. 잠긴 노드·지금 있는 노드면 false. 실제 이동(페이드·씬 교체)은 구현 쪽</summary>
        bool TryTravel(string nodeId);
        event Action NodesChanged;
    }

    /// <summary>상태창 표시 문자열·계산 공용</summary>
    public static class StatusText
    {
        public static string StatName(StatKind s)
        {
            switch (s)
            {
                case StatKind.Strength: return "힘";
                case StatKind.Vitality: return "체력";
                case StatKind.Agility: return "민첩";
                case StatKind.Magic: return "마력";
                default: return "";
            }
        }

        public static string StatDesc(StatKind s)
        {
            switch (s)
            {
                case StatKind.Strength: return "주먹·검술 물리 피해";
                case StatKind.Vitality: return "최대 HP";
                case StatKind.Agility: return "이동·주먹 공격 속도";
                case StatKind.Magic: return "마법 피해";
                default: return "";
            }
        }

        /// <summary>스탯 효과값 표시 (힘 "물리 공격력 23", 체력 "최대 HP +24", 민첩 "+6%")</summary>
        public static string StatValue(StatKind s, float v)
        {
            switch (s)
            {
                case StatKind.Strength: return $"물리 공격력 {v:0}";
                case StatKind.Vitality: return $"최대 HP +{v:0}";
                case StatKind.Agility: return $"속도 +{v * 100f:0}%";
                case StatKind.Magic: return $"마법 공격력 {v:0}";
                default: return v.ToString("0");
            }
        }

        public static string DamageKind(SkillDamageKind k) => k == SkillDamageKind.Magic ? "마법" : "물리";
    }
}
