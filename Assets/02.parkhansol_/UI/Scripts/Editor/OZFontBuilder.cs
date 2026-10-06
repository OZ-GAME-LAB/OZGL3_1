using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace OZ.UI.EditorTools
{
    /// <summary>
    /// Setup 2단계: Galmuri(한글 픽셀폰트, OFL) → TMP 폰트 에셋 생성 + 영문 DeadRevolver 폰트와 TMP 기본 Fallback에 연결.
    /// 픽셀 폰트라 SDF 대신 RASTER_HINTED + Point 필터 + 원본 픽셀 크기로 샘플링.
    /// </summary>
    internal static class OZFontBuilder
    {
        // (ttf 파일, 생성 이름, 원본 픽셀 크기)
        static readonly (string file, string name, int size)[] Fonts =
        {
            ("Galmuri11.ttf", "Galmuri11", 12),
            ("Galmuri9.ttf", "Galmuri9", 10),
            ("Galmuri14.ttf", "Galmuri14", 15),
            ("Galmuri11-Bold.ttf", "Galmuri11 Bold", 12), // 약점 피해 숫자 (두꺼운 글자)
        };

        public static string PathOf(string name) => $"{OZPaths.Fonts}/{name} Pixel.asset";

        public static bool IsDone
        {
            get
            {
                foreach (var f in Fonts) if (!File.Exists(Path.GetFullPath(PathOf(f.name)))) return false;
                return true;
            }
        }

        public static TMP_FontAsset Load(string name) => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PathOf(name));

        public static void Run()
        {
            EnsureFolder(OZPaths.Fonts);
            var created = new List<TMP_FontAsset>();

            foreach (var (file, name, size) in Fonts)
            {
                var existing = Load(name);
                if (existing != null) { created.Add(existing); continue; }

                var font = AssetDatabase.LoadAssetAtPath<Font>($"{OZPaths.Galmuri}/{file}");
                if (font == null) { Debug.LogError($"[OZ UI] 폰트 파일 없음: {OZPaths.Galmuri}/{file}"); continue; }

                var fa = TMP_FontAsset.CreateFontAsset(font, size, 1, GlyphRenderMode.RASTER_HINTED, 1024, 1024,
                    AtlasPopulationMode.Dynamic, true);
                if (fa == null) { Debug.LogError("[OZ UI] TMP 폰트 생성 실패: " + file); continue; }

                fa.name = name + " Pixel";
                AssetDatabase.CreateAsset(fa, PathOf(name));

                var tex = fa.atlasTextures != null && fa.atlasTextures.Length > 0 ? fa.atlasTextures[0] : null;
                if (tex != null)
                {
                    tex.name = name + " Atlas";
                    tex.filterMode = FilterMode.Point;
                    AssetDatabase.AddObjectToAsset(tex, fa);
                }
                if (fa.material != null)
                {
                    fa.material.name = name + " Material";
                    AssetDatabase.AddObjectToAsset(fa.material, fa);
                }
                EditorUtility.SetDirty(fa);
                created.Add(fa);
            }
            AssetDatabase.SaveAssets();

            var body = Load("Galmuri11");
            if (body != null)
            {
                LinkFallbackToPixelUIFonts(body);
                LinkTmpSettingsFallback(body);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[OZ UI] 한글 폰트 {created.Count}종 준비 완료 → {OZPaths.Fonts}");
        }

        static void LinkFallbackToPixelUIFonts(TMP_FontAsset fallback)
        {
            string folder = OZPaths.PixelUI + "/Fonts";
            if (!AssetDatabase.IsValidFolder(folder)) return;
            foreach (var guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { folder }))
            {
                var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (fa == null || fa == fallback) continue;
                if (fa.fallbackFontAssetTable == null) fa.fallbackFontAssetTable = new List<TMP_FontAsset>();
                if (!fa.fallbackFontAssetTable.Contains(fallback))
                {
                    fa.fallbackFontAssetTable.Add(fallback);
                    EditorUtility.SetDirty(fa);
                }
            }
        }

        static void LinkTmpSettingsFallback(TMP_FontAsset fallback)
        {
            var settings = TMP_Settings.instance;
            if (settings == null) { Debug.LogWarning("[OZ UI] TMP Settings를 찾지 못했습니다 (1단계 이식 후 다시 실행)."); return; }
            var so = new SerializedObject(settings);
            var list = so.FindProperty("m_fallbackFontAssets");
            if (list == null) return;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == fallback) return;
            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = fallback;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
