using System;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// [v1.0 · 기획서 v0.5] 상태창 (Tab). 스탯 · 퀘스트 · 상점 · 스킬 · 지도 5탭, 열려 있는 동안 전투 정지.
    ///   GameUI.Status.Open(StatusTab.Shop);   // 튜토리얼 "첫 구매" 안내 등
    ///   GameUI.Status.TabChanged += t => ...; // 튜토리얼 탭 설명 진행
    /// 키: Tab = 마지막 탭, K = 스킬 탭, M = 지도 탭 (UIInputRouter)
    /// </summary>
    public interface IStatusApi
    {
        bool IsOpen { get; }
        StatusTab CurrentTab { get; }
        void Open(StatusTab tab);
        void Close();
        event Action<StatusTab> TabChanged;
    }

    internal sealed class NullStatusApi : IStatusApi
    {
        public static readonly NullStatusApi Instance = new NullStatusApi();
        public bool IsOpen => false;
        public StatusTab CurrentTab => StatusTab.Stats;
        public void Open(StatusTab tab) => NullLog.Once("Status.Open");
        public void Close() { }
        public event Action<StatusTab> TabChanged { add { } remove { } }
    }
}
