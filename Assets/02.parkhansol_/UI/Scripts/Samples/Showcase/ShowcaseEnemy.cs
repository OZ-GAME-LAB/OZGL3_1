using System;
using System.Collections;
using System.Collections.Generic;
using OZ.UI.Contracts;
using UnityEngine;

namespace OZ.UI.Samples
{
    /// <summary>
    /// 지하철역 쇼케이스용 임시 3D 적 (로우폴리 블록). IEnemyHealthSource 참고 구현의 3D판.
    ///   좌우 순찰 · 플레이어 접촉 시 피해 · 맞으면 흰 번쩍임 + 밀림 · 죽으면 작아지며 사라졌다 부활
    ///   풀(SamplePoolManager)용: respawn 끔 → 죽으면 Died 이벤트만, 꺼냈을 때 ResetForSpawn(위치)
    /// </summary>
    [AddComponentMenu("OZ/UI/Samples/Showcase Enemy")]
    public class ShowcaseEnemy : MonoBehaviour, IEnemyHealthSource
    {
        [SerializeField] internal Transform model;
        [SerializeField] internal Transform barAnchor;
        [SerializeField] internal Renderer[] renderers = new Renderer[0];
        [SerializeField] internal float maxHP = 120f;
        [SerializeField] internal bool elite;
        [SerializeField] internal float patrolRange = 2.5f;
        [SerializeField] internal float speed = 1.2f;
        [SerializeField] internal float contactDamage = 8f;
        [SerializeField] internal float halfWidth = 0.45f;
        [SerializeField] internal float height = 1.7f;
        [SerializeField] internal float respawnDelay = 2.5f;
        [Tooltip("끄면 죽은 뒤 제자리 부활 대신 Died 이벤트만 보냄 (풀에서 꺼내 쓰는 적)")]
        [SerializeField] internal bool respawn = true;
        [Tooltip("끄면 스스로 체력바를 등록하지 않음 (EnemyHealthBarTracker 컴포넌트를 쓰거나 보스 몸체일 때)")]
        [SerializeField] internal bool trackHealthBar = true;

        float _hp;
        Vector3 _home;
        float _dir = 1f;
        float _knock;          // 밀림 속도 (x)
        float _flash;          // 0~1
        float _contactCooldown;
        bool _dying;
        Vector3 _baseScale;
        MaterialPropertyBlock _mpb;
        Color[] _baseColors;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        public static readonly List<ShowcaseEnemy> All = new List<ShowcaseEnemy>();

        public Transform BarAnchor => barAnchor != null ? barAnchor : transform;
        public float HP => _hp;
        public float MaxHP => maxHP;
        public bool IsElite => elite;
        public bool IsDead => _hp <= 0f;
        public event Action<HealthChange> HealthChanged;
        /// <summary>쓰러지는 연출이 끝났을 때 (respawn이 꺼져 있으면 여기서 풀에 돌려보내면 됨)</summary>
        public event Action<ShowcaseEnemy> Died;

        void Awake()
        {
            _hp = maxHP;
            _home = transform.position;
            _baseScale = transform.localScale;
            _mpb = new MaterialPropertyBlock();
            _baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                var m = renderers[i] != null ? renderers[i].sharedMaterial : null;
                _baseColors[i] = m == null ? Color.white : m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : m.HasProperty(ColorId) ? m.color : Color.white;
            }
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);
        void Start() { if (trackHealthBar) GameUI.Damage.TrackEnemy(this); }
        void OnDestroy() { if (trackHealthBar) GameUI.Damage.UntrackEnemy(this); }

        /// <summary>풀에서 꺼낼 때: 위치·체력·모습 초기화</summary>
        public void ResetForSpawn(Vector3 position)
        {
            StopAllCoroutines();
            _home = position;
            transform.position = position;
            transform.localScale = _baseScale;
            foreach (var r in renderers) if (r != null) r.enabled = true;
            float prev = _hp;
            _hp = maxHP;
            _dying = false;
            _knock = 0f; _flash = 0f; _contactCooldown = 0.6f;
            _dir = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            HealthChanged?.Invoke(new HealthChange(prev, _hp, maxHP));
        }

        public Vector3 Center => transform.position + Vector3.up * height * 0.5f;

        public Vector3 HitPoint() =>
            transform.position + new Vector3(UnityEngine.Random.Range(-0.25f, 0.25f) * halfWidth * 2f, height * UnityEngine.Random.Range(0.45f, 0.8f), -0.3f);

        public bool Overlaps(Vector3 center, Vector2 halfSize)
        {
            if (IsDead) return false;
            Vector3 c = Center;
            return Mathf.Abs(c.x - center.x) <= halfSize.x + halfWidth && Mathf.Abs(c.y - center.y) <= halfSize.y + height * 0.5f;
        }

        /// <summary>피해. 반환값 = 실제 표시한 종류 (죽였으면 Finisher)</summary>
        public DamageKind TakeHit(float damage, DamageKind kind, float pushDir)
        {
            if (IsDead) return kind;
            float prev = _hp;
            _hp = Mathf.Max(0f, _hp - damage);
            if (IsDead && kind != DamageKind.DamageOverTime) kind = DamageKind.Finisher;
            GameUI.Damage.Show(HitPoint(), damage, kind);
            HealthChanged?.Invoke(new HealthChange(prev, _hp, maxHP));

            bool heavy = kind == DamageKind.Critical || kind == DamageKind.Weakness || kind == DamageKind.Finisher;
            if (kind != DamageKind.DamageOverTime) _knock = pushDir * (heavy ? 6f : 3f) * (elite ? 0.5f : 1f);
            _flash = 1f;
            if (IsDead && !_dying) StartCoroutine(Die());
            return kind;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (!_dying)
            {
                // 순찰 + 밀림
                float vx = _knock != 0f ? _knock : _dir * speed;
                var p = transform.position + new Vector3(vx * dt, 0f, 0f);
                if (p.x > _home.x + patrolRange) { p.x = _home.x + patrolRange; _dir = -1f; }
                if (p.x < _home.x - patrolRange) { p.x = _home.x - patrolRange; _dir = 1f; }
                transform.position = p;
                _knock = Mathf.MoveTowards(_knock, 0f, 30f * dt);
                if (model != null)
                {
                    float face = _knock != 0f ? -Mathf.Sign(_knock) : _dir;
                    model.localRotation = Quaternion.Euler(0f, face > 0f ? 90f : -90f, 0f);
                    model.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(Time.time * speed * 5f)) * 0.06f, 0f); // 걷는 들썩임
                }

                // 플레이어 접촉 피해
                _contactCooldown -= dt;
                var player = ShowcasePlayer.Instance;
                if (player != null && _contactCooldown <= 0f && Overlaps(player.Center, player.HalfSize))
                {
                    _contactCooldown = 1.0f;
                    player.Hurt(contactDamage, Mathf.Sign(player.transform.position.x - transform.position.x));
                }
            }

            // 흰 번쩍임 (머티리얼 복제 없이 PropertyBlock)
            _flash = Mathf.MoveTowards(_flash, 0f, dt * 8f);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                Color c = Color.Lerp(_baseColors[i], Color.white, _flash);
                renderers[i].GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, c);
                _mpb.SetColor(ColorId, c);
                renderers[i].SetPropertyBlock(_mpb);
            }
        }

        IEnumerator Die()
        {
            _dying = true;
            Vector3 s0 = transform.localScale;
            for (float t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                transform.localScale = new Vector3(s0.x * (1f + t), s0.y * (1f - t / 0.35f), s0.z * (1f + t));
                yield return null;
            }
            foreach (var r in renderers) if (r != null) r.enabled = false;
            Died?.Invoke(this);
            if (!respawn) yield break;
            yield return new WaitForSeconds(respawnDelay);
            transform.localScale = s0;
            transform.position = _home;
            foreach (var r in renderers) if (r != null) r.enabled = true;
            float prev = _hp;
            _hp = maxHP;
            _dying = false;
            HealthChanged?.Invoke(new HealthChange(prev, _hp, maxHP));
        }
    }
}
