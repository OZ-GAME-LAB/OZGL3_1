using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OZ.UI.Samples
{
    /// <summary>
    /// UI_Sandbox 흐름 연출 = "코어 담당이 할 일"의 참고 구현.
    ///   타이틀 → 새 게임 → 계열 선택 → 대화 → 전투 HUD → (사망/재도전, 보스, 클리어, 데모 종료)
    ///
    /// 디버그 키
    ///   T 대화 / P 다음 방(지도) / F2 사망 / F3 스테이지 클리어(게이트 파괴 띠) / F4 데모 종료 / F5 타이틀
    /// </summary>
    [AddComponentMenu("OZ/UI/Samples/Sandbox Director")]
    public class SandboxDirector : MonoBehaviour
    {
        [SerializeField] internal DummyPlayer player;
        [SerializeField] internal DummyBoss boss;
        [SerializeField] internal ClassData[] classes;
        [SerializeField] internal DialogueData introDialogue;
        [SerializeField] internal MapData map;
        [SerializeField] internal bool startAtTitle = true;

        int _roomIndex;
        int _stage = 1;

        void OnEnable()
        {
            UIRequests.NewGame += OnNewGame;
            UIRequests.Continue += OnNewGame;
            UIRequests.ClassSelected += OnClassSelected;
            UIRequests.Retry += OnRetry;
            UIRequests.NextStage += OnNextStage;
            UIRequests.ReturnToTitle += ShowTitle;
        }

        void OnDisable()
        {
            UIRequests.NewGame -= OnNewGame;
            UIRequests.Continue -= OnNewGame;
            UIRequests.ClassSelected -= OnClassSelected;
            UIRequests.Retry -= OnRetry;
            UIRequests.NextStage -= OnNextStage;
            UIRequests.ReturnToTitle -= ShowTitle;
        }

        void Start()
        {
            if (map != null)
            {
                GameUI.Map.SetMap(map);
                EnterRoom(0);
            }
            if (startAtTitle) ShowTitle();
        }

        void ShowTitle()
        {
            GameUI.Boss.Hide();
            GameUI.HUD.Visible = false;
            GameUI.Screens.ShowTitle(canContinue: false);
        }

        void OnNewGame() => GameUI.Screens.ShowClassSelect(classes);

        void OnClassSelected(PlayerClass cls)
        {
            ClassData data = null;
            if (classes != null) foreach (var c in classes) if (c != null && c.playerClass == cls) data = c;
            if (player != null) player.SetClass(data);
            GameUI.HUD.Visible = true;
            if (map != null) { GameUI.Map.SetMap(map); EnterRoom(0); }
            if (introDialogue != null)
                GameUI.Dialogue.Play(introDialogue, () => GameUI.HUD.ShowGuide("게이트를 찾아 봉쇄하라"));
            else
                GameUI.HUD.ShowGuide("게이트를 찾아 봉쇄하라");
        }

        void OnRetry()
        {
            if (player != null) player.ResetAll();
            GameUI.Boss.Hide();
            if (map != null) { GameUI.Map.SetMap(map); EnterRoom(0); }
        }

        void OnNextStage()
        {
            _stage++;
            GameUI.Boss.Hide();
            if (_stage > 2) { GameUI.Screens.ShowDemoEnd(); return; }
            GameUI.HUD.ShowGuide($"STAGE {_stage} — 지하철 · 지하 구역", 2.5f);
        }

        void EnterRoom(int index)
        {
            if (map == null || map.rooms.Count == 0) return;
            _roomIndex = (index % map.rooms.Count + map.rooms.Count) % map.rooms.Count;
            GameUI.Map.SetPlayerRoom(map.rooms[_roomIndex].id);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f5Key.wasPressedThisFrame) { GameUI.Screens.CloseAll(); ShowTitle(); }
            if (UIState.IsGameplayInputBlocked) return;

            if (kb.tKey.wasPressedThisFrame && introDialogue != null) GameUI.Dialogue.Play(introDialogue);
            if (kb.pKey.wasPressedThisFrame) EnterRoom(_roomIndex + 1);
            if (kb.f2Key.wasPressedThisFrame) GameUI.Screens.ShowDeath();
            if (kb.f3Key.wasPressedThisFrame)
            {
                if (player != null) player.PromoteRank();
                GameUI.Screens.ShowStageClear(_stage, player != null ? player.Rank : HunterRank.E);
            }
            if (kb.f4Key.wasPressedThisFrame) GameUI.Screens.ShowDemoEnd();
        }
    }
}
