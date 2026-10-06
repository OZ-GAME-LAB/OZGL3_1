using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>일시정지 (ESC): 계속 / 스킬 / 인벤토리 / 지도 / 타이틀로</summary>
    [AddComponentMenu("OZ/UI/Screens/Pause Window")]
    public class PauseWindow : UIWindow
    {
        [SerializeField] internal Button resumeButton;
        [SerializeField] internal Button skillButton;
        [SerializeField] internal Button inventoryButton;
        [SerializeField] internal Button mapButton;
        [SerializeField] internal Button titleButton;

        protected override void Awake()
        {
            base.Awake();
            if (resumeButton != null) resumeButton.onClick.AddListener(Close);
            if (skillButton != null) skillButton.onClick.AddListener(() => SwitchTo(ScreenId.SkillWindow));
            if (inventoryButton != null) inventoryButton.onClick.AddListener(() => SwitchTo(ScreenId.Inventory));
            if (mapButton != null) mapButton.onClick.AddListener(() => SwitchTo(ScreenId.Map));
            if (titleButton != null) titleButton.onClick.AddListener(() =>
            {
                UIManager.Instance?.CloseAll();
                UIRequests.RaiseReturnToTitle();
            });
        }

        void SwitchTo(ScreenId id)
        {
            var ui = UIManager.Instance;
            if (ui == null) return;
            ui.Close(this);
            ui.Open(id);
        }
    }
}
