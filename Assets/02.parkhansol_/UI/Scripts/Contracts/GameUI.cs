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
    ///   GameUI.Notify.System("랭크 승급", "F급 → E급");  // 지도 아래 SYSTEM 알림
    ///   GameUI.Quest.Set("kill", "감염체 처치", 0, 20);    // 좌상단 퀘스트 목록
    ///   GameUI.Map.SetPlayerRoom("A_03");          // 레벨: 방 트리거
    ///   GameUI.Dialogue.Play(dialogueData, next);  // NPC/연출: 대화
    ///   GameUI.Damage.Show(hitPos, 37, DamageKind.Critical); // 전투: 피해 숫자 + 타격 이펙트
    ///   GameUI.Flow.Transition(() => LoadStage(2));       // 코어: 페이드 + 스테이지 교체
    ///   GameUI.Area.Show("서울 거리");                     // 레벨: 지역 이름 (v0.7, AreaNameTrigger)
    ///   GameUI.Indicators.Track(gate, IndicatorKind.Gate); // 화면 밖 목표 화살표 (v0.7)
    ///   GameUI.Prompt.Show(door, "열기");                  // "[F] 열기" (v0.7, InteractPromptLink)
    ///   GameUI.Shop.Open(shopData);                        // 상점 창 (v1.0, 보통은 ShopkeeperLink가 부름)
    ///   GameUI.Status.Open(StatusTab.Shop);                // 상태창 (v1.0 기획서 v0.5, Tab)
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
        static IDamageFxApi _damage;
        static IFlowApi _flow;
        static IQuestApi _quest;
        static IUISoundApi _sound;
        static IAreaApi _area;
        static IIndicatorApi _indicators;
        static IPromptApi _prompt;
        static IShopApi _shop;
        static IStatusApi _status;

        public static IHudApi HUD => _hud ?? NullHudApi.Instance;
        public static IBossApi Boss => _boss ?? NullBossApi.Instance;
        public static IScreenApi Screens => _screens ?? NullScreenApi.Instance;
        public static INotifyApi Notify => _notify ?? NullNotifyApi.Instance;
        public static IMapApi Map => _map ?? NullMapApi.Instance;
        public static IDialogueApi Dialogue => _dialogue ?? NullDialogueApi.Instance;
        public static IDamageFxApi Damage => _damage ?? NullDamageFxApi.Instance;
        /// <summary>로딩 화면 · 페이드 · 스테이지 시작 띠 (GameManager / StageManager용)</summary>
        public static IFlowApi Flow => _flow ?? NullFlowApi.Instance;
        /// <summary>좌상단 퀘스트 목록 (게이트 봉쇄·보스 처치는 자동)</summary>
        public static IQuestApi Quest => _quest ?? NullQuestApi.Instance;
        /// <summary>UI 효과음 (SFX 볼륨 적용). UI 쪽 소리는 자동 — 게임 쪽 특별한 순간에만 호출</summary>
        public static IUISoundApi Sound => _sound ?? NullUISoundApi.Instance;
        /// <summary>[v0.7] 화면 위쪽 지역 이름 (부드럽게 떴다 사라짐). 배치는 AreaNameTrigger</summary>
        public static IAreaApi Area => _area ?? NullAreaApi.Instance;
        /// <summary>[v0.7] 화면 밖 목표 화살표 (게이트·보스 등). 배치는 ObjectiveMarkerLink</summary>
        public static IIndicatorApi Indicators => _indicators ?? NullIndicatorApi.Instance;
        /// <summary>[v0.7] 상호작용 안내 "[F] 조사". 배치는 InteractPromptLink</summary>
        public static IPromptApi Prompt => _prompt ?? NullPromptApi.Instance;
        /// <summary>[v1.0] 상점 창. 배치는 ShopkeeperLink (NPC)</summary>
        public static IShopApi Shop => _shop ?? NullShopApi.Instance;
        /// <summary>[v1.0] 상태창 (스탯·퀘스트·상점·스킬·지도). Tab 키</summary>
        public static IStatusApi Status => _status ?? NullStatusApi.Instance;

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
        internal static void Register(IDamageFxApi api) => _damage = api;
        internal static void Register(IFlowApi api) => _flow = api;
        internal static void Register(IQuestApi api) => _quest = api;
        internal static void Register(IUISoundApi api) => _sound = api;
        internal static void Register(IAreaApi api) => _area = api;
        internal static void Register(IIndicatorApi api) => _indicators = api;
        internal static void Register(IPromptApi api) => _prompt = api;
        internal static void Register(IShopApi api) => _shop = api;
        internal static void Register(IStatusApi api) => _status = api;

        internal static void Unregister(object api)
        {
            if (ReferenceEquals(_hud, api)) _hud = null;
            if (ReferenceEquals(_boss, api)) _boss = null;
            if (ReferenceEquals(_screens, api)) _screens = null;
            if (ReferenceEquals(_notify, api)) _notify = null;
            if (ReferenceEquals(_map, api)) _map = null;
            if (ReferenceEquals(_dialogue, api)) _dialogue = null;
            if (ReferenceEquals(_damage, api)) _damage = null;
            if (ReferenceEquals(_flow, api)) _flow = null;
            if (ReferenceEquals(_quest, api)) _quest = null;
            if (ReferenceEquals(_sound, api)) _sound = null;
            if (ReferenceEquals(_area, api)) _area = null;
            if (ReferenceEquals(_indicators, api)) _indicators = null;
            if (ReferenceEquals(_prompt, api)) _prompt = null;
            if (ReferenceEquals(_shop, api)) _shop = null;
            if (ReferenceEquals(_status, api)) _status = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _hud = null; _boss = null; _screens = null; _notify = null; _map = null; _dialogue = null; _damage = null; _flow = null; _quest = null; _sound = null;
            _area = null; _indicators = null; _prompt = null; _shop = null; _status = null;
        }
    }
}
