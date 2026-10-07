using System.Collections.Generic;
using OZ.UI.Contracts;
using UnityEngine;

namespace OZ.UI
{
    /// <summary>
    /// GameUI.Damage 구현: 피해 숫자 · 타격 이펙트 · 적 머리 위 체력바.
    ///   - 월드 좌표를 받아 매 프레임 화면 위치로 옮긴다 (카메라가 움직여도 맞은 자리에 붙어 있음).
    ///   - 같은 자리에 연달아 뜨면 위로 쌓아서 겹치지 않게 한다.
    ///   - 전부 풀링 (연타·다수 적에도 생성/파괴 없음).
    /// </summary>
    [AddComponentMenu("OZ/UI/Damage/Damage Fx Controller")]
    public class DamageFxController : MonoBehaviour, IDamageFxApi
    {
        [Tooltip("비우면 Camera.main")]
        [SerializeField] internal Camera worldCamera;

        [Header("Templates (비활성 원본)")]
        [SerializeField] internal DamageNumberView numberTemplate;
        [SerializeField] internal HitSparkView sparkTemplate;
        [SerializeField] internal EnemyHealthBarView enemyBarTemplate;

        [Header("Layers (그리는 순서: 체력바 < 이펙트 < 숫자)")]
        [SerializeField] internal RectTransform barLayer;
        [SerializeField] internal RectTransform sparkLayer;
        [SerializeField] internal RectTransform numberLayer;

        [Header("숫자")]
        [SerializeField] internal DamageNumberStyle[] styles = DamageNumberStyle.Defaults();
        [Tooltip("맞은 지점에서 위로 띄우는 기본 높이 (UI px)")]
        [SerializeField] internal float numberLift = 10f;
        [Tooltip("이 시간·거리 안에 또 뜨면 한 줄 위로 쌓음")]
        [SerializeField] internal float stackWindow = 0.35f;
        [SerializeField] internal float stackRadius = 24f;
        [SerializeField] internal float stackStep = 11f;
        [SerializeField] internal int maxNumbers = 40;

        [Header("타격 이펙트")]
        [SerializeField] internal Sprite[] normalSparkFrames = new Sprite[0];
        [SerializeField] internal Sprite[] critSparkFrames = new Sprite[0];
        [SerializeField] internal float sparkFps = 24f;
        [Tooltip("치명타 계열 타격 이펙트 배율 (정수 = 픽셀 유지)")]
        [SerializeField] internal int critSparkScale = 2;

        [Header("적 체력바")]
        [Tooltip("BarAnchor 위로 띄우는 높이 (UI px)")]
        [SerializeField] internal float barLift = 4f;

        readonly List<DamageNumberView> _numbers = new List<DamageNumberView>();
        readonly Stack<DamageNumberView> _freeNumbers = new Stack<DamageNumberView>();
        readonly List<HitSparkView> _sparks = new List<HitSparkView>();
        readonly Stack<HitSparkView> _freeSparks = new Stack<HitSparkView>();
        readonly Dictionary<IEnemyHealthSource, EnemyHealthBarView> _bars = new Dictionary<IEnemyHealthSource, EnemyHealthBarView>();
        readonly Stack<EnemyHealthBarView> _freeBars = new Stack<EnemyHealthBarView>();
        RectTransform _root;
        bool _alternate;

        public int ActiveNumberCount { get { int n = 0; foreach (var v in _numbers) if (v.active) n++; return n; } }
        public int TrackedEnemyCount => _bars.Count;

        void Awake()
        {
            _root = (RectTransform)transform;
            if (numberTemplate != null) numberTemplate.gameObject.SetActive(false);
            if (sparkTemplate != null) sparkTemplate.gameObject.SetActive(false);
            if (enemyBarTemplate != null) enemyBarTemplate.gameObject.SetActive(false);
        }

        void OnEnable() => GameUI.Register((IDamageFxApi)this);
        void OnDisable() => GameUI.Unregister(this);

        Camera Cam => worldCamera != null ? worldCamera : Camera.main;

        // ───────────── IDamageFxApi ─────────────

        public void Show(Vector3 worldPosition, float amount, DamageKind kind = DamageKind.Normal)
        {
            var style = StyleOf(kind);
            if (style.spark != DamageSpark.None) SparkInternal(worldPosition, style.spark == DamageSpark.Critical, style.sparkTint);
            if (!UISettings.ShowDamageNumbers) return;
            float v = Mathf.Max(0f, Mathf.Abs(amount));
            if (style.mergeWindow > 0f && TryMerge(worldPosition, v, style)) return;
            SpawnNumber(worldPosition, v, FormatAmount(v), kind);
        }

        /// <summary>12,345 → 12.3k (옵션 '큰 숫자 줄여 쓰기'), 끄면 12,345</summary>
        public static string FormatAmount(float value)
        {
            int v = Mathf.RoundToInt(value);
            if (!UISettings.CompactNumbers || v < 10000) return v.ToString("N0");
            if (v < 1000000) return (v / 1000f).ToString(v < 100000 ? "0.#" : "0") + "k";
            return (v / 1000000f).ToString("0.#") + "M";
        }

        bool TryMerge(Vector3 worldPos, float v, DamageNumberStyle style)
        {
            Vector2 p = ToLocal(worldPos, out bool visible);
            if (!visible) return false;
            float now = Time.unscaledTime;
            foreach (var n in _numbers)
            {
                if (!n.active || n.kind != style.kind || now - n.spawnTime > style.mergeWindow) continue;
                if ((ToLocal(n.worldPos, out _) - p).sqrMagnitude > stackRadius * stackRadius) continue;
                n.amount += v;
                int mul = UISettings.DamageNumberSize; // 합쳐지면 제자리에서 다시 튐
                n.Play(FormatAmount(n.amount), style, new Vector2(n.offset.x, numberLift + style.lane * mul), mul, done => _freeNumbers.Push(done));
                return true;
            }
            return false;
        }

        public void ShowText(Vector3 worldPosition, string text, DamageKind kind = DamageKind.Miss)
        {
            if (!UISettings.ShowDamageNumbers || string.IsNullOrEmpty(text)) return;
            SpawnNumber(worldPosition, 0f, text, kind, rawText: true);
        }

        public void Spark(Vector3 worldPosition, bool critical = false) => SparkInternal(worldPosition, critical, Color.white);

        void SparkInternal(Vector3 worldPosition, bool critical, Color tint)
        {
            if (sparkTemplate == null) return;
            var frames = critical ? critSparkFrames : normalSparkFrames;
            if (frames == null || frames.Length == 0) return;
            var s = _freeSparks.Count > 0 ? _freeSparks.Pop() : NewSpark();
            s.worldPos = worldPosition;
            s.transform.SetAsLastSibling();
            s.image.color = tint;
            s.Play(frames, sparkFps, 90f * Random.Range(0, 4), done => _freeSparks.Push(done));
            s.transform.localScale = Vector3.one * (critical ? Mathf.Max(1, critSparkScale) : 1);
            Place(s.transform as RectTransform, worldPosition, Vector2.zero);
        }

        public void TrackEnemy(IEnemyHealthSource enemy)
        {
            if (enemy == null || enemyBarTemplate == null || _bars.ContainsKey(enemy)) return;
            var b = _freeBars.Count > 0 ? _freeBars.Pop() : NewBar();
            b.Bind(enemy);
            _bars[enemy] = b;
        }

        public void UntrackEnemy(IEnemyHealthSource enemy)
        {
            if (enemy == null || !_bars.TryGetValue(enemy, out var b)) return;
            _bars.Remove(enemy);
            b.Unbind();
            _freeBars.Push(b);
        }

        // ───────────── 내부 ─────────────

        DamageNumberStyle StyleOf(DamageKind kind)
        {
            foreach (var s in styles) if (s != null && s.kind == kind) return s;
            return styles != null && styles.Length > 0 ? styles[0] : new DamageNumberStyle();
        }

        void SpawnNumber(Vector3 worldPos, float amount, string value, DamageKind kind, bool rawText = false)
        {
            if (numberTemplate == null) return;
            var style = StyleOf(kind);
            if (!rawText && !string.IsNullOrEmpty(style.prefix)) value = style.prefix + value;

            DamageNumberView n;
            if (_freeNumbers.Count > 0) n = _freeNumbers.Pop();
            else if (_numbers.Count < maxNumbers) n = NewNumber();
            else n = OldestSmall() ?? OldestActive(); // 꽉 차면 작은 숫자부터 재사용 (치명타 계열은 오래 남김)
            if (n == null) return;
            n.Stop();

            // 같은 자리 연타 → 같은 줄(일반/치명타 계열)끼리 위로 쌓기
            Vector2 basePos = ToLocal(worldPos, out bool visible);
            int stack = 0;
            float aboveNormals = float.MinValue; // 치명타 계열은 근처 일반 숫자들보다 항상 위에서 시작
            if (visible)
            {
                float now = Time.unscaledTime;
                foreach (var o in _numbers)
                {
                    if (!o.active || o == n) continue;
                    Vector2 op = ToLocal(o.worldPos, out _);
                    if ((op - basePos).sqrMagnitude > stackRadius * stackRadius) continue;
                    if (style.IsCritLane && !o.critLane) aboveNormals = Mathf.Max(aboveNormals, o.offset.y);
                    if (o.critLane == style.IsCritLane && now - o.spawnTime <= stackWindow) stack++;
                }
            }
            // 좌우는 번갈아 (무작위보다 덜 겹침): 오른쪽 → 왼쪽 → 오른쪽 …
            _alternate = !_alternate;
            float side = style.jitterX <= 0f ? 0f : (_alternate ? 1f : -1f) * Mathf.Round(Random.Range(style.jitterX * 0.5f, style.jitterX));
            int sizeMul = UISettings.DamageNumberSize;
            var start = new Vector2(side, numberLift + style.lane * sizeMul + stack * stackStep * sizeMul);
            if (style.IsCritLane && aboveNormals > float.MinValue)
                start.y = Mathf.Max(start.y, Mathf.Round(aboveNormals) + stackStep * sizeMul);

            n.worldPos = worldPos;
            n.amount = amount;
            n.transform.SetAsLastSibling();
            n.Play(value, style, start, sizeMul, done => _freeNumbers.Push(done));
            PlaceNumber(n);
        }

        DamageNumberView OldestSmall()
        {
            DamageNumberView best = null;
            foreach (var v in _numbers) if (v.active && !v.critLane && (best == null || v.spawnTime < best.spawnTime)) best = v;
            return best;
        }

        DamageNumberView OldestActive()
        {
            DamageNumberView best = null;
            foreach (var v in _numbers) if (v.active && (best == null || v.spawnTime < best.spawnTime)) best = v;
            return best;
        }

        DamageNumberView NewNumber()
        {
            var n = Instantiate(numberTemplate, numberLayer != null ? numberLayer : _root);
            n.name = "DamageNumber";
            _numbers.Add(n);
            return n;
        }

        HitSparkView NewSpark()
        {
            var s = Instantiate(sparkTemplate, sparkLayer != null ? sparkLayer : _root);
            s.name = "HitSpark";
            _sparks.Add(s);
            return s;
        }

        EnemyHealthBarView NewBar()
        {
            var b = Instantiate(enemyBarTemplate, barLayer != null ? barLayer : _root);
            b.name = "EnemyBar";
            return b;
        }

        void LateUpdate()
        {
            foreach (var n in _numbers) if (n.active) PlaceNumber(n);
            foreach (var s in _sparks) if (s.active) Place(s.transform as RectTransform, s.worldPos, Vector2.zero);

            List<IEnemyHealthSource> dead = null;
            foreach (var kv in _bars)
            {
                var anchor = kv.Key.BarAnchor;
                if (anchor == null) { (dead ??= new List<IEnemyHealthSource>()).Add(kv.Key); continue; } // 적 오브젝트가 파괴됨
                Place(kv.Value.Rect, anchor.position, new Vector2(0f, barLift));
            }
            if (dead != null) foreach (var d in dead) UntrackEnemy(d);
        }

        void PlaceNumber(DamageNumberView n) => Place(n.Rect, n.worldPos, n.offset + new Vector2(n.shakeX, 0f));

        void Place(RectTransform rt, Vector3 world, Vector2 offset)
        {
            Vector2 local = ToLocal(world, out bool visible);
            if (!visible) { rt.anchoredPosition = new Vector2(-9999, -9999); return; } // 카메라 뒤
            // 픽셀 격자에 맞춰 반올림 → 글자·이펙트가 번지지 않음
            rt.anchoredPosition = new Vector2(Mathf.Round(local.x + offset.x), Mathf.Round(local.y + offset.y));
        }

        Vector2 ToLocal(Vector3 world, out bool visible)
        {
            var cam = Cam;
            if (cam == null) { visible = false; return Vector2.zero; }
            Vector3 sp = cam.WorldToScreenPoint(world);
            visible = sp.z > 0f;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, sp, null, out var local);
            return local;
        }
    }
}
