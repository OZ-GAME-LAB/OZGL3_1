using System;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// [플레이어/코어 담당 구현] 1~4 고정 소모품 슬롯과 버프.
    /// slotIndex는 0~3 (화면 표시 1~4).
    /// </summary>
    public interface IItemSource
    {
        int SlotCount { get; }
        ItemData GetItem(int slotIndex);
        int GetCount(int slotIndex);

        /// <summary>(slotIndex, 새 수량) — 획득·사용·재도전 복구 모두</summary>
        event Action<int, int> CountChanged;
        /// <summary>사용 성공 (사용 연출)</summary>
        event Action<int> ItemUsed;
        /// <summary>수량 0에서 사용 시도 (흔들림)</summary>
        event Action<int> ItemUseFailed;

        /// <summary>버프 시작 또는 남은 시간 갱신 (item, 지속시간 초) — 기획서: 중첩 없이 갱신</summary>
        event Action<ItemData, float> BuffApplied;
        event Action<ItemData> BuffEnded;
    }
}
