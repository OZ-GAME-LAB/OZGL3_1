using UnityEngine;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// [v0.7] 지역 이름 — 화면 위쪽 가운데에 지역 이름이 부드럽게 떴다가 사라진다.
    ///   GameUI.Area.Show("서울 거리", "1스테이지");
    /// 레벨 배치는 코드 없이 AreaNameTrigger 컴포넌트(트리거 콜라이더)로.
    /// 같은 이름을 짧은 시간 안에 다시 부르면 무시 (경계에서 왔다 갔다 할 때 깜빡임 방지).
    /// </summary>
    public interface IAreaApi
    {
        void Show(string areaName, string subtitle = null, float holdSeconds = 2.2f);
        void Hide();
        /// <summary>마지막으로 표시한 지역 이름</summary>
        string Current { get; }
    }

    public enum IndicatorKind
    {
        Objective = 0,
        Gate = 1,
        Boss = 2,
    }

    /// <summary>
    /// [v0.7] 화면 밖 목표 화살표 — 대상이 화면 밖이면 화면 가장자리에 방향 화살표 + 아이콘 + 거리.
    ///   GameUI.Indicators.Track(gate.transform, IndicatorKind.Gate);
    ///   GameUI.Indicators.Untrack(gate.transform);
    /// 코드 없이: 대상에 ObjectiveMarkerLink 컴포넌트.
    /// </summary>
    public interface IIndicatorApi
    {
        void Track(Transform target, IndicatorKind kind = IndicatorKind.Objective, string label = null);
        void Untrack(Transform target);
        void Clear();
        bool IsTracking(Transform target);
        int Count { get; }
    }

    /// <summary>
    /// [v0.7] 상호작용 안내 — 대상 머리 위에 "[F] 조사". 한 번에 하나 (다른 대상을 Show하면 교체).
    ///   GameUI.Prompt.Show(door.transform, "열기");
    ///   GameUI.Prompt.Hide(door.transform);
    /// 코드 없이: 대상에 InteractPromptLink 컴포넌트. 실제 상호작용 입력은 게임 쪽에서 처리.
    /// </summary>
    public interface IPromptApi
    {
        void Show(Transform target, string action, string key = "F", Vector3? worldOffset = null);
        /// <summary>target이 지금 표시 중인 대상일 때만 숨김. null이면 무조건 숨김</summary>
        void Hide(Transform target = null);
        Transform Current { get; }
    }

    internal sealed class NullAreaApi : IAreaApi
    {
        public static readonly NullAreaApi Instance = new NullAreaApi();
        public string Current => null;
        public void Show(string areaName, string subtitle = null, float holdSeconds = 2.2f) => NullLog.Once("Area.Show");
        public void Hide() { }
    }

    internal sealed class NullIndicatorApi : IIndicatorApi
    {
        public static readonly NullIndicatorApi Instance = new NullIndicatorApi();
        public int Count => 0;
        public void Track(Transform target, IndicatorKind kind = IndicatorKind.Objective, string label = null) => NullLog.Once("Indicators.Track");
        public void Untrack(Transform target) { }
        public void Clear() { }
        public bool IsTracking(Transform target) => false;
    }

    internal sealed class NullPromptApi : IPromptApi
    {
        public static readonly NullPromptApi Instance = new NullPromptApi();
        public Transform Current => null;
        public void Show(Transform target, string action, string key = "F", Vector3? worldOffset = null) => NullLog.Once("Prompt.Show");
        public void Hide(Transform target = null) { }
    }
}
