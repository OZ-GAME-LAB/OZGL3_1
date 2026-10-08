using System;
using UnityEngine;

namespace OZ.UI.Contracts
{
    /// <summary>UI 효과음 종류. 실제 소리는 UISoundSet 에셋(UI/Audio/UISoundSet_Default)에서 바꾼다.</summary>
    public enum UISound
    {
        None = 0,
        // 기본 조작
        Hover = 1, Click = 2, Confirm = 3, Back = 4, Tab = 5, Slider = 6, Error = 7,
        // 창
        Open = 10, Close = 11,
        // 알림·진행
        SystemAlarm = 20, RankUp = 21, QuestUpdate = 22, QuestComplete = 23, Toast = 24, ToastWarning = 25,
        // 흐름
        GateSealed = 30, StageIntro = 31, LoadDone = 32, BossAppear = 33, BossDefeat = 34, Death = 35,
        // 스킬 트리·인벤토리·HUD
        Unlock = 40, Reset = 41, ItemPick = 42, ItemPlace = 43, ItemUse = 44, SkillReady = 45, DialogueNext = 46,
    }

    /// <summary>
    /// UI 효과음 재생 (SFX 볼륨 옵션 적용, 같은 소리 연타 방지).
    ///   GameUI.Sound.Play(UISound.Confirm);
    /// 버튼·창·알림·퀘스트·게이트 띠 등 UI 쪽 소리는 자동으로 난다 → 팀원은 게임 쪽 특별한 순간에만 부르면 된다.
    /// </summary>
    public interface IUISoundApi
    {
        void Play(UISound sound, float volumeScale = 1f);
        bool Muted { get; set; }
    }
}
