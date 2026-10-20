using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>일시정지 (ESC): 계속 / 스킬 / 지도 / 옵션 / 타이틀로 (인벤토리 버튼은 연결돼 있을 때만)</summary>
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
            // v1.0: 상태창이 있으면 '스킬' → 상태창 스킬 탭, '지도' → 지도 탭
            if (skillButton != null) skillButton.onClick.AddListener(() => SwitchToStatus(StatusTab.Character, ScreenId.SkillWindow));
            if (inventoryButton != null) inventoryButton.onClick.AddListener(() => SwitchTo(ScreenId.Inventory));
            if (mapButton != null) mapButton.onClick.AddListener(() => SwitchToStatus(StatusTab.Map, ScreenId.Map));
            // 옵션은 일시정지를 닫지 않고 위에 연다 → ESC로 닫으면 일시정지로 돌아옴
            if (optionsButton != null) optionsButton.onClick.AddListener(() => UIManager.Instance?.Open(ScreenId.Options));
            if (titleButton != null) titleButton.onClick.AddListener(() =>
            {
                UIManager.Instance?.CloseAll();
                UIRequests.RaiseReturnToTitle();
            });
        }

        void SwitchToStatus(StatusTab tab, ScreenId fallback)
        {
            var ui = UIManager.Instance;
            if (ui == null) return;
            if (ui.GetWindow(ScreenId.Status) is StatusWindow w)
            {
                ui.Close(this);
                w.Open(tab);
            }
            else SwitchTo(fallback);
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
