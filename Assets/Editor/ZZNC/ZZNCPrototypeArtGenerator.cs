using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ZZNCPrototypeArtGenerator
{
    private const string SpriteDir = "Assets/Resources/Sprite/ZZNC";
    private const string MaterialDir = "Assets/Resources/Material/ZZNC";
    private const string PrefabDir = "Assets/Prefabs/ZZNC";
    private const int TextureSize = 256;
    private const float PixelsPerUnit = 100f;

    private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

    [MenuItem("Tools/ZZNC/Generate Prototype Art")]
    public static void Generate()
    {
        EnsureFolders();
        GenerateSprites();
        GenerateMaterials();
        AssetDatabase.Refresh();
        LoadGeneratedAssets();
        GeneratePrefabs();
        CreatePreviewBoard();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var preview = GameObject.Find("ZZNCPreview");
        if (preview != null)
        {
            Selection.activeGameObject = preview;
            SceneView.FrameLastActiveSceneView();
        }

        Debug.Log("[ZZNC.Art] Generated prototype sprites, materials, prefabs, and scene preview.");
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/Sprite");
        EnsureFolder(SpriteDir);
        EnsureFolder("Assets/Resources/Material");
        EnsureFolder(MaterialDir);
        EnsureFolder("Assets/Prefabs");
        EnsureFolder(PrefabDir);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        var folder = Path.GetFileName(path);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(folder))
        {
            return;
        }

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folder);
    }

    private static void GenerateSprites()
    {
        WritePng("Hex_Cell", DrawHex(
            fill: new Color32(116, 179, 206, 255),
            inner: new Color32(151, 213, 227, 255),
            stroke: new Color32(33, 62, 82, 255),
            stripe: null));

        WritePng("Hex_Wall", DrawHex(
            fill: new Color32(96, 102, 112, 255),
            inner: new Color32(126, 132, 140, 255),
            stroke: new Color32(38, 41, 48, 255),
            stripe: new Color32(65, 69, 78, 190)));

        WritePng("Piece_Normal", DrawCircle(
            fill: new Color32(232, 244, 255, 255),
            rim: new Color32(57, 113, 168, 255),
            glow: new Color32(92, 182, 255, 135),
            icon: PieceIcon.None));

        WritePng("Piece_Score", DrawCircle(
            fill: new Color32(255, 214, 88, 255),
            rim: new Color32(162, 102, 18, 255),
            glow: new Color32(255, 238, 138, 145),
            icon: PieceIcon.Score));

        WritePng("Piece_Explosion", DrawCircle(
            fill: new Color32(244, 83, 44, 255),
            rim: new Color32(114, 32, 27, 255),
            glow: new Color32(255, 164, 57, 150),
            icon: PieceIcon.Explosion));

        WritePng("Piece_Split", DrawCircle(
            fill: new Color32(106, 218, 212, 255),
            rim: new Color32(78, 48, 150, 255),
            glow: new Color32(178, 117, 255, 145),
            icon: PieceIcon.Split));

        WritePng("Highlight_Ring", DrawRing(
            rim: new Color32(255, 236, 86, 230),
            glow: new Color32(255, 236, 86, 80)));

        WritePng("Preview_Dot", DrawCircle(
            fill: new Color32(97, 187, 255, 125),
            rim: new Color32(36, 116, 194, 160),
            glow: new Color32(97, 187, 255, 70),
            icon: PieceIcon.None,
            radiusScale: 0.48f));

        foreach (var path in Directory.GetFiles(SpriteDir, "*.png"))
        {
            ConfigureSpriteImporter(path.Replace('\\', '/'));
        }
    }

    private static Texture2D DrawHex(Color32 fill, Color32 inner, Color32 stroke, Color32? stripe)
    {
        var texture = NewTexture();
        var center = new Vector2((TextureSize - 1) * 0.5f, (TextureSize - 1) * 0.5f);
        var vertices = new Vector2[6];
        var radius = TextureSize * 0.45f;

        for (var i = 0; i < 6; i++)
        {
            var angle = Mathf.Deg2Rad * (60f * i + 30f);
            vertices[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        for (var y = 0; y < TextureSize; y++)
        {
            for (var x = 0; x < TextureSize; x++)
            {
                var p = new Vector2(x, y);
                var signedDistance = SignedDistanceToPolygon(p, vertices);
                if (signedDistance > 2f)
                {
                    continue;
                }

                var edgeAlpha = Mathf.Clamp01((-signedDistance + 2f) / 4f);
                var color = Color.Lerp(fill, inner, Mathf.Clamp01((p.y - TextureSize * 0.22f) / (TextureSize * 0.56f)));

                if (signedDistance > -8f)
                {
                    color = stroke;
                }

                if (stripe.HasValue && signedDistance < -12f && ((x + y) / 18) % 2 == 0)
                {
                    color = Color.Lerp(color, stripe.Value, 0.45f);
                }

                color.a *= edgeAlpha;
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return texture;
    }

    private static Texture2D DrawCircle(Color32 fill, Color32 rim, Color32 glow, PieceIcon icon, float radiusScale = 0.68f)
    {
        var texture = NewTexture();
        var center = new Vector2((TextureSize - 1) * 0.5f, (TextureSize - 1) * 0.5f);
        var radius = TextureSize * radiusScale * 0.5f;

        for (var y = 0; y < TextureSize; y++)
        {
            for (var x = 0; x < TextureSize; x++)
            {
                var p = new Vector2(x, y);
                var d = Vector2.Distance(p, center);
                if (d > radius + 4f)
                {
                    continue;
                }

                var edge = Mathf.Clamp01((radius + 4f - d) / 4f);
                var shade = Mathf.Clamp01((p.y - center.y + radius) / (radius * 2f));
                var color = Color.Lerp(fill, glow, shade * 0.32f);

                if (d > radius - 9f)
                {
                    color = Color.Lerp(rim, color, Mathf.Clamp01((radius + 4f - d) / 13f));
                }

                var highlight = Vector2.Distance(p, center + new Vector2(-radius * 0.28f, radius * 0.28f));
                if (highlight < radius * 0.24f)
                {
                    color = Color.Lerp(color, Color.white, 0.28f);
                }

                color.a *= edge;
                texture.SetPixel(x, y, color);
            }
        }

        DrawPieceIcon(texture, icon);
        texture.Apply();
        return texture;
    }

    private static Texture2D DrawRing(Color32 rim, Color32 glow)
    {
        var texture = NewTexture();
        var center = new Vector2((TextureSize - 1) * 0.5f, (TextureSize - 1) * 0.5f);
        var radius = TextureSize * 0.38f;

        for (var y = 0; y < TextureSize; y++)
        {
            for (var x = 0; x < TextureSize; x++)
            {
                var d = Vector2.Distance(new Vector2(x, y), center);
                var ring = Mathf.Abs(d - radius);
                if (ring > 12f)
                {
                    continue;
                }

                var color = ring < 5f ? (Color)rim : glow;
                color.a *= Mathf.Clamp01((12f - ring) / 7f);
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return texture;
    }

    private static void DrawPieceIcon(Texture2D texture, PieceIcon icon)
    {
        switch (icon)
        {
            case PieceIcon.Score:
                DrawStar(texture, new Color32(130, 78, 12, 225));
                break;
            case PieceIcon.Explosion:
                DrawBurst(texture, new Color32(255, 238, 160, 235));
                break;
            case PieceIcon.Split:
                DrawSplitMark(texture, new Color32(43, 30, 94, 230));
                break;
        }
    }

    private static void DrawStar(Texture2D texture, Color32 color)
    {
        var center = new Vector2(TextureSize * 0.5f, TextureSize * 0.5f);
        var outer = TextureSize * 0.16f;
        var inner = TextureSize * 0.07f;
        var points = new Vector2[10];
        for (var i = 0; i < points.Length; i++)
        {
            var radius = i % 2 == 0 ? outer : inner;
            var angle = Mathf.Deg2Rad * (90f + i * 36f);
            points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        FillPolygon(texture, points, color);
    }

    private static void DrawBurst(Texture2D texture, Color32 color)
    {
        var center = new Vector2(TextureSize * 0.5f, TextureSize * 0.5f);
        for (var i = 0; i < 6; i++)
        {
            var angle = Mathf.Deg2Rad * (i * 60f);
            DrawThickLine(texture, center, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * TextureSize * 0.2f, 7f, color);
        }

        DrawFilledCircle(texture, center, TextureSize * 0.06f, color);
    }

    private static void DrawSplitMark(Texture2D texture, Color32 color)
    {
        var center = new Vector2(TextureSize * 0.5f, TextureSize * 0.5f);
        DrawThickLine(texture, center + new Vector2(0f, TextureSize * 0.17f), center - new Vector2(0f, TextureSize * 0.16f), 8f, color);
        DrawThickLine(texture, center, center + new Vector2(TextureSize * 0.15f, TextureSize * 0.12f), 8f, color);
        DrawThickLine(texture, center, center + new Vector2(-TextureSize * 0.15f, TextureSize * 0.12f), 8f, color);
    }

    private static void GenerateMaterials()
    {
        CreateSpriteMaterial("M_ZZNC_Cell", new Color(1f, 1f, 1f, 1f));
        CreateSpriteMaterial("M_ZZNC_Piece", new Color(1f, 1f, 1f, 1f));
        CreateSpriteMaterial("M_ZZNC_Effect", new Color(1f, 1f, 1f, 1f));
    }

    private static void CreateSpriteMaterial(string name, Color color)
    {
        var shader = Shader.Find("Sprites/Default");
        var material = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/{name}.mat");
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, $"{MaterialDir}/{name}.mat");
        }
        else
        {
            material.shader = shader;
        }

        material.color = color;
        EditorUtility.SetDirty(material);
    }

    private static void LoadGeneratedAssets()
    {
        Sprites.Clear();
        Materials.Clear();

        foreach (var path in Directory.GetFiles(SpriteDir, "*.png"))
        {
            var assetPath = path.Replace('\\', '/');
            Sprites[Path.GetFileNameWithoutExtension(assetPath)] = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        foreach (var path in Directory.GetFiles(MaterialDir, "*.mat"))
        {
            var assetPath = path.Replace('\\', '/');
            Materials[Path.GetFileNameWithoutExtension(assetPath)] = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        }
    }

    private static void GeneratePrefabs()
    {
        CreateSpritePrefab("ZZNC_HexCell", "Hex_Cell", "M_ZZNC_Cell", 0, Vector3.one);
        CreateSpritePrefab("ZZNC_HexWall", "Hex_Wall", "M_ZZNC_Cell", 0, Vector3.one);
        CreateSpritePrefab("ZZNC_PieceNormal", "Piece_Normal", "M_ZZNC_Piece", 2, Vector3.one * 0.78f);
        CreateSpritePrefab("ZZNC_PieceScore", "Piece_Score", "M_ZZNC_Piece", 2, Vector3.one * 0.78f);
        CreateSpritePrefab("ZZNC_PieceExplosion", "Piece_Explosion", "M_ZZNC_Piece", 2, Vector3.one * 0.78f);
        CreateSpritePrefab("ZZNC_PieceSplit", "Piece_Split", "M_ZZNC_Piece", 2, Vector3.one * 0.78f);
        CreateSpritePrefab("ZZNC_HighlightRing", "Highlight_Ring", "M_ZZNC_Effect", 3, Vector3.one * 1.1f);
        CreateSpritePrefab("ZZNC_PreviewDot", "Preview_Dot", "M_ZZNC_Effect", 1, Vector3.one * 0.65f);
    }

    private static void CreateSpritePrefab(string prefabName, string spriteName, string materialName, int sortingOrder, Vector3 scale)
    {
        if (!Sprites.TryGetValue(spriteName, out var sprite) || sprite == null)
        {
            Debug.LogWarning($"[ZZNC.Art] Missing sprite: {spriteName}");
            return;
        }

        var go = new GameObject(prefabName);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        if (Materials.TryGetValue(materialName, out var material))
        {
            renderer.sharedMaterial = material;
        }

        go.transform.localScale = scale;
        PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{prefabName}.prefab");
        UnityEngine.Object.DestroyImmediate(go);
    }

    private static void CreatePreviewBoard()
    {
        var old = GameObject.Find("ZZNCPreview");
        if (old != null)
        {
            UnityEngine.Object.DestroyImmediate(old);
        }

        var root = new GameObject("ZZNCPreview");
        var cells = new GameObject("Cells");
        var pieces = new GameObject("Pieces");
        var effects = new GameObject("Effects");
        cells.transform.SetParent(root.transform);
        pieces.transform.SetParent(root.transform);
        effects.transform.SetParent(root.transform);

        var walls = new HashSet<Vector2Int>
        {
            new Vector2Int(0, 0),
            new Vector2Int(-1, 1),
            new Vector2Int(1, -1)
        };

        for (var q = -2; q <= 2; q++)
        {
            for (var r = -2; r <= 2; r++)
            {
                var s = -q - r;
                if (Mathf.Abs(s) > 2)
                {
                    continue;
                }

                var coord = new Vector2Int(q, r);
                var prefabName = walls.Contains(coord) ? "ZZNC_HexWall" : "ZZNC_HexCell";
                InstantiatePrefab(prefabName, cells.transform, AxialToWorld(q, r), $"{prefabName}_{q}_{r}");
            }
        }

        PlacePiece("ZZNC_PieceNormal", pieces.transform, -2, 0);
        PlacePiece("ZZNC_PieceScore", pieces.transform, -1, -1);
        PlacePiece("ZZNC_PieceExplosion", pieces.transform, 1, 0);
        PlacePiece("ZZNC_PieceSplit", pieces.transform, 0, 2);
        PlacePiece("ZZNC_PieceNormal", pieces.transform, 2, -2);
        PlacePiece("ZZNC_PieceScore", pieces.transform, 0, -2);

        InstantiatePrefab("ZZNC_HighlightRing", effects.transform, AxialToWorld(1, 0), "Highlight_Selected");
        InstantiatePrefab("ZZNC_PreviewDot", effects.transform, AxialToWorld(2, 0), "Preview_PushTarget");
        InstantiatePrefab("ZZNC_PreviewDot", effects.transform, AxialToWorld(-2, 1), "Preview_FallTarget");

        root.transform.position = Vector3.zero;
        EditorUtility.SetDirty(root);
    }

    private static void PlacePiece(string prefabName, Transform parent, int q, int r)
    {
        InstantiatePrefab(prefabName, parent, AxialToWorld(q, r) + new Vector3(0f, 0f, -0.05f), $"{prefabName}_{q}_{r}");
    }

    private static GameObject InstantiatePrefab(string prefabName, Transform parent, Vector3 position, string objectName)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{prefabName}.prefab");
        if (prefab == null)
        {
            Debug.LogWarning($"[ZZNC.Art] Missing prefab: {prefabName}");
            return null;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = objectName;
        instance.transform.SetParent(parent);
        instance.transform.localPosition = position;
        instance.transform.localRotation = Quaternion.identity;
        return instance;
    }

    private static Vector3 AxialToWorld(int q, int r)
    {
        const float cellSize = 1.45f;
        var x = Mathf.Sqrt(3f) * (q + r * 0.5f) * cellSize;
        var y = -1.5f * r * cellSize;
        return new Vector3(x, y, 0f);
    }

    private static Texture2D NewTexture()
    {
        var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        var clear = new Color32[TextureSize * TextureSize];
        for (var i = 0; i < clear.Length; i++)
        {
            clear[i] = new Color32(0, 0, 0, 0);
        }

        texture.SetPixels32(clear);
        return texture;
    }

    private static void WritePng(string name, Texture2D texture)
    {
        var path = $"{SpriteDir}/{name}.png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
    }

    private static void ConfigureSpriteImporter(string path)
    {
        AssetDatabase.ImportAsset(path);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }

    private static float SignedDistanceToPolygon(Vector2 point, Vector2[] vertices)
    {
        var inside = IsPointInPolygon(point, vertices);
        var minDistance = float.MaxValue;

        for (var i = 0; i < vertices.Length; i++)
        {
            var a = vertices[i];
            var b = vertices[(i + 1) % vertices.Length];
            minDistance = Mathf.Min(minDistance, DistanceToSegment(point, a, b));
        }

        return inside ? -minDistance : minDistance;
    }

    private static bool IsPointInPolygon(Vector2 point, Vector2[] vertices)
    {
        var inside = false;
        for (int i = 0, j = vertices.Length - 1; i < vertices.Length; j = i++)
        {
            var crosses = vertices[i].y > point.y != vertices[j].y > point.y;
            if (crosses)
            {
                var x = (vertices[j].x - vertices[i].x) * (point.y - vertices[i].y) / (vertices[j].y - vertices[i].y) + vertices[i].x;
                if (point.x < x)
                {
                    inside = !inside;
                }
            }
        }

        return inside;
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Vector2.Dot(ab, ab));
        return Vector2.Distance(p, a + ab * t);
    }

    private static void FillPolygon(Texture2D texture, Vector2[] vertices, Color32 color)
    {
        for (var y = 0; y < TextureSize; y++)
        {
            for (var x = 0; x < TextureSize; x++)
            {
                if (IsPointInPolygon(new Vector2(x, y), vertices))
                {
                    BlendPixel(texture, x, y, color);
                }
            }
        }
    }

    private static void DrawFilledCircle(Texture2D texture, Vector2 center, float radius, Color32 color)
    {
        var minX = Mathf.Max(0, Mathf.FloorToInt(center.x - radius));
        var maxX = Mathf.Min(TextureSize - 1, Mathf.CeilToInt(center.x + radius));
        var minY = Mathf.Max(0, Mathf.FloorToInt(center.y - radius));
        var maxY = Mathf.Min(TextureSize - 1, Mathf.CeilToInt(center.y + radius));

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                if (Vector2.Distance(new Vector2(x, y), center) <= radius)
                {
                    BlendPixel(texture, x, y, color);
                }
            }
        }
    }

    private static void DrawThickLine(Texture2D texture, Vector2 a, Vector2 b, float thickness, Color32 color)
    {
        var minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - thickness));
        var maxX = Mathf.Min(TextureSize - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + thickness));
        var minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - thickness));
        var maxY = Mathf.Min(TextureSize - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + thickness));

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                if (DistanceToSegment(new Vector2(x, y), a, b) <= thickness)
                {
                    BlendPixel(texture, x, y, color);
                }
            }
        }
    }

    private static void BlendPixel(Texture2D texture, int x, int y, Color color)
    {
        var baseColor = texture.GetPixel(x, y);
        var a = color.a + baseColor.a * (1f - color.a);
        if (a <= 0f)
        {
            texture.SetPixel(x, y, Color.clear);
            return;
        }

        var rgb = (color * color.a + baseColor * baseColor.a * (1f - color.a)) / a;
        rgb.a = a;
        texture.SetPixel(x, y, rgb);
    }

    private enum PieceIcon
    {
        None,
        Score,
        Explosion,
        Split
    }
}
