using UnityEngine;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// 팀원이 UI를 부르는 유일한 창구.
    ///
    ///   GameUI.Bind(this);                         // 플레이어: 체력/스킬/아이템 소스 등록
    ///   GameUI.Boss.Show(this, () => StartFight()); // 보스: 등장 연출 + 체력바
    ///   GameUI.Screens.ShowDeath();                 // 코어: 사망 화면
    ///   GameUI.Notify.Toast("게이트를 봉쇄했다");
    ///   GameUI.Map.SetPlayerRoom("A_03");          // 레벨: 방 트리거
    ///   GameUI.Dialogue.Play(dialogueData, next);  // NPC/연출: 대화
    ///
    /// UI가 씬에 없으면 Null 구현이 대신 받아서 아무 일도 하지 않는다 (에러 없음).
    /// </summary>
    public static class GameUI
    {
        static IHudApi _hud;
        static IBossApi _boss;
        static IScreenApi _screens;
        static INotifyApi _notify;
        static IMapApi _map;
        static IDialogueApi _dialogue;

        public static IHudApi HUD => _hud ?? NullHudApi.Instance;
        public static IBossApi Boss => _boss ?? NullBossApi.Instance;
        public static IScreenApi Screens => _screens ?? NullScreenApi.Instance;
        public static INotifyApi Notify => _notify ?? NullNotifyApi.Instance;
        public static IMapApi Map => _map ?? NullMapApi.Instance;
        public static IDialogueApi Dialogue => _dialogue ?? NullDialogueApi.Instance;

        /// <summary>UI가 씬에 올라와 있는지</summary>
        public static bool IsReady => _screens != null;

        /// <summary>HUD 데이터 소스 등록 (UISources.Bind 단축)</summary>
        public static void Bind(object source) => UISources.Bind(source);
        public static void Unbind(object source) => UISources.Unbind(source);

        // ── UI 어셈블리 전용 등록 ──
        internal static void Register(IHudApi api) => _hud = api;
        internal static void Register(IBossApi api) => _boss = api;
        internal static void Register(IScreenApi api) => _screens = api;
        internal static void Register(INotifyApi api) => _notify = api;
        internal static void Register(IMapApi api) => _map = api;
        internal static void Register(IDialogueApi api) => _dialogue = api;

        internal static void Unregister(object api)
        {
            if (ReferenceEquals(_hud, api)) _hud = null;
            if (ReferenceEquals(_boss, api)) _boss = null;
            if (ReferenceEquals(_screens, api)) _screens = null;
            if (ReferenceEquals(_notify, api)) _notify = null;
            if (ReferenceEquals(_map, api)) _map = null;
            if (ReferenceEquals(_dialogue, api)) _dialogue = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _hud = null; _boss = null; _screens = null; _notify = null; _map = null; _dialogue = null;
        }
    }
}
