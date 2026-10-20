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
    /// [v1.1] 상태창 1번 "캐릭터" 탭 = 스탯 + 스킬.
    ///   위: 계열 [물리] [마법] — 한쪽을 찍으면 그 계열 스탯(힘/마력)·기술만 작동, 다른 쪽은 잠김 (ICombatPathSource)
    ///   왼쪽: 스탯 4줄, 각 줄 [+] = 성장 포인트로 1단계 투자 (IGrowthShopSource.TryBuyStat)
    ///   가운데·오른쪽: 물리 기술 / 마법 기술, 각 줄 [+] = 다음 단계 (0→1 배우기). 단계 수 = SkillOffer.ranks 길이
    ///   아래: Q/E/R 장착 — 기술을 고르고 Q/E/R, X = 그 기술 빼기
    /// 조작: 방향키 고르기 (←→ 열 이동 · ↑↓ 줄 이동) · Enter/[+] 투자 · Q/E/R 장착 · X 빼기
    /// </summary>
    [AddComponentMenu("OZ/UI/Status/Status Character Page")]
    public class StatusCharacterPage : StatusPage
    {
        [Header("계열")]
        [SerializeField] internal Button physicalButton;
        [SerializeField] internal Button magicButton;
        [SerializeField] internal Image physicalMark;
        [SerializeField] internal Image magicMark;
        [SerializeField] internal TMP_Text pathText;

        [Header("스탯 (StatKind 순서)")]
        [SerializeField] internal GrowthCardView[] statRows = new GrowthCardView[4];

        [Header("기술")]
        [SerializeField] internal RectTransform physicalList;
        [SerializeField] internal RectTransform magicList;
        [SerializeField] internal GrowthCardView skillTemplate;
        [SerializeField] internal TMP_Text physicalHeader;
        [SerializeField] internal TMP_Text magicHeader;

        [Header("장착 Q/E/R")]
        [SerializeField] internal GrowthCardView[] slotCards = new GrowthCardView[3];
        [SerializeField] internal TMP_Text emptyText;

        // 열: 0 = 스탯, 1 = 물리 기술, 2 = 마법 기술
        readonly List<GrowthCardView>[] _cols = { new List<GrowthCardView>(), new List<GrowthCardView>(), new List<GrowthCardView>() };
        readonly List<string> _physIds = new List<string>();
        readonly List<string> _magIds = new List<string>();
        int _col, _row;
        InputAction _left, _right, _up, _down, _q, _e, _r, _x;

        internal override string Hint => "[방향키] 고르기   [Enter]/[+] 투자   [Q/E/R] 장착   [X] 빼기";

        void Awake()
        {
            if (skillTemplate != null) skillTemplate.gameObject.SetActive(false);
            for (int i = 0; i < statRows.Length; i++)
            {
                if (statRows[i] == null) continue;
                int idx = i;
                Hook(statRows[i], () => SelectAt(0, idx));
                _cols[0].Add(statRows[i]);
            }
            for (int i = 0; i < slotCards.Length; i++)
            {
                if (slotCards[i] == null) continue;
                var slot = (SkillSlot)i;
                slotCards[i].Picked += _ => Equip(slot);
                slotCards[i].Action += _ => Unequip(slot);
                NoNav(slotCards[i]);
            }
            if (physicalButton != null) { physicalButton.onClick.AddListener(() => ChoosePath(CombatPath.Physical)); NoNav(physicalButton); }
            if (magicButton != null) { magicButton.onClick.AddListener(() => ChoosePath(CombatPath.Magic)); NoNav(magicButton); }

            _left = Key("UI_ChLeft", "<Keyboard>/leftArrow", "<Keyboard>/a", "<Gamepad>/dpad/left", "<Gamepad>/leftStick/left");
            _right = Key("UI_ChRight", "<Keyboard>/rightArrow", "<Keyboard>/d", "<Gamepad>/dpad/right", "<Gamepad>/leftStick/right");
            _up = Key("UI_ChUp", "<Keyboard>/upArrow", "<Keyboard>/w", "<Gamepad>/dpad/up", "<Gamepad>/leftStick/up");
            _down = Key("UI_ChDown", "<Keyboard>/downArrow", "<Keyboard>/s", "<Gamepad>/dpad/down", "<Gamepad>/leftStick/down");
            _q = Key("UI_ChQ", "<Keyboard>/q");
            _e = Key("UI_ChE", "<Keyboard>/e");
            _r = Key("UI_ChR", "<Keyboard>/r");
            _x = Key("UI_ChX", "<Keyboard>/x", "<Gamepad>/buttonWest");
            _left.performed += _ => Move(-1, 0);
            _right.performed += _ => Move(+1, 0);
            _up.performed += _ => Move(0, -1);
            _down.performed += _ => Move(0, +1);
            _q.performed += _ => Equip(SkillSlot.Q);
            _e.performed += _ => Equip(SkillSlot.E);
            _r.performed += _ => Equip(SkillSlot.R);
            _x.performed += _ => UnequipSelected();
        }

        static InputAction Key(string name, params string[] paths)
        {
            var a = new InputAction(name, InputActionType.Button);
            foreach (var p in paths) a.AddBinding(p);
            return a;
        }

        static void NoNav(Selectable s) { if (s != null) s.navigation = new Navigation { mode = Navigation.Mode.None }; }
        static void NoNav(GrowthCardView v)
        {
            NoNav(v.GetComponent<Selectable>());
            if (v.actionButton != null) NoNav(v.actionButton);
        }

        void Hook(GrowthCardView v, System.Action onPick)
        {
            v.Picked += _ => onPick();
            v.Action += _ => { onPick(); Invest(); };
            NoNav(v);
        }

        IEnumerable<InputAction> Keys() { yield return _left; yield return _right; yield return _up; yield return _down; yield return _q; yield return _e; yield return _r; yield return _x; }
        void OnDestroy() { foreach (var k in Keys()) k?.Dispose(); }
        internal override void OnShow() { foreach (var k in Keys()) k.Enable(); }
        internal override void OnHide() { foreach (var k in Keys()) k.Disable(); }

        internal override void Focus()
        {
            var c = Current;
            if (c != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(c.gameObject);
        }

        GrowthCardView Current => _col >= 0 && _col < 3 && _row >= 0 && _row < _cols[_col].Count ? _cols[_col][_row] : null;

        // ───────────── 데이터 ─────────────

        ISkillLoadoutSource Lo => Owner != null ? Owner.Loadout : null;
        CombatPath Path => Owner != null && Owner.Path != null ? Owner.Path.Path : CombatPath.None;
        int Points => Owner != null && Owner.Points != null ? Owner.Points.Points : 0;

        static SkillOffer Find(ISkillLoadoutSource lo, string id)
        {
            if (lo == null || string.IsNullOrEmpty(id)) return default;
            for (int i = 0; i < lo.SkillCount; i++) { var o = lo.GetSkill(i); if (o.id == id) return o; }
            return default;
        }

        string SelectedSkillId()
        {
            if (_col == 1 && _row < _physIds.Count) return _physIds[_row];
            if (_col == 2 && _row < _magIds.Count) return _magIds[_row];
            return null;
        }

        void BuildSkills()
        {
            var lo = Lo;
            _physIds.Clear(); _magIds.Clear();
            if (lo != null)
                for (int i = 0; i < lo.SkillCount; i++)
                {
                    var o = lo.GetSkill(i);
                    if (!o.IsValid) continue;
                    (o.damageKind == SkillDamageKind.Magic ? _magIds : _physIds).Add(o.id);
                }
            Ensure(_cols[1], physicalList, _physIds.Count, 1);
            Ensure(_cols[2], magicList, _magIds.Count, 2);
        }

        void Ensure(List<GrowthCardView> col, RectTransform parent, int count, int colIndex)
        {
            if (skillTemplate == null || parent == null) return;
            while (col.Count < count)
            {
                var v = Instantiate(skillTemplate, parent);
                int row = col.Count;
                Hook(v, () => SelectAt(colIndex, row));
                col.Add(v);
            }
            for (int i = 0; i < col.Count; i++) col[i].gameObject.SetActive(i < count);
        }

        int ColCount(int c) => c == 0 ? _cols[0].Count : c == 1 ? _physIds.Count : _magIds.Count;

        // ───────────── 이동 ─────────────

        void Move(int dx, int dy)
        {
            if (!isActiveAndEnabled) return;
            int col = _col, row = _row;
            if (dx != 0)
            {
                for (int step = 0; step < 3; step++)
                {
                    col = (col + dx + 3) % 3;
                    if (ColCount(col) > 0) break;
                }
                row = Mathf.Clamp(row, 0, Mathf.Max(0, ColCount(col) - 1));
            }
            else
            {
                row += dy;
                if (row < 0 || row >= ColCount(col)) return;
            }
            SelectAt(col, row);
            Focus();
        }

        void SelectAt(int col, int row)
        {
            if (ColCount(col) == 0) return;
            row = Mathf.Clamp(row, 0, ColCount(col) - 1);
            if (col != _col || row != _row) { _col = col; _row = row; UISfx.Play(UISound.Hover); }
            Refresh();
        }

        // ───────────── 동작 ─────────────

        void ChoosePath(CombatPath p)
        {
            var src = Owner != null ? Owner.Path : null;
            if (src == null) { UISfx.Play(UISound.Error); Owner?.ShowMessage("계열 소스가 연결되지 않았습니다 (ICombatPathSource)", false); return; }
            if (src.Path == p) return;
            var r = src.TrySetPath(p);
            if (r == ShopBuyResult.Success) { UISfx.Play(UISound.Confirm); Owner.ShowMessage($"{CombatPathRules.Name(p)} 계열 선택", true); }
            else { UISfx.Play(UISound.Error); Owner.ShowMessage("계열은 한 번 정하면 바꿀 수 없다", false); }
            Refresh();
        }

        void Invest()
        {
            if (Owner == null) return;
            var shop = Owner.GrowthShop;
            if (shop == null) { UISfx.Play(UISound.Error); Owner.ShowMessage("성장 소스가 연결되지 않았습니다 (IGrowthShopSource)", false); return; }
            var path = Path;
            if (_col == 0)
            {
                var s = (StatKind)_row;
                if (CombatPathRules.IsLocked(path, s)) { Locked(StatusText.StatName(s)); return; }
                var res = shop.TryBuyStat(s);
                if (Owner.Report(res, $"{StatusText.StatName(s)} +1", limitText: $"{StatusText.StatName(s)}은(는) 최대 단계다")) Current?.transform.GetComponent<RectTransform>().Punch(0.12f);
            }
            else
            {
                var lo = Lo;
                var o = Find(lo, SelectedSkillId());
                if (!o.IsValid) return;
                if (CombatPathRules.IsLocked(path, o.damageKind)) { Locked(o.displayName); return; }
                int rank = lo.GetRank(o.id);
                var res = shop.TryBuySkill(o.id);
                string ok = rank == 0 ? $"{o.displayName} 배움 — Q/E/R로 장착" : $"{o.displayName} {rank + 1}단계";
                if (Owner.Report(res, ok, limitText: $"{o.displayName}은(는) 최대 단계다")) Current?.transform.GetComponent<RectTransform>().Punch(0.12f);
            }
            Refresh();
            Focus();
        }

        void Locked(string what)
        {
            UISfx.Play(UISound.Error);
            Owner.ShowMessage($"{CombatPathRules.Name(Path)} 계열이라 {what}은(는) 잠겨 있다", false);
            Current?.transform.GetComponent<RectTransform>().Shake(3f, 0.2f);
        }

        void Equip(SkillSlot slot)
        {
            var lo = Lo;
            string id = SelectedSkillId();
            if (lo == null) return;
            if (string.IsNullOrEmpty(id)) { UISfx.Play(UISound.Error); Owner.ShowMessage("기술을 먼저 고르세요 (물리·마법 기술 줄)", false); return; }
            var o = Find(lo, id);
            if (lo.GetRank(id) <= 0) { UISfx.Play(UISound.Error); Owner.ShowMessage($"{o.displayName}은(는) 아직 안 배웠다 — [+]로 배우기", false); return; }
            if (CombatPathRules.IsLocked(Path, o.damageKind)) { Locked(o.displayName); return; }
            if (lo.GetEquipped(slot) == id) return;
            if (!lo.TryEquip(slot, id)) { UISfx.Play(UISound.Error); return; }
            UISfx.Play(UISound.ItemPlace);
            int s = (int)slot;
            if (s < slotCards.Length && slotCards[s] != null) ((RectTransform)slotCards[s].transform).Punch(0.2f);
            Owner.ShowMessage($"{o.displayName} → {slot}", true);
            Refresh();
            Focus();
        }

        void UnequipSelected()
        {
            var lo = Lo;
            string id = SelectedSkillId();
            if (lo == null || string.IsNullOrEmpty(id)) return;
            for (int i = 0; i < 3; i++) if (lo.GetEquipped((SkillSlot)i) == id) { Unequip((SkillSlot)i); return; }
        }

        void Unequip(SkillSlot slot)
        {
            var lo = Lo;
            if (lo == null || string.IsNullOrEmpty(lo.GetEquipped(slot))) return;
            lo.Unequip(slot);
            UISfx.Play(UISound.ItemPick);
            Owner.ShowMessage($"{slot} 칸 비움", true);
            Refresh();
            Focus();
        }

        static string SlotOf(ISkillLoadoutSource lo, string id)
        {
            for (int i = 0; i < 3; i++) if (lo.GetEquipped((SkillSlot)i) == id) return ((SkillSlot)i).ToString();
            return null;
        }

        // ───────────── 그리기 ─────────────

        internal override void Refresh()
        {
            BuildSkills();
            var path = Path;
            int points = Points;
            var shop = Owner != null ? Owner.GrowthShop : null;

            // 계열
            if (physicalMark != null) physicalMark.enabled = path == CombatPath.Physical;
            if (magicMark != null) magicMark.enabled = path == CombatPath.Magic;
            if (pathText != null)
                pathText.text = Owner == null || Owner.Path == null ? "계열 소스 없음 (ICombatPathSource)"
                    : path == CombatPath.None ? "계열 미선택 — 힘·물리 기술 또는 마력·마법 기술에 투자하면 그쪽으로 정해진다"
                    : $"{CombatPathRules.Name(path)} 계열 — {(path == CombatPath.Physical ? "마력·마법 기술" : "힘·물리 기술")} 잠김";

            // 스탯
            var stats = Owner != null ? Owner.Stats : null;
            for (int i = 0; i < statRows.Length && i < 4; i++)
            {
                var row = statRows[i];
                if (row == null) continue;
                var s = (StatKind)i;
                bool sel = _col == 0 && _row == i;
                if (stats == null) { row.Set(null, StatusText.StatName(s), StatusColors.Stat(s), StatusText.StatName(s), "스탯 소스 없음", "", 0, 0, null, false, sel); continue; }
                int lv = stats.GetStatLevel(s), max = stats.GetStatMaxLevel(s);
                bool maxed = lv >= max, locked = CombatPathRules.IsLocked(path, s);
                int price = shop != null ? shop.GetStatPrice(s) : 0;
                string sub = locked ? $"{(s == StatKind.Strength ? "물리" : "마법")} 계열 전용 · 잠김"
                    : maxed ? StatusText.StatShort(s, stats.GetStatValue(s)) + " (최대)"
                    : $"{StatusText.StatShort(s, stats.GetStatValue(s))} → {StatusText.StatShort(s, stats.GetStatNextValue(s))}";
                string act = locked ? "잠김" : maxed ? "최대" : $"+ {price:N0}P";
                row.Set(null, StatusText.StatName(s), StatusColors.Stat(s), StatusText.StatName(s), sub, "",
                    lv, max, act, !locked && !maxed && shop != null && price <= points, sel);
                row.SetLocked(locked);
            }

            // 기술
            var lo = Lo;
            DrawSkills(lo, _physIds, _cols[1], 1, path, points);
            DrawSkills(lo, _magIds, _cols[2], 2, path, points);
            if (physicalHeader != null) physicalHeader.alpha = CombatPathRules.IsLocked(path, SkillDamageKind.Physical) ? 0.45f : 1f;
            if (magicHeader != null) magicHeader.alpha = CombatPathRules.IsLocked(path, SkillDamageKind.Magic) ? 0.45f : 1f;
            if (emptyText != null) emptyText.gameObject.SetActive(lo == null);

            // Q/E/R
            for (int i = 0; i < slotCards.Length; i++)
            {
                if (slotCards[i] == null) continue;
                var slot = (SkillSlot)i;
                var o = Find(lo, lo?.GetEquipped(slot));
                if (o.IsValid)
                {
                    float remain = lo.GetCooldownRemaining(slot);
                    slotCards[i].Set(o.icon, StatusShopPage.FirstChar(o.displayName), StatusColors.Skill(o.damageKind), $"{slot}  {o.displayName}",
                        remain > 0.05f ? $"쿨 {remain:0.0}초" : $"{lo.GetRank(o.id)}단계 · 피해 {lo.GetDamage(o.id)}", "", 0, 0, "빼기", true, false);
                }
                else slotCards[i].Set(null, slot.ToString(), StatusColors.Dim, $"{slot}  비어 있음", "기술 고르고 " + slot, "", 0, 0, null, false, false);
            }
        }

        void DrawSkills(ISkillLoadoutSource lo, List<string> ids, List<GrowthCardView> cards, int colIndex, CombatPath path, int points)
        {
            for (int i = 0; i < ids.Count && i < cards.Count; i++)
            {
                var o = Find(lo, ids[i]);
                int rank = lo.GetRank(o.id), max = o.MaxRank;
                bool locked = CombatPathRules.IsLocked(path, o.damageKind), maxed = rank >= max;
                int price = maxed ? 0 : o.RankPrice(rank + 1);
                string where = SlotOf(lo, o.id);
                string title = where != null ? $"{o.displayName}  [{where}]" : o.displayName;
                string next = maxed ? "" : o.RankDescription(rank + 1);
                string sub = locked ? $"{StatusText.DamageKind(o.damageKind)} 계열 전용 · 잠김"
                    : rank == 0 ? $"피해 {lo.GetDamage(o.id)} · 쿨 {o.cooldown:0.#}초"
                    : maxed ? $"최대 단계 · 피해 {lo.GetDamage(o.id)}"
                    : $"{rank + 1}단계: {(string.IsNullOrEmpty(next) ? "[추가 기능]" : next)}";
                string act = locked ? "잠김" : maxed ? "최대" : $"+ {price:N0}P";
                cards[i].Set(o.icon, StatusShopPage.FirstChar(o.displayName), StatusColors.Skill(o.damageKind), title, sub, "",
                    rank, max, act, !locked && !maxed && price <= points, _col == colIndex && _row == i);
                cards[i].SetLocked(locked);
            }
        }
    }
}
