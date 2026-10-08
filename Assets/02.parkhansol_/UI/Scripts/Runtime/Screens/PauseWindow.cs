using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>일시정지 (ESC): 계속 / 스킬 / 인벤토리 / 지도 / 옵션 / 타이틀로</summary>
    [AddComponentMenu("OZ/UI/Screens/Pause Window")]
    public class PauseWindow : UIWindow
    {
        [SerializeField] internal Button resumeButton;
        [SerializeField] internal Button skillButton;
        [SerializeField] internal Button inventoryButton;
        [SerializeField] internal Button mapButton;
        [SerializeField] internal Button optionsButton;
        [SerializeField] internal Button titleButton;

        protected override void Awake()
        {
            base.Awake();
            if (resumeButton != null) resumeButton.onClick.AddListener(Close);
            if (skillButton != null) skillButton.onClick.AddListener(() => SwitchTo(ScreenId.SkillWindow));
            if (inventoryButton != null) inventoryButton.onClick.AddListener(() => SwitchTo(ScreenId.Inventory));
            if (mapButton != null) mapButton.onClick.AddListener(() => SwitchTo(ScreenId.Map));
            // 옵션은 일시정지를 닫지 않고 위에 연다 → ESC로 닫으면 일시정지로 돌아옴
            if (optionsButton != null) optionsButton.onClick.AddListener(() => UIManager.Instance?.Open(ScreenId.Options));
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
