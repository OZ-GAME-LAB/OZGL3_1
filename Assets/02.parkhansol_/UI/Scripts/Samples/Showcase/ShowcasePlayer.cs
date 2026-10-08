using System.Collections;
using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OZ.UI.Samples
{
    /// <summary>
    /// 지하철역 쇼케이스용 임시 3D 플레이어 (로우폴리 블록 인형).
    /// 이동·점프·공격·스킬을 눈으로 확인해 UI 크기·위치가 게임 화면과 어울리는지 보는 용도. 실제 플레이어 코드가 아님.
    ///   A/D(←/→) 이동 · Space 점프(2단) · Z/마우스 왼쪽 공격(3타) · Q/E/R 스킬 (DummyPlayer 쿨타임 → HUD)
    /// HUD 수치(체력·스킬·아이템)는 같은 오브젝트의 DummyPlayer가 들고 있다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("OZ/UI/Samples/Showcase Player")]
    public class ShowcasePlayer : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] internal DummyPlayer data;
        [SerializeField] internal SandboxFeel feel;
        [SerializeField] internal ShowcaseCamera cam;
        [SerializeField] internal Transform model;
        [SerializeField] internal Transform head;
        [SerializeField] internal Transform armL, armR, legL, legR;
        [SerializeField] internal Renderer[] flashRenderers = new Renderer[0];

        [Header("이동")]
        [SerializeField] internal float moveSpeed = 6f;
        [SerializeField] internal float jumpSpeed = 9.5f;
        [SerializeField] internal float gravity = 26f;
        [SerializeField] internal int airJumps = 1;

        [Header("전투")]
        [SerializeField] internal Vector2 baseDamage = new Vector2(14, 26);
        [SerializeField, Range(0f, 1f)] internal float critChance = 0.2f;
        [SerializeField] internal float critMultiplier = 2.2f;

        public static ShowcasePlayer Instance { get; private set; }

        public Vector3 Center => transform.position + Vector3.up * 0.9f;
        public Vector2 HalfSize => new Vector2(0.35f, 0.9f);
        public float Facing { get; private set; } = 1f;
        public bool Grounded { get; private set; }

        Rigidbody _rb;
        CapsuleCollider _col;
        int _airLeft;
        float _attackTimer, _comboWindow, _invuln, _hurtPush, _land;
        int _combo;
        Vector3 _spawn;
        float _flash;
        MaterialPropertyBlock _mpb;
        Color[] _base;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        void Awake()
        {
            Instance = this;
            _rb = GetComponent<Rigidbody>();
            _col = GetComponent<CapsuleCollider>();
            _rb.useGravity = false;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
            if (_col != null) _col.material = new PhysicsMaterial("NoFriction") { dynamicFriction = 0f, staticFriction = 0f, frictionCombine = PhysicsMaterialCombine.Minimum };
            _spawn = transform.position;
            _mpb = new MaterialPropertyBlock();
            _base = new Color[flashRenderers.Length];
            for (int i = 0; i < flashRenderers.Length; i++)
                _base[i] = flashRenderers[i] != null && flashRenderers[i].sharedMaterial != null && flashRenderers[i].sharedMaterial.HasProperty(BaseColorId)
                    ? flashRenderers[i].sharedMaterial.GetColor(BaseColorId) : Color.white;
        }

        void OnEnable()
        {
            if (data != null) { data.CooldownStarted += OnSkillCast; data.HealthChanged += OnHealth; }
        }

        void OnDisable()
        {
            if (data != null) { data.CooldownStarted -= OnSkillCast; data.HealthChanged -= OnHealth; }
            if (Instance == this) Instance = null;
        }

        public void Respawn()
        {
            transform.position = _spawn;
            _rb.linearVelocity = Vector3.zero;
            _invuln = 1f;
        }

        // ───────────── 이동 ─────────────

        void Update()
        {
            float dt = Time.deltaTime;
            _attackTimer -= dt; _comboWindow -= dt; _invuln -= dt; _land = Mathf.MoveTowards(_land, 0f, dt * 6f);
            bool blocked = UIState.IsGameplayInputBlocked || (data != null && data.HP <= 0f);
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            if (!blocked && kb != null)
            {
                if (kb.spaceKey.wasPressedThisFrame) TryJump();
                if (kb.zKey.wasPressedThisFrame || (mouse != null && mouse.leftButton.wasPressedThisFrame)) TryAttack();
            }
            Animate(dt);
            UpdateFlash(dt);
        }

        void FixedUpdate()
        {
            var kb = Keyboard.current;
            bool blocked = UIState.IsGameplayInputBlocked || (data != null && data.HP <= 0f);
            float input = 0f;
            if (!blocked && kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input += 1f;
            }
            if (input != 0f && _attackTimer <= 0.05f) Facing = Mathf.Sign(input);

            bool wasGrounded = Grounded;
            Grounded = CheckGround();
            if (Grounded && !wasGrounded) { _airLeft = airJumps; _land = 1f; }

            var v = _rb.linearVelocity;
            float target = input * moveSpeed * (_attackTimer > 0f && Grounded ? 0.35f : 1f) + _hurtPush;
            v.x = Mathf.MoveTowards(v.x, target, (Grounded ? 60f : 35f) * Time.fixedDeltaTime);
            v.y -= gravity * Time.fixedDeltaTime;
            if (Grounded && v.y < 0f) v.y = -1f;
            v.z = 0f;
            _rb.linearVelocity = v;
            _hurtPush = Mathf.MoveTowards(_hurtPush, 0f, 40f * Time.fixedDeltaTime);

            if (transform.position.y < -10f) Respawn();
        }

        bool CheckGround()
        {
            Vector3 feet = transform.position + Vector3.up * 0.15f;
            var hits = Physics.OverlapSphere(feet - Vector3.up * 0.12f, 0.28f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits) if (h != _col && !h.transform.IsChildOf(transform)) return _rb.linearVelocity.y <= 0.1f;
            return false;
        }

        void TryJump()
        {
            if (!Grounded && _airLeft <= 0) return;
            if (!Grounded) _airLeft--;
            var v = _rb.linearVelocity;
            v.y = jumpSpeed;
            _rb.linearVelocity = v;
            Grounded = false;
            if (model != null) StartCoroutine(Squash(0.8f, 1.2f));
        }

        // ───────────── 공격 ─────────────

        void TryAttack()
        {
            if (_attackTimer > 0f) return;
            _combo = _comboWindow > 0f ? (_combo + 1) % 3 : 0;
            _attackTimer = _combo == 2 ? 0.32f : 0.22f;
            _comboWindow = 0.6f;
            bool finisher = _combo == 2;
            Vector3 c = Center + new Vector3(Facing * 1.0f, 0.1f, -0.2f);
            if (ShowcaseFx.Instance != null) ShowcaseFx.Instance.Slash(c, Facing, finisher);
            HitArea(c, new Vector2(1.0f, 0.9f), finisher ? 1.4f : 1f, null);
        }

        float RollDamage(float mul) => Mathf.Round(Random.Range(baseDamage.x, baseDamage.y) * mul);

        /// <summary>범위 안 적 전부 타격. kind == null이면 치명타 확률 굴림</summary>
        int HitArea(Vector3 center, Vector2 half, float mul, DamageKind? kind)
        {
            int n = 0;
            foreach (var e in ShowcaseEnemy.All.ToArray())
            {
                if (e == null || !e.Overlaps(center, half)) continue;
                Hit(e, mul, kind);
                n++;
            }
            return n;
        }

        void Hit(ShowcaseEnemy e, float mul, DamageKind? kind)
        {
            var k = kind ?? (Random.value < critChance ? DamageKind.Critical : DamageKind.Normal);
            float dmg = RollDamage(mul * (k == DamageKind.Critical ? critMultiplier : 1f));
            var shown = e.TakeHit(dmg, k, Mathf.Sign(e.transform.position.x - transform.position.x));
            Feel(shown);
        }

        void Feel(DamageKind kind)
        {
            if (feel == null) return;
            var t = feel.Get(kind);
            if (t.hitStop > 0f) feel.HitStop(t.hitStop);
            if (cam != null && t.shake > 0f) cam.Shake(t.shake * 0.02f, t.shakeTime);
        }

        // ───────────── 스킬 (DummyPlayer가 쿨타임을 시작했을 때만 = 실제로 쓴 경우) ─────────────

        void OnSkillCast(SkillSlot slot, float cooldown)
        {
            var skill = data != null ? data.GetSkill(slot) : null;
            int rank = data != null ? Mathf.Max(1, data.GetRank(slot)) : 1;
            float mul = 1f + 0.25f * (rank - 1);
            string id = skill != null ? skill.id : "";
            var fx = ShowcaseFx.Instance;
            if (fx == null) return;
            _attackTimer = 0.25f;
            Vector3 feet = transform.position;
            Vector3 c = Center;

            switch (id)
            {
                case "sword_q": // 짧은 검기: 앞으로 날아가며 닿는 적마다 1번
                {
                    var hit = new System.Collections.Generic.HashSet<ShowcaseEnemy>();
                    fx.Projectile(c + new Vector3(Facing * 0.6f, 0f, -0.2f), Facing, 7f, 20f, false, p =>
                    {
                        foreach (var e in ShowcaseEnemy.All.ToArray())
                            if (e != null && !hit.Contains(e) && e.Overlaps(p, new Vector2(0.3f, 0.7f))) { hit.Add(e); Hit(e, 1.3f * mul, null); }
                    });
                    break;
                }
                case "sword_e": // 근접 범위 베기: 주변 원
                    fx.Ring(feet + Vector3.up * 0.4f, 2.6f, false);
                    HitArea(c, new Vector2(2.6f, 1.2f), 1.2f * mul, null);
                    break;
                case "sword_r": // 집중 참격: 앞 좁은 구역 연타
                    StartCoroutine(Barrage(c + new Vector3(Facing * 1.8f, 0f, -0.2f), 5 + rank - 1, mul));
                    break;
                case "magic_q": // 범위 폭발: 앞 4m
                {
                    Vector3 at = feet + new Vector3(Facing * 4f, 0.8f, -0.2f);
                    fx.Burst(at, 1.8f, true);
                    HitArea(at, new Vector2(1.8f, 1.4f), 1.5f * mul, null);
                    break;
                }
                case "magic_e": // 지속 피해 장판
                {
                    Vector3 at = feet + new Vector3(Facing * 3f, 0f, 0f);
                    float dur = 3f + rank;
                    fx.Zone(at, 2f, dur);
                    StartCoroutine(Dot(at + Vector3.up * 0.8f, new Vector2(2f, 1f), dur, mul));
                    break;
                }
                case "magic_r": // 광역 연속 공격: 앞쪽에 떨어지는 구
                    StartCoroutine(Meteors(feet, 6 + (rank - 1) * 2, mul));
                    break;
                default:
                    fx.Ring(feet + Vector3.up * 0.4f, 2f, false);
                    HitArea(c, new Vector2(2f, 1f), mul, null);
                    break;
            }
        }

        IEnumerator Barrage(Vector3 center, int hits, float mul)
        {
            for (int i = 0; i < hits; i++)
            {
                if (ShowcaseFx.Instance != null) ShowcaseFx.Instance.Slash(center + new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(-0.3f, 0.3f), 0f), i % 2 == 0 ? Facing : -Facing, i == hits - 1);
                HitArea(center, new Vector2(1.6f, 1.0f), 0.6f * mul, i == hits - 1 ? DamageKind.Critical : (DamageKind?)null);
                yield return new WaitForSeconds(0.09f);
            }
        }

        IEnumerator Dot(Vector3 center, Vector2 half, float duration, float mul)
        {
            for (float t = 0f; t < duration; t += 0.4f)
            {
                foreach (var e in ShowcaseEnemy.All.ToArray())
                    if (e != null && e.Overlaps(center, half))
                        e.TakeHit(Mathf.Round(Random.Range(3f, 6f) * mul), DamageKind.DamageOverTime, 0f);
                yield return new WaitForSeconds(0.4f);
            }
        }

        IEnumerator Meteors(Vector3 feet, int count, float mul)
        {
            float dir = Facing;
            for (int i = 0; i < count; i++)
            {
                Vector3 g = feet + new Vector3(dir * Random.Range(1.5f, 7.5f), 0f, -0.2f);
                ShowcaseFx.Instance.Meteor(g, () => HitArea(g + Vector3.up * 0.8f, new Vector2(1.1f, 1.1f), 0.9f * mul, null));
                yield return new WaitForSeconds(0.14f);
            }
        }

        // ───────────── 피격 ─────────────

        public void Hurt(float damage, float pushDir)
        {
            if (_invuln > 0f || data == null || data.HP <= 0f) return;
            _invuln = 0.8f;
            data.ChangeHP(-damage); // → HUD 체력 + OnHealth에서 빨간 숫자
            _hurtPush = pushDir * 7f;
            var v = _rb.linearVelocity; v.y = 4f; _rb.linearVelocity = v;
            _flash = 1f;
            Feel(DamageKind.PlayerHurt);
            if (data.HP <= 0f) GameUI.Screens.ShowDeath();
        }

        void OnHealth(HealthChange c)
        {
            if (head == null || Mathf.Approximately(c.Delta, 0f)) return;
            if (c.Previous <= 0f && c.Current >= c.Max) return;
            GameUI.Damage.Show(head.position + Vector3.up * 0.3f, Mathf.Abs(c.Delta), c.IsDamage ? DamageKind.PlayerHurt : DamageKind.Heal);
        }

        void UpdateFlash(float dt)
        {
            _flash = Mathf.MoveTowards(_flash, 0f, dt * 6f);
            bool blink = _invuln > 0f && Mathf.Repeat(Time.time * 14f, 1f) < 0.5f;
            for (int i = 0; i < flashRenderers.Length; i++)
            {
                var r = flashRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, Color.Lerp(_base[i], Color.white, Mathf.Max(_flash, blink ? 0.35f : 0f)));
                r.SetPropertyBlock(_mpb);
            }
        }

        // ───────────── 임시 애니메이션 (블록 인형) ─────────────

        void Animate(float dt)
        {
            if (model == null) return;
            float speed = Mathf.Abs(_rb.linearVelocity.x);
            float walk = Grounded ? Mathf.Clamp01(speed / moveSpeed) : 0f;
            float t = Time.time * 12f;
            float swing = Mathf.Sin(t) * 40f * walk;

            model.localRotation = Quaternion.Slerp(model.localRotation, Quaternion.Euler(0f, Facing > 0f ? 90f : -90f, 0f), dt * 20f);
            float bob = Grounded ? Mathf.Abs(Mathf.Sin(t)) * 0.06f * walk : 0f;
            model.localPosition = new Vector3(0f, bob - _land * 0.08f, 0f);

            if (legL != null) legL.localRotation = Quaternion.Euler(Grounded ? swing : -25f, 0f, 0f);
            if (legR != null) legR.localRotation = Quaternion.Euler(Grounded ? -swing : 15f, 0f, 0f);

            float attack = Mathf.Clamp01(_attackTimer / 0.22f);
            float armSwing = attack > 0f ? Mathf.Lerp(80f, -100f, attack) : -swing;
            if (armR != null) armR.localRotation = Quaternion.Euler(Grounded || attack > 0f ? armSwing : -150f, 0f, 0f);
            if (armL != null) armL.localRotation = Quaternion.Euler(Grounded ? swing : -150f, 0f, 0f);
        }

        IEnumerator Squash(float x, float y)
        {
            for (float k = 0f; k < 1f; k += Time.deltaTime / 0.18f)
            {
                float e = 1f - (1f - k) * (1f - k);
                model.localScale = new Vector3(Mathf.Lerp(x, 1f, e), Mathf.Lerp(y, 1f, e), Mathf.Lerp(x, 1f, e));
                yield return null;
            }
            model.localScale = Vector3.one;
        }
    }
}
