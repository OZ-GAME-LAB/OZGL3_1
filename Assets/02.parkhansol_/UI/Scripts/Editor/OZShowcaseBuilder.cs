using System.Collections.Generic;
using OZ.UI.Contracts;
using OZ.UI.Samples;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;

namespace OZ.UI.EditorTools
{
    /// <summary>
    /// OZ > UI > Build Subway Showcase : 로우폴리 지하철역 임시 맵 + 블록 인형 플레이어·적.
    /// 목적은 UI 크기·위치가 2.5D 게임 화면과 어울리는지 보는 것 (맵·캐릭터는 진짜 에셋이 나오면 버림).
    /// 모든 결과물은 02.parkhansol_ 안 (씬 UI/Scenes, 머티리얼 UI/Art/Showcase).
    /// </summary>
    internal static class OZShowcaseBuilder
    {
        public const string ScenePath = OZPaths.Scenes + "/UI_Showcase_Subway.unity";
        const string MatDir = OZPaths.UI + "/Art/Showcase";

        [MenuItem("OZ/UI/Build Subway Showcase", priority = 5)]
        public static void Run()
        {
            if (!OZUIRootBuilder.IsDone) OZUIRootBuilder.Run();
            OZSampleData.Run();
            UIB.LoadFonts();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            OZFontBuilder.EnsureFolder(MatDir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var M = new Mats();

            // ── 조명·분위기 ──
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.33f, 0.38f, 0.48f);
            RenderSettings.ambientEquatorColor = new Color(0.2f, 0.22f, 0.28f);
            RenderSettings.ambientGroundColor = new Color(0.08f, 0.08f, 0.1f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.06f, 0.07f, 0.1f);
            RenderSettings.fogDensity = 0.018f;

            var sun = new GameObject("Directional Light", typeof(Light)).GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 0.55f;
            sun.color = new Color(0.8f, 0.88f, 1f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(55f, -25f, 0f);
            for (int i = -2; i <= 2; i++)
            {
                var pl = new GameObject("CeilingLight_" + (i + 2), typeof(Light)).GetComponent<Light>();
                pl.type = LightType.Point;
                pl.range = 13f;
                pl.intensity = 4.5f;
                pl.color = new Color(1f, 0.95f, 0.85f);
                pl.transform.position = new Vector3(i * 11f, 5.2f, 0.5f);
            }

            // ── 지하철역 블록아웃 (y=0 승강장 윗면, 플레이어는 z=0 평면) ──
            var env = new GameObject("Subway_Station").transform;
            Box("Platform", env, new Vector3(0, -0.5f, 0.25f), new Vector3(60, 1, 5), M.concrete, true);
            Box("PlatformEdge", env, new Vector3(0, -0.06f, -2.28f), new Vector3(60, 0.12f, 0.08f), M.trim, false);
            Box("TactileStrip", env, new Vector3(0, 0.012f, 2.4f), new Vector3(60, 0.025f, 0.45f), M.yellow, false);
            for (int i = -14; i <= 14; i++) // 바닥 타일 줄눈
                Box("FloorSeam", env, new Vector3(i * 2f, 0.006f, 0.25f), new Vector3(0.04f, 0.012f, 4.9f), M.seam, false);

            // 선로 (승강장 뒤, 한 단 아래)
            Box("TrackBed", env, new Vector3(0, -1.45f, 4.6f), new Vector3(60, 0.1f, 3.7f), M.gravel, false);
            Box("Rail_A", env, new Vector3(0, -1.32f, 3.95f), new Vector3(60, 0.12f, 0.1f), M.metal, false);
            Box("Rail_B", env, new Vector3(0, -1.32f, 5.15f), new Vector3(60, 0.12f, 0.1f), M.metal, false);
            for (int i = -29; i <= 29; i++)
                Box("Sleeper", env, new Vector3(i, -1.38f, 4.55f), new Vector3(0.25f, 0.06f, 1.9f), M.trim, false);

            // 뒷벽 (타일) + 노선 띠 + 광고판
            Box("BackWall", env, new Vector3(0, 2.4f, 6.6f), new Vector3(60, 8, 0.2f), M.tile, false);
            Box("LineStripe", env, new Vector3(0, 2.3f, 6.48f), new Vector3(60, 0.35f, 0.04f), M.lineGreen, false);
            float[] ads = { -21f, -9f, 3f, 15f };
            Material[] adMats = { M.adBlue, M.adOrange, M.adPurple, M.adBlue };
            for (int i = 0; i < ads.Length; i++)
            {
                Box("AdFrame", env, new Vector3(ads[i], 3.4f, 6.47f), new Vector3(4.2f, 2.2f, 0.06f), M.trim, false);
                Box("AdPanel", env, new Vector3(ads[i], 3.4f, 6.43f), new Vector3(3.9f, 1.9f, 0.04f), adMats[i], false);
            }

            // 천장 + 조명 띠
            Box("Ceiling", env, new Vector3(0, 6.3f, 2.2f), new Vector3(60, 0.3f, 9.5f), M.ceiling, false);
            Box("LightStrip_A", env, new Vector3(0, 6.1f, 0f), new Vector3(56, 0.08f, 0.22f), M.lamp, false);
            Box("LightStrip_B", env, new Vector3(0, 6.1f, 3.2f), new Vector3(56, 0.08f, 0.22f), M.lamp, false);

            // 기둥 (승강장 뒤쪽 가장자리, 플레이어 뒤)
            for (int i = -3; i <= 3; i++)
            {
                var col = Prim(PrimitiveType.Cylinder, "Pillar", env, new Vector3(i * 8f, 3f, 1.9f), new Vector3(0.7f, 3f, 0.7f), M.pillar, false);
                col.GetComponent<MeshFilter>().sharedMesh = LowPolyCylinder();
                Box("PillarBand", env, new Vector3(i * 8f, 2.2f, 1.9f), new Vector3(0.78f, 0.3f, 0.78f), M.lineGreen, false);
            }

            // 역 이름판
            var sign = Box("StationSign", env, new Vector3(0, 4.5f, 2.0f), new Vector3(5.2f, 0.85f, 0.14f), M.signDark, false);
            Box("SignLine", env, new Vector3(-2.15f, 4.5f, 1.92f), new Vector3(0.5f, 0.5f, 0.04f), M.lineGreen, false);
            Text3D("SignText", sign.transform.parent, "오즈역  OZ Station", new Vector3(0.25f, 4.5f, 1.91f), 0.42f);
            Box("SignHanger_L", env, new Vector3(-2f, 5.45f, 2.0f), new Vector3(0.05f, 1.1f, 0.05f), M.metal, false);
            Box("SignHanger_R", env, new Vector3(2f, 5.45f, 2.0f), new Vector3(0.05f, 1.1f, 0.05f), M.metal, false);

            // 정차 중인 열차 (뒤 배경)
            var train = new GameObject("Train").transform;
            train.SetParent(env, false);
            Box("TrainBody", train, new Vector3(-17f, 0.35f, 4.55f), new Vector3(22f, 3.1f, 2.8f), M.train, false);
            Box("TrainWindows", train, new Vector3(-17f, 0.95f, 3.13f), new Vector3(21f, 0.85f, 0.04f), M.window, false);
            Box("TrainStripe", train, new Vector3(-17f, -0.25f, 3.13f), new Vector3(22f, 0.22f, 0.04f), M.lineGreen, false);
            for (int i = 0; i < 4; i++)
                Box("TrainDoor", train, new Vector3(-25f + i * 5.5f, 0.2f, 3.12f), new Vector3(1.3f, 2.4f, 0.05f), M.trainDoor, false);
            Box("TrainNose", train, new Vector3(-5.6f, 0.35f, 4.55f), new Vector3(0.8f, 2.8f, 2.6f), M.trainDoor, false);

            // 양 끝 벽
            Box("EndWall_L", env, new Vector3(-30.25f, 3f, 2f), new Vector3(0.5f, 8f, 10f), M.tile, true);
            Box("EndWall_R", env, new Vector3(30.25f, 3f, 2f), new Vector3(0.5f, 8f, 10f), M.tile, true);

            // 점프 테스트용: 계단 → 위층, 자판기, 벤치, 상자, 철골 발판
            for (int i = 0; i < 6; i++)
            {
                float h = (i + 1) * 0.4f;
                Box("Step_" + i, env, new Vector3(12.4f + i * 0.8f, h * 0.5f, 0.25f), new Vector3(0.8f, h, 5f), M.concreteLight, false);
                Box("StepNose_" + i, env, new Vector3(12.05f + i * 0.8f, h + 0.01f, -1.2f), new Vector3(0.12f, 0.02f, 2.4f), M.yellow, false);
            }
            // 계단은 보이기만 하고, 밟는 건 보이지 않는 경사판 (리지드바디가 턱에 걸리지 않게)
            var ramp = Box("StairRamp (Collider)", env, new Vector3(14.445f, 1.11f, 0.25f), new Vector3(5.37f, 0.2f, 5f), M.concrete, true);
            ramp.transform.localRotation = Quaternion.Euler(0, 0, 26.565f);
            Object.DestroyImmediate(ramp.GetComponent<MeshRenderer>());
            Box("UpperLanding", env, new Vector3(23.3f, 1.2f, 0.25f), new Vector3(13f, 2.4f, 5f), M.concreteLight, true);
            Box("Railing", env, new Vector3(23.3f, 3.0f, -1.9f), new Vector3(13f, 0.06f, 0.06f), M.metal, false);
            for (int i = 0; i < 7; i++) Box("RailPost", env, new Vector3(17.3f + i * 2f, 2.7f, -1.9f), new Vector3(0.06f, 0.6f, 0.06f), M.metal, false);

            var vend = Box("VendingMachine", env, new Vector3(-9f, 0.95f, 0.2f), new Vector3(1.2f, 1.9f, 1.0f), M.adOrange, true);
            Box("VendingGlass", env, new Vector3(-9f, 1.15f, -0.31f), new Vector3(0.9f, 1.1f, 0.03f), M.window, false);
            Box("Bench", env, new Vector3(-14f, 0.25f, 0.3f), new Vector3(2.4f, 0.5f, 0.7f), M.bench, true);
            Box("Crate_A", env, new Vector3(5.5f, 0.45f, 0f), new Vector3(0.9f, 0.9f, 0.9f), M.crate, true);
            Box("Crate_B", env, new Vector3(6.4f, 0.45f, 0f), new Vector3(0.9f, 0.9f, 0.9f), M.crate, true);
            Box("Crate_C", env, new Vector3(5.95f, 1.35f, 0f), new Vector3(0.9f, 0.9f, 0.9f), M.crate, true);
            Box("Girder", env, new Vector3(-2.5f, 2.6f, 0f), new Vector3(3.5f, 0.3f, 1.0f), M.metal, true);

            // ── 플레이어 ──
            var playerGo = new GameObject("Player");
            playerGo.transform.position = new Vector3(-4f, 0.05f, 0f);
            var capsule = playerGo.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0, 0.9f, 0); capsule.height = 1.8f; capsule.radius = 0.32f;
            playerGo.AddComponent<Rigidbody>();
            var data = playerGo.AddComponent<DummyPlayer>();
            data.classData = OZSampleData.Load<ClassData>("Class_Sword");
            data.skillTree = OZSampleData.Load<SkillTreeData>("SkillTree_Main");
            data.quickItems = new[]
            {
                OZSampleData.Load<ItemData>("Item_Heal"), OZSampleData.Load<ItemData>("Item_Attack"),
                OZSampleData.Load<ItemData>("Item_Defense"), OZSampleData.Load<ItemData>("Item_Speed"),
            };
            data.extraItems = new[] { OZSampleData.Load<ItemData>("Item_GateKey") };
            var avatar = playerGo.AddComponent<ShowcasePlayer>();
            avatar.data = data;
            BuildPlayerModel(playerGo.transform, avatar, M);

            // ── 적 (일반 3 + 엘리트 1) ──
            var specs = new (string name, Vector3 pos, float scale, float hp, bool elite, float range)[]
            {
                ("Enemy_A", new Vector3(2f, 0f, 0f), 1f, 120f, false, 2.2f),
                ("Enemy_B", new Vector3(9f, 0f, 0f), 1f, 120f, false, 1.6f),
                ("Enemy_C", new Vector3(22f, 2.4f, 0f), 1f, 120f, false, 3f),
                ("Enemy_Elite", new Vector3(-19f, 0f, 0f), 1.4f, 400f, true, 2.5f),
            };
            foreach (var sp in specs) BuildEnemy(sp.name, sp.pos, sp.scale, sp.hp, sp.elite, sp.range, M);

            // ── 카메라 ──
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = false;
            cam.fieldOfView = 38f;
            cam.nearClipPlane = 0.3f; cam.farClipPlane = 80f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            camGo.transform.position = new Vector3(-2.5f, 2.2f, -13f);
            var follow = camGo.AddComponent<ShowcaseCamera>();
            follow.target = playerGo.transform;
            avatar.cam = follow;

            // ── UI + 연출 ──
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OZUIRootBuilder.PrefabPath);
            var ui = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var mapCtrl = ui.GetComponent<MapController>();
            if (mapCtrl != null) mapCtrl.startMap = null;

            var dirGo = new GameObject("Showcase (Director)");
            var feel = dirGo.AddComponent<SandboxFeel>();
            feel.cam = cam;
            avatar.feel = feel;
            var fx = dirGo.AddComponent<ShowcaseFx>();
            fx.swordMat = M.fxSword; fx.magicMat = M.fxMagic; fx.hitMat = M.fxHit;
            var boss = dirGo.AddComponent<DummyBoss>();
            boss.data = OZSampleData.Load<BossData>("Boss_Stage1");
            var dir = dirGo.AddComponent<ShowcaseDirector>();
            dir.player = data;
            dir.avatar = avatar;
            dir.map = OZSampleData.Load<MapData>("Map_Stage1");

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[OZ UI] 지하철역 쇼케이스 씬 생성 → " + ScenePath + "  (Play: A/D 이동 · Space 점프 · Z 공격 · Q/E/R 스킬)");
        }

        [MenuItem("OZ/UI/Open Subway Showcase", priority = 6)]
        static void Open()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) { Run(); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        // ───────────────────────────── 캐릭터 ─────────────────────────────

        static void BuildPlayerModel(Transform root, ShowcasePlayer p, Mats M)
        {
            var model = new GameObject("Model").transform;
            model.SetParent(root, false);
            model.localRotation = Quaternion.Euler(0, 90, 0);
            p.model = model;
            var rs = new List<Renderer>();

            rs.Add(Box("Torso", model, new Vector3(0, 1.08f, 0), new Vector3(0.56f, 0.68f, 0.34f), M.playerCoat, false).GetComponent<Renderer>());
            rs.Add(Box("Belt", model, new Vector3(0, 0.76f, 0), new Vector3(0.58f, 0.08f, 0.36f), M.trim, false).GetComponent<Renderer>());
            rs.Add(Box("Head", model, new Vector3(0, 1.66f, 0), new Vector3(0.44f, 0.44f, 0.42f), M.skin, false).GetComponent<Renderer>());
            rs.Add(Box("Hair", model, new Vector3(0, 1.86f, -0.03f), new Vector3(0.48f, 0.14f, 0.46f), M.hair, false).GetComponent<Renderer>());
            Box("Visor", model, new Vector3(0, 1.68f, 0.215f), new Vector3(0.32f, 0.07f, 0.02f), M.fxSword, false);
            rs.Add(Box("Scarf", model, new Vector3(0, 1.4f, -0.05f), new Vector3(0.5f, 0.1f, 0.4f), M.playerAccent, false).GetComponent<Renderer>());

            p.armL = Limb("ArmL", model, new Vector3(-0.37f, 1.38f, 0), new Vector3(0.15f, 0.6f, 0.15f), M.playerCoat, rs);
            p.armR = Limb("ArmR", model, new Vector3(0.37f, 1.38f, 0), new Vector3(0.15f, 0.6f, 0.15f), M.playerCoat, rs);
            p.legL = Limb("LegL", model, new Vector3(-0.14f, 0.72f, 0), new Vector3(0.2f, 0.72f, 0.22f), M.trim, rs);
            p.legR = Limb("LegR", model, new Vector3(0.14f, 0.72f, 0), new Vector3(0.2f, 0.72f, 0.22f), M.trim, rs);
            // 오른손 검 (발광 칼날)
            Box("SwordGrip", p.armR, new Vector3(0, -0.6f, 0.05f), new Vector3(0.06f, 0.06f, 0.2f), M.metal, false);
            Box("SwordBlade", p.armR, new Vector3(0, -0.6f, 0.55f), new Vector3(0.05f, 0.09f, 0.85f), M.fxSword, false);

            var head = new GameObject("HeadAnchor").transform;
            head.SetParent(root, false);
            head.localPosition = new Vector3(0, 2.0f, 0);
            p.head = head;
            p.flashRenderers = rs.ToArray();
        }

        static void BuildEnemy(string name, Vector3 pos, float scale, float hp, bool elite, float range, Mats M)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var e = go.AddComponent<ShowcaseEnemy>();
            var model = new GameObject("Model").transform;
            model.SetParent(go.transform, false);
            model.localScale = Vector3.one * scale;
            var rs = new List<Renderer>();
            var body = elite ? M.enemyElite : M.enemy;
            rs.Add(Box("Body", model, new Vector3(0, 0.85f, 0), new Vector3(0.66f, 0.9f, 0.42f), body, false).GetComponent<Renderer>());
            rs.Add(Box("Head", model, new Vector3(0, 1.52f, 0), new Vector3(0.5f, 0.44f, 0.46f), body, false).GetComponent<Renderer>());
            rs.Add(Box("Hood", model, new Vector3(0, 1.78f, -0.04f), new Vector3(0.56f, 0.14f, 0.5f), M.trim, false).GetComponent<Renderer>());
            Box("Eye_L", model, new Vector3(-0.11f, 1.55f, 0.235f), new Vector3(0.09f, 0.07f, 0.02f), M.enemyEye, false);
            Box("Eye_R", model, new Vector3(0.11f, 1.55f, 0.235f), new Vector3(0.09f, 0.07f, 0.02f), M.enemyEye, false);
            rs.Add(Box("Arm_L", model, new Vector3(-0.42f, 0.95f, 0.05f), new Vector3(0.16f, 0.7f, 0.16f), body, false).GetComponent<Renderer>());
            rs.Add(Box("Arm_R", model, new Vector3(0.42f, 0.95f, 0.05f), new Vector3(0.16f, 0.7f, 0.16f), body, false).GetComponent<Renderer>());
            rs.Add(Box("Legs", model, new Vector3(0, 0.22f, 0), new Vector3(0.5f, 0.44f, 0.3f), M.trim, false).GetComponent<Renderer>());
            if (elite) Box("Horn", model, new Vector3(0, 1.98f, 0.05f), new Vector3(0.12f, 0.3f, 0.12f), M.enemyEye, false);

            var anchor = new GameObject("BarAnchor").transform;
            anchor.SetParent(go.transform, false);
            anchor.localPosition = new Vector3(0, 2.05f * scale, 0);

            e.model = model;
            e.barAnchor = anchor;
            e.renderers = rs.ToArray();
            e.maxHP = hp;
            e.elite = elite;
            e.patrolRange = range;
            e.speed = elite ? 0.8f : 1.2f;
            e.contactDamage = elite ? 14f : 8f;
            e.halfWidth = 0.4f * scale;
            e.height = 1.9f * scale;
        }

        static Transform Limb(string name, Transform parent, Vector3 pivot, Vector3 size, Material mat, List<Renderer> rs)
        {
            var p = new GameObject(name).transform;
            p.SetParent(parent, false);
            p.localPosition = pivot;
            rs.Add(Box(name + "_Mesh", p, new Vector3(0, -size.y * 0.5f, 0), size, mat, false).GetComponent<Renderer>());
            return p;
        }

        // ───────────────────────────── 도형 · 머티리얼 ─────────────────────────────

        static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 size, Material mat, bool collider) =>
            Prim(PrimitiveType.Cube, name, parent, localPos, size, mat, collider);

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool collider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.isStatic = parent != null && parent.name == "Subway_Station";
            return go;
        }

        static void Text3D(string name, Transform parent, string text, Vector3 pos, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var t = go.AddComponent<TextMeshPro>();
            t.font = UIB.Title != null ? UIB.Title : UIB.Body;
            t.text = text;
            t.fontSize = size * 10f;
            t.color = Color.white;
            t.alignment = TextAlignmentOptions.Center;
            t.rectTransform.sizeDelta = new Vector2(4.5f, 0.8f);
        }

        /// <summary>8각 기둥 (로우폴리 느낌, 면마다 꺾인 노멀)</summary>
        static Mesh LowPolyCylinder()
        {
            string path = MatDir + "/LowPolyCylinder8.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            const int N = 8;
            var v = new List<Vector3>(); var tris = new List<int>();
            for (int i = 0; i < N; i++)
            {
                float a0 = i * Mathf.PI * 2f / N, a1 = (i + 1) * Mathf.PI * 2f / N;
                var p0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0, Mathf.Sin(a0) * 0.5f);
                var p1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0, Mathf.Sin(a1) * 0.5f);
                int b = v.Count;
                v.Add(p0 + Vector3.down); v.Add(p1 + Vector3.down); v.Add(p1 + Vector3.up); v.Add(p0 + Vector3.up);
                tris.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            }
            var m = new Mesh { name = "LowPolyCylinder8" };
            m.SetVertices(v); m.SetTriangles(tris, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        sealed class Mats
        {
            public readonly Material concrete = Lit("Concrete", new Color(0.46f, 0.47f, 0.5f));
            public readonly Material concreteLight = Lit("ConcreteLight", new Color(0.58f, 0.59f, 0.62f));
            public readonly Material seam = Lit("Seam", new Color(0.33f, 0.34f, 0.37f));
            public readonly Material trim = Lit("Trim", new Color(0.16f, 0.17f, 0.2f));
            public readonly Material yellow = Lit("SafetyYellow", new Color(0.95f, 0.78f, 0.15f));
            public readonly Material gravel = Lit("Gravel", new Color(0.22f, 0.2f, 0.19f));
            public readonly Material metal = Lit("Metal", new Color(0.55f, 0.58f, 0.62f), 0.6f, 0.7f);
            public readonly Material tile = Lit("WallTile", new Color(0.82f, 0.8f, 0.72f));
            public readonly Material ceiling = Lit("Ceiling", new Color(0.2f, 0.21f, 0.24f));
            public readonly Material pillar = Lit("Pillar", new Color(0.72f, 0.72f, 0.7f));
            public readonly Material lineGreen = Lit("Line2Green", new Color(0.2f, 0.66f, 0.3f));
            public readonly Material signDark = Lit("SignNavy", new Color(0.07f, 0.12f, 0.24f));
            public readonly Material lamp = Lit("LampStrip", Color.white, 0f, 0.2f, new Color(1f, 0.97f, 0.9f) * 2.2f);
            public readonly Material window = Lit("Window", new Color(0.5f, 0.75f, 0.85f), 0f, 0.8f, new Color(0.45f, 0.7f, 0.85f) * 0.9f);
            public readonly Material train = Lit("TrainBody", new Color(0.78f, 0.8f, 0.82f), 0.4f, 0.6f);
            public readonly Material trainDoor = Lit("TrainDoor", new Color(0.62f, 0.64f, 0.67f), 0.4f, 0.5f);
            public readonly Material adBlue = Lit("AdBlue", new Color(0.15f, 0.35f, 0.75f), 0f, 0.3f, new Color(0.1f, 0.25f, 0.6f) * 0.8f);
            public readonly Material adOrange = Lit("AdOrange", new Color(0.9f, 0.45f, 0.15f), 0f, 0.3f, new Color(0.7f, 0.3f, 0.08f) * 0.6f);
            public readonly Material adPurple = Lit("AdPurple", new Color(0.5f, 0.25f, 0.7f), 0f, 0.3f, new Color(0.35f, 0.15f, 0.55f) * 0.7f);
            public readonly Material bench = Lit("Bench", new Color(0.55f, 0.36f, 0.22f));
            public readonly Material crate = Lit("Crate", new Color(0.62f, 0.5f, 0.3f));
            public readonly Material playerCoat = Lit("PlayerCoat", new Color(0.12f, 0.36f, 0.5f));
            public readonly Material playerAccent = Lit("PlayerScarf", new Color(0.95f, 0.75f, 0.25f));
            public readonly Material skin = Lit("PlayerSkin", new Color(0.93f, 0.78f, 0.66f));
            public readonly Material hair = Lit("PlayerHair", new Color(0.12f, 0.1f, 0.12f));
            public readonly Material enemy = Lit("EnemyBody", new Color(0.2f, 0.19f, 0.24f));
            public readonly Material enemyElite = Lit("EnemyElite", new Color(0.32f, 0.12f, 0.16f));
            public readonly Material enemyEye = Lit("EnemyEye", new Color(1f, 0.15f, 0.2f), 0f, 0.5f, new Color(1f, 0.1f, 0.15f) * 3f);
            public readonly Material fxSword = Unlit("FxSword", new Color(0.55f, 0.95f, 1f));
            public readonly Material fxMagic = Unlit("FxMagic", new Color(0.78f, 0.45f, 1f));
            public readonly Material fxHit = Unlit("FxHit", new Color(1f, 0.88f, 0.4f));
        }

        static Material Lit(string name, Color color, float metallic = 0f, float smooth = 0.15f, Color? emission = null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = GetOrCreate(name, shader);
            m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smooth);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Unlit(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var m = GetOrCreate(name, shader);
            m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material GetOrCreate(string name, Shader shader)
        {
            string path = $"{MatDir}/M_{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = "M_" + name };
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader;
            return m;
        }
    }
}
