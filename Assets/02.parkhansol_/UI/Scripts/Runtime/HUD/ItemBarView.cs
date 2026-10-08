using DG.Tweening;
using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>1~4 아이템 바</summary>
    [AddComponentMenu("OZ/UI/HUD/Item Bar View")]
    public class ItemBarView : MonoBehaviour
    {
        [SerializeField] internal ItemSlotView[] slots = new ItemSlotView[4];

        IItemSource _source;

        internal void Bind(IItemSource source)
        {
            Unsubscribe();
            _source = source;
            if (_source != null)
            {
                _source.CountChanged += OnCountChanged;
                _source.ItemUsed += OnItemUsed;
                _source.ItemUseFailed += OnItemUseFailed;
            }
            RefreshAll();
        }

        void Unsubscribe()
        {
            if (_source == null) return;
            _source.CountChanged -= OnCountChanged;
            _source.ItemUsed -= OnItemUsed;
            _source.ItemUseFailed -= OnItemUseFailed;
        }

        void OnDestroy() => Unsubscribe();

        internal void RefreshAll()
        {
            foreach (var s in slots) if (s != null) s.Refresh(_source);
        }

        ItemSlotView Find(int index)
        {
            foreach (var s in slots) if (s != null && s.SlotIndex == index) return s;
            return null;
        }

        void OnCountChanged(int index, int count) => Find(index)?.Refresh(_source);
        void OnItemUsed(int index) => Find(index)?.PlayUsed(_source?.GetItem(index));
        void OnItemUseFailed(int index) => Find(index)?.PlayFailed();
    }
}
