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
            if (messageText != null) { messageText.text = ""; messageText.gameObject.SetActive(false); } // 안내 문구 없음 (버튼만)
        }
    }
}
