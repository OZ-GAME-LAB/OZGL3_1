using System.Collections;
using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OZ.UI.Samples
{
    /// <summary>
    /// 지하철역 쇼케이스 시작 설정: 타이틀 없이 바로 전투 HUD.
    /// UI 크기·위치를 실제 게임 화면 느낌에서 보기 위한 씬이라, 시작하자마자 E·R까지 쓸 수 있게 레벨·노드를 미리 찍어 둔다.
    ///   F1 조작 안내 다시 보기 · F5 처음 상태로 · 사망 → 재도전 = 제자리 부활
    ///   SampleGameManager가 있으면(managedByGameManager) 흐름은 그쪽이 맡고, 여기선 플레이어 준비만 한다.
    /// </summary>
    [AddComponentMenu("OZ/UI/Samples/Showcase Director")]
    public class ShowcaseDirector : MonoBehaviour
    {
        [SerializeField] internal DummyPlayer player;
        [SerializeField] internal ShowcasePlayer avatar;
        [SerializeField] internal MapData map;
        [SerializeField] internal int startLevel = 6;
        [Tooltip("시작할 때 찍어 둘 스킬 트리 노드 (E 검술 · R 마법 — 두 계열 이펙트를 같이 보려고)")]
        [SerializeField] internal string[] startNodes = { "sword_e", "magic_r" };
        [TextArea] [SerializeField] internal string guide = "A/D 이동 · Space 점프 · Z 공격 · Q/E/R 스킬 · K 스킬트리 · I 인벤 · Tab 지도";
        [Tooltip("켜면 시작·재도전을 SampleGameManager가 처리 (게임 흐름 시연)")]
        [SerializeField] internal bool managedByGameManager;

        void OnEnable() { if (!managedByGameManager) UIRequests.Retry += OnRetry; }
        void OnDisable() => UIRequests.Retry -= OnRetry;

        IEnumerator Start()
        {
            yield return null; // UI 등록(OnEnable/Start) 이후
            if (!managedByGameManager) Setup();
        }

        void Setup()
        {
            GameUI.Screens.CloseAll();
            GameUI.HUD.Visible = true;
            if (map != null) { GameUI.Map.SetMap(map); if (map.rooms.Count > 0) GameUI.Map.SetPlayerRoom(map.rooms[0].id); }
            if (player != null)
            {
                player.ResetAll();
                while (player.Level < startLevel && player.ExpToNextLevel > 0f) player.LevelUpNow();
                foreach (var id in startNodes) player.TryUnlock(id);
            }
            if (avatar != null) avatar.Respawn();
            GameUI.HUD.ShowGuide(guide, 6f);
        }

        void OnRetry()
        {
            GameUI.Screens.CloseAll();
            RevivePlayer();
        }

        /// <summary>레벨·스킬 노드 세팅 + 시작 위치 (게임 시작 시)</summary>
        public void PreparePlayer()
        {
            if (player != null)
            {
                player.ResetAll();
                while (player.Level < startLevel && player.ExpToNextLevel > 0f) player.LevelUpNow();
                foreach (var id in startNodes) player.TryUnlock(id);
            }
            if (avatar != null) avatar.Respawn();
            if (map != null) { GameUI.Map.SetMap(map); if (map.rooms.Count > 0) GameUI.Map.SetPlayerRoom(map.rooms[0].id); }
        }

        public void ShowHelp(float seconds = 6f) => GameUI.HUD.ShowGuide(guide, seconds);

        /// <summary>체력 회복 + 시작 위치 (재도전·스테이지 교체)</summary>
        public void RevivePlayer()
        {
            if (player != null) player.ChangeHP(9999f);
            if (avatar != null) avatar.Respawn();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f1Key.wasPressedThisFrame) GameUI.HUD.ShowGuide(guide, 6f);
            if (kb.f5Key.wasPressedThisFrame && !managedByGameManager) Setup();
        }
    }
}
