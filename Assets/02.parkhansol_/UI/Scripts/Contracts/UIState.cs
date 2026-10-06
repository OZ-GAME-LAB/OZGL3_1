using System;
using UnityEngine;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// 게임플레이가 확인할 UI 상태.
    /// 플레이어 입력 처리 앞에 한 줄: if (UIState.IsGameplayInputBlocked) return;
    /// </summary>
    public static class UIState
    {
        /// <summary>스킬 창·일시정지·사망 화면 등 입력을 막는 창이 열려 있으면 true</summary>
        public static bool IsGameplayInputBlocked { get; private set; }

        /// <summary>UI가 Time.timeScale을 0으로 멈춘 상태인지</summary>
        public static bool IsPausedByUI { get; private set; }

        public static event Action<bool> GameplayInputBlockedChanged;
        public static event Action<bool> PausedByUIChanged;

        internal static void SetInputBlocked(bool value)
        {
            if (IsGameplayInputBlocked == value) return;
            IsGameplayInputBlocked = value;
            GameplayInputBlockedChanged?.Invoke(value);
        }

        internal static void SetPaused(bool value)
        {
            if (IsPausedByUI == value) return;
            IsPausedByUI = value;
            PausedByUIChanged?.Invoke(value);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            IsGameplayInputBlocked = false; IsPausedByUI = false;
            GameplayInputBlockedChanged = null; PausedByUIChanged = null;
        }
    }
}
