using System;
using System.Collections.Generic;
using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>상점 탭: 포인트로 기술(한 번 구매·영구 보유)과 스탯(단계별 가격·상한)을 산다. Q = 기술, E = 스탯</summary>
    [AddComponentMenu("OZ/UI/Status/Status Shop Page")]
    public class StatusShopPage : StatusPage
    {
        [SerializeField] internal Button skillsTab;
        [SerializeField] internal Button statsTab;
        [SerializeField] internal Image skillsMark;
        [SerializeField] internal Image statsMark;
        [SerializeField] internal RectTransform grid;
        [Tooltip("기술 3열 / 스탯 2열 칸 크기")]
        [SerializeField] internal GridLayoutGroup layout;
        [SerializeField] internal Vector2 skillCellSize = new Vector2(188, 74);
        [SerializeField] internal Vector2 statCellSize = new Vector2(285, 74);
        [SerializeField] internal GrowthCardView cardTemplate;
        [SerializeField] internal TMP_Text emptyText;
        [SerializeField] internal TMP_Text sectionText;

        CardPool _pool;
        bool _stats;
        int _selected;
        InputAction _switchSkills, _switchStats;

        internal override string Hint => "[Q] 기술 / [E] 스탯   [방향키] 고르기   [Enter] 구매";

        CardPool Pool
        {
            get
            {
                if (_pool != null) return _pool;
                _pool = new CardPool(cardTemplate, grid) { Columns = 3 };
                _pool.OnPicked = v => { if (v.Index != _selected) { _selected = v.Index; Refresh(); } };
                _pool.OnAction = v => { _selected = v.Index; Buy(); };
                return _pool;
            }
        }

        void Awake()
        {
            if (skillsTab != null) skillsTab.onClick.AddListener(() => Switch(false));
            if (statsTab != null) statsTab.onClick.AddListener(() => Switch(true));
            _switchSkills = new InputAction("UI_ShopSkills", InputActionType.Button, "<Keyboard>/q");
            _switchStats = new InputAction("UI_ShopStats", InputActionType.Button, "<Keyboard>/e");
            _switchSkills.performed += _ => Switch(false);
            _switchStats.performed += _ => Switch(true);
        }

        void OnDestroy() { _switchSkills?.Dispose(); _switchStats?.Dispose(); }

        internal override void OnShow() { _switchSkills.Enable(); _switchStats.Enable(); }
        internal override void OnHide() { _switchSkills.Disable(); _switchStats.Disable(); }
        internal override void Focus() => Pool.Focus(_selected);

        /// <summary>튜토리얼 등에서 하위 탭 지정</summary>
        public void ShowStats(bool stats) => Switch(stats);

        void Switch(bool stats)
        {
            if (_stats == stats) return;
            _stats = stats;
            _selected = 0;
            UISfx.Play(UISound.Tab);
            Refresh();
            Focus();
        }

        internal override void Refresh()
        {
            if (skillsMark != null) skillsMark.enabled = !_stats;
            if (statsMark != null) statsMark.enabled = _stats;
            if (sectionText != null) sectionText.text = _stats
                ? "같은 스탯은 살수록 가격이 오른다 · 구매 상한 있음"
                : "기술은 한 번 사면 영구 보유 · 강화 없음 · 스킬 탭에서 Q/E/R 장착";
            int points = Owner != null && Owner.Points != null ? Owner.Points.Points : 0;
            var shop = Owner != null ? Owner.GrowthShop : null;

            if (_stats)
            {
                var stats = Owner != null ? Owner.Stats : null;
                Pool.Columns = 2;
                if (layout != null) { layout.cellSize = statCellSize; layout.constraintCount = 2; }
                Pool.Ensure(stats != null ? 4 : 0);
                if (emptyText != null) { emptyText.gameObject.SetActive(stats == null); emptyText.text = "스탯 소스가 연결되지 않았습니다 (IStatSource)"; }
                if (stats == null) return;
                for (int i = 0; i < 4; i++)
                {
                    var s = (StatKind)i;
                    int lv = stats.GetStatLevel(s), max = stats.GetStatMaxLevel(s);
                    bool maxed = lv >= max;
                    int price = shop != null ? shop.GetStatPrice(s) : 0;
                    string detail = maxed ? StatusText.StatValue(s, stats.GetStatValue(s)) + "  (최대)"
                        : $"{StatusText.StatValue(s, stats.GetStatValue(s))} → {StatusText.StatValue(s, stats.GetStatNextValue(s))}";
                    Pool.Cards[i].Set(null, StatusText.StatName(s), StatusColors.Stat(s), StatusText.StatName(s), StatusText.StatDesc(s), detail,
                        lv, max, maxed ? "최대" : $"{price:N0}P", !maxed && price <= points && shop != null, i == _selected);
                }
            }
            else
            {
                var lo = Owner != null ? Owner.Loadout : null;
                int n = lo != null ? lo.SkillCount : 0;
                Pool.Columns = 3;
                if (layout != null) { layout.cellSize = skillCellSize; layout.constraintCount = 3; }
                Pool.Ensure(n);
                if (emptyText != null) { emptyText.gameObject.SetActive(n == 0); emptyText.text = "기술 소스가 연결되지 않았습니다 (ISkillLoadoutSource)"; }
                for (int i = 0; i < n; i++)
                {
                    var o = lo.GetSkill(i);
                    bool owned = lo.IsOwned(o.id);
                    Pool.Cards[i].Set(o.icon, FirstChar(o.displayName), StatusColors.Skill(o.damageKind), o.displayName,
                        $"{StatusText.DamageKind(o.damageKind)} · 쿨다운 {o.cooldown:0.#}초", $"피해 {lo.GetDamage(o.id)}  (계수 {o.coefficient:0.##})",
                        0, 0, owned ? "보유 중" : $"{o.price:N0}P", !owned && o.price <= points && shop != null, i == _selected);
                }
            }
        }

        void Buy()
        {
            var shop = Owner != null ? Owner.GrowthShop : null;
            if (shop == null) { UISfx.Play(UISound.Error); Owner?.ShowMessage("상점 소스가 연결되지 않았습니다 (IGrowthShopSource)", false); return; }
            ShopBuyResult r;
            string name;
            if (_stats)
            {
                var s = (StatKind)_selected;
                name = StatusText.StatName(s);
                r = shop.TryBuyStat(s);
            }
            else
            {
                var lo = Owner.Loadout;
                if (lo == null || _selected >= lo.SkillCount) return;
                var o = lo.GetSkill(_selected);
                name = o.displayName;
                if (lo.IsOwned(o.id)) { UISfx.Play(UISound.Error); Owner.ShowMessage("이미 보유한 기술", false); Pool.Shake(_selected); return; }
                r = shop.TryBuySkill(o.id);
            }
            if (Owner.Report(r, _stats ? $"{name} +1" : $"{name} 구매 — 스킬 탭에서 장착")) Pool.Punch(_selected);
            else Pool.Shake(_selected);
            Refresh();
            Focus();
        }

        internal static string FirstChar(string s) => string.IsNullOrEmpty(s) ? "?" : s.Substring(0, 1);
    }
}
