using OZ.UI.Contracts;
using OZ.UI.Samples;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace OZ.UI.EditorTools
{
    /// <summary>
    /// Setup 5단계: UI_Sandbox 씬 생성 — 게임플레이 없이 더미 데이터로 모든 UI를 구동하는 실작동 테스트 씬.
    /// </summary>
    internal static class OZSandboxBuilder
    {
        public static bool IsDone => AssetDatabase.LoadAssetAtPath<SceneAsset>(OZPaths.SandboxScene) != null;

        public const string HelpText =
            "<b>UI SANDBOX</b>  (F1 숨기기)\n" +
            "<color=#FFD966>HUD</color> H 피격  J 회복  X 경험치\n" +
            "<color=#FFD966>사용</color> Q/E/R 스킬  1~4 아이템  U 회복제+1\n" +
            "<color=#FFD966>게이트</color> G 봉쇄  O 열기   P 다음 방\n" +
            "<color=#FFD966>보스</color> B 등장  N 피격  V 큰 피격\n" +
            "<color=#FFD966>창</color> K 스킬  I 인벤  Tab 지도  T 대화  ESC\n" +
            "<color=#FFD966>화면</color> F2 사망  F3 클리어  F4 끝  F5 타이틀";

        public static void Run()
        {
            if (!OZUIRootBuilder.IsDone) OZUIRootBuilder.Run();
            if (!OZSampleData.IsDone) OZSampleData.Run();
            UIB.LoadFonts();

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            OZFontBuilder.EnsureFolder(OZPaths.Scenes);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 카메라 (2.5D 게임 화면 자리)
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.2f, 0.28f);
            cam.orthographic = true;
            camGo.transform.position = new Vector3(0, 0, -10);

            // 바닥/배경 플레이스홀더 (게임 화면 느낌용)
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Placeholder_Ground";
            ground.transform.position = new Vector3(0, -3.5f, 0);
            ground.transform.localScale = new Vector3(30, 1, 1);

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            // UIRoot 프리팹
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OZUIRootBuilder.PrefabPath);
            var ui = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var mapCtrl = ui.GetComponent<MapController>();

            // 더미 플레이어 / 보스 / 흐름 연출
            var sandbox = new GameObject("Sandbox (Dummy Gameplay)");
            var player = sandbox.AddComponent<DummyPlayer>();
            player.classData = OZSampleData.Load<ClassData>("Class_Sword");
            player.quickItems = new[]
            {
                OZSampleData.Load<ItemData>("Item_Heal"), OZSampleData.Load<ItemData>("Item_Attack"),
                OZSampleData.Load<ItemData>("Item_Defense"), OZSampleData.Load<ItemData>("Item_Speed"),
            };
            player.extraItems = new[] { OZSampleData.Load<ItemData>("Item_GateKey") };

            var boss = sandbox.AddComponent<DummyBoss>();
            boss.data = OZSampleData.Load<BossData>("Boss_Stage1");

            var director = sandbox.AddComponent<SandboxDirector>();
            director.player = player;
            director.boss = boss;
            director.classes = new[] { OZSampleData.Load<ClassData>("Class_Sword"), OZSampleData.Load<ClassData>("Class_Magic") };
            director.introDialogue = OZSampleData.Load<DialogueData>("Dialogue_Intro");
            director.map = OZSampleData.Load<MapData>("Map_Stage1");

            if (mapCtrl != null) mapCtrl.startMap = null; // 지도는 Director가 지정

            BuildHelpOverlay();

            EditorSceneManager.SaveScene(scene, OZPaths.SandboxScene);
            Debug.Log("[OZ UI] UI_Sandbox 씬 생성 → " + OZPaths.SandboxScene + "  (Play 후 화면 왼쪽 아래 도움말 참고)");
        }

        static void BuildHelpOverlay()
        {
            var go = new GameObject("SandboxHelp", typeof(RectTransform));
            go.layer = 5;
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5; // HUD 위, 창 아래
            var scaler = go.AddComponent<PixelCanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            var bg = UIB.Solid("Background", go.transform, new Color(0, 0, 0, 0.55f));
            bg.rectTransform.Place(1, 1, -8, -84, 206, 90);
            var t = UIB.Small10("Text", bg.transform, HelpText, TextAlignmentOptions.TopLeft, Color.white);
            t.richText = true;
            t.rectTransform.Stretch(6, 6, 4, 4);
            go.AddComponent<SandboxHelpToggle>().target = bg.gameObject;
        }
    }
}
