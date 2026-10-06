using System;
using System.Collections.Generic;
using UnityEngine;

namespace OZ.UI.Contracts
{
    // UI가 없는 씬(팀원 테스트 씬)에서도 GameUI 호출이 에러 없이 무시되도록 하는 기본 구현.
    // 첫 호출 시 한 번만 로그를 남겨 "UI가 안 붙어 있음"을 알 수 있게 한다.

    internal static class NullLog
    {
        static readonly HashSet<string> _logged = new HashSet<string>();

        public static void Once(string api)
        {
            if (_logged.Add(api))
                Debug.Log($"[GameUI] {api}: UI가 씬에 없어 호출을 무시합니다. (UI_Sandbox 또는 UIRoot 프리팹을 넣으면 동작)");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _logged.Clear();
    }

    internal sealed class NullHudApi : IHudApi
    {
        public static readonly NullHudApi Instance = new NullHudApi();
        public bool Visible { get => false; set => NullLog.Once("HUD.Visible"); }
        public void ShowGuide(string message, float duration = 3f) => NullLog.Once("HUD.ShowGuide");
        public void HideGuide() { }
        public void Flash(Color color, float duration = 0.15f) => NullLog.Once("HUD.Flash");
    }

    internal sealed class NullBossApi : IBossApi
    {
        public static readonly NullBossApi Instance = new NullBossApi();
        public bool IsShowing => false;

        public void Show(IBossSource boss, Action onIntroFinished = null)
        {
            NullLog.Once("Boss.Show");
            onIntroFinished?.Invoke(); // 연출이 없으니 바로 전투 재개
        }

        public void Hide() { }
    }

    internal sealed class NullScreenApi : IScreenApi
    {
        public static readonly NullScreenApi Instance = new NullScreenApi();
        public bool IsOpen(ScreenId id) => false;
        public void Open(ScreenId id) => NullLog.Once("Screens.Open");
        public void Close(ScreenId id) { }
        public void CloseAll() { }
        public void ShowTitle(bool canContinue) => NullLog.Once("Screens.ShowTitle");
        public void ShowClassSelect(IReadOnlyList<ClassData> classes) => NullLog.Once("Screens.ShowClassSelect");
        public void ShowDeath() => NullLog.Once("Screens.ShowDeath");
        public void ShowStageClear(int stageNumber, HunterRank newRank) => NullLog.Once("Screens.ShowStageClear");
        public void ShowDemoEnd() => NullLog.Once("Screens.ShowDemoEnd");
    }

    internal sealed class NullNotifyApi : INotifyApi
    {
        public static readonly NullNotifyApi Instance = new NullNotifyApi();
        public void Toast(string message, ToastType type = ToastType.Info) => NullLog.Once("Notify.Toast");
    }

    internal sealed class NullMapApi : IMapApi
    {
        public static readonly NullMapApi Instance = new NullMapApi();
        public MapData Current => null;
        public void SetMap(MapData map) => NullLog.Once("Map.SetMap");
        public void SetPlayerRoom(string roomId) => NullLog.Once("Map.SetPlayerRoom");
        public void RevealRoom(string roomId) { }
        public void SetRoomIcon(string roomId, MapRoomIcon icon) { }
    }

    internal sealed class NullDialogueApi : IDialogueApi
    {
        public static readonly NullDialogueApi Instance = new NullDialogueApi();
        public bool IsPlaying => false;

        public void Play(DialogueData dialogue, Action onFinished = null)
        {
            NullLog.Once("Dialogue.Play");
            onFinished?.Invoke(); // 대화가 없으니 바로 다음 진행
        }

        public void Say(SpeakerData speaker, string text, DialogueSide side = DialogueSide.Right, Action onFinished = null)
        {
            NullLog.Once("Dialogue.Say");
            onFinished?.Invoke();
        }

        public void Stop() { }
    }
}
