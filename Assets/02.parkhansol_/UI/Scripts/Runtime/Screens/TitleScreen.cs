using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>시작 화면: 새 게임 / 이어하기 / 종료 → UIRequests로 코어에 전달</summary>
    [AddComponentMenu("OZ/UI/Screens/Title Screen")]
    public class TitleScreen : UIWindow
    {
        [SerializeField] internal Button newGameButton;
        [SerializeField] internal Button continueButton;
        [SerializeField] internal Button quitButton;

        protected override void Awake()
        {
            base.Awake();
            if (newGameButton != null) newGameButton.onClick.AddListener(() => { Close(); UIRequests.RaiseNewGame(); });
            if (continueButton != null) continueButton.onClick.AddListener(() => { Close(); UIRequests.RaiseContinue(); });
            if (quitButton != null) quitButton.onClick.AddListener(UIRequests.RaiseQuitGame);
        }

        protected override void OnSetup(object args)
        {
            bool canContinue = args is TitleArgs t && t.CanContinue;
            if (continueButton != null) continueButton.interactable = canContinue;
        }
    }
}
