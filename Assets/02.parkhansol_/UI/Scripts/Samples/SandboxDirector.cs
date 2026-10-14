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
    ///   v0.7: F6 지역 이름 / F7 상호작용 안내(보스 머리 위) / F8 화면 밖 화살표(멀리 있는 게이트·보스) / 저체력 경고는 H 연타
    ///   v0.9: 계열 선택·재도전·P 다음 방·타이틀로 → 화면이 잠깐 어두워졌다 밝아짐 (GameUI.Flow.Transition)
    ///         플레이어 표시(UIPlayerTag)는 화면에 보이는 임시 캐릭터(Player (Avatar))에 붙음 → 플레이어 옆 게이지가 캐릭터를 따라감
    /// </summary>
    [AddComponentMenu("OZ/UI/Samples/Sandbox Director")]
    public class SandboxDirector : MonoBehaviour
    {
        [SerializeField] internal DummyPlayer player;
        [Tooltip("화면에 보이는 임시 캐릭터. 비우면 씬에서 'Player (Avatar)'를 찾고, 없으면 player 오브젝트를 씀")]
        [SerializeField] internal Transform avatar;
        [SerializeField] internal DummyBoss boss;
        [SerializeField] internal ClassData[] classes;
        [SerializeField] internal DialogueData introDialogue;
        [SerializeField] internal MapData map;
        [SerializeField] internal bool startAtTitle = true;

        [Header("v0.9 화면 전환")]
        [Tooltip("맵이 바뀔 때(계열 선택·재도전·다음 방·타이틀로) 화면을 잠깐 어둡게")]
        [SerializeField] internal bool fadeOnMapChange = true;
        [Tooltip("어두워지는 시간 = 밝아지는 시간 (초)")]
        [SerializeField, Min(0f)] internal float fadeDuration = 0.35f;

        int _roomIndex;
        int _stage = 1;

        // v0.7 디버그
        static readonly string[] AreaNames = { "서울 거리", "지하철역 승강장", "지하 구역" };
        int _areaIndex;
        Transform _farGate, _farBoss;

        void OnEnable()
        {
            UIRequests.NewGame += OnNewGame;
            UIRequests.Continue += OnNewGame;
            UIRequests.ClassSelected += OnClassSelected;
            UIRequests.Retry += OnRetry;
            UIRequests.NextStage += OnNextStage;
            UIRequests.ReturnToTitle += OnReturnToTitle;
        }

        void OnDisable()
        {
            UIRequests.NewGame -= OnNewGame;
            UIRequests.Continue -= OnNewGame;
            UIRequests.ClassSelected -= OnClassSelected;
            UIRequests.Retry -= OnRetry;
            UIRequests.NextStage -= OnNextStage;
            UIRequests.ReturnToTitle -= OnReturnToTitle;
        }

        void Start()
        {
            TagPlayerAvatar(); // v0.9 보이는 캐릭터 = 플레이어 (거리·안내·플레이어 옆 게이지 기준)
            GameUI.Prompt.Pressed += OnPromptPressed; // v0.7 F 누름 확인용
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

        void OnReturnToTitle() => MapChange(ShowTitle);

        /// <summary>v0.9: 맵이 바뀌는 처리를 화면이 어두운 동안 실행. 끄면 바로 실행</summary>
        void MapChange(System.Action whileBlack, System.Action onFinished = null)
        {
            if (!fadeOnMapChange || fadeDuration <= 0f)
            {
                whileBlack?.Invoke();
                onFinished?.Invoke();
                return;
            }
            GameUI.Flow.Transition(whileBlack, fadeDuration, onFinished);
        }

        void TagPlayerAvatar()
        {
            if (avatar == null)
            {
                var go = GameObject.Find("Player (Avatar)");
                if (go != null) avatar = go.transform;
            }
            Transform target = avatar != null ? avatar : (player != null ? player.transform : null);
            if (target == null) return;
            // 예전처럼 player(로직 오브젝트)에 붙어 있던 표시는 떼어 냄 → 게이지가 화면 가운데에 뜨지 않게
            if (player != null && player.transform != target)
            {
                var old = player.GetComponent<UIPlayerTag>();
                if (old != null) Destroy(old);
            }
            var tag = target.GetComponent<UIPlayerTag>();
            if (tag == null) target.gameObject.AddComponent<UIPlayerTag>();
            else if (UIPlayerTag.Current != target) { tag.enabled = false; tag.enabled = true; } // Current 다시 지정
        }

        void OnNewGame() => GameUI.Screens.ShowClassSelect(classes);

        void OnClassSelected(PlayerClass cls)
        {
            ClassData data = null;
            if (classes != null) foreach (var c in classes) if (c != null && c.playerClass == cls) data = c;
            MapChange(() =>
            {
                if (player != null) player.SetClass(data);
                GameUI.HUD.Visible = true;
                if (map != null) { GameUI.Map.SetMap(map); EnterRoom(0); }
            },
            () =>
            {
                // 화면이 다 밝아진 뒤 대화 시작
                if (introDialogue != null)
                    GameUI.Dialogue.Play(introDialogue, () => GameUI.HUD.ShowGuide("게이트를 찾아 봉쇄하라"));
                else
                    GameUI.HUD.ShowGuide("게이트를 찾아 봉쇄하라");
            });
        }

        void OnRetry()
        {
            MapChange(() =>
            {
                if (player != null) player.ResetAll();
                GameUI.Boss.Hide();
                if (map != null) { GameUI.Map.SetMap(map); EnterRoom(0); }
            });
        }

        void OnNextStage()
        {
            _stage++;
            if (_stage > 2) { GameUI.Boss.Hide(); GameUI.Screens.ShowDemoEnd(); return; }
            MapChange(() => GameUI.Boss.Hide(),
                      () => GameUI.HUD.ShowGuide($"STAGE {_stage} — 지하철 · 지하 구역", 2.5f));
        }

        void EnterRoom(int index)
        {
            if (map == null || map.rooms.Count == 0) return;
            _roomIndex = (index % map.rooms.Count + map.rooms.Count) % map.rooms.Count;
            GameUI.Map.SetPlayerRoom(map.rooms[_roomIndex].id);
        }

        /// <summary>P 키와 같음: 화면이 어두운 동안 다음 방으로 (시연 녹화에서도 사용)</summary>
        internal void GoNextRoom()
        {
            int next = _roomIndex + 1;
            MapChange(() => EnterRoom(next));
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f5Key.wasPressedThisFrame) { GameUI.Screens.CloseAll(); ShowTitle(); }
            if (UIState.IsGameplayInputBlocked) return;

            if (kb.tKey.wasPressedThisFrame && introDialogue != null) GameUI.Dialogue.Play(introDialogue);
            if (kb.pKey.wasPressedThisFrame) GoNextRoom();
            if (kb.f2Key.wasPressedThisFrame) GameUI.Screens.ShowDeath();
            if (kb.f3Key.wasPressedThisFrame)
            {
                if (player != null) player.PromoteRank();
                GameUI.Screens.ShowStageClear(_stage, player != null ? player.Rank : HunterRank.E);
            }
            if (kb.f4Key.wasPressedThisFrame) GameUI.Screens.ShowDemoEnd();

            // v0.7
            if (kb.f6Key.wasPressedThisFrame) GameUI.Area.Show(AreaNames[_areaIndex++ % AreaNames.Length], $"STAGE {_stage}");
            if (kb.f7Key.wasPressedThisFrame && boss != null)
            {
                if (GameUI.Prompt.Current == boss.transform) GameUI.Prompt.Hide(boss.transform);
                else GameUI.Prompt.Show(boss.transform, "조사");
            }
            if (kb.f8Key.wasPressedThisFrame) ToggleFarTargets();
        }

        void OnPromptPressed(Transform target) => GameUI.Notify.Toast("수상한 흔적을 조사했다");

        void OnDestroy() => GameUI.Prompt.Pressed -= OnPromptPressed;

        void ToggleFarTargets()
        {
            Vector3 origin = player != null ? player.transform.position : Vector3.zero;
            if (_farGate == null) _farGate = new GameObject("FarGate (테스트)").transform;
            if (_farBoss == null) _farBoss = new GameObject("FarBoss (테스트)").transform;
            _farGate.position = origin + new Vector3(-40f, 3f, 0f);
            _farBoss.position = origin + new Vector3(55f, -2f, 0f);
            if (GameUI.Indicators.IsTracking(_farGate))
            {
                GameUI.Indicators.Untrack(_farGate);
                GameUI.Indicators.Untrack(_farBoss);
            }
            else
            {
                GameUI.Indicators.Track(_farGate, IndicatorKind.Gate);
                GameUI.Indicators.Track(_farBoss, IndicatorKind.Boss, "보스");
            }
        }
    }
}
