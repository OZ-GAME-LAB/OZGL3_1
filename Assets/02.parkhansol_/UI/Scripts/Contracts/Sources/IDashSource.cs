namespace OZ.UI.Contracts
{
    /// <summary>
    /// [플레이어 담당 구현 · v0.8] 대시 상태. 플레이어 옆 게이지(PlayerSideGaugeView)가 매 프레임 읽는다 (이벤트 필요 없음).
    /// 게이지에는 '지금 쓸 수 있는 대시 횟수'만큼 동그라미가 뜬다 — 쓰면 하나 사라지고, 다시 차면 짧게 깜빡이며 나타남.
    /// GameUI.Bind(this)로 등록. MainScene은 GameBridge/PlayerDashBridge가 PlayerController 값을 읽어 대신 등록해 준다.
    /// </summary>
    public interface IDashSource
    {
        /// <summary>지금 쓸 수 있는 대시 횟수 (= 보이는 동그라미 수)</summary>
        int DashCharges { get; }
        /// <summary>최대 대시 횟수 (1~8)</summary>
        int MaxDashCharges { get; }
        /// <summary>다음 대시까지 남은 시간(초). 0이면 대기 끝 (표시용 참고값)</summary>
        float DashCooldownRemaining { get; }
        /// <summary>대시 대기 시간 전체(초)</summary>
        float DashCooldownDuration { get; }
        /// <summary>지금 대시 가능한지 (대기 시간 + 공중 대시 횟수 등 모두 포함)</summary>
        bool CanDash { get; }
    }
}
