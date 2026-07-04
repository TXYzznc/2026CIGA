using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

/// <summary>
/// 把 Assets/Resources/Sprite/Num/0~9.png 打成图集并生成 TMP Sprite Asset（美术字）。
/// 菜单：ZZNC -> Generate Num Sprite Asset。可重复执行（覆盖旧资产）。
/// TMP 直接用 glyphRect 采样图集，不依赖 Unity Sprite 切片。
/// </summary>
public static class NumSpriteAssetGenerator
{
    private const string SrcDir = "Assets/Resources/Sprite/Num";
    private const string AtlasPath = SrcDir + "/ZZNC_NumAtlas.png";
    private const string SpriteAssetPath = SrcDir + "/ZZNC_NumSpriteAsset.asset";
    private const int Padding = 20;

    [MenuItem("ZZNC/Generate Num Sprite Asset")]
    public static void Generate()
    {
        // ── 1. 从磁盘读原始 PNG（绕开导入压缩保留原始像素） ─────────
        var digitTextures = new Texture2D[10];
        for (int i = 0; i < 10; i++)
        {
            string path = $"{SrcDir}/{i}.png";
            if (!File.Exists(path))
            {
                Debug.LogError($"[ZZNC] Missing {path}");
                return;
            }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(path));
            digitTextures[i] = tex;
        }

        // ── 2. 横排打包图集 ────────────────────────────────────────
        int atlasWidth = Padding;
        int maxHeight = 0;
        foreach (var t in digitTextures)
        {
            atlasWidth += t.width + Padding;
            maxHeight = Mathf.Max(maxHeight, t.height);
        }
        int atlasHeight = maxHeight + Padding * 2;

        var atlas = new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGBA32, false);
        atlas.SetPixels32(new Color32[atlasWidth * atlasHeight]);

        var rects = new RectInt[10];
        int cursorX = Padding;
        for (int i = 0; i < 10; i++)
        {
            var t = digitTextures[i];
            // 垂直居中放置每个字符
            int y = Padding + (maxHeight - t.height) / 2;
            atlas.SetPixels32(cursorX, y, t.width, t.height, t.GetPixels32());
            rects[i] = new RectInt(cursorX, y, t.width, t.height);
            cursorX += t.width + Padding;
            Object.DestroyImmediate(t);
        }
        atlas.Apply();

        string atlasFullPath = System.IO.Path.GetFullPath(AtlasPath);
        File.WriteAllBytes(atlasFullPath, atlas.EncodeToPNG());
        Object.DestroyImmediate(atlas);

        // 导入为普通 Texture2D（不需要 Sprite 模式）
        AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 4096;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();

        var atlasTex = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        if (atlasTex == null)
        {
            Debug.LogError("[ZZNC] Failed to load atlas texture after import.");
            return;
        }

        // ── 3. 生成 TMP Sprite Asset（直接用 GlyphRect，不依赖 Sprite 切片）──
        var old = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(SpriteAssetPath);
        if (old != null) AssetDatabase.DeleteAsset(SpriteAssetPath);

        var spriteAsset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
        spriteAsset.spriteSheet = atlasTex;

        for (int i = 0; i < 10; i++)
        {
            var r = rects[i];
            var glyph = new TMP_SpriteGlyph
            {
                index = (uint)i,
                metrics = new GlyphMetrics(r.width, r.height, 0, r.height, r.width + 20),
                glyphRect = new GlyphRect(r.x, r.y, r.width, r.height),
                scale = 1f,
                atlasIndex = 0,
            };
            spriteAsset.spriteGlyphTable.Add(glyph);

            var character = new TMP_SpriteCharacter((uint)('0' + i), glyph)
            {
                name = i.ToString(),
                scale = 1f,
            };
            spriteAsset.spriteCharacterTable.Add(character);
        }

        AssetDatabase.CreateAsset(spriteAsset, SpriteAssetPath);

        // 材质
        var mat = new Material(Shader.Find("TextMeshPro/Sprite"))
        {
            name = "ZZNC_NumSpriteAsset Material",
            hideFlags = HideFlags.HideInHierarchy,
        };
        mat.SetTexture(ShaderUtilities.ID_MainTex, atlasTex);
        spriteAsset.material = mat;
        AssetDatabase.AddObjectToAsset(mat, spriteAsset);

        // FaceInfo
        var so = new SerializedObject(spriteAsset);
        var versionProp = so.FindProperty("m_Version");
        if (versionProp != null) versionProp.stringValue = "1.1.0";
        var faceInfo = so.FindProperty("m_FaceInfo");
        if (faceInfo != null)
        {
            var pointSizeProp = faceInfo.FindPropertyRelative("m_PointSize");
            if (pointSizeProp != null)
            {
                if (pointSizeProp.propertyType == SerializedPropertyType.Float)
                    pointSizeProp.floatValue = maxHeight;
                else if (pointSizeProp.propertyType == SerializedPropertyType.Integer)
                    pointSizeProp.intValue = maxHeight;
            }
            var scaleProp = faceInfo.FindPropertyRelative("m_Scale");
            if (scaleProp != null) scaleProp.floatValue = 1f;
            var ascentProp = faceInfo.FindPropertyRelative("m_AscentLine");
            if (ascentProp != null) ascentProp.floatValue = maxHeight;
            var baselineProp = faceInfo.FindPropertyRelative("m_Baseline");
            if (baselineProp != null) baselineProp.floatValue = 0f;
        }
        so.ApplyModifiedProperties();

        spriteAsset.UpdateLookupTables();
        EditorUtility.SetDirty(spriteAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[ZZNC] Sprite Asset generated: {SpriteAssetPath}\n" +
                  $"Usage: <sprite=\"ZZNC_NumSpriteAsset\" name=\"3\">\n" +
                  $"Code:  ZZNCNumText.ToSpriteTags(320)");
        Selection.activeObject = spriteAsset;
    }
}
