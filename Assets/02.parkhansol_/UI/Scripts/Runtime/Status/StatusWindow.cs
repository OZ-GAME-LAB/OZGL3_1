using System;
using DG.Tweening;
using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// [v1.1] 상태창 (GameUI.Status 구현). 캐릭터 · 퀘스트 · 상점 · 지도 4탭, 열린 동안 전투 정지.
    ///   탭 바꾸기: 탭 클릭 · 숫자 1~4 · 게임패드 LB/RB
    ///   재화 2종을 머리 오른쪽에 함께 표시: 지금 탭이 쓰는 쪽을 크게 (캐릭터 = 성장 포인트, 상점 = 아이템 포인트)
    ///   데이터: UISources.Points / Stats / Loadout / GrowthShop / Path / Shop / StageMap + GameUI.Quest — 바뀌면 지금 탭만 다시 그린다.
    /// 탭 내용은 StatusPage 상속 컴포넌트 (OZUIRootBuilder.BuildStatus가 만든다).
    /// </summary>
    [AddComponentMenu("OZ/UI/Status/Status Window")]
    public class StatusWindow : UIWindow, IStatusApi
    {
        [Header("머리")]
        [SerializeField] internal Button[] tabButtons = new Button[TabCount];
        [SerializeField] internal Image[] tabMarks = new Image[TabCount];
        [Tooltip("탭 그림 (옵션 창과 같은 TabTop / TabInactiveTop). 비우면 tabMarks만 켜고 끔")]
        [SerializeField] internal Sprite tabActive;
        [SerializeField] internal Sprite tabInactive;
        [SerializeField] internal TMP_Text[] tabLabels = new TMP_Text[TabCount];
        [Tooltip("성장 포인트 (캐릭터 탭 [+] 투자)")]
        [SerializeField] internal TMP_Text pointsText;
        [Tooltip("아이템 포인트 (상점 탭 구매)")]
        [SerializeField] internal TMP_Text itemPointsText;
        [SerializeField] internal RectTransform pointsPunch;
        [SerializeField] internal float currencyBigSize = 12f;
        [SerializeField] internal float currencySmallSize = 10f;
        [SerializeField] internal Color itemPointsColor = new Color(0.37f, 0.88f, 0.72f);

        [Header("탭 내용 (StatusTab 순서)")]
        [SerializeField] internal StatusPage[] pages = new StatusPage[TabCount];

        [Header("아래")]
        [SerializeField] internal TMP_Text messageText;
        [SerializeField] internal TMP_Text hintText;

        [Header("색")]
        [SerializeField] internal Color tabOnColor = new Color(1f, 0.85f, 0.4f);
        [SerializeField] internal Color tabOffColor = new Color(0.65f, 0.7f, 0.82f);
        [SerializeField] internal Color okColor = new Color(0.55f, 1f, 0.65f);
        [SerializeField] internal Color badColor = new Color(1f, 0.45f, 0.45f);
        [SerializeField] internal float messageSeconds = 1.6f;

        internal const int TabCount = 4;
        StatusTab _tab = StatusTab.Character;
        InputAction _tabKeys, _prevTab, _nextTab;
        Tween _msgTween;
        IPointsSource _points;
        IStatSource _stats;
        ISkillLoadoutSource _loadout;
        IStageMapSource _map;
        IQuestApi _quests;
        ICombatPathSource _path;
        IShopSource _shop;
        Vector2 _bigPos, _smallPos;
        bool _posCaptured;

        public StatusTab CurrentTab => _tab;
        public event Action<StatusTab> TabChanged;

        // ───────────── IStatusApi ─────────────

        public void Open(StatusTab tab)
        {
            var ui = UIManager.Instance;
            if (ui == null) return;
            if (IsOpen) { SetTab(tab); return; }
            ui.Open(this, tab);
        }
        public void SetItemShop(ShopData shop)
        {
            foreach (var p in pages)
                if (p is StatusItemPage ip) ip.Shop = shop;
        }

        /// <summary>계열 선택 소스 (캐릭터 탭)</summary>
        internal ICombatPathSource Path => _path;
        /// <summary>아이템 구매 소스 (상점 탭)</summary>
        internal IShopSource ItemShop => _shop;
        // Close() · IsOpen 은 UIWindow 것

        // ───────────── 수명 ─────────────

        protected override void Awake()
        {
            base.Awake();
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int t = i;
                if (tabButtons[i] != null) tabButtons[i].onClick.AddListener(() => SetTab((StatusTab)t));
            }
            for (int i = 0; i < pages.Length; i++)
                if (pages[i] != null) { pages[i].Owner = this; pages[i].gameObject.SetActive(false); }

            _tabKeys = new InputAction("UI_StatusTabKey", InputActionType.Button);
            for (int i = 1; i <= TabCount; i++) _tabKeys.AddBinding("<Keyboard>/" + i);
            _tabKeys.performed += ctx =>
            {
                if (!IsOpen || ctx.control == null) return;
                if (int.TryParse(ctx.control.name, out int n) && n >= 1 && n <= TabCount && n <= pages.Length) SetTab((StatusTab)(n - 1));
            };
            _prevTab = new InputAction("UI_StatusPrev", InputActionType.Button, "<Gamepad>/leftShoulder");
            _nextTab = new InputAction("UI_StatusNext", InputActionType.Button, "<Gamepad>/rightShoulder");
            _prevTab.performed += _ => { if (IsOpen) SetTab((StatusTab)(((int)_tab + pages.Length - 1) % pages.Length)); };
            _nextTab.performed += _ => { if (IsOpen) SetTab((StatusTab)(((int)_tab + 1) % pages.Length)); };
        }

        // 중복 UIRoot(UIManager가 지우는 쪽)의 창이 등록을 덮어쓰지 않게
        bool IsLiveRoot => UIManager.Instance == null || GetComponentInParent<UIManager>() == UIManager.Instance;
        void OnEnable() { if (IsLiveRoot) GameUI.Register((IStatusApi)this); }
        void OnDisable() { GameUI.Unregister(this); _tabKeys?.Disable(); _prevTab?.Disable(); _nextTab?.Disable(); }
        void OnDestroy()
        {
            UISources.Changed -= Rebind;
            Unbind();
            _tabKeys?.Dispose(); _prevTab?.Dispose(); _nextTab?.Dispose();
        }

        protected override void OnSetup(object args)
        {
            if (args is StatusTab t) _tab = t;
        }

        protected override void OnOpened()
        {
            UISources.Changed += Rebind;
            Rebind();
            _tabKeys.Enable(); _prevTab.Enable(); _nextTab.Enable();
            ShowMessage("", true, true);
            ApplyTab(false);
        }

        protected override void OnClosed()
        {
            UISources.Changed -= Rebind;
            Unbind();
            _tabKeys.Disable(); _prevTab.Disable(); _nextTab.Disable();
            var page = Page(_tab);
            if (page != null) page.OnHide();
        }

        // ───────────── 탭 ─────────────

        internal StatusPage Page(StatusTab t) => (int)t >= 0 && (int)t < pages.Length ? pages[(int)t] : null;

        void SetTab(StatusTab tab)
        {
            if (tab == _tab && IsOpen) { Page(tab)?.Focus(); return; }
            var old = Page(_tab);
            if (old != null) old.OnHide();
            _tab = tab;
            UISfx.Play(UISound.Tab);
            ApplyTab(true);
            TabChanged?.Invoke(_tab);
        }

        void ApplyTab(bool animate)
        {
            for (int i = 0; i < pages.Length; i++)
                if (pages[i] != null) pages[i].gameObject.SetActive(i == (int)_tab);
            for (int i = 0; i < tabMarks.Length; i++)
            {
                if (tabMarks[i] == null) continue;
                bool on = i == (int)_tab;
                if (tabActive != null && tabInactive != null) tabMarks[i].sprite = on ? tabActive : tabInactive;
                else tabMarks[i].enabled = on;
            }
            for (int i = 0; i < tabLabels.Length; i++)
                if (tabLabels[i] != null) tabLabels[i].color = i == (int)_tab ? tabOnColor : tabOffColor;

            var page = Page(_tab);
            if (page == null) return;
            page.OnShow();
            page.Refresh();
            page.Focus();
            if (hintText != null) hintText.text = $"[1~{pages.Length}] 탭   " + page.Hint + "   [Tab]/[ESC] 닫기";
            if (animate && page.transform is RectTransform rt) rt.SlideIn(Vector2.down, 6f, UITweenStyle.Fast);
            RefreshPoints(0);
        }

        // ───────────── 데이터 ─────────────

        internal IPointsSource Points => _points;
        internal IStatSource Stats => _stats;
        internal ISkillLoadoutSource Loadout => _loadout;
        internal IGrowthShopSource GrowthShop => UISources.GrowthShop;
        internal IStageMapSource StageMap => _map;
        internal IQuestApi Quests => _quests;

        void Rebind()
        {
            Unbind();
            _points = UISources.Points;
            _stats = UISources.Stats;
            _loadout = UISources.Loadout;
            _map = UISources.StageMap;
            _quests = GameUI.Quest;
            _path = UISources.Path;
            _shop = UISources.Shop;
            if (_path != null) _path.PathChanged += RefreshPage;
            if (_shop != null) _shop.ShopChanged += OnShop;
            if (_points != null) _points.PointsChanged += OnPoints;
            if (_stats != null) _stats.StatChanged += OnStat;
            if (_loadout != null) { _loadout.SkillOwned += OnSkillOwned; _loadout.SlotChanged += OnSlot; }
            if (_map != null) _map.NodesChanged += RefreshPage;
            _quests.Changed += RefreshPage;
            if (IsOpen) { RefreshPoints(0); RefreshPage(); }
        }

        void Unbind()
        {
            if (_points != null) _points.PointsChanged -= OnPoints;
            if (_stats != null) _stats.StatChanged -= OnStat;
            if (_loadout != null) { _loadout.SkillOwned -= OnSkillOwned; _loadout.SlotChanged -= OnSlot; }
            if (_map != null) _map.NodesChanged -= RefreshPage;
            if (_quests != null) _quests.Changed -= RefreshPage;
            if (_path != null) _path.PathChanged -= RefreshPage;
            if (_shop != null) _shop.ShopChanged -= OnShop;
            _points = null; _stats = null; _loadout = null; _map = null; _quests = null; _path = null; _shop = null;
        }

        void OnPoints(int value, int delta) { RefreshPoints(delta); RefreshPage(); }
        void OnStat(StatKind _) => RefreshPage();
        void OnSkillOwned(string _) => RefreshPage();
        void OnSlot(SkillSlot _) => RefreshPage();
        void OnShop() { RefreshPoints(0); RefreshPage(); }

        void RefreshPage()
        {
            if (!IsOpen) return;
            Page(_tab)?.Refresh();
        }

        /// <summary>머리 재화 글자만 다시</summary>
        internal void RefreshHeader() => RefreshPoints(0);

        void RefreshPoints(int delta)
        {
            if (pointsText == null) return;
            pointsText.text = _points != null ? $"성장 {_points.Points:N0}P" : "성장 -P";
            if (itemPointsText != null) itemPointsText.text = _shop != null ? $"아이템 {_shop.Currency:N0}P" : "아이템 -P";

            // 지금 탭이 쓰는 재화를 크게·오른쪽에, 다른 쪽은 작게·왼쪽에
            if (itemPointsText != null)
            {
                if (!_posCaptured)
                {
                    _bigPos = pointsText.rectTransform.anchoredPosition;
                    _smallPos = itemPointsText.rectTransform.anchoredPosition;
                    _posCaptured = true;
                }
                bool item = IsOpen && Page(_tab) != null && Page(_tab).UsesItemPoints;
                var big = item ? itemPointsText : pointsText;
                var small = item ? pointsText : itemPointsText;
                big.rectTransform.anchoredPosition = _bigPos;
                small.rectTransform.anchoredPosition = _smallPos;
                big.fontSize = currencyBigSize; small.fontSize = currencySmallSize;
                pointsText.color = item ? tabOffColor : tabOnColor;
                itemPointsText.color = item ? itemPointsColor : tabOffColor;
                if (pointsPunch != null && pointsPunch != big.rectTransform) pointsPunch = big.rectTransform;
            }
            if (delta != 0 && pointsPunch != null) pointsPunch.Punch(0.25f);
        }

        // ───────────── 페이지 공용 ─────────────

        internal void ShowMessage(string text, bool ok, bool instant = false)
        {
            if (messageText == null) return;
            _msgTween?.Kill();
            messageText.text = text;
            messageText.color = ok ? okColor : badColor;
            messageText.alpha = 1f;
            if (instant || string.IsNullOrEmpty(text)) return;
            _msgTween = messageText.DOFade(0f, 0.4f).SetDelay(messageSeconds).SetUpdate(true).SetLink(messageText.gameObject);
        }

        static bool EndsWithBatchim(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            char c = s[s.Length - 1];
            return c >= 0xAC00 && c <= 0xD7A3 && (c - 0xAC00) % 28 != 0;
        }

        /// <summary>구매 결과 → 소리·문구 (성공 true)</summary>
        internal bool Report(ShopBuyResult result, string successText, string currencyName = "성장 포인트", string limitText = null)
        {
            switch (result)
            {
                case ShopBuyResult.Success:
                    UISfx.Play(UISound.Confirm);
                    ShowMessage(successText, true);
                    return true;
                case ShopBuyResult.NotEnoughCurrency:
                    UISfx.Play(UISound.Error);
                    ShowMessage($"{currencyName}{(EndsWithBatchim(currencyName) ? "이" : "가")} 부족하다", false);
                    if (pointsPunch != null) pointsPunch.Shake(3f, 0.2f);
                    return false;
                case ShopBuyResult.LimitReached:
                    UISfx.Play(UISound.Error);
                    ShowMessage(limitText ?? "더 올릴 수 없다 (최대 단계)", false);
                    return false;
                default:
                    UISfx.Play(UISound.Error);
                    ShowMessage("지금은 살 수 없다", false);
                    return false;
            }
        }
    }
}
