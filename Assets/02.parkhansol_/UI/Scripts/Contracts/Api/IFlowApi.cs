using System;
using System.Threading.Tasks;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// 게임 흐름 화면 연출: 로딩 화면 · 화면 페이드 · 스테이지 시작 띠.
    /// 코어(GameManager / StageManager)가 부르는 쪽. 보이는 동안에는 UIState.IsGameplayInputBlocked = true.
    ///
    ///   // GameManager.InitializeAsync
    ///   GameUI.Flow.ShowLoading("데이터 불러오는 중");
    ///   GameUI.Flow.SetLoadingProgress(0.5f);
    ///   GameUI.Flow.HideLoading();
    ///
    ///   // GameManager.ChangeStage / RestartStage — 검은 화면 동안 씬·맵 교체
    ///   GameUI.Flow.Transition(() => LoadStage(next));
    ///   await GameUI.Flow.TransitionAsync(async () => await LoadStageAsync(next));
    ///
    ///   // StageManager.StartStage
    ///   GameUI.Flow.StageIntro(stage, "지하철역");
    ///
    /// 모든 연출은 unscaled time (일시정지·히트스톱 중에도 진행). UI가 없으면 콜백만 바로 실행.
    /// </summary>
    public interface IFlowApi
    {
        /// <summary>로딩 화면이 떠 있는지</summary>
        bool IsLoading { get; }
        /// <summary>화면이 (일부라도) 검게 덮여 있는지</summary>
        bool IsFaded { get; }

        /// <summary>로딩 화면 표시 (진행률 0). 이미 떠 있으면 문구만 바꿈</summary>
        void ShowLoading(string message = null);
        /// <summary>진행률 0~1. message가 null이면 문구 유지</summary>
        void SetLoadingProgress(float progress01, string message = null);
        /// <summary>진행률을 100%로 채운 뒤 사라짐. onHidden: 완전히 사라진 뒤</summary>
        void HideLoading(Action onHidden = null);

        /// <summary>화면을 검게 덮음. onBlack: 다 덮였을 때</summary>
        void FadeOut(float duration = 0.35f, Action onBlack = null);
        /// <summary>검은 화면을 걷음. onClear: 다 걷혔을 때</summary>
        void FadeIn(float duration = 0.35f, Action onClear = null);
        /// <summary>FadeOut → whileBlack 실행 → FadeIn. 스테이지 교체·재시작용</summary>
        void Transition(Action whileBlack, float duration = 0.35f, Action onFinished = null);

        /// <summary>
        /// 스테이지 시작 띠 ("STAGE 1" + 제목). 게임은 멈추지 않음.
        /// 화면이 검게 덮여 있으면 페이드가 걷힌 뒤에 나온다.
        /// </summary>
        void StageIntro(int stageNumber, string title, string subtitle = null, Action onFinished = null);

        // ── async 버전 (InitializeAsync 등에서 await) ──
        Task FadeOutAsync(float duration = 0.35f);
        Task FadeInAsync(float duration = 0.35f);
        /// <summary>FadeOut → await whileBlack → FadeIn</summary>
        Task TransitionAsync(Func<Task> whileBlack, float duration = 0.35f);
    }
}
