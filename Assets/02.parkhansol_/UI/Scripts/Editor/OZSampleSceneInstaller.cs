using OZ.UI.Contracts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace OZ.UI.EditorTools
{
    /// <summary>
    /// OZ > UI > Install UI → SampleScene
    /// 팀 공용 씬(Assets/Scenes/SampleScene)에 아래 3개만 넣는다. 기존 오브젝트는 건드리지 않음. 여러 번 실행해도 중복 생성 안 함.
    ///   UIRoot            : 모든 UI (프리팹 연결 유지 → UI 쪽 수정은 프리팹 갱신만으로 반영)
    ///   EventSystem       : 없을 때만
    ///   UI Links (팀원 연결용) : GateUILink · PlayerUILink · BossUILink · UIRequestEvents + 안내 메모
    /// </summary>
    internal static class OZSampleSceneInstaller
    {
        public const string ScenePath = "Assets/Scenes/SampleScene.unity";
        const string LinksName = "UI Links (팀원 연결용)";

        internal const string Note =
@"[UI 연결 방법 — 박한솔(UI)]  자세한 설명: Assets/02.parkhansol_/Docs/UI_Integration.md

이 오브젝트의 컴포넌트를 매니저에 드래그해서 메서드만 부르면 UI가 따라옵니다.

GameManager
  InitializeAsync : GameUI.Flow.ShowLoading(""..."") → SetLoadingProgress(0~1) → HideLoading()
  RestartStage / ChangeStage : GameUI.Flow.Transition(() => { 검은 화면 동안 교체 })
  UI 버튼 → 게임 : 아래 UIRequestEvents 인스펙터에 RestartStage 등 연결 (재도전/다음 스테이지/새 게임)

StageManager
  StartStage     : GameUI.Flow.StageIntro(번호, ""스테이지 이름"")
  StartBossphase : BossUILink.Begin(maxHP, () => 보스AI시작)  / 맞을 때 SetHP(hp)
  ClearStage     : GameUI.Screens.ShowStageClear(번호, 랭크)  (마지막이면 ShowDemoEnd)

GateManager
  StartGatePhase   : GateUILink.BeginStage(stage, 목표수)
  ActivateNextGate : GateUILink.GateOpened()
  게이트 봉쇄       : GateUILink.GateSealed()   → '게이트 파괴' 띠 자동

적 프리팹 (SpawnManager/PoolManager)
  EnemyUILink 컴포넌트 추가 → 스폰 때 Init(maxHP), 맞을 때 Hit(피해, 치명타여부)
  풀에서 꺼내고 넣는 건 자동 처리

플레이어
  PlayerUILink.SetHP(hp, max) / SetExp / SetLevel / SetRank
  입력 처리 앞에: if (UIState.IsGameplayInputBlocked) return;

※ 매니저가 IGateSource / IHealthSource 등을 직접 구현했다면 해당 Link 컴포넌트는 지우고 GameUI.Bind(this).";

        [MenuItem("OZ/UI/Install UI → SampleScene", priority = 7)]
        public static void Run()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) { Debug.LogError("[OZ UI] " + ScenePath + " 이 없습니다."); return; }
            if (!OZUIRootBuilder.IsDone) OZUIRootBuilder.Run();
            OZSampleData.Run();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject ui = null, links = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponent<UIManager>() != null) ui = root;
                if (root.name == LinksName) links = root;
            }

            if (ui == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OZUIRootBuilder.PrefabPath);
                ui = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                ui.name = "UIRoot";
            }
            var mapCtrl = ui.GetComponent<MapController>();
            if (mapCtrl != null) mapCtrl.startMap = null; // 지도는 레벨 담당이 GameUI.Map.SetMap으로 지정

            if (Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            if (links == null) links = new GameObject(LinksName);
            Ensure<UINote>(links).note = Note;
            Ensure<GateUILink>(links);
            Ensure<PlayerUILink>(links);
            var boss = Ensure<BossUILink>(links);
            if (boss.data == null) boss.data = OZSampleData.Load<BossData>("Boss_Stage1");
            Ensure<UIRequestEvents>(links);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = links;
            Debug.Log("[OZ UI] SampleScene UI 세팅 완료 → " + ScenePath + "  (연결 안내: '" + LinksName + "' 오브젝트)", links);
        }

        static T Ensure<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }
    }
}
