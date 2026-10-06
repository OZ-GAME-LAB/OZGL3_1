using System.Collections.Generic;
using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// 옵션 창 (시작 화면 '설정', 일시정지 '옵션'에서 열림). 탭 3개: 사운드 / 화면 / 게임.
    ///   값은 바꾸는 즉시 UISettings에 반영 → 닫을 때 저장.
    ///   Q/E 또는 패드 LB/RB = 탭 전환, ESC/B = 닫기 (열었던 창으로 돌아감).
    /// </summary>
    [AddComponentMenu("OZ/UI/Options/Options Window")]
    public class OptionsWindow : UIWindow
    {
        [Header("Tabs")]
        [SerializeField] internal Button[] tabButtons = new Button[0];
        [SerializeField] internal Image[] tabImages = new Image[0];
        [SerializeField] internal TMP_Text[] tabLabels = new TMP_Text[0];
        [SerializeField] internal GameObject[] pages = new GameObject[0];
        [SerializeField] internal Sprite tabActive;
        [SerializeField] internal Sprite tabInactive;
        [SerializeField] internal Color tabActiveText = new Color(0.4f, 0.9f, 1f);
        [SerializeField] internal Color tabInactiveText = new Color(0.55f, 0.6f, 0.72f);

        [Header("사운드")]
        [SerializeField] internal OptionSlider master;
        [SerializeField] internal OptionSlider bgm;
        [SerializeField] internal OptionSlider sfx;

        [Header("화면")]
        [SerializeField] internal OptionToggle fullscreen;
        [SerializeField] internal OptionSelector resolution;
        [SerializeField] internal OptionToggle vsync;

        [Header("게임")]
        [SerializeField] internal OptionToggle screenShake;
        [SerializeField] internal OptionToggle damageNumbers;
        [SerializeField] internal OptionSelector numberSize;
        [SerializeField] internal OptionToggle compactNumbers;

        [Header("Footer")]
        [SerializeField] internal Button defaultsButton;
        [SerializeField] internal Button closeButton;

        static readonly Vector2Int[] FallbackResolutions =
            { new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(3840, 2160) };

        static readonly string[] NumberSizeLabels = { "보통", "크게 (2배)" };

        readonly List<Vector2Int> _resolutions = new List<Vector2Int>();
        int _tab;
        GameObject _returnSelection; // 닫으면 열기 전 선택(타이틀 '설정' 버튼 등)으로 복귀
        bool _refreshing;

        public int CurrentTab => _tab;

        protected override void Awake()
        {
            base.Awake();
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int idx = i;
                if (tabButtons[i] != null) tabButtons[i].onClick.AddListener(() => ShowTab(idx));
            }

            if (master != null) master.ValueChanged += v => { if (!_refreshing) UISettings.MasterVolume = v; };
            if (bgm != null) bgm.ValueChanged += v => { if (!_refreshing) UISettings.BgmVolume = v; };
            if (sfx != null) sfx.ValueChanged += v => { if (!_refreshing) UISettings.SfxVolume = v; };
            if (fullscreen != null) fullscreen.ValueChanged += v => { if (!_refreshing) UISettings.Fullscreen = v; };
            if (vsync != null) vsync.ValueChanged += v => { if (!_refreshing) UISettings.VSync = v; };
            if (screenShake != null) screenShake.ValueChanged += v => { if (!_refreshing) UISettings.ScreenShake = v; };
            if (damageNumbers != null) damageNumbers.ValueChanged += v => { if (!_refreshing) UISettings.ShowDamageNumbers = v; };
            if (numberSize != null) numberSize.IndexChanged += i => { if (!_refreshing) UISettings.DamageNumberSize = i + 1; };
            if (compactNumbers != null) compactNumbers.ValueChanged += v => { if (!_refreshing) UISettings.CompactNumbers = v; };
            if (resolution != null) resolution.IndexChanged += i =>
            {
                if (_refreshing || i < 0 || i >= _resolutions.Count) return;
                UISettings.Resolution = _resolutions[i];
            };

            if (defaultsButton != null) defaultsButton.onClick.AddListener(UISettings.ResetToDefaults);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        protected override void OnSetup(object args)
        {
            _returnSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            BuildResolutionList();
            Refresh();
            ShowTab(0, false);
        }

        protected override void OnOpened() => UISettings.Changed += Refresh;

        protected override void OnClosed()
        {
            UISettings.Changed -= Refresh;
            UISettings.Save();
            if (_returnSelection != null && _returnSelection.activeInHierarchy && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_returnSelection);
        }

        void OnDestroy() => UISettings.Changed -= Refresh;

        void Update()
        {
            if (!IsOpen) return;
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            if ((kb != null && kb.qKey.wasPressedThisFrame) || (pad != null && pad.leftShoulder.wasPressedThisFrame)) ShowTab(_tab - 1);
            if ((kb != null && kb.eKey.wasPressedThisFrame) || (pad != null && pad.rightShoulder.wasPressedThisFrame)) ShowTab(_tab + 1);
        }

        public void ShowTab(int index) => ShowTab(index, true);

        void ShowTab(int index, bool selectFirst)
        {
            int n = pages.Length;
            if (n == 0) return;
            _tab = (index % n + n) % n;
            for (int i = 0; i < n; i++)
            {
                bool on = i == _tab;
                if (pages[i] != null) pages[i].SetActive(on);
                if (i < tabImages.Length && tabImages[i] != null && tabActive != null) tabImages[i].sprite = on ? tabActive : tabInactive;
                if (i < tabLabels.Length && tabLabels[i] != null) tabLabels[i].color = on ? tabActiveText : tabInactiveText;
            }
            var first = FirstSelectable(pages[_tab]);
            if (first != null && EventSystem.current != null && (selectFirst || IsOpen))
                EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        static Selectable FirstSelectable(GameObject page)
        {
            if (page == null) return null;
            foreach (var s in page.GetComponentsInChildren<Selectable>(false))
                if (s.interactable && s.navigation.mode != Navigation.Mode.None) return s;
            return null;
        }

        void BuildResolutionList()
        {
            _resolutions.Clear();
            foreach (var r in Screen.resolutions)
            {
                var v = new Vector2Int(r.width, r.height);
                if (v.x >= 1280 && !_resolutions.Contains(v)) _resolutions.Add(v);
            }
            if (_resolutions.Count == 0) _resolutions.AddRange(FallbackResolutions);
            _resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        }

        int CurrentResolutionIndex()
        {
            var want = UISettings.Resolution;
            if (want.x <= 0) want = new Vector2Int(Screen.width, Screen.height);
            int best = 0; int bestDiff = int.MaxValue;
            for (int i = 0; i < _resolutions.Count; i++)
            {
                int d = Mathf.Abs(_resolutions[i].x - want.x) + Mathf.Abs(_resolutions[i].y - want.y);
                if (d < bestDiff) { bestDiff = d; best = i; }
            }
            return best;
        }

        void Refresh()
        {
            _refreshing = true;
            try
            {
                if (master != null) master.SetWithoutNotify(UISettings.MasterVolume);
                if (bgm != null) bgm.SetWithoutNotify(UISettings.BgmVolume);
                if (sfx != null) sfx.SetWithoutNotify(UISettings.SfxVolume);
                if (fullscreen != null) fullscreen.SetWithoutNotify(UISettings.Fullscreen);
                if (vsync != null) vsync.SetWithoutNotify(UISettings.VSync);
                if (screenShake != null) screenShake.SetWithoutNotify(UISettings.ScreenShake);
                if (damageNumbers != null) damageNumbers.SetWithoutNotify(UISettings.ShowDamageNumbers);
                if (numberSize != null) numberSize.SetOptions(NumberSizeLabels, UISettings.DamageNumberSize - 1);
                if (compactNumbers != null) compactNumbers.SetWithoutNotify(UISettings.CompactNumbers);
                if (resolution != null)
                {
                    var labels = new List<string>(_resolutions.Count);
                    foreach (var r in _resolutions) labels.Add($"{r.x} × {r.y}");
                    resolution.SetOptions(labels, CurrentResolutionIndex());
                }
            }
            finally { _refreshing = false; }
        }
    }
}
