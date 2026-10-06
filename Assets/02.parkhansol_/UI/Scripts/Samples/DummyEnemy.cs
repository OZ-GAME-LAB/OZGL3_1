using System;
using System.Collections;
using OZ.UI.Contracts;
using UnityEngine;

namespace OZ.UI.Samples
{
    /// <summary>
    /// UI_Sandbox용 가상의 적. IEnemyHealthSource 참고 구현 (적 담당은 이 모양대로 구현하면 됨).
    ///   맞으면: GameUI.Damage.Show(맞은 지점, 피해, 종류) → 숫자 + 타격 이펙트
    ///          흰색 번쩍임(HitFlash) + 뒤로 밀림 (치명타 계열은 더 크게) → 머리 위 체력바 자동 표시
    ///   마지막 일격은 자동으로 DamageKind.Finisher (빨간 "처치")
    ///   죽으면: 흐려지며 사라졌다가 respawnDelay 뒤 부활
    /// </summary>
    [AddComponentMenu("OZ/UI/Samples/Dummy Enemy")]
    public class DummyEnemy : MonoBehaviour, IEnemyHealthSource
    {
        [SerializeField] internal SpriteRenderer body;
        [SerializeField] internal Transform barAnchor;
        [SerializeField] internal float maxHP = 120f;
        [SerializeField] internal bool elite;
        [SerializeField] internal float respawnDelay = 2f;
        [SerializeField] internal HitFlash flash;

        float _hp;
        Vector3 _home;
        Color _baseColor = Color.white;
        Coroutine _knock;

        public Transform BarAnchor => barAnchor != null ? barAnchor : transform;
        public float HP => _hp;
        public float MaxHP => maxHP;
        public bool IsElite => elite;
        public bool IsDead => _hp <= 0f;
        public event Action<HealthChange> HealthChanged;

        void Awake()
        {
            _hp = maxHP;
            _home = transform.position;
            if (body != null) _baseColor = body.color;
        }

        // UI(DamageFxController)가 OnEnable에서 등록되므로 Start에서 연결
        void Start() => GameUI.Damage.TrackEnemy(this);
        void OnDestroy() => GameUI.Damage.UntrackEnemy(this);

        /// <summary>몸 중앙 근처 랜덤 지점 (숫자·이펙트 위치)</summary>
        public Vector3 HitPoint()
        {
            var b = body != null ? body.bounds : new Bounds(transform.position, Vector3.one);
            return new Vector3(b.center.x + UnityEngine.Random.Range(-0.25f, 0.25f) * b.extents.x,
                               b.center.y + UnityEngine.Random.Range(-0.1f, 0.4f) * b.extents.y, b.center.z);
        }

        public bool Contains(Vector3 world)
        {
            if (body == null) return false;
            var b = body.bounds; b.Expand(new Vector3(0.2f, 0.2f, 100f));
            return b.Contains(new Vector3(world.x, world.y, b.center.z));
        }

        /// <summary>피해 적용. 반환값 = 실제 표시한 종류 (죽였으면 Finisher). 번쩍임·히트스톱은 호출한 쪽이 종류에 맞춰 처리</summary>
        public DamageKind TakeHit(float damage, DamageKind kind)
        {
            if (IsDead) return kind;
            float prev = _hp;
            _hp = Mathf.Max(0f, _hp - damage);
            if (IsDead && kind != DamageKind.DamageOverTime) kind = DamageKind.Finisher;
            GameUI.Damage.Show(HitPoint(), damage, kind);
            HealthChanged?.Invoke(new HealthChange(prev, _hp, maxHP));

            bool heavy = kind == DamageKind.Critical || kind == DamageKind.Weakness || kind == DamageKind.Finisher;
            if (kind != DamageKind.DamageOverTime)
            {
                if (_knock != null) StopCoroutine(_knock);
                _knock = StartCoroutine(Knockback(heavy ? 0.22f : 0.08f)); // timeScale 기준 → 히트스톱 동안 밀린 자리에서 멈춤
            }
            if (IsDead) StartCoroutine(DieAndRespawn());
            return kind;
        }

        public void TakeHit(float damage, bool critical) => TakeHit(damage, critical ? DamageKind.Critical : DamageKind.Normal);

        public void Miss() => GameUI.Damage.ShowText(HitPoint(), "MISS", DamageKind.Miss);

        IEnumerator Knockback(float dist)
        {
            float dir = body != null && body.flipX ? 1f : -1f; // 바라보는 반대쪽으로 밀림
            Vector3 from = _home + new Vector3(-dir * dist, 0, 0);
            float t = 0f;
            while (t < 0.15f)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(from, _home, t / 0.15f);
                yield return null;
            }
            transform.position = _home;
        }

        IEnumerator DieAndRespawn()
        {
            if (body != null)
            {
                for (float a = 1f; a > 0f; a -= Time.deltaTime * 3f)
                {
                    body.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, a);
                    yield return null;
                }
                body.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, 0f);
            }
            yield return new WaitForSeconds(respawnDelay);
            ResetHP();
        }

        public void ResetHP()
        {
            StopAllCoroutines();
            float prev = _hp;
            _hp = maxHP;
            transform.position = _home;
            if (body != null) body.color = _baseColor;
            HealthChanged?.Invoke(new HealthChange(prev, _hp, maxHP));
        }
    }
}
