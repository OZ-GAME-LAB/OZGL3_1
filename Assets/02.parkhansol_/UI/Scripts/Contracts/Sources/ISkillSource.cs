using System;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// [플레이어 담당 구현] Q/E/R 스킬 상태. UI는 입력을 직접 읽지 않고 이 이벤트만 보고 그린다.
    /// 스킬 창의 포인트 투자 버튼은 CanInvest / TryInvest 를 호출한다.
    /// </summary>
    public interface ISkillSource
    {
        SkillData GetSkill(SkillSlot slot);
        /// <summary>0 = 미습득, 1~maxRank = 강화 단계</summary>
        int GetRank(SkillSlot slot);

        float GetCooldownRemaining(SkillSlot slot);
        float GetCooldownDuration(SkillSlot slot);

        /// <summary>투자 가능 여부. 불가능하면 reason에 표시 문구 (예: "레벨 4 필요")</summary>
        bool CanInvest(SkillSlot slot, out string reason);
        /// <summary>포인트 1회 투자(습득 또는 강화). 성공 시 SkillChanged 호출</summary>
        bool TryInvest(SkillSlot slot);

        /// <summary>스킬 사용 직후 (slot, 대기시간 초)</summary>
        event Action<SkillSlot, float> CooldownStarted;
        /// <summary>습득/강화/계열 변경 등으로 표시가 바뀔 때</summary>
        event Action<SkillSlot> SkillChanged;
        /// <summary>미습득·대기 중에 키를 눌렀을 때 (HUD 흔들림)</summary>
        event Action<SkillSlot, SkillUseFailReason> SkillUseFailed;
    }
}
