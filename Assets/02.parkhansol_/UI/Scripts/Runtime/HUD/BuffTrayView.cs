using System.Collections.Generic;
using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// 활성 버프 목록. 같은 아이템 버프는 중첩하지 않고 남은 시간만 갱신 (기획서 v0.2).
    /// template은 비활성 자식으로 두면 복제해서 사용.
    /// </summary>
    [AddComponentMenu("OZ/UI/HUD/Buff Tray View")]
    public class BuffTrayView : MonoBehaviour
    {
        [SerializeField] internal BuffIconView template;

        readonly Dictionary<ItemData, BuffIconView> _active = new Dictionary<ItemData, BuffIconView>();
        IItemSource _source;

        void Awake()
        {
            if (template != null) template.gameObject.SetActive(false);
        }

        internal void Bind(IItemSource source)
        {
            Unsubscribe();
            _source = source;
            if (_source != null)
            {
                _source.BuffApplied += OnBuffApplied;
                _source.BuffEnded += OnBuffEnded;
            }
            ClearAll();
        }

        void Unsubscribe()
        {
            if (_source == null) return;
            _source.BuffApplied -= OnBuffApplied;
            _source.BuffEnded -= OnBuffEnded;
        }

        void OnDestroy() => Unsubscribe();

        void OnBuffApplied(ItemData item, float duration)
        {
            if (item == null || template == null) return;
            if (!_active.TryGetValue(item, out var view) || view == null)
            {
                view = Instantiate(template, template.transform.parent);
                view.gameObject.SetActive(true);
                _active[item] = view;
            }
            view.Show(item, duration);
        }

        void OnBuffEnded(ItemData item)
        {
            if (item == null || !_active.TryGetValue(item, out var view)) return;
            _active.Remove(item);
            if (view != null) Destroy(view.gameObject);
        }

        void ClearAll()
        {
            foreach (var kv in _active) if (kv.Value != null) Destroy(kv.Value.gameObject);
            _active.Clear();
        }
    }
}
