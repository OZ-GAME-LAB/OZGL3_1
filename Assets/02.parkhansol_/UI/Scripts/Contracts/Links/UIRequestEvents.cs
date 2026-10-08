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
    /// </summary>
    [AddComponentMenu("OZ/UI/Links/UI Request Events")]
    public class UIRequestEvents : MonoBehaviour
    {
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
        void ClassSelected(PlayerClass c) => onClassSelected.Invoke((int)c);
        void Retry() => onRetry.Invoke();
        void NextStage() => onNextStage.Invoke();
        void ReturnToTitle() => onReturnToTitle.Invoke();
        void QuitGame() => onQuitGame.Invoke();
    }
}
