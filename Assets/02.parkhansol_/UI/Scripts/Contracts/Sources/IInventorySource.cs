using System;
using System.Collections.Generic;

namespace OZ.UI.Contracts
{
    /// <summary>인벤토리 한 칸 (1×1 슬롯)</summary>
    public readonly struct ItemStack
    {
        public readonly ItemData Item;
        public readonly int Count;

        public ItemStack(ItemData item, int count)
        {
            Item = item;
            Count = count;
        }

        public bool IsEmpty => Item == null || Count <= 0;
        public static readonly ItemStack Empty = new ItemStack(null, 0);
    }

    /// <summary>
    /// [코어 담당 구현] 인벤토리 (1×1 슬롯 그리드).
    /// UI는 칸을 그리고 이동/사용 요청만 보낸다. 실제 데이터 변경은 구현 쪽이 하고 Changed를 호출.
    /// 1~4 퀵슬롯(IItemSource)과 같은 오브젝트가 둘 다 구현해도 된다.
    /// </summary>
    public interface IInventorySource
    {
        int Capacity { get; }
        ItemStack GetSlot(int index);

        /// <summary>칸 이동/교환 (디아블로식 집기-놓기). 성공 시 Changed</summary>
        bool TryMove(int from, int to);
        /// <summary>사용 (소모품). 성공 시 Changed</summary>
        bool TryUse(int index);

        event Action Changed;
    }
}
