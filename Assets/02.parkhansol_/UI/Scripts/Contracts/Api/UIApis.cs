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
        void ShowStageClear(int stageNumber, HunterRank newRank);
        void ShowDemoEnd();
    }

    /// <summary>짧은 알림 (아이템 획득, 게이트 봉쇄 등)</summary>
    public interface INotifyApi
    {
        void Toast(string message, ToastType type = ToastType.Info);
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
