using System;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// [코어 담당 구현] 스테이지 게이트 진행 상황.
    /// 기획서 v0.2: 한 번에 한 곳만 활성, 봉쇄 수 누적, 목표 달성 시 보스 구역 개방.
    /// </summary>
    public interface IGateSource
    {
        int StageNumber { get; }
        int SealedCount { get; }
        int TargetCount { get; }
        bool IsGateActive { get; }
        bool IsBossAreaUnlocked { get; }

        event Action GateOpened;
        /// <summary>(봉쇄 수, 목표 수)</summary>
        event Action<int, int> GateSealed;
        event Action BossAreaUnlocked;
        /// <summary>재도전/스테이지 시작 등으로 전체 값이 초기화됐을 때</summary>
        event Action GateStateReset;
    }
}
