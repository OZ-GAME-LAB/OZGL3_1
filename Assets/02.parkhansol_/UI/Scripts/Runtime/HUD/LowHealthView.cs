using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// [v0.7] 저체력 경고 — 체력이 threshold 이하면 화면 가장자리 붉은 비네트가 심장 박동(쿵-쿵)에 맞춰 뛴다.
    ///   낮을수록 진하고 빠르게, 박동마다 심장 소리(UISound.Heartbeat, SFX 볼륨 적용).
    ///   회복해서 threshold를 넘으면 부드럽게 사라짐. 사망(0)이면 꺼짐(사망 화면이 대신).
    ///   일시정지(timeScale 0) 중에는 박동도 멈춤.
    /// HudView가 IHealthSource를 연결해 준다 → 팀원 작업 없음.
    /// </summary>
    [AddComponentMenu("OZ/UI/HUD/Low Health View")]
    public class LowHealthView : MonoBehaviour
    {
        [SerializeField] internal Image vignette;
        [SerializeField, Range(0.05f, 0.6f)] internal float threshold = 0.3f;
        [SerializeField] internal Color color = new Color(0.85f, 0.04f, 0.08f, 1f);
        [Tooltip("가장 낮은 체력일 때 박동 사이 바탕 진하기")]
        [SerializeField, Range(0f, 1f)] internal float maxBaseAlpha = 0.38f;
        [Tooltip("박동 순간 추가 진하기")]
        [SerializeField, Range(0f, 1f)] internal float beatAlpha = 0.32f;
        [Tooltip("박동 간격 (체력 threshold일 때 → 거의 0일 때)")]
        [SerializeField] internal Vector2 beatPeriod = new Vector2(1.1f, 0.55f);
        [SerializeField] internal bool heartbeatSound = true;
        [SerializeField] internal UISound sound = UISound.Heartbeat;

        IHealthSource _source;
        float _intensity;      // 0 = 경고 없음, 1 = 체력 거의 0
        float _shown;          // 화면에 보이는 세기 (부드럽게 따라감)
        float _beatTime;
        float _hitKick;
        bool _beatNow;

        public bool IsActive => _intensity > 0f;
        public float Intensity => _intensity;
        public float CurrentAlpha => vignette != null ? vignette.color.a : 0f;

        internal void Bind(IHealthSource source)
        {
            if (_source != null) _source.HealthChanged -= OnChanged;
            _source = source;
            if (_source != null) _source.HealthChanged += OnChanged;
            Evaluate(false);
        }

        void OnDestroy() { if (_source != null) _source.HealthChanged -= OnChanged; }

        void OnEnable() => Apply(0f);

        void OnChanged(HealthChange c)
        {
            bool was = _intensity > 0f;
            Evaluate(c.IsDamage);
            if (!was && _intensity > 0f) { _beatTime = 0f; _beatNow = true; } // 경고 시작 = 바로 첫 박동
        }

        void Evaluate(bool damaged)
        {
            float max = _source != null ? _source.MaxHP : 0f;
            float n = max > 0f ? Mathf.Clamp01(_source.HP / max) : 1f;
            _intensity = n > 0f && n <= threshold ? Mathf.Clamp01(Mathf.InverseLerp(threshold, 0f, n) * 0.85f + 0.15f) : 0f;
            if (damaged && _intensity > 0f) _hitKick = 1f;
        }

        void Update()
        {
            float target = _intensity;
            _shown = Mathf.MoveTowards(_shown, target, Time.unscaledDeltaTime * (target > _shown ? 2.5f : 1.5f));
            if (_shown <= 0.001f && _intensity <= 0f) { Apply(0f); return; }

            float dt = Time.deltaTime; // 일시정지 중엔 박동 멈춤
            float period = Mathf.Lerp(beatPeriod.x, beatPeriod.y, _intensity);
            _beatTime += dt;
            if (_beatTime >= period) { _beatTime -= period; _beatNow = true; }
            if (_beatNow && dt > 0f)
            {
                _beatNow = false;
                if (_intensity > 0f && heartbeatSound) UISfx.Play(sound, Mathf.Lerp(0.55f, 1f, _intensity));
            }

            // 쿵(0초) - 쿵(0.2초) 두 번 뛰는 모양
            float pulse = Beat(_beatTime, 0f, 0.16f) + 0.6f * Beat(_beatTime, 0.2f, 0.16f);
            _hitKick = Mathf.MoveTowards(_hitKick, 0f, Time.unscaledDeltaTime * 3f);

            float a = _shown * (maxBaseAlpha * Mathf.Lerp(0.45f, 1f, _intensity) + beatAlpha * pulse) + _hitKick * 0.25f * _shown;
            Apply(Mathf.Clamp01(a));
        }

        static float Beat(float t, float at, float width)
        {
            float x = (t - at) / width;
            if (x < 0f || x > 1f) return 0f;
            return x < 0.25f ? x / 0.25f : 1f - (x - 0.25f) / 0.75f; // 빠르게 차오르고 천천히 빠짐
        }

        void Apply(float alpha)
        {
            if (vignette == null) return;
            var c = color;
            c.a = alpha;
            vignette.color = c;
            bool on = alpha > 0.002f;
            if (vignette.enabled != on) vignette.enabled = on;
        }
    }
}
