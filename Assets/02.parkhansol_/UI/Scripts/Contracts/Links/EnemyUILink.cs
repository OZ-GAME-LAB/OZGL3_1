using System;
using UnityEngine;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// [적 프리팹용] 붙이고 체력만 알려 주면 머리 위 체력바 + 피해 숫자가 따라온다. PoolManager 재사용 그대로 OK.
    ///
    ///   enemyUI.Init(maxHP);                          // 스폰(풀에서 꺼냄) 때
    ///   enemyUI.Hit(damage, isCrit);                  // 맞을 때 → 피해 숫자 + 타격 이펙트 + 체력바 (마지막 일격은 자동 강조)
    ///   enemyUI.SetHP(hp);                            // 숫자 없이 체력만 맞출 때
    ///
    /// 꺼낼 때(OnEnable) 체력바 등록, 넣을 때(OnDisable) 해제 — 코드 필요 없음.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("OZ/UI/Links/Enemy UI Link")]
    public class EnemyUILink : MonoBehaviour, IEnemyHealthSource
    {
        [Tooltip("체력바 위치 (머리 위 빈 오브젝트). 비우면 이 오브젝트 위치 + headOffset")]
        [SerializeField] internal Transform barAnchor;
        [SerializeField] internal Vector3 headOffset = new Vector3(0f, 2f, 0f);
        [Tooltip("켜면 체력바 항상 표시 + 조금 크게")]
        [SerializeField] internal bool elite;
        [SerializeField] internal float maxHP = 100f;

        float _hp;
        Transform _autoAnchor;
        bool _tracked;

        public Transform BarAnchor
        {
            get
            {
                if (barAnchor != null) return barAnchor;
                if (_autoAnchor == null)
                {
                    _autoAnchor = new GameObject("UI_BarAnchor").transform;
                    _autoAnchor.SetParent(transform, false);
                    _autoAnchor.localPosition = headOffset;
                }
                return _autoAnchor;
            }
        }
        public float HP => _hp;
        public float MaxHP => maxHP;
        public bool IsElite => elite;
        public event Action<HealthChange> HealthChanged;

        void Awake() => _hp = maxHP;

        void OnEnable() => Track();
        void Start() { if (!_tracked) Track(); } // UI가 적보다 늦게 뜬 경우

        void Track()
        {
            if (!GameUI.IsReady) return;
            GameUI.Damage.TrackEnemy(this);
            _tracked = true;
        }

        void OnDisable()
        {
            GameUI.Damage.UntrackEnemy(this);
            _tracked = false;
        }

        /// <summary>스폰 시: 체력 가득 (maxHP 0 이하면 기존 값)</summary>
        public void Init(float max = 0f, bool? isElite = null)
        {
            if (max > 0f) maxHP = max;
            if (isElite.HasValue) elite = isElite.Value;
            float prev = _hp;
            _hp = maxHP;
            HealthChanged?.Invoke(new HealthChange(prev, _hp, maxHP));
        }

        public void SetHP(float hp)
        {
            float prev = _hp;
            _hp = Mathf.Clamp(hp, 0f, maxHP);
            if (!Mathf.Approximately(prev, _hp)) HealthChanged?.Invoke(new HealthChange(prev, _hp, maxHP));
        }

        /// <summary>맞음: 피해 숫자 + 타격 이펙트 + 체력바. 이 타격으로 죽으면 Finisher(빨강 큰 숫자)로 표시. 반환 = 죽었는지</summary>
        public bool Hit(float damage, bool critical = false, Vector3? hitPoint = null)
        {
            return Hit(damage, critical ? DamageKind.Critical : DamageKind.Normal, hitPoint);
        }

        public bool Hit(float damage, DamageKind kind, Vector3? hitPoint = null)
        {
            if (_hp <= 0f) return true;
            SetHP(_hp - damage);
            bool dead = _hp <= 0f;
            if (dead && kind != DamageKind.DamageOverTime) kind = DamageKind.Finisher;
            Vector3 p = hitPoint ?? (transform.position + headOffset * 0.6f);
            GameUI.Damage.Show(p, damage, kind);
            return dead;
        }
    }
}
