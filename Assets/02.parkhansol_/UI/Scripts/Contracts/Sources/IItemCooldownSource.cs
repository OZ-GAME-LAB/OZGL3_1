using System;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// [v0.7 · 선택] 소모품 1~4 칸 쿨타임. IItemSource를 구현한 같은 오브젝트에 추가로 구현하면
    /// 아이템 칸도 스킬 칸처럼 어둡게 + 도는 쿨타임 + 완료 점멸이 나온다. 구현 안 하면 쿨타임 표시 없음.
    /// </summary>
    public interface IItemCooldownSource
    {
        float GetItemCooldownRemaining(int slotIndex);
        float GetItemCooldownDuration(int slotIndex);
        /// <summary>(slotIndex, 대기시간 초) — 사용 직후</summary>
        event Action<int, float> ItemCooldownStarted;
    }
}
