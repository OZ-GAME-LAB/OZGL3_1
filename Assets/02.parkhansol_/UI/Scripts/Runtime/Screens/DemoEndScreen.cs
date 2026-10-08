using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>데모 종료 안내</summary>
    [AddComponentMenu("OZ/UI/Screens/Demo End Screen")]
    public class DemoEndScreen : UIWindow
    {
        [SerializeField] internal Button titleButton;

        protected override void Awake()
        {
            base.Awake();
            if (titleButton != null) titleButton.onClick.AddListener(() => { UIManager.Instance?.CloseAll(); UIRequests.RaiseReturnToTitle(); });
        }
    }
}
