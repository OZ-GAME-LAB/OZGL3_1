using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>Q/E/R 3칸 묶음. ISkillSource 이벤트를 각 슬롯으로 분배한다.</summary>
    [AddComponentMenu("OZ/UI/HUD/Skill Bar View")]
    public class SkillBarView : MonoBehaviour
    {
        [SerializeField] internal SkillSlotView[] slots = new SkillSlotView[3];

        ISkillSource _source;

        internal void Bind(ISkillSource source)
        {
            Unsubscribe();
            _source = source;
            if (_source != null)
            {
                _source.CooldownStarted += OnCooldownStarted;
                _source.SkillChanged += OnSkillChanged;
                _source.SkillUseFailed += OnUseFailed;
            }
            RefreshAll();
        }

        void Unsubscribe()
        {
            if (_source == null) return;
            _source.CooldownStarted -= OnCooldownStarted;
            _source.SkillChanged -= OnSkillChanged;
            _source.SkillUseFailed -= OnUseFailed;
        }

        void OnDestroy() => Unsubscribe();

        internal void RefreshAll()
        {
            foreach (var s in slots) if (s != null) s.Refresh(_source);
        }

        SkillSlotView Find(SkillSlot slot)
        {
            foreach (var s in slots) if (s != null && s.Slot == slot) return s;
            return null;
        }

        void OnCooldownStarted(SkillSlot slot, float duration) => Find(slot)?.OnCooldownStarted(duration);
        void OnSkillChanged(SkillSlot slot) => Find(slot)?.Refresh(_source);
        void OnUseFailed(SkillSlot slot, SkillUseFailReason reason) => Find(slot)?.OnUseFailed(reason);
    }
}
