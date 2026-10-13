using System.Collections.Generic;
using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// [v0.8] 플레이어 옆에 작게 따라다니는 세로 배터리형 체력 + 대시 동그라미 (산나비 레퍼런스).
    ///
    ///      ▄        ← 배터리 꼭지
    ///    ┌───┐ ●    ← 대시: 쓸 수 있는 횟수만큼 동그라미 (바깥쪽, 위부터). 쓰면 사라지고, 다시 차면 0.5초 깜빡이며 나타남
    ///    │▬▬▬│ ●
    ///    │▬▬▬│      ← 체력: hpPerSegment(기본 10)마다 한 칸, 아래부터 위로 쌓임
    ///    │   │        60% 이하 노랑 · 30% 이하 빨강 깜빡임
    ///    └───┘
    ///
    ///   · 위치: 플레이어 등 뒤 쪽(바라보는 반대편). 방향을 바꾸면 그 즉시 반대쪽으로 넘어감 → 공격 방향을 안 가림
    ///   · 표시(Auto): 맞음·회복·대시 직후, 체력이 가득이 아닐 때, 대시 대기 중일 때만 보이고 평소엔 숨김
    ///   · 창(일시정지·인벤토리 등)이 열렸거나 HUD가 꺼져 있거나 플레이어가 화면 밖이면 숨김
    ///
    /// 데이터: 체력 = UISources.Health (좌상단 체력바와 같은 값) · 대시 = UISources.Dash (IDashSource)
    /// 대상: UIPlayerTag.Current (MainScene은 GameBridge가 자동으로 붙임)
    /// 씬에 UIManager가 있으면 Play 시 HUD 레이어에 자동 생성 (프리팹 수정 불필요). 끄려면 PlayerSideGaugeView.AutoCreate = false
    /// 그림은 코드로 그린 사각형뿐이라 에셋 없이 동작 (픽셀 단위로 반올림해 640×360 정수배에서 선명).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("OZ/UI/HUD/Player Side Gauge View")]
    public class PlayerSideGaugeView : MonoBehaviour
    {
        public enum ShowMode { Auto = 0, Always = 1, Off = 2 }

        /// <summary>false면 씬 로드 때 자동 생성 안 함</summary>
        public static bool AutoCreate = true;

        [Header("표시")]
        [SerializeField] internal ShowMode mode = ShowMode.Auto;
        [Tooltip("[Auto] 체력 가득 + 대시 준비 상태가 이 시간(초) 이어지면 숨김")]
        [SerializeField, Min(0f)] internal float idleHideDelay = 2.5f;

        [Header("위치")]
        [Tooltip("배터리 세로 가운데 기준점 (플레이어 발밑 기준 월드 오프셋)")]
        [SerializeField] internal Vector3 worldOffset = new Vector3(0f, 1.0f, 0f);
        [Tooltip("플레이어 중심에서 배터리 안쪽 끝까지 거리 (UI px, 640×360 기준)")]
        [SerializeField, Min(0f)] internal float sideDistance = 10f;
        [Tooltip("켜면 바라보는 반대쪽(등 뒤)에 표시")]
        [SerializeField] internal bool behindFacing = true;

        [Header("체력 칸 (세로로 쌓임)")]
        [SerializeField, Min(1f)] internal float hpPerSegment = 10f;
        [SerializeField, Range(1, 30)] internal int maxSegments = 20;
        [Tooltip("칸 가로 폭 (px)")]
        [SerializeField, Min(1)] internal int segmentWidth = 5;
        [Tooltip("칸 세로 두께 (px)")]
        [SerializeField, Min(1)] internal int segmentHeight = 2;
        [SerializeField, Min(0)] internal int segmentGap = 1;
        [Tooltip("배터리 테두리와 칸 사이 여백 (px)")]
        [SerializeField, Min(0)] internal int innerPadding = 1;
        [SerializeField, Min(1)] internal int capWidth = 3;
        [SerializeField, Min(0)] internal int capHeight = 2;
        [SerializeField, Range(0f, 1f)] internal float cautionThreshold = 0.6f;
        [SerializeField, Range(0f, 1f)] internal float dangerThreshold = 0.3f;

        [Header("대시 동그라미 (배터리 바깥쪽, 위부터 아래로)")]
        [Tooltip("배터리와 동그라미 사이 간격 (px)")]
        [SerializeField, Min(0)] internal int dashGap = 2;
        [Tooltip("동그라미끼리 간격 (px)")]
        [SerializeField, Min(0)] internal int pipGap = 2;
        [Tooltip("다시 찼을 때 깜빡이는 시간 (초)")]
        [SerializeField, Min(0f)] internal float pipBlinkDuration = 0.5f;
        [Tooltip("깜빡임 한 번(켜짐+꺼짐) 길이 (초)")]
        [SerializeField, Min(0.02f)] internal float pipBlinkPeriod = 0.125f;
        [SerializeField] internal Color pipColor = new Color(0.36f, 0.92f, 1f, 1f);
        [SerializeField] internal Color pipBlinkColor = Color.white;

        [Header("색")]
        [SerializeField] internal Color normalColor = new Color(0.36f, 0.92f, 1f, 1f);
        [SerializeField] internal Color cautionColor = new Color(1f, 0.85f, 0.35f, 1f);
        [SerializeField] internal Color dangerColor = new Color(1f, 0.38f, 0.38f, 1f);
        [SerializeField] internal Color emptyColor = new Color(0.15f, 0.31f, 0.36f, 0.9f);
        [SerializeField] internal Color backColor = new Color(0.02f, 0.06f, 0.08f, 0.75f);
        [SerializeField] internal Color lostColor = new Color(1f, 0.3f, 0.3f, 1f);
        [SerializeField] internal Color healColor = new Color(0.45f, 1f, 0.55f, 1f);

        const float LostDuration = 0.5f;   // 잃은 칸: 하양(0.1초) → 빨강 유지 → 빈 칸
        const float HealDuration = 0.3f;   // 찬 칸: 초록 → 기본색
        const float ShakeDuration = 0.15f;
        const int PipSize = 5;             // 5×5 픽셀 동그라미 (모서리 4px 뺀 모양)
        const int MaxPips = 8;

        RectTransform _rt, _area;
        CanvasGroup _group;
        Image _frame, _back, _cap;
        readonly List<Image> _segs = new List<Image>();
        float[] _lostT = new float[0], _healT = new float[0];
        // 동그라미 = 가로 5×3 + 세로 3×5 두 사각형을 겹쳐서 픽셀 원 모양
        readonly Image[] _pipH = new Image[MaxPips], _pipV = new Image[MaxPips];
        readonly float[] _pipBlinkT = new float[MaxPips];
        int _pipMax, _pipCharges = -1;

        IHealthSource _health;
        Camera _cam;
        int _segCount, _lit;
        float _alpha, _lastEventTime = -99f, _shakeT;
        float _side = -1f;           // +1 플레이어 오른쪽, -1 왼쪽
        bool _hadDash, _targetWasActive;

        // ── 외부 확인용 ──
        public ShowMode Mode { get => mode; set => mode = value; }
        public int SegmentCount => _segCount;
        public int LitSegments => _lit;
        public bool IsShowing => _alpha > 0.5f;
        /// <summary>+1 = 플레이어 오른쪽, -1 = 왼쪽</summary>
        public float Side => _side;
        /// <summary>지금 보이는(깜빡임 끝난) 대시 동그라미 수</summary>
        public int VisiblePips { get { int n = 0; for (int i = 0; i < _pipMax; i++) if (i < _pipCharges && _pipBlinkT[i] <= 0f) n++; return n; } }
        /// <summary>i번째 동그라미가 깜빡이는 중인지</summary>
        public bool IsPipBlinking(int i) => i >= 0 && i < MaxPips && _pipBlinkT[i] > 0f;

        void Awake() => Build();

        /// <summary>
        /// 사각형들을 코드로 만든다. Play 중 스크립트가 다시 로드되면(Recompile And Continue Playing) 코드로 만든 참조가 비므로
        /// LateUpdate에서 비어 있으면 기존 자식을 지우고 다시 만든다.
        /// </summary>
        void Build()
        {
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            _segs.Clear();
            _segCount = 0;

            _rt = (RectTransform)transform;
            _rt.anchorMin = _rt.anchorMax = new Vector2(0.5f, 0.5f);
            _rt.pivot = new Vector2(0.5f, 0.5f);
            _rt.sizeDelta = Vector2.zero;
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            // 그리는 순서 = 만드는 순서: 테두리 → 안쪽 바탕 → 꼭지 → 대시 동그라미 → (칸은 필요할 때 뒤에 추가)
            _frame = NewRect("Frame");
            _back = NewRect("Back");
            _cap = NewRect("Cap");
            for (int i = 0; i < MaxPips; i++)
            {
                _pipH[i] = NewRect("Pip" + i + "_H");
                _pipV[i] = NewRect("Pip" + i + "_V");
            }
            if (_health != null) RebuildSegments(false);
        }

        void OnEnable()
        {
            _area = transform.parent as RectTransform;
            UISources.Changed += Rebind;
            Rebind();
        }

        void OnDisable()
        {
            UISources.Changed -= Rebind;
            SetHealth(null);
        }

        Image NewRect(string n)
        {
            var go = new GameObject(n, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = Vector2.zero; // 왼쪽 아래 기준 → 좌표를 정수 px로 바로 지정
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            return img;
        }

        static void Place(Image img, float x, float y, float w, float h)
        {
            var r = img.rectTransform;
            r.anchoredPosition = new Vector2(Mathf.Round(x), Mathf.Round(y));
            r.sizeDelta = new Vector2(Mathf.Max(0f, Mathf.Round(w)), Mathf.Max(0f, Mathf.Round(h)));
        }

        // ───────────── 데이터 ─────────────

        void Rebind()
        {
            SetHealth(UISources.Health);
            _hadDash = UISources.Dash != null;
        }

        void SetHealth(IHealthSource h)
        {
            if (ReferenceEquals(h, _health)) return;
            if (_health != null) _health.HealthChanged -= OnHealthChanged;
            _health = h;
            if (_health != null) _health.HealthChanged += OnHealthChanged;
            RebuildSegments(false);
        }

        void OnHealthChanged(HealthChange c)
        {
            int before = _lit;
            RebuildSegments(true);
            if (c.IsDamage)
            {
                _shakeT = ShakeDuration;
                for (int i = _lit; i < before && i < _lostT.Length; i++) _lostT[i] = LostDuration;
                _lastEventTime = Time.unscaledTime;
            }
            else if (c.IsHeal)
            {
                for (int i = before; i < _lit && i < _healT.Length; i++) _healT[i] = HealDuration;
                _lastEventTime = Time.unscaledTime;
            }
        }

        /// <summary>칸 수(최대 체력 기준)와 켜진 칸 수 계산. 칸 수가 바뀌면 이미지를 다시 만든다.</summary>
        void RebuildSegments(bool keepTimers)
        {
            float max = _health != null ? Mathf.Max(1f, _health.MaxHP) : 100f;
            float hp = _health != null ? Mathf.Clamp(_health.HP, 0f, max) : max;
            int count = Mathf.Clamp(Mathf.CeilToInt(max / Mathf.Max(1f, hpPerSegment) - 0.0001f), 1, maxSegments);

            if (count != _segCount)
            {
                _segCount = count;
                while (_segs.Count < count) _segs.Add(NewRect("Seg" + _segs.Count));
                for (int i = 0; i < _segs.Count; i++) _segs[i].gameObject.SetActive(i < count);
                _lostT = new float[count];
                _healT = new float[count];
                keepTimers = false;
            }
            if (!keepTimers) { System.Array.Clear(_lostT, 0, _lostT.Length); System.Array.Clear(_healT, 0, _healT.Length); }

            float per = max / count;
            // 조금이라도 남으면 그 칸은 켜짐 (0.1 남아도 1칸 보이게). 0번 = 맨 아래 칸
            _lit = hp <= 0f ? 0 : Mathf.Clamp(Mathf.CeilToInt(hp / per - 0.0001f), 1, count);
        }

        // ───────────── 매 프레임 ─────────────

        void LateUpdate()
        {
            if (_frame == null || _group == null) Build(); // Play 중 스크립트 다시 로드 대비
            float dt = Time.unscaledDeltaTime;
            Transform target = UIPlayerTag.Current;
            IDashSource dash = UISources.Dash;
            _hadDash = dash != null;

            // 대시 상태 → 동그라미 수. 줄면 그 동그라미 바로 사라짐, 늘면 새로 찬 동그라미 깜빡임
            bool dashFull = true;
            if (dash != null)
            {
                int max = Mathf.Clamp(dash.MaxDashCharges, 1, MaxPips);
                int charges = Mathf.Clamp(dash.DashCharges, 0, max);
                if (max != _pipMax) { _pipMax = max; _pipCharges = charges; System.Array.Clear(_pipBlinkT, 0, MaxPips); }
                else if (charges != _pipCharges)
                {
                    if (charges > _pipCharges)
                        for (int i = Mathf.Max(0, _pipCharges); i < charges; i++) _pipBlinkT[i] = pipBlinkDuration;
                    else
                    {
                        for (int i = charges; i < _pipCharges; i++) _pipBlinkT[i] = 0f;
                        _lastEventTime = Time.unscaledTime; // 방금 대시함 → 게이지 보이기
                    }
                    _pipCharges = charges;
                }
                dashFull = charges >= max;
            }
            else { _pipMax = 0; _pipCharges = -1; }

            // 플레이어가 막 나타남(스폰·재도전) → 잠깐 표시
            bool targetActive = target != null && target.gameObject.activeInHierarchy;
            if (targetActive && !_targetWasActive) _lastEventTime = Time.unscaledTime;
            _targetWasActive = targetActive;

            // 보일지
            bool hpFull = _health == null || _health.HP >= _health.MaxHP;
            bool want = mode != ShowMode.Off && targetActive
                        && _health != null && !UIState.IsGameplayInputBlocked && GameUI.HUD.Visible;
            if (want && mode == ShowMode.Auto)
                want = !hpFull || !dashFull || Time.unscaledTime - _lastEventTime < idleHideDelay;

            if (target != null) want &= Follow(target);

            _alpha = Mathf.MoveTowards(_alpha, want ? 1f : 0f, dt * (want ? 12f : 3f)); // 빠르게 나타나고 천천히 사라짐
            _group.alpha = _alpha;
            if (_alpha <= 0f && !want) { TickTimers(dt); return; }

            Layout();
            Paint(dt);
        }

        /// <summary>플레이어 화면 위치를 따라감. 화면 밖이면 false</summary>
        bool Follow(Transform target)
        {
            if (_area == null) _area = transform.parent as RectTransform;
            if (_cam == null || !_cam.isActiveAndEnabled) _cam = Camera.main;
            if (_cam == null || _area == null) return false;

            Vector3 sp = _cam.WorldToScreenPoint(target.position + worldOffset);
            if (sp.z < 0f) return false;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, sp, null, out var local);
            var r = _area.rect;
            if (local.x < r.xMin || local.x > r.xMax || local.y < r.yMin || local.y > r.yMax) return false;

            // 바라보는 방향 (플레이어가 ±X를 향해 회전) → 등 뒤 쪽. 방향이 바뀌면 그 프레임에 바로 넘어감
            float fx = target.forward.x;
            if (Mathf.Abs(fx) > 0.1f) _side = (behindFacing ? -1f : 1f) * Mathf.Sign(fx);

            float shake = _shakeT > 0f ? (Mathf.Repeat(Time.unscaledTime * 40f, 2f) < 1f ? 1f : -1f) : 0f;
            _rt.anchoredPosition = new Vector2(Mathf.Round(local.x + shake), Mathf.Round(local.y));
            return true;
        }

        // ── 크기 (px) ──
        int ColumnHeight => _segCount * segmentHeight + Mathf.Max(0, _segCount - 1) * segmentGap;   // 칸 기둥
        int BodyWidth => segmentWidth + 2 * (innerPadding + 1);                                      // 테두리 1px 포함
        int BodyHeight => ColumnHeight + 2 * (innerPadding + 1);

        // 위치를 매 프레임 계산 (칸 수·대시 유무·좌우 전환이 바로 반영). 사각형 20여 개라 부담 없음
        void Layout()
        {
            int bw = BodyWidth, bh = BodyHeight;
            float bottom = -Mathf.Round(bh * 0.5f);
            bool right = _side > 0f;
            // 배터리는 플레이어 쪽(안쪽), 대시 동그라미는 바깥쪽
            float bodyX = right ? sideDistance : -sideDistance - bw;
            float pipX = right ? bodyX + bw + dashGap : bodyX - dashGap - PipSize;

            Place(_frame, bodyX, bottom, bw, bh);
            Place(_back, bodyX + 1, bottom + 1, bw - 2, bh - 2);
            Place(_cap, bodyX + Mathf.Floor((bw - capWidth) * 0.5f), bottom + bh, capWidth, capHeight);

            float segX = bodyX + 1 + innerPadding;
            float segY = bottom + 1 + innerPadding;
            for (int i = 0; i < _segCount; i++)
                Place(_segs[i], segX, segY + i * (segmentHeight + segmentGap), segmentWidth, segmentHeight);

            // 동그라미: 배터리 윗줄에 맞춰 위에서 아래로
            float pipTop = bottom + bh;
            for (int i = 0; i < MaxPips; i++)
            {
                bool used = _hadDash && i < _pipMax;
                float py = pipTop - PipSize - i * (PipSize + pipGap);
                Place(_pipH[i], pipX, py + 1, PipSize, PipSize - 2);
                Place(_pipV[i], pipX + 1, py, PipSize - 2, PipSize);
                if (!used) { _pipH[i].enabled = false; _pipV[i].enabled = false; }
            }
        }

        void Paint(float dt)
        {
            float max = _health != null ? Mathf.Max(1f, _health.MaxHP) : 1f;
            float n = _health != null ? Mathf.Clamp01(_health.HP / max) : 1f;
            Color baseColor = n <= dangerThreshold ? dangerColor : n <= cautionThreshold ? cautionColor : normalColor;
            bool blinkDim = n > 0f && n <= dangerThreshold && Mathf.Repeat(Time.unscaledTime * 3f, 1f) > 0.5f;

            for (int i = 0; i < _segCount; i++)
            {
                Color c;
                if (i < _lit)
                {
                    c = blinkDim ? Color.Lerp(baseColor, emptyColor, 0.45f) : baseColor;
                    if (_healT[i] > 0f) c = Color.Lerp(c, healColor, _healT[i] / HealDuration);
                }
                else if (_lostT[i] > 0f)
                {
                    float t = 1f - _lostT[i] / LostDuration;      // 0 → 1
                    c = t < 0.2f ? Color.white : t < 0.55f ? lostColor : Color.Lerp(lostColor, emptyColor, (t - 0.55f) / 0.45f);
                }
                else c = emptyColor;
                _segs[i].color = c;
            }

            // 배터리 테두리·꼭지는 체력 상태 색 (살짝 어둡게), 위험이면 칸과 같이 깜빡
            Color frame = Color.Lerp(baseColor, backColor, blinkDim ? 0.6f : 0.25f);
            frame.a = 1f;
            _frame.color = frame;
            _cap.color = frame;
            _back.color = backColor;

            // 대시 동그라미: 남은 횟수만 보임. 깜빡이는 중이면 켜짐(하양)/꺼짐을 번갈아
            for (int i = 0; i < _pipMax && i < MaxPips; i++)
            {
                bool on = i < _pipCharges;
                Color c = pipColor;
                if (on && _pipBlinkT[i] > 0f)
                {
                    float elapsed = pipBlinkDuration - _pipBlinkT[i];
                    on = Mathf.Repeat(elapsed, pipBlinkPeriod) < pipBlinkPeriod * 0.5f;
                    c = pipBlinkColor;
                }
                _pipH[i].enabled = on; _pipV[i].enabled = on;
                _pipH[i].color = c; _pipV[i].color = c;
            }

            TickTimers(dt);
        }

        void TickTimers(float dt)
        {
            for (int i = 0; i < _lostT.Length; i++) if (_lostT[i] > 0f) _lostT[i] = Mathf.Max(0f, _lostT[i] - dt);
            for (int i = 0; i < _healT.Length; i++) if (_healT[i] > 0f) _healT[i] = Mathf.Max(0f, _healT[i] - dt);
            if (_shakeT > 0f) _shakeT = Mathf.Max(0f, _shakeT - dt);
            for (int i = 0; i < MaxPips; i++) if (_pipBlinkT[i] > 0f) _pipBlinkT[i] = Mathf.Max(0f, _pipBlinkT[i] - dt);
        }

        // ───────────── 자동 생성 ─────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoInstall()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode loadMode) => TryCreate();

        static void TryCreate()
        {
            if (!AutoCreate || UIManager.Instance == null) return;
            if (FindAnyObjectByType<PlayerSideGaugeView>(FindObjectsInactive.Include) != null) return;
            var hud = UIManager.Instance.GetLayer(UILayer.HUD);
            if (hud == null) return;
            var go = new GameObject("PlayerSideGauge (auto, UI)", typeof(RectTransform));
            go.layer = hud.gameObject.layer;
            go.transform.SetParent(hud.transform, false);
            go.AddComponent<PlayerSideGaugeView>();
        }
    }
}
