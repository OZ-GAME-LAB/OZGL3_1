using System;
using System.Collections.Generic;
using UnityEngine;

namespace OZ.UI.Contracts
{
    /// <summary>HUD 전체 제어 (UI 구현, 팀원 호출)</summary>
    public interface IHudApi
    {
        bool Visible { get; set; }
        /// <summary>화면 상단 안내 문구 (예: "보스 구역이 열렸다"). duration 0 이하면 HideGuide 전까지 유지</summary>
        void ShowGuide(string message, float duration = 3f);
        void HideGuide();
        /// <summary>화면 전체 색 플래시 (큰 피격, 페이즈 전환 등)</summary>
        void Flash(Color color, float duration = 0.15f);
        /// <summary>
        /// 화면 가운데 큰 띠 문구 (예: "게이트 파괴"). holdSeconds 동안 떠 있다가 사라진다. 게임은 멈추지 않음.
        /// 게이트 봉쇄(IGateSource.GateSealed) 시에는 UI가 자동으로 "게이트 파괴"를 띄우므로 따로 부를 필요 없음.
        /// </summary>
        void ShowBanner(string title, string subtitle = null, float holdSeconds = 2f, Action onFinished = null);
        /// <summary>HP 바 옆 초상화 칸 이미지 (null이면 기본 초상화)</summary>
        void SetPortrait(Sprite portrait);
    }

    /// <summary>보스 체력바 + 등장/페이즈/처치 연출</summary>
    public interface IBossApi
    {
        bool IsShowing { get; }
        /// <summary>등장 연출 후 체력바 표시. onIntroFinished: 연출 종료 시 (전투 재개 타이밍)</summary>
        void Show(IBossSource boss, Action onIntroFinished = null);
        void Hide();
    }

    /// <summary>전체 화면/창 제어</summary>
    public interface IScreenApi
    {
        bool IsOpen(ScreenId id);
        void Open(ScreenId id);
        void Close(ScreenId id);
        void CloseAll();

        void ShowTitle(bool canContinue);
        void ShowClassSelect(IReadOnlyList<ClassData> classes);
        void ShowDeath();
        /// <summary>
        /// 스테이지 클리어 → 창 없이 "게이트 파괴" 띠를 잠깐 띄운 뒤 UIRequests.NextStage를 보낸다 (게임 안 멈춤).
        /// </summary>
        void ShowStageClear(int stageNumber, HunterRank newRank);
        void ShowDemoEnd();
    }

    /// <summary>
    /// 피해 숫자 + 타격 이펙트 + 적 머리 위 체력바.
    ///   GameUI.Damage.Show(hitPoint, 37, isCrit ? DamageKind.Critical : DamageKind.Normal);
    /// 위치는 월드 좌표 (UI가 화면 좌표로 바꿔 따라감). 옵션의 '피해 숫자 표시'가 꺼져 있으면 숫자만 생략.
    /// </summary>
    public interface IDamageFxApi
    {
        void Show(Vector3 worldPosition, float amount, DamageKind kind = DamageKind.Normal);
        /// <summary>숫자 대신 글자 (예: "MISS", "면역")</summary>
        void ShowText(Vector3 worldPosition, string text, DamageKind kind = DamageKind.Miss);
        /// <summary>타격 이펙트만 (숫자 없이)</summary>
        void Spark(Vector3 worldPosition, bool critical = false);
        void TrackEnemy(IEnemyHealthSource enemy);
        void UntrackEnemy(IEnemyHealthSource enemy);
    }

    /// <summary>짧은 알림 (아이템 획득, 게이트 봉쇄 등)</summary>
    public interface INotifyApi
    {
        /// <summary>오른쪽 아래 한 줄 알림 (아이템 획득·경고 등)</summary>
        void Toast(string message, ToastType type = ToastType.Info);
        /// <summary>
        /// 지도 바로 아래에서 내려오는 "SYSTEM" 알림 (지도와 같은 폭, 최대 3장 쌓임, 2.5초 뒤 지도 뒤로 올라감).
        ///   GameUI.Notify.System("랭크 승급", "F급 → E급", "새 스킬 해금");
        /// mergeKey가 같은 알림이 떠 있으면 새로 쌓지 않고 그 카드 내용만 바꾼다 (레벨업 연속 → "Lv.3 → Lv.6" 한 장).
        /// 레벨업·랭크 승급은 IProgressionSource가 연결돼 있으면 UI가 자동으로 띄운다.
        /// </summary>
        void System(string kind, string main, string sub = null, string mergeKey = null);
    }
}

namespace OZ.UI.Contracts
{
    /// <summary>미니맵 + 전체 지도</summary>
    public interface IMapApi
    {
        MapData Current { get; }
        /// <summary>스테이지 시작 시 지도 지정 (발견 상태 초기화)</summary>
        void SetMap(MapData map);
        /// <summary>플레이어가 들어간 방 (자동으로 발견 처리)</summary>
        void SetPlayerRoom(string roomId);
        void RevealRoom(string roomId);
        /// <summary>방 아이콘 변경 (예: 게이트 봉쇄 후 아이콘 제거)</summary>
        void SetRoomIcon(string roomId, MapRoomIcon icon);
    }

    /// <summary>대화창 + 초상화</summary>
    public interface IDialogueApi
    {
        bool IsPlaying { get; }
        void Play(DialogueData dialogue, Action onFinished = null);
        /// <summary>코드로 한 줄 띄우기 (데이터 에셋 없이)</summary>
        void Say(SpeakerData speaker, string text, DialogueSide side = DialogueSide.Right, Action onFinished = null);
        void Stop();
    }
}
