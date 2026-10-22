using System;
using System.Collections.Generic;
using UnityEngine;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// [v1.3 · 기획서 v0.5 3장] HUD 부위. GameUI.HUD.SetParts 로 따로 숨기고 보인다.
    ///   시작 화면 "UI가 전혀 없는 화면"  → GameUI.HUD.SetParts(HudPart.All, false, animate: false);
    ///   각성 뒤 HUD 등장                 → GameUI.Tutorial.ShowAwakening(...) 이 알아서 켠다
    /// Alarm(미니맵 아래 방송·상태창 알림)은 All에 들어 있지 않다 → HUD를 숨겨도 대피 방송은 뜬다.
    /// </summary>
    [Flags]
    public enum HudPart
    {
        None = 0,
        /// <summary>좌상단 초상화·체력바·랭크</summary>
        Health = 1 << 0,
        /// <summary>우상단 미니맵</summary>
        Minimap = 1 << 1,
        /// <summary>우하단 아이템 1~4</summary>
        Items = 1 << 2,
        /// <summary>좌상단 퀘스트 목록</summary>
        Quests = 1 << 3,
        /// <summary>좌하단 스킬 Q/E/R</summary>
        Skills = 1 << 4,
        /// <summary>버프 아이콘 줄</summary>
        Buffs = 1 << 5,
        /// <summary>미니맵 아래 방송·SYSTEM 알림 (All에 없음)</summary>
        Alarm = 1 << 6,

        /// <summary>전투 HUD 전부 (알림 제외)</summary>
        All = Health | Minimap | Items | Quests | Skills | Buffs,
    }

    /// <summary>튜토리얼 한 단계가 가리키는 곳</summary>
    public enum TutorialTarget
    {
        /// <summary>강조 없이 화면 전체 어둡게 + 가운데 설명</summary>
        None = 0,
        /// <summary>HUD 부위 (hudPart)</summary>
        Hud = 1,
        /// <summary>상태창 탭 버튼 (tab) — 상태창을 열고 그 탭으로 바꾼다</summary>
        StatusTab = 2,
        /// <summary>상태창 전체</summary>
        StatusWindow = 3,
        /// <summary>상태창 포인트 표시</summary>
        StatusPoints = 4,
        /// <summary>월드 오브젝트 (world, 몬스터·게이트 등) — 화면 좌표로 따라감</summary>
        World = 5,
        /// <summary>아무 UI (rect)</summary>
        Rect = 6,
    }

    /// <summary>
    /// 튜토리얼 한 단계: 어디를 밝힐지 + 제목 + 설명.
    ///   TutorialStep.World(monster.transform, "기본 공격", "좌클릭으로 주먹 공격")
    ///   TutorialStep.Hud(HudPart.Quests, "현재 목표", "게이트 소속 몬스터를 모두 잡아야 봉쇄할 수 있습니다")
    /// 인스펙터에서도 채울 수 있게 [Serializable].
    /// </summary>
    [Serializable]
    public class TutorialStep
    {
        public string title;
        [TextArea(2, 4)] public string body;
        public TutorialTarget target;
        [Tooltip("target = Hud 일 때")]
        public HudPart hudPart = HudPart.Health;
        [Tooltip("target = StatusTab 일 때")]
        public StatusTab tab;
        [Tooltip("target = World 일 때 따라갈 오브젝트")]
        public Transform world;
        public Vector3 worldOffset;
        [Tooltip("월드 강조 칸 크기 (UI 픽셀, 640×360 기준)")]
        public Vector2 worldSize = new Vector2(40f, 40f);
        [Tooltip("target = Rect 일 때")]
        public RectTransform rect;
        [Tooltip("강조 칸 여백 (UI 픽셀)")]
        public float padding = 3f;

        public static TutorialStep Message(string title, string body) =>
            new TutorialStep { title = title, body = body, target = TutorialTarget.None };

        public static TutorialStep Hud(HudPart part, string title, string body) =>
            new TutorialStep { title = title, body = body, target = TutorialTarget.Hud, hudPart = part };

        public static TutorialStep Tab(StatusTab tab, string title, string body) =>
            new TutorialStep { title = title, body = body, target = TutorialTarget.StatusTab, tab = tab };

        public static TutorialStep World(Transform target, string title, string body, float size = 40f, Vector3 offset = default) =>
            new TutorialStep { title = title, body = body, target = TutorialTarget.World, world = target, worldSize = new Vector2(size, size), worldOffset = offset };

        public static TutorialStep Ui(RectTransform target, string title, string body) =>
            new TutorialStep { title = title, body = body, target = TutorialTarget.Rect, rect = target };
    }

    /// <summary>
    /// [v1.3 · 기획서 v0.5 3장] 튜토리얼 연출 (UI 구현, 코어/튜토리얼 담당 호출).
    ///
    ///   // 2 각성: 번쩍 + "각성 · F급 · 상태창 · +1,000P" 카드 → HUD 등장
    ///   GameUI.HUD.SetParts(HudPart.All, false, animate: false);   // 게임 시작 (UI 없음)
    ///   GameUI.Tutorial.ShowAwakening(1000, HunterRank.F, () => SpawnFirstGate());
    ///
    ///   // 3 첫 게이트: 화면 정지 + 몬스터·목표 강조
    ///   GameUI.Tutorial.Play(new[] {
    ///       TutorialStep.World(monster.transform, "기본 공격", "좌클릭으로 주먹 공격. 공격은 커서 방향을 따릅니다."),
    ///       TutorialStep.Hud(HudPart.Quests, "현재 목표", "게이트 소속 몬스터를 전부 잡아야 봉쇄할 수 있습니다."),
    ///   }, () => StartFight());
    ///
    ///   // 6 상태창 설명: 상태창을 열고 탭을 순서대로 강조 → 끝나면 상태창은 열린 채 (첫 구매)
    ///   GameUI.Tutorial.PlayStatusTour(() => UnlockStage1Node());
    ///
    /// 설명이 떠 있는 동안 게임 정지(Time.timeScale 0) + 전투 입력 차단. [Space]/[Enter]/패드 A 또는 설명 칸 클릭 = 다음.
    /// </summary>
    public interface ITutorialApi
    {
        /// <summary>설명(하이라이트)이 떠 있는지</summary>
        bool IsShowing { get; }
        /// <summary>각성 카드 연출 중인지</summary>
        bool IsAwakening { get; }
        /// <summary>지금 단계 (0부터, 안 떠 있으면 -1)</summary>
        int StepIndex { get; }
        int StepCount { get; }
        /// <summary>단계가 바뀔 때 (index)</summary>
        event Action<int> StepChanged;

        /// <summary>한 단계만 띄운다. 다음을 누르면 닫히고 onNext</summary>
        void Show(TutorialStep step, Action onNext = null);
        /// <summary>여러 단계를 순서대로. 마지막 다음 → 닫히고 onFinished</summary>
        void Play(IReadOnlyList<TutorialStep> steps, Action onFinished = null);
        /// <summary>기획서 3장 6단계: 상태창을 열고 탭을 순서대로 설명. 끝나도 상태창은 열어 둔다 (첫 구매 안내)</summary>
        void PlayStatusTour(Action onFinished = null);
        /// <summary>다음 단계 (코드로 넘길 때)</summary>
        void Next();
        /// <summary>바로 닫기 (콜백 없음)</summary>
        void Stop();

        /// <summary>
        /// 기획서 3장 2단계: 화면 번쩍 + 상태창 첫 등장 카드 ("각성 · 헌터 랭크 F급 · 능력 상태창 · +1,000P").
        /// revealHud면 카드가 사라질 때 HUD(HudPart.All)를 하나씩 띄운다. 게임은 멈추지 않는다.
        /// 포인트 지급 자체는 코어(IPointsSource)가 한다 — 여기는 표시만.
        /// </summary>
        void ShowAwakening(int points = 1000, HunterRank rank = HunterRank.F, Action onFinished = null, bool revealHud = true);
    }
}
