using System;
using System.Collections.Generic;
using UnityEngine;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// 액티브 스킬 1개의 표시용/밸런스 데이터.
    /// 기획서 v0.2: 계열당 Q/E/R 3개 고정, 최대 3단계, 단계당 1포인트, 마나 없음(쿨타임만).
    /// 수치는 검증값이므로 이 에셋만 고치면 UI에 바로 반영된다.
    /// </summary>
    [CreateAssetMenu(menuName = "OZ/UI/Skill Data", fileName = "Skill_")]
    public class SkillData : ScriptableObject
    {
        [Tooltip("고유 ID (예: sword_q)")]
        public string id;
        public string displayName;
        [TextArea(2, 4)] public string description;
        public Sprite icon;

        [Header("배치")]
        public PlayerClass playerClass;
        public SkillSlot slot;

        [Header("습득/강화")]
        [Tooltip("습득 가능 레벨 (Q=1, E=2, R=4 검증값)")]
        [Min(1)] public int learnLevel = 1;
        [Tooltip("시작 시 1단계로 보유 (검증값: Q)")]
        public bool startsLearned;
        [Min(1)] public int maxRank = 3;
        [Min(0)] public int costPerRank = 1;

        [Tooltip("단계별 정보. index 0 = 1단계")]
        public List<SkillRankInfo> ranks = new List<SkillRankInfo>();

        /// <summary>rank: 1부터. 범위를 벗어나면 가장 가까운 단계 값을 돌려준다.</summary>
        public SkillRankInfo GetRank(int rank)
        {
            if (ranks == null || ranks.Count == 0) return default;
            int i = Mathf.Clamp(rank - 1, 0, ranks.Count - 1);
            return ranks[i];
        }

        public float GetCooldown(int rank) => GetRank(rank).cooldown;
    }

    [Serializable]
    public struct SkillRankInfo
    {
        [Tooltip("재사용 대기시간(초)")]
        [Min(0f)] public float cooldown;
        [Tooltip("스킬 창 '다음 효과'에 보일 한 줄 요약 (예: 피해 120%)")]
        public string summary;
    }
}
