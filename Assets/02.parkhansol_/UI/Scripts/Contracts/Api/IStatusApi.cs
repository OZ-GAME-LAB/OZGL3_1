using System;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// [v1.1] 상태창 (Tab). 캐릭터 · 퀘스트 · 상점 · 지도 4탭, 열려 있는 동안 전투 정지.
    ///   캐릭터 탭 = 계열(물리/마법) + 스탯·기술 [+] 투자(성장 포인트, IPointsSource) + Q/E/R 장착
    ///   상점 탭   = 아이템 구매(아이템 포인트, IShopSource.Currency) + 내 아이템 한눈에
    ///   GameUI.Status.Open(StatusTab.Character); // 튜토리얼 "첫 투자" 안내 (스탯·기술 [+])
    ///   GameUI.Status.TabChanged += t => ...; // 튜토리얼 탭 설명 진행
    ///   GameUI.Status.SetItemShop(shopData);  // 상점 탭 판매 목록 바꾸기 (기본은 빌더가 넣은 목록)
    /// 키: Tab = 마지막 탭, K = 캐릭터 탭, M = 지도 탭 (UIInputRouter)
    /// </summary>
    public interface IStatusApi
    {
        bool IsOpen { get; }
        StatusTab CurrentTab { get; }
        void Open(StatusTab tab);
        void Close();
        /// <summary>상점 탭 판매 목록 (가격·최대 보유)</summary>
        void SetItemShop(ShopData shop);
        event Action<StatusTab> TabChanged;
    }

    internal sealed class NullStatusApi : IStatusApi
    {
        public static readonly NullStatusApi Instance = new NullStatusApi();
        public bool IsOpen => false;
        public StatusTab CurrentTab => StatusTab.Character;
        public void Open(StatusTab tab) => NullLog.Once("Status.Open");
        public void Close() { }
        public void SetItemShop(ShopData shop) { }
        public event Action<StatusTab> TabChanged { add { } remove { } }
    }
}
