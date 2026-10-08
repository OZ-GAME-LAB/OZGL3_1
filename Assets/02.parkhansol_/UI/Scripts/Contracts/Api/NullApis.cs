using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
        public void ShowBanner(string title, string subtitle = null, float holdSeconds = 2f, Action onFinished = null)
        {
            NullLog.Once("HUD.ShowBanner");
            onFinished?.Invoke();
        }
        public void SetPortrait(Sprite portrait) { }
    }

    internal sealed class NullDamageFxApi : IDamageFxApi
    {
        public static readonly NullDamageFxApi Instance = new NullDamageFxApi();
        public void Show(Vector3 worldPosition, float amount, DamageKind kind = DamageKind.Normal) => NullLog.Once("Damage.Show");
        public void ShowText(Vector3 worldPosition, string text, DamageKind kind = DamageKind.Miss) => NullLog.Once("Damage.ShowText");
        public void Spark(Vector3 worldPosition, bool critical = false) { }
        public void TrackEnemy(IEnemyHealthSource enemy) => NullLog.Once("Damage.TrackEnemy");
        public void UntrackEnemy(IEnemyHealthSource enemy) { }
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
        public void ShowStageClear(int stageNumber, HunterRank newRank)
        {
            NullLog.Once("Screens.ShowStageClear");
            UIRequests.RaiseNextStage(); // UI가 없어도 흐름이 멈추지 않게
        }
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

    internal sealed class NullFlowApi : IFlowApi
    {
        public static readonly NullFlowApi Instance = new NullFlowApi();
        public bool IsLoading => false;
        public bool IsFaded => false;
        public void ShowLoading(string message = null) => NullLog.Once("Flow.ShowLoading");
        public void SetLoadingProgress(float progress01, string message = null) { }
        public void HideLoading(Action onHidden = null) => onHidden?.Invoke();
        public void FadeOut(float duration = 0.35f, Action onBlack = null) { NullLog.Once("Flow.FadeOut"); onBlack?.Invoke(); }
        public void FadeIn(float duration = 0.35f, Action onClear = null) => onClear?.Invoke();
        public void Transition(Action whileBlack, float duration = 0.35f, Action onFinished = null)
        {
            NullLog.Once("Flow.Transition");
            whileBlack?.Invoke(); // 연출이 없어도 실제 교체 작업은 반드시 실행
            onFinished?.Invoke();
        }
        public void StageIntro(int stageNumber, string title, string subtitle = null, Action onFinished = null)
        {
            NullLog.Once("Flow.StageIntro");
            onFinished?.Invoke();
        }
        public Task FadeOutAsync(float duration = 0.35f) => Task.CompletedTask;
        public Task FadeInAsync(float duration = 0.35f) => Task.CompletedTask;
        public Task TransitionAsync(Func<Task> whileBlack, float duration = 0.35f) => whileBlack != null ? whileBlack() : Task.CompletedTask;
    }
}
