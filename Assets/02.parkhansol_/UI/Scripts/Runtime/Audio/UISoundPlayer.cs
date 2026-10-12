using System.Collections.Generic;
using OZ.UI.Contracts;
using UnityEngine;

namespace OZ.UI
{
    /// <summary>
    /// GameUI.Sound 구현. UIRoot에 붙어 있음.
    ///   - AudioSource 풀 (2D, 일시정지·timeScale 0에도 재생)
    ///   - 볼륨 = 항목 볼륨 × 옵션 'SFX' (마스터는 AudioListener.volume으로 이미 적용됨)
    ///   - 같은 소리 연타 방지 (항목별 cooldown)
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("OZ/UI/Audio/UI Sound Player")]
    public class UISoundPlayer : MonoBehaviour, IUISoundApi
    {
        [SerializeField] internal UISoundSet soundSet;
        [SerializeField, Range(1, 16)] internal int voices = 8;

        readonly List<AudioSource> _sources = new List<AudioSource>();
        readonly Dictionary<UISound, float> _lastPlayed = new Dictionary<UISound, float>();
        int _next;

        public bool Muted { get; set; }
        /// <summary>테스트용: 마지막으로 재생한 소리</summary>
        public UISound LastPlayed { get; private set; }
        public int PlayCount { get; private set; }

        void Awake() => EnsureSources();

        // 플레이 중 스크립트 재컴파일(도메인 리로드) 뒤에는 리스트가 비어 있으므로 다시 채운다
        void EnsureSources()
        {
            if (_sources.Count > 0) return;
            foreach (var old in GetComponents<AudioSource>()) _sources.Add(old);
            if (_sources.Count > 0) return;
            for (int i = 0; i < voices; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                src.ignoreListenerPause = true;
                src.bypassReverbZones = true;
                src.priority = 64;
                _sources.Add(src);
            }
        }

        void OnEnable() => GameUI.Register((IUISoundApi)this);
        void OnDisable() => GameUI.Unregister(this);

        public void Play(UISound sound, float volumeScale = 1f)
        {
            if (sound == UISound.None || Muted || soundSet == null) return;
            var e = soundSet.Find(sound);
            if (e == null || e.clips == null || e.clips.Length == 0) return;
            float now = Time.unscaledTime;
            if (_lastPlayed.TryGetValue(sound, out var last) && now - last < e.cooldown) return;
            _lastPlayed[sound] = now;

            var clip = e.clips[e.clips.Length == 1 ? 0 : Random.Range(0, e.clips.Length)];
            if (clip == null) return;
            float vol = e.volume * volumeScale * UISettings.SfxVolume;
            LastPlayed = sound;
            PlayCount++;
            if (vol <= 0.001f) return;

            EnsureSources();
            if (_next >= _sources.Count) _next = 0;
            var src = _sources[_next];
            _next = (_next + 1) % _sources.Count;
            src.pitch = 1f + (e.pitchJitter > 0f ? Random.Range(-e.pitchJitter, e.pitchJitter) : 0f);
            src.PlayOneShot(clip, vol);
        }
    }

    /// <summary>UI 코드 안에서 짧게 부르는 용도</summary>
    internal static class UISfx
    {
        public static void Play(UISound s, float volumeScale = 1f) => GameUI.Sound.Play(s, volumeScale);
    }
}
