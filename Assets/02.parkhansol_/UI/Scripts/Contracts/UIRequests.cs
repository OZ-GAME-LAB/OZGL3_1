using System;
using UnityEngine;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// UI → 게임플레이 방향 요청. 버튼은 UI가 그리고, 실제 처리(저장/로드/씬 이동)는 코어가 한다.
    /// 코어 담당은 필요한 이벤트만 구독하면 된다.
    ///
    ///   UIRequests.ClassSelected += cls => GameManager.StartNewGame(cls);
    ///   UIRequests.Retry += () => StageManager.RestartStage();
    /// </summary>
    public static class UIRequests
    {
        public static event Action NewGame;
        public static event Action Continue;
        public static event Action QuitGame;
        public static event Action<PlayerClass> ClassSelected;
        public static event Action Retry;
        public static event Action NextStage;
        public static event Action ReturnToTitle;

        internal static void RaiseNewGame() => NewGame?.Invoke();
        internal static void RaiseContinue() => Continue?.Invoke();
        internal static void RaiseQuitGame()
        {
            if (QuitGame != null) QuitGame.Invoke();
            else
            {
#if UNITY_EDITOR
                Debug.Log("[UIRequests] QuitGame 구독자가 없어 아무 일도 하지 않음 (에디터)");
#else
                Application.Quit();
#endif
            }
        }
        internal static void RaiseClassSelected(PlayerClass cls) => ClassSelected?.Invoke(cls);
        internal static void RaiseRetry() => Retry?.Invoke();
        internal static void RaiseNextStage() => NextStage?.Invoke();
        internal static void RaiseReturnToTitle() => ReturnToTitle?.Invoke();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            NewGame = null; Continue = null; QuitGame = null; ClassSelected = null;
            Retry = null; NextStage = null; ReturnToTitle = null;
        }
    }
}
