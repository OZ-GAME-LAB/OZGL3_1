using UnityEngine;
using UnityEngine.Events;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// UI 버튼 → 게임 처리 연결을 인스펙터에서 (코드 없이).
    ///   예) On Retry       → GameManager.RestartStage
    ///       On Next Stage  → GameManager.ChangeStage (다음 번호는 GameManager가 계산)
    ///       On New Game    → GameManager.StartGame
    /// 코드로 하고 싶으면 UIRequests.Retry += RestartStage; 처럼 구독해도 같다.
    ///
    /// [v0.9] 맵이 바뀌는 요청(계열 선택·재도전·다음 스테이지·타이틀로)은 화면이 어두워진 동안 연결된 처리를 부른다
    ///   (GameUI.Flow.Transition: 어두워짐 → 처리 → 밝아짐). 그래서 스테이지 교체가 화면에 보이지 않는다.
    ///   연결된 처리가 없으면 페이드도 하지 않음. 끄려면 Fade On Map Change 체크 해제.
    /// </summary>
    [AddComponentMenu("OZ/UI/Links/UI Request Events")]
    public class UIRequestEvents : MonoBehaviour
    {
        [Header("v0.9 화면 전환")]
        [Tooltip("켜면 계열 선택·재도전·다음 스테이지·타이틀로 처리를 화면이 어두운 동안 실행")]
        public bool fadeOnMapChange = true;
        [Tooltip("어두워지는 시간 = 밝아지는 시간 (초)")]
        [Min(0f)] public float fadeDuration = 0.35f;

        [Header("연결")]
        [Tooltip("타이틀 '새 게임'")] public UnityEvent onNewGame = new UnityEvent();
        [Tooltip("타이틀 '이어하기'")] public UnityEvent onContinue = new UnityEvent();
        [Tooltip("계열 선택 (0 검술, 1 마법)")] public UnityEvent<int> onClassSelected = new UnityEvent<int>();
        [Tooltip("사망 화면 '재도전'")] public UnityEvent onRetry = new UnityEvent();
        [Tooltip("스테이지 클리어 띠가 끝났을 때")] public UnityEvent onNextStage = new UnityEvent();
        [Tooltip("'타이틀로' (사망·일시정지·데모 종료)")] public UnityEvent onReturnToTitle = new UnityEvent();
        [Tooltip("타이틀 '종료' (비워 두면 UI가 Application.Quit)")] public UnityEvent onQuitGame = new UnityEvent();

        void OnEnable()
        {
            UIRequests.NewGame += NewGame;
            UIRequests.Continue += Continue;
            UIRequests.ClassSelected += ClassSelected;
            UIRequests.Retry += Retry;
            UIRequests.NextStage += NextStage;
            UIRequests.ReturnToTitle += ReturnToTitle;
            if (onQuitGame.GetPersistentEventCount() > 0) UIRequests.QuitGame += QuitGame;
        }

        void OnDisable()
        {
            UIRequests.NewGame -= NewGame;
            UIRequests.Continue -= Continue;
            UIRequests.ClassSelected -= ClassSelected;
            UIRequests.Retry -= Retry;
            UIRequests.NextStage -= NextStage;
            UIRequests.ReturnToTitle -= ReturnToTitle;
            UIRequests.QuitGame -= QuitGame;
        }

        void NewGame() => onNewGame.Invoke();
        void Continue() => onContinue.Invoke();
        void ClassSelected(PlayerClass c) => MapChange(onClassSelected, () => onClassSelected.Invoke((int)c));
        void Retry() => MapChange(onRetry, onRetry.Invoke);
        void NextStage() => MapChange(onNextStage, onNextStage.Invoke);
        void ReturnToTitle() => MapChange(onReturnToTitle, onReturnToTitle.Invoke);
        void QuitGame() => onQuitGame.Invoke();

        /// <summary>연결된 처리가 있으면 화면이 어두운 동안 실행, 없거나 페이드를 끄면 바로 실행</summary>
        void MapChange(UnityEventBase evt, System.Action invoke)
        {
            if (!fadeOnMapChange || fadeDuration <= 0f || evt.GetPersistentEventCount() == 0)
            {
                invoke();
                return;
            }
            GameUI.Flow.Transition(invoke, fadeDuration);
        }
    }
}
