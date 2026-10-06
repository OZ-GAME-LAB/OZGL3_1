using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>사망: "스테이지 시작 상태로 복구" 안내 + 재도전 / 타이틀</summary>
    [AddComponentMenu("OZ/UI/Screens/Death Screen")]
    public class DeathScreen : UIWindow
    {
        [SerializeField] internal Button retryButton;
        [SerializeField] internal Button titleButton;
        [SerializeField] internal TMP_Text messageText;
        [SerializeField] internal RectTransform titleRoot;

        protected override void Awake()
        {
            base.Awake();
            if (retryButton != null) retryButton.onClick.AddListener(() => { Close(); UIRequests.RaiseRetry(); });
            if (titleButton != null) titleButton.onClick.AddListener(() => { UIManager.Instance?.CloseAll(); UIRequests.RaiseReturnToTitle(); });
        }

        protected override void OnOpened()
        {
            if (titleRoot != null) titleRoot.SlideIn(Vector2.up, 20f, UITweenStyle.Slow);
            if (messageText != null) messageText.text = "현재 스테이지를 처음부터 다시 시작합니다.\n레벨·스킬·아이템은 스테이지 입장 시점으로 돌아갑니다.";
        }
    }
}
