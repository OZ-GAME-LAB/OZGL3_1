using System.Collections.Generic;
using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// [v0.7] 버프 화면 테두리 — 포션 버프가 걸려 있는 동안 화면 가장자리에 그 포션 색(ItemData.fxColor)의 얇은 빛 테두리가 점멸.
    ///   두께는 저체력 비네트의 약 1/4. 버프가 여러 개면 같은 자리에서 색이 번갈아 점멸한다
    ///   (공격 주황 → 방어 파랑 → 신속 초록 순서 고정) → 한 번에 한 색만 보여서 어떤 버프가 걸려 있는지 구분된다.
    ///   새 버프를 먹으면 그 색부터 바로 보여 줌. 점멸 주기는 버프가 사라질 때까지 일정하게 유지.
    ///   점멸 한 번 = 살짝 안쪽에서 바깥으로 밀려나며 밝아졌다 사라짐.
    /// HudView가 IItemSource를 연결해 준다 → 팀원 작업 없음 (BuffApplied / BuffEnded만 오면 됨).
    /// </summary>
    [AddComponentMenu("OZ/UI/HUD/Buff Edge View")]
    public class BuffEdgeView : MonoBehaviour
    {
        [Tooltip("테두리 이미지 (9-slice BuffEdgeGlow)")]
        [SerializeField] internal Image template;
        [SerializeField, Range(0f, 1f)] internal float alpha = 0.85f;
        [Tooltip("색 하나가 켜졌다 꺼지는 시간 (초)")]
        [SerializeField, Min(0.2f)] internal float slotSeconds = 0.75f;
        [Tooltip("점멸할 때 안쪽에서 밀려 나오는 거리 (논리 px)")]
        [SerializeField] internal float pushDistance = 3f;

        class Buff
        {
            public ItemData item;
            public float endTime;
        }

        readonly List<Buff> _buffs = new List<Buff>();
        readonly List<Buff> _ordered = new List<Buff>();
        IItemSource _source;
        float _clock;
        float _master;

        public int ActiveCount => _buffs.Count;
        /// <summary>지금 보이는 색의 버프 (없으면 null)</summary>
        public ItemData CurrentItem { get; private set; }
        public float CurrentAlpha => template != null && template.enabled ? template.color.a : 0f;
        /// <summary>테스트용: 한 번이라도 화면에 나온 버프들</summary>
        public readonly HashSet<ItemData> Shown = new HashSet<ItemData>();

        void Awake()
        {
            if (template == null) return;
            template.gameObject.SetActive(true);
            template.enabled = false;
            template.rectTransform.offsetMin = template.rectTransform.offsetMax = Vector2.zero;
        }

        internal void Bind(IItemSource source)
        {
            if (_source != null) { _source.BuffApplied -= OnApplied; _source.BuffEnded -= OnEnded; }
            _source = source;
            if (_source != null) { _source.BuffApplied += OnApplied; _source.BuffEnded += OnEnded; }
            _buffs.Clear();
        }

        void OnDestroy()
        {
            if (_source != null) { _source.BuffApplied -= OnApplied; _source.BuffEnded -= OnEnded; }
        }

        static int Order(ItemData item)
        {
            if (item == null) return 99;
            switch (item.effectType)
            {
                case ItemEffectType.AttackBuff: return 0;
                case ItemEffectType.DefenseBuff: return 1;
                case ItemEffectType.SpeedBuff: return 2;
                default: return 3;
            }
        }

        void Sort()
        {
            _ordered.Clear();
            _ordered.AddRange(_buffs);
            _ordered.Sort((a, b) => Order(a.item).CompareTo(Order(b.item)));
        }

        void OnApplied(ItemData item, float duration)
        {
            if (item == null) return;
            var b = _buffs.Find(x => x.item == item);
            if (b == null) { b = new Buff { item = item }; _buffs.Add(b); }
            b.endTime = Time.time + Mathf.Max(0.1f, duration);
            // 방금 먹은 버프 색부터 바로 보여 주기
            Sort();
            _clock = _ordered.IndexOf(b) * slotSeconds;
        }

        void OnEnded(ItemData item) => _buffs.RemoveAll(x => x.item == item);

        void Update()
        {
            if (template == null) return;
            _buffs.RemoveAll(x => Time.time - x.endTime > 1f); // 종료 알림이 안 와도 정리
            float dt = Time.unscaledDeltaTime;
            _master = Mathf.MoveTowards(_master, _buffs.Count > 0 ? 1f : 0f, dt * 4f);
            if (_master <= 0f || _buffs.Count == 0)
            {
                CurrentItem = null;
                if (_master <= 0f) { template.enabled = false; return; }
            }

            if (_buffs.Count > 0)
            {
                Sort();
                _clock += dt;
                int n = _ordered.Count;
                float cycle = slotSeconds * n;
                if (_clock >= cycle) _clock %= cycle;
                int idx = Mathf.Min(n - 1, Mathf.FloorToInt(_clock / slotSeconds));
                var cur = _ordered[idx];
                if (CurrentItem != cur.item) { CurrentItem = cur.item; Shown.Add(cur.item); }
            }

            float frac = (_clock % slotSeconds) / slotSeconds;
            float env = Mathf.Pow(Mathf.Sin(frac * Mathf.PI), 0.7f); // 켜졌다 꺼짐 (점멸)

            var c = CurrentItem != null ? CurrentItem.fxColor : template.color;
            c.a = alpha * env * _master;
            template.color = c;
            template.enabled = c.a > 0.002f;
            float px = Mathf.Round((1f - env) * pushDistance); // 안쪽에서 바깥으로 밀려나며 밝아짐
            template.rectTransform.offsetMin = new Vector2(px, px);
            template.rectTransform.offsetMax = new Vector2(-px, -px);
        }
    }
}
