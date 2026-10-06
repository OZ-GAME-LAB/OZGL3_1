using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OZ.UI
{
    /// <summary>
    /// UI 전용 단축키. 공용 InputSystem_Actions.inputactions는 건드리지 않고 코드로 액션을 만든다.
    ///   ESC / 게임패드 Start : 맨 위 창 닫기, 없으면 일시정지 메뉴
    ///   K   / 게임패드 Select: 스킬 창 토글
    ///   I   / 게임패드 Y     : 인벤토리 토글
    ///   Tab·M / 게임패드 D-Pad↑: 지도 토글
    /// Q/E/R, 1~4 같은 전투 입력은 플레이어 담당이 처리하고, UI는 이벤트(ISkillSource/IItemSource)만 받는다.
    /// </summary>
    [AddComponentMenu("OZ/UI/UI Input Router")]
    public class UIInputRouter : MonoBehaviour
    {
        [SerializeField] internal string backBinding = "<Keyboard>/escape";
        [SerializeField] internal string backGamepadBinding = "<Gamepad>/start";
        [SerializeField] internal string skillWindowBinding = "<Keyboard>/k";
        [SerializeField] internal string skillWindowGamepadBinding = "<Gamepad>/select";
        [SerializeField] internal string inventoryBinding = "<Keyboard>/i";
        [SerializeField] internal string inventoryGamepadBinding = "<Gamepad>/buttonNorth";
        [SerializeField] internal string mapBinding = "<Keyboard>/m";
        [SerializeField] internal string mapGamepadBinding = "<Gamepad>/dpad/up";
        [SerializeField] internal string mapAltBinding = "<Keyboard>/tab";

        [Tooltip("이 화면들이 열려 있을 땐 스킬 창/일시정지 단축키 무시 (타이틀, 사망 등)")]
        [SerializeField] internal ScreenId[] hotkeyBlockingScreens =
        {
            ScreenId.Title, ScreenId.ClassSelect, ScreenId.Death, ScreenId.DemoEnd,
            ScreenId.Dialogue, ScreenId.Options,
        };

        InputAction _back;
        InputAction _skillWindow;
        InputAction _inventory;
        InputAction _map;

        static InputAction Make(string name, string a, string b)
        {
            var action = new InputAction(name, InputActionType.Button);
            if (!string.IsNullOrEmpty(a)) action.AddBinding(a);
            if (!string.IsNullOrEmpty(b)) action.AddBinding(b);
            return action;
        }

        void Awake()
        {
            _back = Make("UI_Back", backBinding, backGamepadBinding);
            _skillWindow = Make("UI_SkillWindow", skillWindowBinding, skillWindowGamepadBinding);
            _inventory = Make("UI_Inventory", inventoryBinding, inventoryGamepadBinding);
            _map = Make("UI_Map", mapBinding, mapGamepadBinding);
            if (!string.IsNullOrEmpty(mapAltBinding)) _map.AddBinding(mapAltBinding);
        }

        void OnEnable()
        {
            _back.performed += OnBack;
            _skillWindow.performed += OnSkillWindow;
            _inventory.performed += OnInventory;
            _map.performed += OnMap;
            _back.Enable(); _skillWindow.Enable(); _inventory.Enable(); _map.Enable();
        }

        void OnDisable()
        {
            _back.performed -= OnBack;
            _skillWindow.performed -= OnSkillWindow;
            _inventory.performed -= OnInventory;
            _map.performed -= OnMap;
            _back.Disable(); _skillWindow.Disable(); _inventory.Disable(); _map.Disable();
        }

        void OnDestroy()
        {
            _back?.Dispose(); _skillWindow?.Dispose(); _inventory?.Dispose(); _map?.Dispose();
        }

        bool HotkeysBlocked(UIManager ui)
        {
            foreach (var id in hotkeyBlockingScreens)
                if (ui.IsOpen(id)) return true;
            return false;
        }

        void OnBack(InputAction.CallbackContext _)
        {
            var ui = UIManager.Instance;
            if (ui == null) return;
            if (ui.Back()) return;
            if (HotkeysBlocked(ui)) return;
            ui.Open(ScreenId.Pause);
        }

        void OnSkillWindow(InputAction.CallbackContext _) => ToggleMenu(ScreenId.SkillWindow);
        void OnInventory(InputAction.CallbackContext _) => ToggleMenu(ScreenId.Inventory);
        void OnMap(InputAction.CallbackContext _) => ToggleMenu(ScreenId.Map);

        /// <summary>메뉴 창은 한 번에 하나: 다른 메뉴가 열려 있으면 닫고 연다</summary>
        void ToggleMenu(ScreenId id)
        {
            var ui = UIManager.Instance;
            if (ui == null || HotkeysBlocked(ui)) return;
            if (ui.IsOpen(ScreenId.Pause)) return;
            if (ui.IsOpen(id)) { ui.Close(id); return; }
            foreach (var other in MenuScreens)
                if (other != id) ui.Close(other);
            ui.Open(id);
        }

        static readonly ScreenId[] MenuScreens = { ScreenId.SkillWindow, ScreenId.Inventory, ScreenId.Map };
    }
}
