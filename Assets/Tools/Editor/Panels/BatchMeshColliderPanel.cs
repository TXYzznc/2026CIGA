#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 批量添加 MeshCollider 工具 - 对所有没有 Collider 的子对象添加 MeshCollider
/// 支持：场景对象、预制体资源、文件夹
/// </summary>
[ToolHubItem(
    "场景工具/批量添加MeshCollider",
    "批量给对象及子对象中缺少Collider的节点添加MeshCollider",
    35
)]
public class BatchMeshColliderPanel : IToolHubPanel
{
    // ── 拖入的目标列表 ────────────────────────────────────────────────────
    // 可以是场景 GameObject 或 Project 中的 GameObject/DefaultAsset(文件夹)
    private readonly List<Object> m_Targets = new();

    // ── 选项 ──────────────────────────────────────────────────────────────
    private bool m_ConvexCollider = false; // 是否生成 Convex MeshCollider
    private bool m_OnlyWithMeshFilter = true; // 只处理有 MeshFilter 的节点
    private bool m_IncludeInactive = true; // 是否包含非激活对象

    // ── 结果日志 ──────────────────────────────────────────────────────────
    private readonly List<string> m_Log = new();
    private Vector2 m_LogScroll;
    private Vector2 m_MainScroll;

    // ── 样式 ──────────────────────────────────────────────────────────────
    private GUIStyle m_SectionStyle;
    private GUIStyle m_LogSuccessStyle;
    private GUIStyle m_LogSkipStyle;
    private bool m_StylesInit;

    public void OnEnable() { }

    public void OnDisable() { }

    public void OnDestroy() { }

    public string GetHelpText() => "批量给对象及子对象中缺少 Collider 的节点添加 MeshCollider";

    public void OnGUI()
    {
        InitStyles();
        m_MainScroll = EditorGUILayout.BeginScrollView(m_MainScroll);

        EditorGUILayout.Space(6);
        GUILayout.Label("批量添加 MeshCollider", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        DrawTargetList();
        EditorGUILayout.Space(6);
        DrawOptions();
        EditorGUILayout.Space(6);
        DrawActions();
        EditorGUILayout.Space(6);
        DrawLog();

        EditorGUILayout.EndScrollView();
    }

    // ── 目标列表 + 拖放区域 ───────────────────────────────────────────────
    private void DrawTargetList()
    {
        EditorGUILayout.BeginVertical(m_SectionStyle);
        EditorGUILayout.LabelField("目标对象", EditorStyles.boldLabel);

        // 已添加的目标
        for (int i = m_Targets.Count - 1; i >= 0; i--)
        {
            EditorGUILayout.BeginHorizontal();
            m_Targets[i] = EditorGUILayout.ObjectField(m_Targets[i], typeof(Object), true);
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("✕", GUILayout.Width(24)))
                m_Targets.RemoveAt(i);
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        // 拖放区域
        EditorGUILayout.Space(4);
        Rect dropRect = GUILayoutUtility.GetRect(0f, 50f, GUILayout.ExpandWidth(true));
        GUI.Box(dropRect, "🎯 拖入场景对象 / 预制体 / 文件夹", EditorStyles.helpBox);

        Event evt = Event.current;
        if (dropRect.Contains(evt.mousePosition))
        {
            if (evt.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                evt.Use();
            }
            else if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (var obj in DragAndDrop.objectReferences)
                {
                    if (obj != null && !m_Targets.Contains(obj))
                        m_Targets.Add(obj);
                }
                evt.Use();
            }
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("清空列表", EditorStyles.miniButton))
            m_Targets.Clear();
        EditorGUILayout.LabelField($"共 {m_Targets.Count} 个目标", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    // ── 选项 ──────────────────────────────────────────────────────────────
    private void DrawOptions()
    {
        EditorGUILayout.BeginVertical(m_SectionStyle);
        EditorGUILayout.LabelField("选项", EditorStyles.boldLabel);

        m_OnlyWithMeshFilter = DrawToggleWithFullLabel(
            m_OnlyWithMeshFilter,
            "仅处理有 MeshFilter 的节点",
            "跳过没有网格的空节点"
        );

        m_ConvexCollider = DrawToggleWithFullLabel(
            m_ConvexCollider,
            "生成 Convex MeshCollider",
            "适用于动态刚体；静态碰撞体通常不需要"
        );

        m_IncludeInactive = DrawToggleWithFullLabel(
            m_IncludeInactive,
            "包含非激活对象",
            "处理层级中被禁用的子对象"
        );

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 用足够宽的 labelWidth 绘制 Toggle，防止文字被截断
    /// </summary>
    private static bool DrawToggleWithFullLabel(bool value, string label, string tooltip = "")
    {
        float savedWidth = EditorGUIUtility.labelWidth;
        // 按文字内容动态计算所需宽度，加 8px 余量
        EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(new GUIContent(label)).x + 8f;
        value = EditorGUILayout.Toggle(new GUIContent(label, tooltip), value);
        EditorGUIUtility.labelWidth = savedWidth;
        return value;
    }

    // ── 执行按钮 ──────────────────────────────────────────────────────────
    private void DrawActions()
    {
        EditorGUILayout.BeginVertical(m_SectionStyle);

        bool hasTargets = m_Targets.Count > 0;
        EditorGUI.BeginDisabledGroup(!hasTargets);
        GUI.backgroundColor = hasTargets ? new Color(0.4f, 0.9f, 0.5f) : Color.white;
        if (GUILayout.Button("▶ 开始批量添加 MeshCollider", GUILayout.Height(34)))
            Execute();
        GUI.backgroundColor = Color.white;
        EditorGUI.EndDisabledGroup();

        if (!hasTargets)
            EditorGUILayout.HelpBox("请先拖入目标对象或文件夹", MessageType.Info);

        EditorGUILayout.EndVertical();
    }

    // ── 日志 ──────────────────────────────────────────────────────────────
    private void DrawLog()
    {
        if (m_Log.Count == 0)
            return;

        EditorGUILayout.BeginVertical(m_SectionStyle);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("执行日志", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("清空", EditorStyles.miniButton, GUILayout.Width(40)))
            m_Log.Clear();
        EditorGUILayout.EndHorizontal();

        m_LogScroll = EditorGUILayout.BeginScrollView(m_LogScroll, GUILayout.Height(160));
        foreach (var line in m_Log)
        {
            bool isSkip = line.StartsWith("  ○");
            GUILayout.Label(line, isSkip ? m_LogSkipStyle : m_LogSuccessStyle);
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    // ── 核心执行逻辑 ──────────────────────────────────────────────────────
    private void Execute()
    {
        m_Log.Clear();
        int added = 0,
            skipped = 0;

        // 收集所有需要处理的 GameObject
        var roots = CollectGameObjects();
        if (roots.Count == 0)
        {
            m_Log.Add("⚠ 没有找到可处理的 GameObject");
            return;
        }

        m_Log.Add($"── 开始处理，共 {roots.Count} 个根对象 ──");

        foreach (var root in roots)
        {
            bool isPrefabAsset = !root.scene.IsValid(); // Project 中的预制体
            ProcessGameObject(root, isPrefabAsset, ref added, ref skipped);
        }

        // 保存预制体资源修改
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        m_Log.Add($"── 完成：新增 {added} 个 MeshCollider，跳过 {skipped} 个节点 ──");
        Debug.Log($"[BatchMeshCollider] 完成：新增 {added} 个，跳过 {skipped} 个");
    }

    /// <summary>
    /// 从 m_Targets 中收集所有 GameObject（展开文件夹、实例化预制体引用）
    /// </summary>
    private List<GameObject> CollectGameObjects()
    {
        var result = new List<GameObject>();
        var visited = new HashSet<int>(); // 防止重复（instanceID）

        foreach (var target in m_Targets)
        {
            if (target == null)
                continue;

            // 场景对象
            if (target is GameObject go)
            {
                if (visited.Add(go.GetInstanceID()))
                    result.Add(go);
                continue;
            }

            // Project 中的文件夹
            string path = AssetDatabase.GetAssetPath(target);
            if (AssetDatabase.IsValidFolder(path))
            {
                // 递归查找文件夹下所有预制体
                string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { path });
                foreach (var guid in guids)
                {
                    string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (prefab != null && visited.Add(prefab.GetInstanceID()))
                        result.Add(prefab);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 递归处理一个 GameObject 及其所有子对象
    /// </summary>
    private void ProcessGameObject(
        GameObject root,
        bool isPrefabAsset,
        ref int added,
        ref int skipped
    )
    {
        var allTransforms = root.GetComponentsInChildren<Transform>(m_IncludeInactive);

        // 预制体资源需要通过 PrefabUtility 修改
        string prefabPath = isPrefabAsset ? AssetDatabase.GetAssetPath(root) : null;
        GameObject prefabRoot = null;

        if (isPrefabAsset && !string.IsNullOrEmpty(prefabPath))
        {
            // 加载预制体内容进行编辑
            prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            allTransforms = prefabRoot.GetComponentsInChildren<Transform>(m_IncludeInactive);
        }

        bool prefabDirty = false;

        foreach (var t in allTransforms)
        {
            var node = t.gameObject;

            // 跳过已有任意 Collider 的节点
            if (node.GetComponent<Collider>() != null)
            {
                skipped++;
                m_Log.Add($"  ○ 跳过（已有Collider）: {GetPath(node)}");
                continue;
            }

            // 如果选项要求只处理有 MeshFilter 的节点
            if (m_OnlyWithMeshFilter && node.GetComponent<MeshFilter>() == null)
            {
                skipped++;
                continue;
            }

            // 添加 MeshCollider
            var mc = Undo.AddComponent<MeshCollider>(node);
            mc.convex = m_ConvexCollider;

            // 如果有 MeshFilter，自动绑定 sharedMesh
            var mf = node.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
                mc.sharedMesh = mf.sharedMesh;

            if (isPrefabAsset)
            {
                EditorUtility.SetDirty(node);
                prefabDirty = true;
            }

            added++;
            m_Log.Add($"  ✓ 添加 MeshCollider: {GetPath(node)}");
        }

        // 保存预制体修改
        if (isPrefabAsset && prefabRoot != null)
        {
            if (prefabDirty)
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static string GetPath(GameObject go)
    {
        var parts = new List<string>();
        var t = go.transform;
        while (t != null)
        {
            parts.Insert(0, t.name);
            t = t.parent;
        }
        return string.Join("/", parts);
    }

    private void InitStyles()
    {
        if (m_StylesInit)
            return;
        m_SectionStyle = new GUIStyle(EditorStyles.helpBox);
        m_LogSuccessStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(0.4f, 0.9f, 0.4f) },
        };
        m_LogSkipStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(0.6f, 0.6f, 0.6f) },
        };
        m_StylesInit = true;
    }
}
#endif
