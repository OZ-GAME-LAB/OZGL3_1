using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>사망: 안내 문구 + 재도전 / 타이틀. v1.4: 재도전 = 팀 코드 RestartStage (현재 스테이지 처음부터)</summary>
    [AddComponentMenu("OZ/UI/Screens/Death Screen")]
    public class DeathScreen : UIWindow
    {
        [SerializeField] internal Button retryButton;
        [SerializeField] internal Button titleButton;
        [SerializeField] internal TMP_Text messageText;
        [SerializeField] internal RectTransform titleRoot;
        [Tooltip("v1.4: 재도전 안내 (UIRequests.Retry → GameManager.RestartStage). 비우면 문구 없음")]
        [SerializeField, TextArea] internal string message = "현재 스테이지를 처음부터 다시 시작합니다";

        protected override void Awake()
        {
            base.Awake();
            if (retryButton != null) retryButton.onClick.AddListener(() => { Close(); UIRequests.RaiseRetry(); });
            if (titleButton != null) titleButton.onClick.AddListener(() => { UIManager.Instance?.CloseAll(); UIRequests.RaiseReturnToTitle(); });
        }

        protected override void OnOpened()
        {
            if (titleRoot != null) titleRoot.SlideIn(Vector2.up, 20f, UITweenStyle.Slow);
            if (messageText != null)
            {
                messageText.text = message ?? "";
                messageText.gameObject.SetActive(!string.IsNullOrEmpty(message));
            }
        }
    }
}
