#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 材质批量转换器 - 工具箱集成面板（内嵌完整逻辑）
/// </summary>
[ToolHubItem(
    "渲染工具/材质批量转换器",
    "批量将材质从一种渲染管线转换到另一种（Standard/URP/HDRP）",
    50
)]
public class MaterialConverterPanel : IToolHubPanel
{
    private static readonly Dictionary<string, string[]> ShaderPresets = new()
    {
        ["Standard"] = new[] { "Standard", "Standard (Specular setup)" },
        ["URP"] = new[]
        {
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Simple Lit",
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/Particles/Lit",
            "Universal Render Pipeline/Particles/Simple Lit",
            "Universal Render Pipeline/Particles/Unlit",
        },
        ["HDRP"] = new[]
        {
            "HDRP/Lit",
            "HDRP/Unlit",
            "HDRP/Eye",
            "HDRP/Hair",
            "HDRP/StackLit",
            "HDRP/LayeredLit",
        },
    };

    private static readonly string[] TargetPipelineOptions =
    {
        "Standard",
        "URP",
        "HDRP",
        "Custom",
    };

    private static readonly Dictionary<string, string> DefaultTargetShader = new()
    {
        ["Standard"] = "Standard",
        ["URP"] = "Universal Render Pipeline/Lit",
        ["HDRP"] = "HDRP/Lit",
    };

    private static readonly Dictionary<string, string[]> PropAliases = new()
    {
        // 基础颜色
        ["_Color"] = new[] { "_BaseColor" },
        ["_BaseColor"] = new[] { "_Color" },
        ["_AlbedoColor"] = new[] { "_BaseColor", "_Color" },
        ["_MainColor"] = new[] { "_BaseColor", "_Color" },
        ["_TintColor"] = new[] { "_BaseColor", "_Color" },
        ["_DiffuseColor"] = new[] { "_BaseColor", "_Color" },
        // 主贴图 / Albedo
        ["_MainTex"] = new[] { "_BaseMap", "_BaseColorMap" },
        ["_BaseMap"] = new[] { "_MainTex" },
        ["_BaseColorMap"] = new[] { "_MainTex", "_BaseMap" },
        ["_Albedo"] = new[] { "_BaseMap", "_MainTex" },
        ["_AlbedoMap"] = new[] { "_BaseMap", "_MainTex" },
        ["_AlbedoTexture"] = new[] { "_BaseMap", "_MainTex" },
        ["_DiffuseMap"] = new[] { "_BaseMap", "_MainTex" },
        ["_DiffuseTex"] = new[] { "_BaseMap", "_MainTex" },
        ["_ColorMap"] = new[] { "_BaseMap", "_MainTex" },
        // 自发光颜色
        ["_EmissionColor"] = new[] { "_EmissiveColor" },
        ["_EmissiveColor"] = new[] { "_EmissionColor" },
        // 自发光贴图
        ["_EmissionMap"] = new[] { "_EmissiveColorMap" },
        ["_EmissiveColorMap"] = new[] { "_EmissionMap" },
        // 法线贴图
        ["_BumpMap"] = new[] { "_NormalMap" },
        ["_NormalMap"] = new[] { "_BumpMap" },
        ["_NormalTex"] = new[] { "_BumpMap", "_NormalMap" },
        ["_NormalTexture"] = new[] { "_BumpMap", "_NormalMap" },
        // 法线强度
        ["_BumpScale"] = new[] { "_NormalScale", "_NormalMapDepth" },
        ["_NormalScale"] = new[] { "_BumpScale" },
        ["_NormalMapDepth"] = new[] { "_BumpScale", "_NormalScale" },
        // 遮挡贴图
        ["_OcclusionMap"] = new[] { "_MaskMap" },
        ["_AOMap"] = new[] { "_OcclusionMap", "_MaskMap" },
        ["_AmbientOcclusionMap"] = new[] { "_OcclusionMap", "_MaskMap" },
        // 金属度
        ["_Metallic"] = new[] { "_Metalness" },
        ["_Metalness"] = new[] { "_Metallic" },
        // 金属贴图
        ["_MetallicGlossMap"] = new[] { "_MaskMap" },
        ["_MetallicMap"] = new[] { "_MetallicGlossMap", "_MaskMap" },
        ["_MetallicTexture"] = new[] { "_MetallicGlossMap", "_MaskMap" },
        // 平滑度 / 粗糙度
        ["_Glossiness"] = new[] { "_Smoothness" },
        ["_GlossMapScale"] = new[] { "_Smoothness" },
        ["_Smoothness"] = new[] { "_Glossiness", "_GlossMapScale" },
        ["_Snoothness"] = new[] { "_Smoothness", "_Glossiness" }, // ASE 拼写错误
        ["_SmoothnessMap"] = new[] { "_MetallicGlossMap" },
        ["_RoughnessMap"] = new[] { "_MetallicGlossMap" },
        ["_Roughness"] = new[] { "_Smoothness" },
        // 高度贴图
        ["_ParallaxMap"] = new[] { "_HeightMap" },
        ["_HeightMap"] = new[] { "_ParallaxMap" },
        // 高度强度
        ["_Parallax"] = new[] { "_HeightAmplitude", "_ParalaxOffset" },
        ["_HeightAmplitude"] = new[] { "_Parallax" },
        ["_ParalaxOffset"] = new[] { "_Parallax", "_HeightAmplitude" }, // ASE 拼写错误
        // 透明度裁剪
        ["_Cutoff"] = new[] { "_AlphaClipThreshold" },
        ["_AlphaClipThreshold"] = new[] { "_Cutoff" },
    };

    private static readonly HashSet<string> ForceAliasProps = new() { "_Color", "_MainTex" };

    private static readonly HashSet<string> SilentSkipProps = new()
    {
        "_Mode",
        "_UVSec",
        "_SrcBlend",
        "_DstBlend",
        "_ZWrite",
        "_SmoothnessTextureChannel",
        "_SpecularHighlights",
        "_GlossyReflections",
        "_Surface",
        "_Blend",
        "_AlphaClip",
        "_ReceiveShadows",
        "_QueueOffset",
        "_QueueControl",
    };

    private DefaultAsset m_TargetFolder;
    private int m_SrcPipelineIndex = 0;
    private int m_DstPipelineIndex = 1;
    private string m_CustomTargetShader = "";
    private bool m_IncludeSubfolders = true;
    private Vector2 m_PreviewScroll;
    private Vector2 m_LogScroll;
    private Vector2 m_MainScroll;

    private readonly List<Material> m_PreviewMaterials = new();
    private bool m_PreviewDirty = true;
    private readonly List<(string text, bool isWarning)> m_Log = new();

    private GUIStyle m_WarnStyle;
    private GUIStyle WarnStyle =>
        m_WarnStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(1f, 0.6f, 0f) },
        };

    public void OnEnable() { }

    public void OnDisable() { }

    public void OnDestroy() { }

    public string GetHelpText() =>
        "将材质从 Standard / URP / HDRP 批量转换到目标管线，保留贴图、颜色等属性";

    public void OnGUI()
    {
        m_MainScroll = EditorGUILayout.BeginScrollView(m_MainScroll);
        EditorGUILayout.Space(8);
        GUILayout.Label("材质批量转换器", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);
        DrawFolderField();
        EditorGUILayout.Space(6);
        DrawPipelineSelectors();
        EditorGUILayout.Space(6);
        DrawOptions();
        EditorGUILayout.Space(8);
        DrawPreviewSection();
        EditorGUILayout.Space(6);
        DrawActionButtons();
        EditorGUILayout.Space(6);
        DrawLog();
        EditorGUILayout.EndScrollView();
    }

    private void DrawFolderField()
    {
        GUILayout.Label("目标文件夹", EditorStyles.miniBoldLabel);
        var dropRect = GUILayoutUtility.GetRect(0, 48, GUILayout.ExpandWidth(true));
        GUI.Box(
            dropRect,
            m_TargetFolder == null
                ? "将文件夹拖到此处，或点击选择"
                : AssetDatabase.GetAssetPath(m_TargetFolder),
            EditorStyles.helpBox
        );

        var evt = Event.current;
        if (!dropRect.Contains(evt.mousePosition))
            return;

        if (evt.type == EventType.DragUpdated)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            evt.Use();
        }
        else if (evt.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            foreach (var path in DragAndDrop.paths)
            {
                if (!AssetDatabase.IsValidFolder(path))
                    continue;
                m_TargetFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
                m_PreviewDirty = true;
                break;
            }
            evt.Use();
        }
        else if (evt.type == EventType.MouseDown)
        {
            string selected = EditorUtility.OpenFolderPanel("选择文件夹", "Assets", "");
            if (!string.IsNullOrEmpty(selected))
            {
                if (selected.StartsWith(Application.dataPath))
                    selected = "Assets" + selected.Substring(Application.dataPath.Length);
                m_TargetFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(selected);
                m_PreviewDirty = true;
            }
            evt.Use();
        }
    }

    private void DrawPipelineSelectors()
    {
        float halfWidth = (EditorGUIUtility.currentViewWidth - 40) * 0.45f;
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(halfWidth));
        GUILayout.Label("待转换管线（来源）", EditorStyles.miniBoldLabel);
        int newSrc = GUILayout.SelectionGrid(
            m_SrcPipelineIndex,
            new[] { "Standard", "URP", "HDRP", "Other" },
            1,
            EditorStyles.radioButton
        );
        if (newSrc != m_SrcPipelineIndex)
        {
            m_SrcPipelineIndex = newSrc;
            m_PreviewDirty = true;
        }
        EditorGUILayout.EndVertical();

        GUILayout.FlexibleSpace();
        GUILayout.Label("→", GUILayout.Width(20));
        GUILayout.FlexibleSpace();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(halfWidth));
        GUILayout.Label("目标管线", EditorStyles.miniBoldLabel);
        m_DstPipelineIndex = GUILayout.SelectionGrid(
            m_DstPipelineIndex,
            TargetPipelineOptions,
            1,
            EditorStyles.radioButton
        );
        if (m_DstPipelineIndex == 3)
            m_CustomTargetShader = EditorGUILayout.TextField("Shader 名称", m_CustomTargetShader);
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawOptions()
    {
        bool sub = EditorGUILayout.Toggle("包含子文件夹", m_IncludeSubfolders);
        if (sub != m_IncludeSubfolders)
        {
            m_IncludeSubfolders = sub;
            m_PreviewDirty = true;
        }
    }

    private void DrawPreviewSection()
    {
        if (m_PreviewDirty)
            RefreshPreview();
        GUILayout.Label(
            $"匹配到的材质（{m_PreviewMaterials.Count} 个）",
            EditorStyles.miniBoldLabel
        );
        m_PreviewScroll = EditorGUILayout.BeginScrollView(
            m_PreviewScroll,
            EditorStyles.helpBox,
            GUILayout.Height(Mathf.Clamp(m_PreviewMaterials.Count * 20 + 8, 60, 160))
        );
        if (m_PreviewMaterials.Count == 0)
            GUILayout.Label("  — 无匹配材质 —", EditorStyles.centeredGreyMiniLabel);
        else
            foreach (var mat in m_PreviewMaterials)
                EditorGUILayout.ObjectField(mat, typeof(Material), false);
        EditorGUILayout.EndScrollView();
    }

    private void DrawActionButtons()
    {
        string targetShaderName = GetTargetShaderName();
        bool canConvert =
            m_TargetFolder != null
            && m_PreviewMaterials.Count > 0
            && !string.IsNullOrEmpty(targetShaderName)
            && Shader.Find(targetShaderName) != null;

        EditorGUI.BeginDisabledGroup(!canConvert);
        if (GUILayout.Button($"开始转换  →  {targetShaderName}", GUILayout.Height(32)))
            Convert(targetShaderName);
        EditorGUI.EndDisabledGroup();

        if (!canConvert && m_TargetFolder != null)
        {
            string hint = string.IsNullOrEmpty(targetShaderName)
                ? "请输入目标 Shader 名称"
                : $"找不到 Shader：{targetShaderName}";
            EditorGUILayout.HelpBox(hint, MessageType.Warning);
        }
    }

    private void DrawLog()
    {
        if (m_Log.Count == 0)
            return;
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("转换日志", EditorStyles.miniBoldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("复制日志", EditorStyles.miniButton, GUILayout.Width(60)))
        {
            var sb = new StringBuilder();
            foreach (var (text, _) in m_Log)
                sb.AppendLine(text);
            GUIUtility.systemCopyBuffer = sb.ToString();
        }
        EditorGUILayout.EndHorizontal();
        m_LogScroll = EditorGUILayout.BeginScrollView(
            m_LogScroll,
            EditorStyles.helpBox,
            GUILayout.Height(120)
        );
        foreach (var (text, isWarning) in m_Log)
            GUILayout.Label(text, isWarning ? WarnStyle : EditorStyles.miniLabel);
        EditorGUILayout.EndScrollView();
    }

    private void RefreshPreview()
    {
        m_PreviewMaterials.Clear();
        m_PreviewDirty = false;
        if (m_TargetFolder == null)
            return;

        string folderPath = AssetDatabase.GetAssetPath(m_TargetFolder);
        string[] guids = AssetDatabase.FindAssets(
            "t:Material",
            m_IncludeSubfolders ? new[] { folderPath } : null
        );

        bool isOther = m_SrcPipelineIndex == 3; // Other

        HashSet<string> allPresetShaders = null;
        string[] sourceShaders = null;
        if (isOther)
        {
            allPresetShaders = new HashSet<string>();
            foreach (var kv in ShaderPresets)
            foreach (var s in kv.Value)
                allPresetShaders.Add(s);
        }
        else
        {
            sourceShaders = ShaderPresets[new[] { "Standard", "URP", "HDRP" }[m_SrcPipelineIndex]];
        }

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (
                !m_IncludeSubfolders
                && Path.GetDirectoryName(path)?.Replace('\\', '/') != folderPath
            )
                continue;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader == null)
                continue;

            if (isOther)
            {
                if (!allPresetShaders.Contains(mat.shader.name))
                    m_PreviewMaterials.Add(mat);
            }
            else
            {
                foreach (var s in sourceShaders)
                    if (mat.shader.name == s)
                    {
                        m_PreviewMaterials.Add(mat);
                        break;
                    }
            }
        }
    }

    private void Convert(string targetShaderName)
    {
        var targetShader = Shader.Find(targetShaderName);
        if (targetShader == null)
        {
            Debug.LogError($"[MaterialConverter] 找不到 Shader: {targetShaderName}");
            return;
        }

        m_Log.Clear();
        int success = 0,
            skip = 0,
            warnCount = 0;
        try
        {
            AssetDatabase.StartAssetEditing();
            foreach (var mat in m_PreviewMaterials)
            {
                if (mat == null)
                {
                    skip++;
                    continue;
                }
                var snapshot = SnapshotMaterial(mat);
                string oldName = mat.shader.name;
                mat.shader = targetShader;
                var warnings = RestoreProperties(mat, snapshot, targetShader);
                EditorUtility.SetDirty(mat);
                m_Log.Add(($"✓  {mat.name}  [{oldName}  →  {targetShaderName}]", false));
                foreach (var w in warnings)
                {
                    m_Log.Add(
                        ($"   ⚠  {mat.name}: 属性 \"{w}\" 在目标 Shader 中不存在，已跳过", true)
                    );
                    warnCount++;
                }
                success++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        string summary =
            warnCount > 0
                ? $"── 完成：{success} 个成功，{skip} 个跳过，{warnCount} 条属性警告 ──"
                : $"── 完成：{success} 个成功，{skip} 个跳过 ──";
        m_Log.Add((summary, false));
        m_PreviewDirty = true;
        Debug.Log($"[MaterialConverter] {summary}");
    }

    private struct MatSnapshot
    {
        public Dictionary<string, Color> Colors;
        public Dictionary<string, Vector4> Vectors;
        public Dictionary<string, float> Floats;
        public Dictionary<string, int> Ints;
        public Dictionary<string, Texture> Textures;
        public Dictionary<string, Vector2> TextureOffsets;
        public Dictionary<string, Vector2> TextureScales;
        public int RenderQueue;
    }

    private static MatSnapshot SnapshotMaterial(Material mat)
    {
        var snap = new MatSnapshot
        {
            Colors = new(),
            Vectors = new(),
            Floats = new(),
            Ints = new(),
            Textures = new(),
            TextureOffsets = new(),
            TextureScales = new(),
            RenderQueue = mat.renderQueue,
        };
        int count = ShaderUtil.GetPropertyCount(mat.shader);
        for (int i = 0; i < count; i++)
        {
            string name = ShaderUtil.GetPropertyName(mat.shader, i);
            switch (ShaderUtil.GetPropertyType(mat.shader, i))
            {
                case ShaderUtil.ShaderPropertyType.Color:
                    snap.Colors[name] = mat.GetColor(name);
                    break;
                case ShaderUtil.ShaderPropertyType.Vector:
                    snap.Vectors[name] = mat.GetVector(name);
                    break;
                case ShaderUtil.ShaderPropertyType.Float:
                case ShaderUtil.ShaderPropertyType.Range:
                    snap.Floats[name] = mat.GetFloat(name);
                    break;
                case ShaderUtil.ShaderPropertyType.TexEnv:
                    snap.Textures[name] = mat.GetTexture(name);
                    snap.TextureOffsets[name] = mat.GetTextureOffset(name);
                    snap.TextureScales[name] = mat.GetTextureScale(name);
                    break;
            }
        }
        return snap;
    }

    private static List<string> RestoreProperties(
        Material mat,
        MatSnapshot snap,
        Shader targetShader
    )
    {
        var missing = new List<string>();
        int tc = ShaderUtil.GetPropertyCount(targetShader);
        var targetProps = new Dictionary<string, ShaderUtil.ShaderPropertyType>(tc);
        for (int i = 0; i < tc; i++)
            targetProps[ShaderUtil.GetPropertyName(targetShader, i)] = ShaderUtil.GetPropertyType(
                targetShader,
                i
            );

        static bool IsCV(ShaderUtil.ShaderPropertyType t) =>
            t == ShaderUtil.ShaderPropertyType.Color || t == ShaderUtil.ShaderPropertyType.Vector;
        static bool IsFR(ShaderUtil.ShaderPropertyType t) =>
            t == ShaderUtil.ShaderPropertyType.Float || t == ShaderUtil.ShaderPropertyType.Range;

        string Resolve(string src, System.Func<ShaderUtil.ShaderPropertyType, bool> compat)
        {
            if (
                !ForceAliasProps.Contains(src)
                && targetProps.TryGetValue(src, out var t)
                && compat(t)
            )
                return src;
            if (PropAliases.TryGetValue(src, out var aliases))
                foreach (var a in aliases)
                    if (targetProps.TryGetValue(a, out var at) && compat(at))
                        return a;
            if (
                ForceAliasProps.Contains(src)
                && targetProps.TryGetValue(src, out var fb)
                && compat(fb)
            )
                return src;
            return null;
        }

        var handled = new HashSet<string>();
        foreach (var kv in snap.Colors)
        {
            handled.Add(kv.Key);
            var dst = Resolve(kv.Key, IsCV);
            if (dst != null)
                mat.SetColor(dst, kv.Value);
            else if (!SilentSkipProps.Contains(kv.Key))
                missing.Add(kv.Key);
        }
        foreach (var kv in snap.Vectors)
        {
            if (handled.Contains(kv.Key))
                continue;
            handled.Add(kv.Key);
            var dst = Resolve(kv.Key, IsCV);
            if (dst != null)
                mat.SetColor(dst, kv.Value);
            else if (!SilentSkipProps.Contains(kv.Key))
                missing.Add(kv.Key);
        }
        foreach (var kv in snap.Floats)
        {
            var dst = Resolve(kv.Key, IsFR);
            if (dst != null)
                mat.SetFloat(dst, kv.Value);
            else if (!SilentSkipProps.Contains(kv.Key))
                missing.Add(kv.Key);
        }
        foreach (var kv in snap.Ints)
        {
            if (targetProps.ContainsKey(kv.Key))
                mat.SetInt(kv.Key, kv.Value);
            else if (!SilentSkipProps.Contains(kv.Key))
                missing.Add(kv.Key);
        }
        foreach (var kv in snap.Textures)
        {
            var dst = Resolve(kv.Key, t => t == ShaderUtil.ShaderPropertyType.TexEnv);
            if (dst != null)
            {
                mat.SetTexture(dst, kv.Value);
                mat.SetTextureOffset(dst, snap.TextureOffsets[kv.Key]);
                mat.SetTextureScale(dst, snap.TextureScales[kv.Key]);
            }
            else if (!SilentSkipProps.Contains(kv.Key))
                missing.Add(kv.Key);
        }
        mat.renderQueue = snap.RenderQueue;
        return missing;
    }

    private string GetTargetShaderName()
    {
        string key = TargetPipelineOptions[m_DstPipelineIndex];
        if (key == "Custom")
            return m_CustomTargetShader?.Trim();
        return DefaultTargetShader.TryGetValue(key, out var s) ? s : "";
    }
}
#endif
