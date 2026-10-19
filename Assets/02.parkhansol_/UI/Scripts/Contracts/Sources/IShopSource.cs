using System;
using UnityEngine;

namespace OZ.UI.Contracts
{
    /// <summary>구매 결과 (UI가 문구·흔들림으로 표시)</summary>
    public enum ShopBuyResult
    {
        Success = 0,
        /// <summary>재화 부족</summary>
        NotEnoughCurrency = 1,
        /// <summary>보유 상한 (ShopEntry.maxOwned)</summary>
        LimitReached = 2,
        /// <summary>가방에 자리 없음</summary>
        InventoryFull = 3,
        /// <summary>데이터 없음·지금은 못 삼 (전투 중 등)</summary>
        Unavailable = 4,
    }

    /// <summary>
    /// [v1.0 · 코어/플레이어 담당 구현] 상점 구매. UI는 TryBuy 요청만 보내고, 재화 차감·아이템 지급은 구현 쪽이 한다.
    /// 1~4 퀵슬롯(IItemSource)·인벤토리(IInventorySource)와 같은 오브젝트가 함께 구현해도 된다.
    ///   GameUI.Bind(this);  // 다른 소스와 똑같이 등록
    /// 참고 구현: Samples/DummyPlayer
    /// </summary>
    public interface IShopSource
    {
        /// <summary>지금 가진 재화 (기획서: 포인트)</summary>
        int Currency { get; }
        /// <summary>이 아이템을 지금 몇 개 가지고 있나 (보유 상한 표시용)</summary>
        int GetOwned(ItemData item);
        /// <summary>구매 시도. 성공하면 재화·아이템을 바꾸고 ShopChanged를 부른다</summary>
        ShopBuyResult TryBuy(ShopEntry entry, int amount);
        /// <summary>재화 또는 보유 수량이 바뀜 (아이템 사용 포함)</summary>
        event Action ShopChanged;
    }

    /// <summary>UI와 구현 쪽이 같은 규칙을 쓰도록 모아 둔 계산</summary>
    public static class ShopRules
    {
        /// <summary>보유 상한 기준 앞으로 더 살 수 있는 개수 (상한 없으면 int.MaxValue)</summary>
        public static int RoomLeft(ShopEntry entry, int owned) =>
            entry == null ? 0 : entry.maxOwned <= 0 ? int.MaxValue : Mathf.Max(0, entry.maxOwned - owned);

        /// <summary>재화·보유 상한을 모두 고려한 최대 구매 수량</summary>
        public static int MaxBuyable(ShopEntry entry, int owned, int currency)
        {
            if (entry == null || entry.item == null) return 0;
            int byMoney = entry.price <= 0 ? int.MaxValue : currency / entry.price;
            return Mathf.Max(0, Mathf.Min(RoomLeft(entry, owned), byMoney));
        }

        /// <summary>재화·상한 검사 (가방 자리 검사는 구현 쪽)</summary>
        public static ShopBuyResult Check(ShopEntry entry, int amount, int owned, int currency)
        {
            if (entry == null || entry.item == null || amount <= 0) return ShopBuyResult.Unavailable;
            if (amount > RoomLeft(entry, owned)) return ShopBuyResult.LimitReached;
            if ((long)entry.price * amount > currency) return ShopBuyResult.NotEnoughCurrency;
            return ShopBuyResult.Success;
        }
    }
}
