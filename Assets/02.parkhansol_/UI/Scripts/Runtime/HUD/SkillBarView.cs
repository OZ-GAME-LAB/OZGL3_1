using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// Q/E/R 3칸 묶음. ISkillSource 이벤트를 각 슬롯으로 분배한다.
    /// v1.0: ISkillLoadoutSource(기획서 v0.5 — 구매·장착)가 등록돼 있으면 그쪽을 우선 (장착 기술 아이콘 · 빈 칸 = 미장착 · 쿨다운).
    /// </summary>
    [AddComponentMenu("OZ/UI/HUD/Skill Bar View")]
    public class SkillBarView : MonoBehaviour
    {
        [SerializeField] internal SkillSlotView[] slots = new SkillSlotView[3];

        ISkillSource _source;
        ISkillLoadoutSource _loadout;

        /// <summary>v1.0 장착 소스 (HudView가 Bind와 함께 부름)</summary>
        internal void BindLoadout(ISkillLoadoutSource loadout)
        {
            if (_loadout != null)
            {
                _loadout.CooldownStarted -= OnCooldownStarted;
                _loadout.SlotChanged -= OnSkillChanged;
                _loadout.SkillUseFailed -= OnUseFailed;
                _loadout.SkillOwned -= OnOwned;
            }
            _loadout = loadout;
            if (_loadout != null)
            {
                _loadout.CooldownStarted += OnCooldownStarted;
                _loadout.SlotChanged += OnSkillChanged;
                _loadout.SkillUseFailed += OnUseFailed;
                _loadout.SkillOwned += OnOwned;
            }
            ResubscribeSource(); // 같은 오브젝트가 둘 다 구현하면 쿨다운·실패 이벤트를 한 번만 받게
            RefreshAll();
        }

        void OnOwned(string _) => RefreshAll();

        internal void Bind(ISkillSource source)
        {
            Unsubscribe();
            _source = source;
            Subscribe();
            RefreshAll();
        }

        bool _sharedEvents; // _source와 _loadout이 같은 오브젝트 → CooldownStarted·SkillUseFailed는 loadout 쪽으로만

        void Subscribe()
        {
            if (_source == null) return;
            _sharedEvents = _loadout != null && ReferenceEquals(_source, _loadout);
            if (!_sharedEvents)
            {
                _source.CooldownStarted += OnCooldownStarted;
                _source.SkillUseFailed += OnUseFailed;
            }
            _source.SkillChanged += OnSkillChanged;
        }

        void ResubscribeSource()
        {
            if (_source == null) return;
            Unsubscribe();
            Subscribe();
        }

        void Unsubscribe()
        {
            if (_source == null) return;
            if (!_sharedEvents)
            {
                _source.CooldownStarted -= OnCooldownStarted;
                _source.SkillUseFailed -= OnUseFailed;
            }
            _source.SkillChanged -= OnSkillChanged;
        }

        void OnDestroy() { Unsubscribe(); BindLoadout(null); }

        internal void RefreshAll()
        {
            foreach (var s in slots) if (s != null) Refresh(s);
        }

        SkillSlotView Find(SkillSlot slot)
        {
            foreach (var s in slots) if (s != null && s.Slot == slot) return s;
            return null;
        }

        void OnCooldownStarted(SkillSlot slot, float duration) => Find(slot)?.OnCooldownStarted(duration);
        void OnSkillChanged(SkillSlot slot) { var s = Find(slot); if (s != null) Refresh(s); }

        void Refresh(SkillSlotView s)
        {
            if (_loadout != null && _loadout.SkillCount > 0) s.RefreshLoadout(_loadout); // 기술 0개 = 예전 방식 소스
            else s.Refresh(_source);
        }
        void OnUseFailed(SkillSlot slot, SkillUseFailReason reason) => Find(slot)?.OnUseFailed(reason);
    }
}
