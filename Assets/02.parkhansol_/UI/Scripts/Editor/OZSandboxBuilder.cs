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
            "<color=#FFD966>HUD</color> H 피격  J 회복  X 경험치  = 레벨업\n" +
            "<color=#FFD966>사용</color> Q/E/R 스킬  1~4 아이템  U 회복제+1\n" +
            "<color=#FFD966>게이트</color> G 봉쇄  O 열기   P 다음 방\n" +
            "<color=#FFD966>보스</color> B 등장  N 피격  V 큰 피격\n" +
            "<color=#FFD966>창</color> K 스킬트리  I 인벤  Tab 지도  T 대화  ESC\n" +
            "<color=#FFD966>화면</color> F2 사망  F3 게이트파괴  F4 끝  F5 타이틀\n" +
            "<color=#FFD966>전투</color> Z 공격 C 치명 W 약점 L 연타\n" +
            "       D 장판(지속) Y 빗나감  적 클릭";

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
            player.skillTree = OZSampleData.Load<SkillTreeData>("SkillTree_Main");
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

            // 가상의 플레이어 모습 + 적 3마리 (일반 2 + 엘리트 1) — 바닥 윗면 y = -3
            var avatar = Actor("Player (Avatar)", UIB.Px("Portraits/PlayerLarge"), new Vector2(-5.5f, -3f), 4f, false, out var head);
            var flashMat = FlashMaterial();
            AddFlash(avatar, flashMat);
            var combat = sandbox.AddComponent<SandboxCombat>();
            combat.feel = sandbox.AddComponent<SandboxFeel>();
            combat.player = player;
            combat.playerAvatar = avatar.transform;
            combat.playerHead = head;
            var enemySprite = UIB.Px("Portraits/EnemyLarge");
            var specs = new (string name, float x, float scale, float hp, bool elite)[]
            {
                ("Enemy_A", 0.5f, 4f, 120f, false), ("Enemy_B", 2.5f, 4f, 120f, false), ("Enemy_Elite", 5.2f, 6f, 400f, true),
            };
            foreach (var sp in specs)
            {
                var go = Actor(sp.name, enemySprite, new Vector2(sp.x, -3f), sp.scale, true, out var anchor);
                var e = go.AddComponent<DummyEnemy>();
                e.body = go.GetComponent<SpriteRenderer>();
                e.barAnchor = anchor;
                e.flash = AddFlash(go, flashMat);
                e.maxHP = sp.hp;
                e.elite = sp.elite;
                combat.enemies.Add(e);
            }

            BuildHelpOverlay();

            EditorSceneManager.SaveScene(scene, OZPaths.SandboxScene);
            Debug.Log("[OZ UI] UI_Sandbox 씬 생성 → " + OZPaths.SandboxScene + "  (Play 후 화면 왼쪽 아래 도움말 참고)");
        }

        /// <summary>피격 흰색 번쩍임용 머티리얼 (OZ/Sprite Flash 셰이더)</summary>
        static Material FlashMaterial()
        {
            string path = OZPaths.UI + "/Art/FX/OZ_SpriteFlash.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            var shader = Shader.Find("OZ/Sprite Flash");
            if (shader == null) { Debug.LogWarning("[OZ UI] OZ/Sprite Flash 셰이더를 찾지 못해 번쩍임 없이 진행합니다."); return null; }
            mat = new Material(shader) { name = "OZ_SpriteFlash" };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static HitFlash AddFlash(GameObject go, Material mat)
        {
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr != null && mat != null) sr.sharedMaterial = mat;
            return go.AddComponent<HitFlash>();
        }

        /// <summary>발이 바닥(footY)에 닿게 놓은 스프라이트 + 머리 위 기준점</summary>
        static GameObject Actor(string name, Sprite sprite, Vector2 foot, float scale, bool faceLeft, out Transform head)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.flipX = faceLeft;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            float h = sprite != null ? sprite.bounds.size.y : 0.35f;
            go.transform.position = new Vector3(foot.x, foot.y + h * scale * 0.5f, 0f);
            var anchor = new GameObject("Head").transform;
            anchor.SetParent(go.transform, false);
            anchor.localPosition = new Vector3(0f, h * 0.5f + 0.02f, 0f);
            head = anchor;
            return go;
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
            bg.rectTransform.Place(1, 1, -8, -84, 206, 112);
            var t = UIB.Small10("Text", bg.transform, HelpText, TextAlignmentOptions.TopLeft, Color.white);
            t.richText = true;
            t.rectTransform.Stretch(6, 6, 4, 4);
            go.AddComponent<SandboxHelpToggle>().target = bg.gameObject;
        }
    }
}
