using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OZ.UI
{
    /// <summary>
    /// UI 전용 단축키. 공용 InputSystem_Actions.inputactions는 건드리지 않고 코드로 액션을 만든다.
    ///   ESC / 게임패드 Start : 맨 위 창 닫기, 없으면 일시정지 메뉴
    ///   Tab / 게임패드 Select : 상태창 토글 (v1.0 기획서 v0.5 — 스탯·퀘스트·상점·스킬·지도)
    ///   K : 상태창 스킬 탭 / M · D-Pad↑ : 상태창 지도 탭 (상태창이 없는 예전 프리팹이면 스킬 트리·지도 창)
    ///   (I 인벤토리: v0.9 기획서 범위 밖 → 기본 키 없음. inventoryBinding을 채우면 다시 켜짐)
    /// Q/E/R, 1~4 같은 전투 입력은 플레이어 담당이 처리하고, UI는 이벤트(ISkillSource/IItemSource)만 받는다.
    /// </summary>
    [AddComponentMenu("OZ/UI/UI Input Router")]
    public class UIInputRouter : MonoBehaviour
    {
        [SerializeField] internal string backBinding = "<Keyboard>/escape";
        [SerializeField] internal string backGamepadBinding = "<Gamepad>/start";
        [SerializeField] internal string skillWindowBinding = "<Keyboard>/k";
        [SerializeField] internal string skillWindowGamepadBinding = "";
        [Tooltip("v1.0 상태창 (기획서: Tab)")]
        [SerializeField] internal string statusBinding = "<Keyboard>/tab";
        [SerializeField] internal string statusGamepadBinding = "<Gamepad>/select";
        [Tooltip("v1.0: 상태창이 있으면 K = 스킬 탭, M = 지도 탭. 끄면 예전 스킬 트리·지도 창")]
        [SerializeField] internal bool useStatusWindow = true;
        [Tooltip("v0.9: 기획서에 인벤토리 창이 없어 비워 둠 (예: <Keyboard>/i)")]
        [SerializeField] internal string inventoryBinding = "";
        [SerializeField] internal string inventoryGamepadBinding = "";
        [SerializeField] internal string mapBinding = "<Keyboard>/m";
        [SerializeField] internal string mapGamepadBinding = "<Gamepad>/dpad/up";
        [Tooltip("v1.0: Tab은 상태창으로 옮김")]
        [SerializeField] internal string mapAltBinding = "";

        [Tooltip("이 화면들이 열려 있을 땐 스킬 창/일시정지 단축키 무시 (타이틀, 사망 등)")]
        [SerializeField] internal ScreenId[] hotkeyBlockingScreens =
        {
            ScreenId.Title, ScreenId.ClassSelect, ScreenId.Death, ScreenId.DemoEnd,
            ScreenId.Dialogue, ScreenId.Options, ScreenId.Shop,
        };

        InputAction _back;
        InputAction _skillWindow;
        InputAction _inventory;
        InputAction _map;
        InputAction _status;

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
            _status = Make("UI_Status", statusBinding, statusGamepadBinding);
        }

        void OnEnable()
        {
            _back.performed += OnBack;
            _skillWindow.performed += OnSkillWindow;
            _inventory.performed += OnInventory;
            _map.performed += OnMap;
            _status.performed += OnStatus;
            _back.Enable(); _skillWindow.Enable(); _inventory.Enable(); _map.Enable(); _status.Enable();
        }

        void OnDisable()
        {
            _back.performed -= OnBack;
            _skillWindow.performed -= OnSkillWindow;
            _inventory.performed -= OnInventory;
            _map.performed -= OnMap;
            _status.performed -= OnStatus;
            _back.Disable(); _skillWindow.Disable(); _inventory.Disable(); _map.Disable(); _status.Disable();
        }

        void OnDestroy()
        {
            _back?.Dispose(); _skillWindow?.Dispose(); _inventory?.Dispose(); _map?.Dispose(); _status?.Dispose();
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

        void OnSkillWindow(InputAction.CallbackContext _) { if (!ToggleStatus(StatusTab.Character)) ToggleMenu(ScreenId.SkillWindow); }
        void OnInventory(InputAction.CallbackContext _) => ToggleMenu(ScreenId.Inventory);
        void OnMap(InputAction.CallbackContext ctx)
        {
            // 상태창이 열려 있으면 게임패드 D-Pad↑는 창 안 이동용 (탭 전환·닫기 안 함)
            var ui = UIManager.Instance;
            if (ctx.control != null && ctx.control.device is Gamepad && ui != null && ui.IsOpen(ScreenId.Status)) return;
            if (!ToggleStatus(StatusTab.Map)) ToggleMenu(ScreenId.Map);
        }
        void OnStatus(InputAction.CallbackContext _) => ToggleStatus(null);

        /// <summary>
        /// 상태창 열기/닫기. tab null = 마지막 탭. 이미 열려 있으면: 같은 탭(또는 Tab 키) → 닫기, 다른 탭 → 탭만 바꿈.
        /// 상태창이 없거나 꺼져 있으면 false (예전 창으로).
        /// </summary>
        bool ToggleStatus(StatusTab? tab)
        {
            var ui = UIManager.Instance;
            if (ui == null || !useStatusWindow) return false;
            if (!(ui.GetWindow(ScreenId.Status) is StatusWindow w)) return false;
            if (HotkeysBlocked(ui) || ui.IsOpen(ScreenId.Pause)) return true;
            if (w.IsOpen)
            {
                if (tab == null || w.CurrentTab == tab.Value) ui.Close(w);
                else w.Open(tab.Value);
                return true;
            }
            foreach (var other in MenuScreens)
                if (other != ScreenId.Status) ui.Close(other);
            w.Open(tab ?? w.CurrentTab);
            return true;
        }

        /// <summary>메뉴 창은 한 번에 하나: 다른 메뉴가 열려 있으면 닫고 연다</summary>
        void ToggleMenu(ScreenId id)
        {
            var ui = UIManager.Instance;
            if (ui == null || HotkeysBlocked(ui)) return;
            if (ui.IsOpen(ScreenId.Pause)) return;
            if (ui.GetWindow(id) == null) return; // 프리팹에 없는 창 (예: 인벤토리)
            if (ui.IsOpen(id)) { ui.Close(id); return; }
            foreach (var other in MenuScreens)
                if (other != id) ui.Close(other);
            ui.Open(id);
        }

        static readonly ScreenId[] MenuScreens = { ScreenId.SkillWindow, ScreenId.Inventory, ScreenId.Map, ScreenId.Status };
    }
}
