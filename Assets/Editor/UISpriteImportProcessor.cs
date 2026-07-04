using UnityEditor;
using UnityEngine;

namespace TotemWarrior.EditorTools
{
    /// <summary>
    /// UI 美术素材自动导入设置。
    ///
    /// 范围：Assets下的贴图（含子目录）
    /// 触发：首次导入或贴图变更时自动重跑（Unity AssetPostprocessor 标准生命周期）。
    /// </summary>
    public sealed class UISpriteImportProcessor : AssetPostprocessor
    {
        const string UISpriteRoot = "Assets";

        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(UISpriteRoot)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.spritePixelsPerUnit = 100f;
            importer.textureCompression = TextureImporterCompression.Compressed;

            var settings = new TextureImporterPlatformSettings
            {
                maxTextureSize = 2048,
                textureCompression = TextureImporterCompression.Compressed,
            };
            importer.SetPlatformTextureSettings(settings);
        }
    }
}
