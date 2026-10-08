using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEngine;

namespace OZ.UI.EditorTools
{
    /// <summary>
    /// Setup 1단계: Pixel UI &amp; HUD Unity Demo zip에서 필요한 폴더만 ThirdParty로 풀기 (.meta 포함 → GUID 유지).
    ///   가져옴: Sprites, Prefabs, Animations, Fonts, Scripts(데모 제외), Presets, Tilemaps, TextMesh Pro
    ///   제외: Scenes, Settings, Resources(URP/입력 설정 충돌), Scripts/Demo(구 Input 사용), DemoManager 프리팹
    /// </summary>
    internal static class OZPixelUIImporter
    {
        const string ZipRoot = "Pixel UI & HUD/Assets/";
        static readonly string[] Folders = { "Sprites/", "Prefabs/", "Animations/", "Fonts/", "Scripts/", "Presets/", "Tilemaps/" };
        static readonly string[] Skip = { "Scripts/Demo/", "Scripts/Demo.meta", "Prefabs/Demo/DemoManager.prefab" };

        public static bool IsDone => AssetDatabase.IsValidFolder(OZPaths.PixelUI + "/Sprites")
                                     && AssetDatabase.IsValidFolder(OZPaths.TextMeshPro);

        public static bool ZipExists => File.Exists(Full(OZPaths.ImportZip));

        public static void Run()
        {
            string zip = Full(OZPaths.ImportZip);
            if (!File.Exists(zip))
            {
                EditorUtility.DisplayDialog("OZ UI Setup",
                    "에셋 zip이 없습니다:\n" + OZPaths.ImportZip + "\n\nPixel UI & HUD - Unity Demo.zip을 이 경로에 PixelUIHUD_UnityDemo.zip 이름으로 넣어주세요.", "확인");
                return;
            }

            int written = 0, skipped = 0;
            try
            {
                using (var archive = ZipFile.OpenRead(zip))
                {
                    int total = archive.Entries.Count, i = 0;
                    foreach (var entry in archive.Entries)
                    {
                        if (++i % 200 == 0)
                            EditorUtility.DisplayProgressBar("Pixel UI 이식", entry.FullName, i / (float)total);

                        string dest = MapPath(entry.FullName.Replace('\\', '/'));
                        if (dest == null) continue;

                        string full = Full(dest);
                        if (entry.FullName.EndsWith("/")) { Directory.CreateDirectory(full); continue; }
                        if (File.Exists(full)) { skipped++; continue; } // 이미 있는 파일(우리 asmdef 등)은 보존

                        Directory.CreateDirectory(Path.GetDirectoryName(full));
                        entry.ExtractToFile(full);
                        written++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"[OZ UI] Pixel UI 이식 완료: {written}개 파일 (기존 {skipped}개 유지) → {OZPaths.PixelUI}, {OZPaths.TextMeshPro}");
        }

        static string MapPath(string name)
        {
            if (!name.StartsWith(ZipRoot)) return null;
            string rel = name.Substring(ZipRoot.Length);
            if (rel.Length == 0) return null;

            if (rel.StartsWith("TextMesh Pro/") || rel == "TextMesh Pro.meta")
                return OZPaths.ThirdParty + "/" + rel;

            foreach (var s in Skip) if (rel.StartsWith(s, StringComparison.Ordinal)) return null;
            foreach (var f in Folders)
            {
                string folderMeta = f.TrimEnd('/') + ".meta";
                if (rel.StartsWith(f, StringComparison.Ordinal) || rel == folderMeta)
                    return OZPaths.PixelUI + "/" + rel;
            }
            return null;
        }

        static string Full(string assetPath) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
    }
}
