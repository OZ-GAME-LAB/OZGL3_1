using System;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// [v1.0 · 임시 상점] 상점 창.
    ///   GameUI.Shop.Open(shopData);                       // NPC에서 (보통은 ShopkeeperLink가 대신 부름)
    ///   GameUI.Shop.Open(shopData, () => npc.SayBye());  // 닫힐 때 콜백
    /// 구매 처리는 UISources.Shop (IShopSource)로 요청만 보낸다.
    /// </summary>
    public interface IShopApi
    {
        bool IsOpen { get; }
        void Open(ShopData shop, Action onClosed = null);
        void Close();
    }

    internal sealed class NullShopApi : IShopApi
    {
        public static readonly NullShopApi Instance = new NullShopApi();
        public bool IsOpen => false;
        public void Open(ShopData shop, Action onClosed = null)
        {
            NullLog.Once("Shop.Open");
            onClosed?.Invoke();
        }
        public void Close() { }
    }
}
