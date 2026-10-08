using System;
using UnityEngine;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// 옵션 창 설정값 (PlayerPrefs 저장). 옵션 창(UI)이 값을 바꾸고, 팀원은 읽기만 하면 된다.
    ///
    ///   [사운드 담당]  bgmSource.volume = UISettings.BgmVolume;  UISettings.Changed += 다시적용;
    ///                  (MasterVolume은 UI가 AudioListener.volume으로 직접 적용)
    ///   [카메라 담당]  if (UISettings.ScreenShake) 흔들기();
    ///   [전투 담당]    피해 숫자는 GameUI.Damage가 알아서 옵션(표시·크기·줄여 쓰기)을 따름
    ///
    /// 화면(전체 화면·해상도·수직 동기화)은 UI가 직접 적용한다.
    /// </summary>
    public static class UISettings
    {
        const string P = "OZ.Settings.";

        static bool _loaded;
        static float _master = 1f, _bgm = 0.8f, _sfx = 0.8f;
        static bool _fullscreen = true, _vsync = true, _shake = true, _damageNumbers = true, _compact = true;
        static int _numberSize = 1;
        static int _resW, _resH;

        /// <summary>어떤 값이든 바뀌면 호출</summary>
        public static event Action Changed;

        public static float MasterVolume { get { Load(); return _master; } set => Set(ref _master, Mathf.Clamp01(value)); }
        public static float BgmVolume    { get { Load(); return _bgm; }    set => Set(ref _bgm, Mathf.Clamp01(value)); }
        public static float SfxVolume    { get { Load(); return _sfx; }    set => Set(ref _sfx, Mathf.Clamp01(value)); }
        public static bool Fullscreen    { get { Load(); return _fullscreen; } set => Set(ref _fullscreen, value); }
        public static bool VSync         { get { Load(); return _vsync; } set => Set(ref _vsync, value); }
        public static bool ScreenShake   { get { Load(); return _shake; } set => Set(ref _shake, value); }
        public static bool ShowDamageNumbers { get { Load(); return _damageNumbers; } set => Set(ref _damageNumbers, value); }
        /// <summary>피해 숫자 크기 배율 (1 = 보통, 2 = 크게). 픽셀 폰트가 번지지 않게 정수배만</summary>
        public static int DamageNumberSize { get { Load(); return _numberSize; } set => Set(ref _numberSize, Mathf.Clamp(value, 1, 2)); }
        /// <summary>큰 숫자 줄여 쓰기 (12,345 → 12.3k)</summary>
        public static bool CompactNumbers { get { Load(); return _compact; } set => Set(ref _compact, value); }

        /// <summary>저장된 해상도 (0이면 아직 고른 적 없음 → 현재 해상도 유지)</summary>
        public static Vector2Int Resolution
        {
            get { Load(); return new Vector2Int(_resW, _resH); }
            set
            {
                Load();
                if (value.x == _resW && value.y == _resH) return;
                _resW = value.x; _resH = value.y;
                Changed?.Invoke();
            }
        }

        /// <summary>BGM 실제 볼륨 = 마스터 × 배경음 (AudioListener를 안 쓰는 경우용)</summary>
        public static float EffectiveBgm => MasterVolume * BgmVolume;
        public static float EffectiveSfx => MasterVolume * SfxVolume;

        static void Set<T>(ref T field, T value)
        {
            Load();
            if (Equals(field, value)) return;
            field = value;
            Changed?.Invoke();
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _master = PlayerPrefs.GetFloat(P + "Master", 1f);
            _bgm = PlayerPrefs.GetFloat(P + "Bgm", 0.8f);
            _sfx = PlayerPrefs.GetFloat(P + "Sfx", 0.8f);
            _fullscreen = PlayerPrefs.GetInt(P + "Fullscreen", 1) == 1;
            _vsync = PlayerPrefs.GetInt(P + "VSync", 1) == 1;
            _shake = PlayerPrefs.GetInt(P + "Shake", 1) == 1;
            _damageNumbers = PlayerPrefs.GetInt(P + "DamageNumbers", 1) == 1;
            _numberSize = Mathf.Clamp(PlayerPrefs.GetInt(P + "NumberSize", 1), 1, 2);
            _compact = PlayerPrefs.GetInt(P + "Compact", 1) == 1;
            _resW = PlayerPrefs.GetInt(P + "ResW", 0);
            _resH = PlayerPrefs.GetInt(P + "ResH", 0);
        }

        /// <summary>디스크에 저장 (옵션 창을 닫을 때 UI가 호출)</summary>
        public static void Save()
        {
            Load();
            PlayerPrefs.SetFloat(P + "Master", _master);
            PlayerPrefs.SetFloat(P + "Bgm", _bgm);
            PlayerPrefs.SetFloat(P + "Sfx", _sfx);
            PlayerPrefs.SetInt(P + "Fullscreen", _fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(P + "VSync", _vsync ? 1 : 0);
            PlayerPrefs.SetInt(P + "Shake", _shake ? 1 : 0);
            PlayerPrefs.SetInt(P + "DamageNumbers", _damageNumbers ? 1 : 0);
            PlayerPrefs.SetInt(P + "NumberSize", _numberSize);
            PlayerPrefs.SetInt(P + "Compact", _compact ? 1 : 0);
            PlayerPrefs.SetInt(P + "ResW", _resW);
            PlayerPrefs.SetInt(P + "ResH", _resH);
            PlayerPrefs.Save();
        }

        /// <summary>기본값으로 (해상도는 유지)</summary>
        public static void ResetToDefaults()
        {
            Load();
            _master = 1f; _bgm = 0.8f; _sfx = 0.8f;
            _fullscreen = true; _vsync = true; _shake = true; _damageNumbers = true; _numberSize = 1; _compact = true;
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Changed = null;
            _loaded = false;
        }
    }
}
