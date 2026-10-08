using System.Threading.Tasks;
using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OZ.UI.Samples
{
    /// <summary>
    /// [팀 설계 GameManager 모양 샘플] InitializeAsync() / StartGame() / RestartStage() / ChangeStage().
    ///
    /// UI 연결 (각 메서드 안 [UI] 줄):
    ///   InitializeAsync → GameUI.Flow.ShowLoading / SetLoadingProgress / HideLoading
    ///   RestartStage    → GameUI.Flow.Transition(검은 화면 동안 리셋)
    ///   ChangeStage     → GameUI.Flow.Transition(검은 화면 동안 교체)
    /// UI → 게임 요청 (구독):
    ///   UIRequests.Retry     → RestartStage()   (사망 화면 '재도전')
    ///   UIRequests.NextStage → ChangeStage(+1)  (스테이지 클리어 띠가 끝나면 UI가 보냄)
    ///   UIRequests.ReturnToTitle → 타이틀 화면,  UIRequests.NewGame → 페이드 후 StartGame()
    ///
    /// 디버그 키: F6 재시작 · F7 다음 스테이지 · F8 로딩 다시 보기
    /// </summary>
    [AddComponentMenu("OZ/UI/Samples/Flow/Sample Game Manager")]
    public class SampleGameManager : MonoBehaviour
    {
        [SerializeField] internal SampleStageManager stageManager;
        [SerializeField] internal SampleSpawnManager spawnManager;
        [SerializeField] internal SamplePoolManager poolManager;
        [SerializeField] internal ShowcaseEnemy enemyPrefab;
        [SerializeField] internal int prewarm = 8;
        [SerializeField] internal ShowcaseDirector director; // 쇼케이스 전용: 레벨·스킬 미리 세팅
        [SerializeField] internal bool runOnStart = true;
        [Tooltip("켜면 로딩 뒤 타이틀부터 (끄면 바로 스테이지 1)")]
        [SerializeField] internal bool showTitleFirst;

        bool _busy;

        void OnEnable()
        {
            UIRequests.Retry += RestartStage;      // [UI]
            UIRequests.NextStage += NextStage;     // [UI]
            UIRequests.ReturnToTitle += ToTitle;   // [UI]
            UIRequests.NewGame += NewGame;         // [UI]
        }

        void OnDisable()
        {
            UIRequests.Retry -= RestartStage;
            UIRequests.NextStage -= NextStage;
            UIRequests.ReturnToTitle -= ToTitle;
            UIRequests.NewGame -= NewGame;
        }

        async void Start()
        {
            if (!runOnStart) return;
            await Task.Yield(); // UI 등록(OnEnable/Start) 이후
            await InitializeAsync();
            if (this == null) return; // 기다리는 사이 Play 종료
            if (showTitleFirst) ToTitle();
            else StartGame();
        }

        public async Task InitializeAsync()
        {
            _busy = true;
            GameUI.Flow.ShowLoading("데이터 불러오는 중");                      // [UI]
            await Task.Delay(350);
            if (this == null) return;
            GameUI.Flow.SetLoadingProgress(0.3f, "오브젝트 풀 준비");            // [UI]
            if (poolManager != null && enemyPrefab != null) poolManager.Prewarm(enemyPrefab, prewarm);
            await Task.Delay(400);
            if (this == null) return;
            GameUI.Flow.SetLoadingProgress(0.7f, "스테이지 구성");               // [UI]
            await Task.Delay(400);
            if (this == null) return;
            var hidden = new TaskCompletionSource<bool>();
            GameUI.Flow.HideLoading(() => hidden.TrySetResult(true));           // [UI] 100% 보여 주고 사라짐
            await hidden.Task;
            _busy = false;
        }

        public void StartGame()
        {
            GameUI.Screens.CloseAll();
            GameUI.HUD.Visible = true;
            if (director != null) director.PreparePlayer();
            if (stageManager != null) stageManager.StartStage(1);
            if (director != null) StartCoroutine(HelpAfterIntro());
        }

        System.Collections.IEnumerator HelpAfterIntro()
        {
            yield return new WaitForSecondsRealtime(2.6f); // 스테이지 띠와 겹치지 않게
            if (director != null) director.ShowHelp(7f);
        }

        public void RestartStage()
        {
            if (_busy) return;
            _busy = true;
            GameUI.Flow.Transition(() =>                                         // [UI] 검은 화면 동안
            {
                GameUI.Screens.CloseAll();
                if (director != null) director.RevivePlayer();
                if (stageManager != null) stageManager.StartStage(stageManager.CurrentStage);
            }, 0.35f, () => _busy = false);
        }

        void ToTitle()
        {
            if (stageManager != null) stageManager.StopStage();
            GameUI.HUD.Visible = false;
            GameUI.Screens.ShowTitle(false);                                     // [UI]
        }

        void NewGame()
        {
            if (_busy) return;
            _busy = true;
            GameUI.Flow.Transition(StartGame, 0.4f, () => _busy = false);       // [UI]
        }

        void NextStage() => ChangeStage(stageManager != null ? stageManager.CurrentStage + 1 : 1);

        public void ChangeStage(int stage)
        {
            if (_busy) return;
            _busy = true;
            GameUI.Flow.Transition(() =>                                         // [UI] 검은 화면 동안 교체
            {
                GameUI.Screens.CloseAll();
                if (director != null) director.RevivePlayer();
                if (stageManager != null) stageManager.StartStage(stage);
            }, 0.45f, () => _busy = false);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || _busy) return;
            if (kb.f6Key.wasPressedThisFrame) RestartStage();
            if (kb.f7Key.wasPressedThisFrame && stageManager != null && stageManager.CurrentStage < stageManager.StageCount) NextStage();
            if (kb.f8Key.wasPressedThisFrame) ReplayLoading();
        }

        async void ReplayLoading()
        {
            await InitializeAsync();
        }
    }
}
