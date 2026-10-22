using System;
using UnityEngine;

namespace ProjectM.CombatVfxPreview
{
    public enum CombatVfxKind { DashBurst, SlashWave, MagicMissile, Explosion }
    public enum CombatVfxLayerRole { DashStreak, DashAir, SlashGlow, SlashCore, SlashTail, MissileCore, MissileShell, MissileOrbit, MissileTail, BlastFlash, BlastStar, BlastRing, GroundRing }

    /// <summary>Standalone visual sample. It never subscribes to game input, combat, collision or item systems.</summary>
    [DisallowMultipleComponent]
    public sealed class CombatVfxSample : MonoBehaviour
    {
        [Serializable]
        public sealed class MeshLayer
        {
            public Renderer renderer;
            public CombatVfxLayerRole role;
            public Vector3 position, scale = Vector3.one;
            public Quaternion rotation = Quaternion.identity;
            public float delay, opacity = 1f;
        }

        [SerializeField] private CombatVfxKind _kind;
        [SerializeField] private float _duration = 1f, _travelSpeed, _travelDuration;
        [SerializeField] private Transform _flight;
        [SerializeField] private Vector3 _flightOrigin;
        [SerializeField] private MeshLayer[] _layers = Array.Empty<MeshLayer>();
        [SerializeField] private ParticleSystem[] _particles = Array.Empty<ParticleSystem>();
        private MaterialPropertyBlock _properties;
        private bool _initialized, _playing, _completed;
        private float _time;
        private const float Step = 1f / 60f;
        private static readonly int TintId = Shader.PropertyToID("_Tint");

        public CombatVfxKind Kind => _kind;
        public float Duration => _duration;
        public float TravelSpeed => _travelSpeed;
        public float TravelDuration => _travelDuration;
        public float SampleTime => _time;
        public bool IsPlaying => _playing;
        public event Action<CombatVfxSample> Completed;

        public void Configure(CombatVfxKind kind, float duration, float travelSpeed, float travelDuration,
            Transform flight, MeshLayer[] layers, ParticleSystem[] particles)
        {
            _kind = kind; _duration = duration; _travelSpeed = travelSpeed; _travelDuration = travelDuration;
            _flight = flight; _flightOrigin = flight != null ? flight.localPosition : Vector3.zero;
            _layers = layers ?? Array.Empty<MeshLayer>(); _particles = particles ?? Array.Empty<ParticleSystem>();
            _initialized = false;
            ResetPreview();
        }

        /// <summary>Explicit preview playback only; placing/enabling a prefab never starts it automatically.</summary>
        public void Play()
        {
            ResetPreview();
            _playing = true;
        }

        private void Update()
        {
            if (!_playing) return;
            SetPreviewTime(_time + Time.deltaTime);
            if (_time >= _duration && !_completed)
            {
                _completed = true; _playing = false;
                Completed?.Invoke(this);
            }
        }

        public void ResetPreview()
        {
            _properties ??= new MaterialPropertyBlock();
            _time = 0; _playing = false; _completed = false;
            ApplyMeshPose(0);
            for (int i = 0; i < _particles.Length; i++)
            {
                var particle = _particles[i];
                if (particle == null) continue;
                particle.useAutoRandomSeed = false;
                particle.randomSeed = (uint)(1249 + i * 83 + (int)_kind * 241);
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Simulate(0, false, true, false);
            }
            _initialized = true;
        }

        /// <summary>Deterministic forward/rewind sampling using native ParticleSystem simulation at fixed 60 Hz.</summary>
        public void SetPreviewTime(float seconds)
        {
            float target = Mathf.Clamp(seconds, 0, _duration);
            if (!_initialized || target < _time - .00001f) ResetPreview();
            // A complete reset on every request would retrace particle paths unnecessarily.
            // Sequential captures and preview playback share this same bounded fixed-step simulation.
            while (_time + .000001f < target)
            {
                float dt = Mathf.Min(Step, target - _time);
                _time += dt;
                ApplyMeshPose(_time);
                for (int i = 0; i < _particles.Length; i++)
                    if (_particles[i] != null) _particles[i].Simulate(dt, false, false, false);
            }
            _time = target;
            ApplyMeshPose(_time);
        }

        private void OnDisable()
        {
            _playing = false; _initialized = false;
            for (int i = 0; i < _particles.Length; i++)
                if (_particles[i] != null) _particles[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            for (int i = 0; i < _layers.Length; i++)
                if (_layers[i] != null && _layers[i].renderer != null) _layers[i].renderer.enabled = false;
        }

        private void ApplyMeshPose(float seconds)
        {
            _properties ??= new MaterialPropertyBlock();
            if (_flight != null) _flight.localPosition = _flightOrigin + Vector3.forward * (_travelSpeed * Mathf.Min(seconds, _travelDuration));
            for (int i = 0; i < _layers.Length; i++)
            {
                MeshLayer layer = _layers[i];
                if (layer == null || layer.renderer == null) continue;
                Transform part = layer.renderer.transform;
                part.localPosition = layer.position; part.localRotation = layer.rotation; part.localScale = layer.scale;
                float t = seconds - layer.delay, alpha = 0;
                switch (layer.role)
                {
                    case CombatVfxLayerRole.DashStreak:
                        alpha = Envelope(t, .018f, .13f, .39f);
                        part.localScale = Vector3.Scale(layer.scale, new Vector3(1, Mathf.Lerp(.3f, 1f, Mathf.Clamp01(t / .045f)),
                            Mathf.Lerp(.12f, 1f, Mathf.Clamp01(t / .10f))));
                        part.localPosition += Vector3.back * Mathf.Max(0, t - .12f) * 2f;
                        break;
                    case CombatVfxLayerRole.DashAir:
                        alpha = Envelope(t, .025f, .12f, .34f);
                        part.localScale = layer.scale * Mathf.Lerp(.7f, 1.15f, Mathf.Clamp01(t / .28f));
                        break;
                    case CombatVfxLayerRole.SlashGlow:
                    case CombatVfxLayerRole.SlashCore:
                    case CombatVfxLayerRole.SlashTail:
                        alpha = Envelope(t, .035f, .52f, .82f);
                        part.localScale = layer.scale * Mathf.Lerp(.65f, 1f, Mathf.Clamp01(t / .075f));
                        if (layer.role == CombatVfxLayerRole.SlashTail)
                            part.localScale = Vector3.Scale(part.localScale, new Vector3(1, 1, .92f + .08f * Mathf.Sin(t * 34)));
                        break;
                    case CombatVfxLayerRole.MissileCore:
                        alpha = Envelope(t, .025f, .66f, .91f);
                        part.localRotation = layer.rotation * Quaternion.Euler(t * 180, t * 210, t * 110);
                        part.localScale = layer.scale * (1f + .07f * Mathf.Sin(t * 33));
                        break;
                    case CombatVfxLayerRole.MissileShell:
                        alpha = Envelope(t, .03f, .64f, .93f);
                        part.localRotation = layer.rotation * Quaternion.Euler(-t * 110, t * 160, -t * 190);
                        break;
                    case CombatVfxLayerRole.MissileOrbit:
                        alpha = Envelope(t, .045f, .65f, .94f);
                        part.localRotation = Quaternion.AngleAxis(t * 260, Vector3.forward) * layer.rotation;
                        break;
                    case CombatVfxLayerRole.MissileTail:
                        alpha = Envelope(t, .04f, .65f, .94f);
                        part.localScale = Vector3.Scale(layer.scale, new Vector3(1, .94f + .06f * Mathf.Sin(t * 41),
                            Mathf.Lerp(.1f, 1f, Mathf.Clamp01(t / .11f))));
                        break;
                    case CombatVfxLayerRole.BlastFlash:
                        alpha = Envelope(t, .012f, .035f, .16f);
                        part.localScale = layer.scale * Mathf.Lerp(.25f, 1.3f, Mathf.Clamp01(t / .13f));
                        break;
                    case CombatVfxLayerRole.BlastStar:
                        // Keep the bright accent brief; a long dim star reads as a gray solid spike cluster.
                        alpha = Envelope(t, .015f, .035f, .16f);
                        part.localScale = layer.scale * Mathf.Lerp(.15f, 1.15f, Mathf.Clamp01(t / .14f));
                        part.localRotation = Quaternion.AngleAxis(t * 35, Vector3.right) * layer.rotation;
                        break;
                    case CombatVfxLayerRole.BlastRing:
                    case CombatVfxLayerRole.GroundRing:
                        alpha = Envelope(t, .025f, .11f, .44f);
                        part.localScale = layer.scale * Mathf.Lerp(.12f, 2.1f, Mathf.Clamp01(t / .40f));
                        break;
                }
                alpha *= layer.opacity;
                layer.renderer.enabled = alpha > .001f;
                _properties.Clear();
                _properties.SetColor(TintId, new Color(1, 1, 1, alpha));
                layer.renderer.SetPropertyBlock(_properties);
            }
        }

        private static float Envelope(float t, float rise, float hold, float end)
        {
            if (t <= 0 || t >= end) return 0;
            return Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / rise)) *
                (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(hold, end, t)));
        }
    }
}
