using UnityEditor;
using UnityEngine;

namespace OZ.UI.EditorTools
{
    /// <summary>
    /// 02.parkhansol_/UI/Art 아래 PNG를 픽셀 UI 규칙으로 자동 임포트
    /// (Sprite / Point 필터 / 비압축 / PPU 100 / 밉맵 없음).
    /// </summary>
    internal class OZArtImportProcessor : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(OZPaths.UI + "/Art/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.spritePixelsPerUnit = 100f;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
