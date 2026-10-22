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
    /// <summary>
    /// 스킬 탭: 위 Q/E/R 칸 · 아래 보유 기술. 보유 기술을 고르고 Q/E/R(키 또는 칸 클릭)로 장착, 칸의 버튼으로 해제.
    /// 같은 기술은 한 칸만 (다른 칸에 있으면 옮겨짐). 장착을 바꿔도 남은 쿨다운은 유지 (구현 쪽).
    /// </summary>
    [AddComponentMenu("OZ/UI/Status/Status Skill Page")]
    public class StatusSkillPage : StatusPage
    {
        [SerializeField] internal GrowthCardView[] slotCards = new GrowthCardView[3];
        [SerializeField] internal RectTransform grid;
        [SerializeField] internal GrowthCardView cardTemplate;
        [SerializeField] internal TMP_Text emptyText;

        CardPool _pool;
        readonly List<string> _owned = new List<string>();
        int _selected;
        InputAction _q, _e, _r;

        internal override string Hint => "[방향키] 고르기   [Q/E/R] 그 칸에 장착   칸의 [빼기] 해제";

        CardPool Pool
        {
            get
            {
                if (_pool != null) return _pool;
                _pool = new CardPool(cardTemplate, grid) { Columns = 3 };
                _pool.OnPicked = v => { if (v.Index != _selected) { _selected = v.Index; Refresh(); } };
                _pool.OnAction = v => { _selected = v.Index; EquipToFirstFree(); };
                return _pool;
            }
        }

        void Awake()
        {
            for (int i = 0; i < slotCards.Length; i++)
            {
                if (slotCards[i] == null) continue;
                var slot = (SkillSlot)i;
                slotCards[i].Picked += _ => { if (_owned.Count > 0) Equip(slot); };
                slotCards[i].Action += _ => Unequip(slot);
                var sel = slotCards[i].GetComponent<Selectable>();
                if (sel != null) sel.navigation = new Navigation { mode = Navigation.Mode.None };
                if (slotCards[i].actionButton != null) slotCards[i].actionButton.navigation = new Navigation { mode = Navigation.Mode.None };
            }
            _q = new InputAction("UI_EquipQ", InputActionType.Button, "<Keyboard>/q");
            _e = new InputAction("UI_EquipE", InputActionType.Button, "<Keyboard>/e");
            _r = new InputAction("UI_EquipR", InputActionType.Button, "<Keyboard>/r");
            _q.performed += _ => Equip(SkillSlot.Q);
            _e.performed += _ => Equip(SkillSlot.E);
            _r.performed += _ => Equip(SkillSlot.R);
        }

        void OnDestroy() { _q?.Dispose(); _e?.Dispose(); _r?.Dispose(); }
        internal override void OnShow() { _q.Enable(); _e.Enable(); _r.Enable(); }
        internal override void OnHide() { _q.Disable(); _e.Disable(); _r.Disable(); }
        internal override void Focus() => Pool.Focus(_selected);

        internal override void Refresh()
        {
            var lo = Owner != null ? Owner.Loadout : null;
            _owned.Clear();
            if (lo != null)
                for (int i = 0; i < lo.SkillCount; i++) { var o = lo.GetSkill(i); if (lo.IsOwned(o.id)) _owned.Add(o.id); }
            _selected = Mathf.Clamp(_selected, 0, Mathf.Max(0, _owned.Count - 1));

            // Q/E/R 칸
            for (int i = 0; i < slotCards.Length; i++)
            {
                if (slotCards[i] == null) continue;
                var slot = (SkillSlot)i;
                string id = lo?.GetEquipped(slot);
                var o = Find(lo, id);
                if (o.IsValid)
                    slotCards[i].Set(o.icon, FirstOr(o.displayName, slot), StatusColors.Skill(o.damageKind), o.displayName,
                        $"{slot} · {StatusText.DamageKind(o.damageKind)}", CooldownText(lo, slot, o), 0, 0, "빼기", true, false);
                else
                    slotCards[i].Set(null, slot.ToString(), StatusColors.Dim, "비어 있음", $"{slot} 칸", _owned.Count > 0 ? slot + " 키로 장착" : "상점에서 구매", 0, 0, null, false, false);
            }

            // 보유 기술
            Pool.Ensure(_owned.Count);
            if (emptyText != null) emptyText.gameObject.SetActive(_owned.Count == 0);
            for (int i = 0; i < _owned.Count; i++)
            {
                var o = Find(lo, _owned[i]);
                string where = SlotOf(lo, o.id);
                Pool.Cards[i].Set(o.icon, PageFirst(o.displayName), StatusColors.Skill(o.damageKind), o.displayName,
                    $"{StatusText.DamageKind(o.damageKind)} · 쿨다운 {o.cooldown:0.#}초",
                    where != null ? $"장착 중: {where}" : $"피해 {lo.GetDamage(o.id)}",
                    0, 0, where != null ? where + " 장착됨" : "장착", where == null, i == _selected);
            }
        }

        static string PageFirst(string s) => StatusShopPage.FirstChar(s);
        static string FirstOr(string s, SkillSlot slot) => string.IsNullOrEmpty(s) ? slot.ToString() : StatusShopPage.FirstChar(s);

        static SkillOffer Find(ISkillLoadoutSource lo, string id)
        {
            if (lo == null || string.IsNullOrEmpty(id)) return default;
            for (int i = 0; i < lo.SkillCount; i++) { var o = lo.GetSkill(i); if (o.id == id) return o; }
            return default;
        }

        static string SlotOf(ISkillLoadoutSource lo, string id)
        {
            if (lo == null) return null;
            for (int i = 0; i < 3; i++) if (lo.GetEquipped((SkillSlot)i) == id) return ((SkillSlot)i).ToString();
            return null;
        }

        static string CooldownText(ISkillLoadoutSource lo, SkillSlot slot, SkillOffer o)
        {
            float remain = lo.GetCooldownRemaining(slot);
            return remain > 0.05f ? $"쿨 {remain:0.0}초" : $"피해 {lo.GetDamage(o.id)}"; // [빼기] 버튼 왼쪽 좁은 칸
        }

        void Equip(SkillSlot slot)
        {
            var lo = Owner != null ? Owner.Loadout : null;
            if (lo == null || _owned.Count == 0) { UISfx.Play(UISound.Error); Owner?.ShowMessage("보유한 기술이 없다 — 상점 탭에서 구매", false); return; }
            string id = _owned[Mathf.Clamp(_selected, 0, _owned.Count - 1)];
            if (lo.GetEquipped(slot) == id) return;
            if (!lo.TryEquip(slot, id)) { UISfx.Play(UISound.Error); Pool.Shake(_selected); return; }
            UISfx.Play(UISound.ItemPlace);
            int s = (int)slot;
            if (s < slotCards.Length && slotCards[s] != null) ((RectTransform)slotCards[s].transform).Punch(0.2f);
            Owner.ShowMessage($"{Find(lo, id).displayName} → {slot}", true);
            Refresh();
            Focus();
        }

        void EquipToFirstFree()
        {
            var lo = Owner != null ? Owner.Loadout : null;
            if (lo == null || _owned.Count == 0) return;
            string id = _owned[Mathf.Clamp(_selected, 0, _owned.Count - 1)];
            for (int i = 0; i < 3; i++) if (lo.GetEquipped((SkillSlot)i) == id) { UISfx.Play(UISound.Error); Owner?.ShowMessage("이미 장착됨 — Q/E/R 키로 칸을 옮길 수 있다", false); return; }
            for (int i = 0; i < 3; i++) if (string.IsNullOrEmpty(lo.GetEquipped((SkillSlot)i))) { Equip((SkillSlot)i); return; }
            Equip(SkillSlot.Q);
        }

        void Unequip(SkillSlot slot)
        {
            var lo = Owner != null ? Owner.Loadout : null;
            if (lo == null || string.IsNullOrEmpty(lo.GetEquipped(slot))) return;
            lo.Unequip(slot);
            UISfx.Play(UISound.ItemPick);
            Owner.ShowMessage($"{slot} 칸 비움", true);
            Refresh();
            Focus();
        }
    }
}
