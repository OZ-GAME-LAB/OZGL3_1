using OZ.UI.Contracts;
using UnityEngine;

namespace OZ.UI
{
    /// <summary>
    /// UISettings 중 UI가 직접 책임지는 값 적용: 마스터 볼륨(AudioListener), 전체 화면, 해상도, 수직 동기화.
    /// UIManager가 시작할 때 한 번 연결한다.
    /// </summary>
    public static class UISettingsApplier
    {
        static bool _hooked;

        public static void Hook()
        {
            if (!_hooked) { UISettings.Changed += Apply; _hooked = true; }
            Apply();
        }

        public static void Apply()
        {
            AudioListener.volume = UISettings.MasterVolume;
            QualitySettings.vSyncCount = UISettings.VSync ? 1 : 0;
#if !UNITY_EDITOR
            var res = UISettings.Resolution;
            var mode = UISettings.Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (res.x > 0 && res.y > 0)
            {
                if (Screen.width != res.x || Screen.height != res.y || Screen.fullScreenMode != mode)
                    Screen.SetResolution(res.x, res.y, mode);
            }
            else if (Screen.fullScreenMode != mode) Screen.fullScreenMode = mode;
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _hooked = false;
    }
}
