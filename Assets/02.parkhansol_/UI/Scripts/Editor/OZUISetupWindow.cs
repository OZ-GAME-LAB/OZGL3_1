using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OZ.UI.EditorTools
{
    /// <summary>
    /// OZ > UI > Setup : 처음 한 번 실행하는 UI 세팅 마법사.
    ///   1 Pixel UI 에셋 이식 → 2 한글 폰트 → 3 샘플 데이터 → 4 UIRoot 프리팹 → 5 UI_Sandbox 씬
    /// </summary>
    internal class OZUISetupWindow : EditorWindow
    {
        [MenuItem("OZ/UI/Setup", priority = 0)]
        static void Open() => GetWindow<OZUISetupWindow>(true, "OZ UI Setup", true).minSize = new Vector2(420, 330);

        [MenuItem("OZ/UI/Open UI Sandbox", priority = 1)]
        static void OpenSandbox()
        {
            if (!OZSandboxBuilder.IsDone) { Open(); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(OZPaths.SandboxScene);
        }

        [MenuItem("OZ/UI/Rebuild UIRoot Prefab", priority = 2)]
        static void Rebuild() => OZUIRootBuilder.Run();

        void OnGUI()
        {
            GUILayout.Label("OZ UI Setup — 02.parkhansol_", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("순서대로 누르거나 '전체 실행'. 모두 Assets/02.parkhansol_ 안에만 생성합니다.\n" +
                                    "(TMP 기본 리소스는 02.parkhansol_/ThirdParty/TextMesh Pro 로 들어갑니다)", MessageType.Info);

            Step("1. Pixel UI 에셋 이식 (zip → ThirdParty)", OZPixelUIImporter.IsDone, OZPixelUIImporter.Run,
                OZPixelUIImporter.ZipExists ? null : "zip 없음: " + OZPaths.ImportZip);
            Step("2. 한글 폰트 (Galmuri → TMP, Fallback 연결)", OZFontBuilder.IsDone, OZFontBuilder.Run,
                OZPixelUIImporter.IsDone ? null : "1단계 먼저");
            Step("3. 샘플 데이터 (기획서 v0.2 검증값)", OZSampleData.IsDone, OZSampleData.Run);
            Step("4. UIRoot 프리팹 생성", OZUIRootBuilder.IsDone, () => OZUIRootBuilder.Run(),
                OZFontBuilder.IsDone ? null : "2단계 먼저 (한글 폰트)");
            Step("5. UI_Sandbox 씬 생성", OZSandboxBuilder.IsDone, OZSandboxBuilder.Run);

            GUILayout.Space(10);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("전체 실행", GUILayout.Height(28))) RunAll();
                if (GUILayout.Button("데이터 검사", GUILayout.Height(28))) OZDataValidator.Run();
                GUI.enabled = OZSandboxBuilder.IsDone;
                if (GUILayout.Button("Sandbox 열기", GUILayout.Height(28))) OpenSandbox();
                GUI.enabled = true;
            }
        }

        static void Step(string label, bool done, System.Action run, string blocked = null)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(done ? "✔" : "•", GUILayout.Width(16));
                GUILayout.Label(label);
                GUI.enabled = blocked == null;
                if (GUILayout.Button(done ? "다시" : "실행", GUILayout.Width(60))) run();
                GUI.enabled = true;
            }
            if (blocked != null) EditorGUILayout.LabelField("   " + blocked, EditorStyles.miniLabel);
        }

        /// <summary>메뉴/배치 모드용: 전체 단계를 순서대로 실행</summary>
        [MenuItem("OZ/UI/Setup - Run All", priority = 3)]
        public static void RunAll()
        {
            if (!OZPixelUIImporter.IsDone) OZPixelUIImporter.Run();
            if (!OZFontBuilder.IsDone) OZFontBuilder.Run();
            OZSampleData.Run(); // 이미 있는 에셋은 건드리지 않고, 새로 추가된 샘플(스킬 트리 등)만 만든다
            OZUISoundSetup.Run(); // v0.6 UI 효과음 (비어 있는 항목만)
            OZUIRootBuilder.Run();
            OZSandboxBuilder.Run();
            OZShowcaseBuilder.Run(); // 지하철역 쇼케이스 (UI를 게임 화면 느낌에서 확인)
            Debug.Log("[OZ UI] Setup 전체 완료 — UI_Sandbox 또는 UI_Showcase_Subway 씬에서 Play 하세요.");
        }
    }
}
