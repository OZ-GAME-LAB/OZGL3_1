using System;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// [코어/플레이어 담당 구현] 레벨·경험치·스킬 포인트·헌터 랭크·계열.
    /// </summary>
    public interface IProgressionSource
    {
        PlayerClass Class { get; }
        int Level { get; }
        float Exp { get; }
        /// <summary>현재 레벨에서 다음 레벨까지 필요한 경험치 (최대 레벨이면 0)</summary>
        float ExpToNextLevel { get; }
        int SkillPoints { get; }
        HunterRank Rank { get; }

        /// <summary>경험치/포인트 등 값이 바뀌면 호출 (UI는 전체를 다시 읽음)</summary>
        event Action ProgressionChanged;
        /// <summary>레벨업 순간 1회 (새 레벨) — 레벨업 연출용</summary>
        event Action<int> LevelUp;
        /// <summary>승급 순간 1회 (새 랭크) — 승급 연출용</summary>
        event Action<HunterRank> RankUp;
    }
}
