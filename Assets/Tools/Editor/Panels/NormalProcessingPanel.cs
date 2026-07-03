using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 法线处理工具 - 集成平滑法线生成和MeshRenderer显示/隐藏功能
/// </summary>
[ToolHubItem("模型工具/法线处理工具", "平滑法线生成 + MeshRenderer显示/隐藏", 20)]
public class NormalProcessingPanel : IToolHubPanel
{
    // ========== 共用变量 ==========
    private List<Object> targetAssets = new List<Object>();
    private List<GameObject> sceneObjects = new List<GameObject>();
    private Vector2 scrollPosition;

    // ========== 平滑法线相关 ==========
    private bool autoApplyToScene = true;
    private bool showSuccessDialog = true;
    private int processedCount = 0;
    private int totalCount = 0;
    private bool isProcessing = false;

    // ========== MeshRenderer相关 ==========
    private int totalRenderers = 0;
    private int visibleRenderers = 0;
    private int hiddenRenderers = 0;

    // ========== UI状态 ==========
    private enum ToolMode
    {
        SmoothNormal,
        MeshRendererVisibility
    }
    private ToolMode currentMode = ToolMode.SmoothNormal;

    public void OnEnable() { }
    public void OnDisable() { }
    public void OnDestroy() { }

    public string GetHelpText() =>
        "法线处理工具：\n" +
        "• 平滑法线：将平滑法线烘焙到模型的切线空间，用于描边等效果\n" +
        "• MeshRenderer控制：批量显示/隐藏对象及其所有子对象的MeshRenderer（递归）";

    public void OnGUI()
    {
        EditorGUILayout.Space(5);

        // 模式切换
        DrawModeSelector();
        EditorGUILayout.Space(5);

        // 帮助信息
        DrawHelpBox();
        EditorGUILayout.Space(5);

        // 拖拽区域
        DrawDropArea();
        EditorGUILayout.Space(5);

        // 对象列表
        DrawObjectList();
        EditorGUILayout.Space(5);

        // 根据模式显示不同的内容
        if (currentMode == ToolMode.SmoothNormal)
        {
            DrawSmoothNormalOptions();
            EditorGUILayout.Space(5);
            DrawSmoothNormalActions();
            DrawSmoothNormalProgress();
        }
        else
        {
            DrawMeshRendererStatistics();
            EditorGUILayout.Space(5);
            DrawMeshRendererActions();
        }
    }

    // ========== 模式切换 ==========

    private void DrawModeSelector()
    {
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = currentMode == ToolMode.SmoothNormal ? Color.green : Color.white;
        if (GUILayout.Button("🔧 平滑法线生成", GUILayout.Height(30)))
        {
            currentMode = ToolMode.SmoothNormal;
            UpdateStatistics();
        }

        GUI.backgroundColor = currentMode == ToolMode.MeshRendererVisibility ? Color.green : Color.white;
        if (GUILayout.Button("👁️ MeshRenderer控制", GUILayout.Height(30)))
        {
            currentMode = ToolMode.MeshRendererVisibility;
            UpdateStatistics();
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
    }

    // ========== 通用UI ==========

    private void DrawHelpBox()
    {
        string helpText = currentMode == ToolMode.SmoothNormal
            ? "拖拽方式：\n" +
              "• 从 Project 窗口拖拽：文件夹、模型文件、Prefab\n" +
              "• 从 Hierarchy 窗口拖拽：场景中的游戏对象\n" +
              "• 生成的Mesh会保存在原文件同目录下"
            : "拖拽方式：\n" +
              "• 从 Project 窗口拖拽：文件夹、预制体\n" +
              "• 从 Hierarchy 窗口拖拽：场景中的游戏对象\n" +
              "• 操作会递归处理对象及其所有子对象的MeshRenderer";

        EditorGUILayout.HelpBox(helpText, MessageType.Info);
    }

    private void DrawDropArea()
    {
        Event evt = Event.current;
        Rect dropArea = GUILayoutUtility.GetRect(0, 60, GUILayout.ExpandWidth(true));

        Color originalColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.3f, 0.5f, 0.8f, 0.3f);

        string dropText = currentMode == ToolMode.SmoothNormal
            ? "🎯 拖拽文件夹、模型文件或场景对象到这里"
            : "🎯 拖拽文件夹、预制体或场景对象到这里";

        GUI.Box(dropArea, dropText, EditorStyles.helpBox);
        GUI.backgroundColor = originalColor;

        switch (evt.type)
        {
            case EventType.DragUpdated:
            case EventType.DragPerform:
                if (!dropArea.Contains(evt.mousePosition)) break;

                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();

                    foreach (Object draggedObject in DragAndDrop.objectReferences)
                    {
                        if (draggedObject is GameObject go)
                        {
                            if (IsSceneObject(go))
                                AddSceneObject(go);
                            else
                                AddAsset(draggedObject);
                        }
                        else
                        {
                            AddAsset(draggedObject);
                        }
                    }

                    UpdateStatistics();
                    evt.Use();
                }
                break;
        }
    }

    private void DrawObjectList()
    {
        int totalItems = targetAssets.Count + sceneObjects.Count;

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField($"📦 待处理项目 ({totalItems})", EditorStyles.boldLabel);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(120));

        // 显示资产文件
        if (targetAssets.Count > 0)
        {
            EditorGUILayout.LabelField("📁 资产文件:", EditorStyles.miniLabel);
            for (int i = targetAssets.Count - 1; i >= 0; i--)
            {
                if (targetAssets[i] == null)
                {
                    targetAssets.RemoveAt(i);
                    continue;
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(targetAssets[i], typeof(Object), false);

                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    targetAssets.RemoveAt(i);
                    UpdateStatistics();
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        // 显示场景对象
        if (sceneObjects.Count > 0)
        {
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("🎮 场景对象:", EditorStyles.miniLabel);

            for (int i = sceneObjects.Count - 1; i >= 0; i--)
            {
                if (sceneObjects[i] == null)
                {
                    sceneObjects.RemoveAt(i);
                    continue;
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(sceneObjects[i], typeof(GameObject), true);

                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    sceneObjects.RemoveAt(i);
                    UpdateStatistics();
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        if (totalItems == 0)
        {
            EditorGUILayout.LabelField("列表为空，请拖入对象", EditorStyles.centeredGreyMiniLabel);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    // ========== 平滑法线UI ==========

    private void DrawSmoothNormalOptions()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("⚙️ 选项", EditorStyles.boldLabel);
        autoApplyToScene = EditorGUILayout.Toggle("自动应用到场景物体", autoApplyToScene);
        showSuccessDialog = EditorGUILayout.Toggle("显示完成提示", showSuccessDialog);
        EditorGUILayout.EndVertical();
    }

    private void DrawSmoothNormalActions()
    {
        EditorGUI.BeginDisabledGroup((targetAssets.Count == 0 && sceneObjects.Count == 0) || isProcessing);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("🗑️ 清空列表", GUILayout.Height(30)))
        {
            targetAssets.Clear();
            sceneObjects.Clear();
            UpdateStatistics();
        }

        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("🚀 生成平滑法线", GUILayout.Height(30)))
        {
            ProcessSmoothNormal();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        EditorGUI.EndDisabledGroup();
    }

    private void DrawSmoothNormalProgress()
    {
        if (!isProcessing) return;

        EditorGUILayout.Space(5);
        float progress = totalCount > 0 ? (float)processedCount / totalCount : 0;
        EditorGUI.ProgressBar(
            EditorGUILayout.GetControlRect(GUILayout.Height(20)),
            progress,
            $"处理中... {processedCount}/{totalCount}"
        );
    }

    // ========== MeshRenderer UI ==========

    private void DrawMeshRendererStatistics()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("📊 统计信息", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"总计 MeshRenderer: {totalRenderers}");
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("●", new GUIStyle(EditorStyles.label) { normal = { textColor = Color.green } }, GUILayout.Width(15));
        EditorGUILayout.LabelField($"可见: {visibleRenderers}");
        GUILayout.FlexibleSpace();
        GUILayout.Label("●", new GUIStyle(EditorStyles.label) { normal = { textColor = Color.red } }, GUILayout.Width(15));
        EditorGUILayout.LabelField($"隐藏: {hiddenRenderers}");
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawMeshRendererActions()
    {
        bool hasObjects = targetAssets.Count > 0 || sceneObjects.Count > 0;

        EditorGUI.BeginDisabledGroup(!hasObjects);

        EditorGUILayout.BeginHorizontal();

        // 显示按钮
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("👁️ 显示所有 MeshRenderer", GUILayout.Height(35)))
        {
            SetMeshRenderersVisibility(true);
        }

        // 隐藏按钮
        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("🚫 隐藏所有 MeshRenderer", GUILayout.Height(35)))
        {
            SetMeshRenderersVisibility(false);
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(3);

        EditorGUILayout.BeginHorizontal();

        // 刷新统计
        if (GUILayout.Button("🔄 刷新统计", GUILayout.Height(25)))
        {
            UpdateStatistics();
        }

        // 清空列表
        if (GUILayout.Button("🗑️ 清空列表", GUILayout.Height(25)))
        {
            targetAssets.Clear();
            sceneObjects.Clear();
            UpdateStatistics();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUI.EndDisabledGroup();
    }

    // ========== 平滑法线处理逻辑 ==========

    private void ProcessSmoothNormal()
    {
        isProcessing = true;
        processedCount = 0;
        totalCount = 0;

        var meshMapping = new Dictionary<Mesh, Mesh>();
        var meshesToProcess = new List<Mesh>();

        foreach (Object obj in targetAssets)
            CollectMeshes(obj, meshesToProcess);
        foreach (GameObject go in sceneObjects)
            CollectMeshesFromSceneObject(go, meshesToProcess);

        totalCount = meshesToProcess.Count;

        if (totalCount == 0)
        {
            EditorUtility.DisplayDialog("提示", "未找到可处理的Mesh", "确定");
            isProcessing = false;
            return;
        }

        foreach (Mesh originalMesh in meshesToProcess)
        {
            try
            {
                Mesh newMesh = ProcessMesh(originalMesh);
                if (newMesh != null)
                    meshMapping[originalMesh] = newMesh;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"处理 {originalMesh.name} 时出错: {e.Message}");
            }
            processedCount++;
        }

        if (autoApplyToScene && meshMapping.Count > 0)
            ApplyToScene(meshMapping);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        isProcessing = false;

        if (showSuccessDialog)
            EditorUtility.DisplayDialog("完成", $"成功处理 {meshMapping.Count} 个Mesh", "确定");
    }

    private void CollectMeshes(Object obj, List<Mesh> meshList)
    {
        if (obj is Mesh mesh && !meshList.Contains(mesh))
        {
            meshList.Add(mesh);
            return;
        }

        string path = AssetDatabase.GetAssetPath(obj);
        if (!string.IsNullOrEmpty(path))
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Mesh m && !meshList.Contains(m))
                    meshList.Add(m);
            }
        }
    }

    private void CollectMeshesFromSceneObject(GameObject go, List<Mesh> meshList)
    {
        foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
            if (mf.sharedMesh != null && !meshList.Contains(mf.sharedMesh))
                meshList.Add(mf.sharedMesh);

        foreach (SkinnedMeshRenderer smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
            if (smr.sharedMesh != null && !meshList.Contains(smr.sharedMesh))
                meshList.Add(smr.sharedMesh);
    }

    private Mesh ProcessMesh(Mesh originalMesh)
    {
        Mesh newMesh = Object.Instantiate(originalMesh);
        newMesh.name = originalMesh.name + "_SmoothNormal";
        BakeSmoothNormalToTangent(newMesh);

        string originalPath = AssetDatabase.GetAssetPath(originalMesh);
        string directory, fileName;

        if (string.IsNullOrEmpty(originalPath))
        {
            directory = "Assets/GeneratedMeshes";
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            fileName = originalMesh.name + "_SmoothNormal.asset";
        }
        else
        {
            directory = Path.GetDirectoryName(originalPath);
            fileName = Path.GetFileNameWithoutExtension(originalPath) + "_SmoothNormal.asset";
        }

        string newPath = Path.Combine(directory, fileName).Replace("\\", "/");

        if (File.Exists(newPath))
        {
            if (!EditorUtility.DisplayDialog("文件已存在", $"{fileName} 已存在，是否覆盖？", "覆盖", "跳过"))
                return null;
            AssetDatabase.DeleteAsset(newPath);
        }

        AssetDatabase.CreateAsset(newMesh, newPath);
        return newMesh;
    }

    private void ApplyToScene(Dictionary<Mesh, Mesh> meshMapping)
    {
        int replacedCount = 0;

        foreach (MeshFilter mf in Object.FindObjectsOfType<MeshFilter>())
        {
            if (meshMapping.ContainsKey(mf.sharedMesh))
            {
                Undo.RecordObject(mf, "Replace Mesh");
                mf.sharedMesh = meshMapping[mf.sharedMesh];
                replacedCount++;
            }
        }

        foreach (SkinnedMeshRenderer smr in Object.FindObjectsOfType<SkinnedMeshRenderer>())
        {
            if (meshMapping.ContainsKey(smr.sharedMesh))
            {
                Undo.RecordObject(smr, "Replace Mesh");
                smr.sharedMesh = meshMapping[smr.sharedMesh];
                replacedCount++;
            }
        }

        if (replacedCount > 0)
            Debug.Log($"✅ 已替换场景中 {replacedCount} 个物体的Mesh");
    }

    private static void BakeSmoothNormalToTangent(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        Vector4[] tangents = new Vector4[vertices.Length];

        var normalDict = new Dictionary<Vector3, Vector3>();

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 pos = vertices[i];
            if (!normalDict.ContainsKey(pos))
                normalDict[pos] = Vector3.zero;
            normalDict[pos] += normals[i];
        }

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 smoothNormal = normalDict[vertices[i]].normalized;
            tangents[i] = new Vector4(smoothNormal.x, smoothNormal.y, smoothNormal.z, 0);
        }

        mesh.tangents = tangents;
        EditorUtility.SetDirty(mesh);
    }

    // ========== MeshRenderer处理逻辑（增强递归处理） ==========

    /// <summary>
    /// 设置MeshRenderer可见性 - 递归处理所有子对象（增强刷新版）
    /// </summary>
    private void SetMeshRenderersVisibility(bool visible)
    {
        int changedCount = 0;
        var allRenderers = new List<MeshRenderer>();
        var affectedGameObjects = new HashSet<GameObject>(); // 收集所有受影响的GameObject

        // 从场景对象收集所有MeshRenderer（递归）
        foreach (var obj in sceneObjects)
        {
            if (obj != null)
            {
                var renderers = obj.GetComponentsInChildren<MeshRenderer>(true);
                allRenderers.AddRange(renderers);

                // 收集所有受影响的GameObject
                foreach (var r in renderers)
                {
                    if (r != null)
                        affectedGameObjects.Add(r.gameObject);
                }

                Debug.Log($"[场景对象] {obj.name}: 找到 {renderers.Length} 个 MeshRenderer（递归）");
            }
        }

        // 从预制体收集所有MeshRenderer（递归）
        foreach (var asset in targetAssets)
        {
            if (asset is GameObject prefab)
            {
                var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
                allRenderers.AddRange(renderers);

                Debug.Log($"[预制体] {prefab.name}: 找到 {renderers.Length} 个 MeshRenderer（递归）");
            }
        }

        if (allRenderers.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "没有找到任何 MeshRenderer 组件", "确定");
            return;
        }

        // 去重
        var uniqueRenderers = allRenderers.Distinct().ToArray();

        Debug.Log($"总计找到 {allRenderers.Count} 个 MeshRenderer，去重后 {uniqueRenderers.Length} 个");

        // 记录Undo
        Undo.RecordObjects(uniqueRenderers, visible ? "显示 MeshRenderer" : "隐藏 MeshRenderer");

        // 设置可见性
        foreach (var renderer in uniqueRenderers)
        {
            if (renderer != null && renderer.enabled != visible)
            {
                renderer.enabled = visible;
                EditorUtility.SetDirty(renderer);
                EditorUtility.SetDirty(renderer.gameObject);
                changedCount++;

                string path = GetGameObjectPath(renderer.gameObject);
                Debug.Log($"  └─ {(visible ? "显示" : "隐藏")}: {path}");
            }
        }

        // ========== 多重刷新机制 ==========

        // 1. 标记所有受影响的GameObject为Dirty
        foreach (var go in affectedGameObjects)
        {
            if (go != null)
            {
                EditorUtility.SetDirty(go);
            }
        }

        // 2. 刷新场景视图（立即）
        SceneView.RepaintAll();

        // 3. 刷新所有编辑器视图
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

        // 4. 标记场景为已修改
        if (sceneObjects.Count > 0)
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
        }

        // 5. 刷新Hierarchy窗口
        EditorApplication.DirtyHierarchyWindowSorting();
        EditorApplication.RepaintHierarchyWindow();

        // 6. 如果是预制体，保存修改
        foreach (var asset in targetAssets)
        {
            if (asset is GameObject prefab)
            {
                EditorUtility.SetDirty(prefab);
            }
        }

        // 7. 保存并刷新资源
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 8. 延迟刷新（确保所有更改都被应用）
        EditorApplication.delayCall += () =>
        {
            SceneView.RepaintAll();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            Debug.Log("✅ 延迟刷新完成");
        };

        // ========== 刷新结束 ==========

        UpdateStatistics();

        string action = visible ? "显示" : "隐藏";
        EditorUtility.DisplayDialog("完成",
            $"已{action} {changedCount} 个 MeshRenderer\n" +
            $"（从 {sceneObjects.Count} 个场景对象和 {targetAssets.Count} 个资产中递归搜索）\n\n" +
            $"场景视图已自动刷新",
            "确定");

        Debug.Log($"✅ MeshRenderer可见性已更新：{action} {changedCount} 个组件（递归处理）");
        Debug.Log($"✅ 场景和资源已刷新（包括延迟刷新）");
    }

    /// <summary>
    /// 更新统计信息 - 递归统计所有MeshRenderer
    /// </summary>
    private void UpdateStatistics()
    {
        if (currentMode == ToolMode.MeshRendererVisibility)
        {
            totalRenderers = 0;
            visibleRenderers = 0;
            hiddenRenderers = 0;

            // 统计场景对象（递归）
            foreach (var obj in sceneObjects)
            {
                if (obj != null)
                {
                    // GetComponentsInChildren(true) 递归搜索所有子对象
                    // true 参数包括未激活的GameObject
                    var renderers = obj.GetComponentsInChildren<MeshRenderer>(true);
                    totalRenderers += renderers.Length;
                    visibleRenderers += renderers.Count(r => r.enabled);
                    hiddenRenderers += renderers.Count(r => !r.enabled);
                }
            }

            // 统计预制体（递归）
            foreach (var asset in targetAssets)
            {
                if (asset is GameObject prefab)
                {
                    var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
                    totalRenderers += renderers.Length;
                    visibleRenderers += renderers.Count(r => r.enabled);
                    hiddenRenderers += renderers.Count(r => !r.enabled);
                }
            }

            Debug.Log($"[统计] 总计: {totalRenderers}, 可见: {visibleRenderers}, 隐藏: {hiddenRenderers}");
        }
    }

    /// <summary>
    /// 获取GameObject的完整层级路径（用于日志输出）
    /// </summary>
    private string GetGameObjectPath(GameObject go)
    {
        if (go == null) return "null";

        string path = go.name;
        Transform current = go.transform.parent;

        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    // ========== 通用辅助方法 ==========

    private bool IsSceneObject(GameObject go)
    {
        return string.IsNullOrEmpty(AssetDatabase.GetAssetPath(go));
    }

    private void AddSceneObject(GameObject go)
    {
        if (go == null) return;

        if (currentMode == ToolMode.SmoothNormal)
        {
            // 平滑法线模式：检查MeshFilter或SkinnedMeshRenderer（递归）
            var childMF = go.GetComponentsInChildren<MeshFilter>(true);
            var childSMR = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            if (childMF.Length > 0 || childSMR.Length > 0)
            {
                if (!sceneObjects.Contains(go))
                {
                    sceneObjects.Add(go);
                    Debug.Log($"已添加场景对象: {go.name} (包含 {childMF.Length} 个 MeshFilter, {childSMR.Length} 个 SkinnedMeshRenderer)");
                }
            }
            else
            {
                Debug.LogWarning($"对象 '{go.name}' 及其子对象中没有找到 MeshFilter 或 SkinnedMeshRenderer 组件");
            }
        }
        else
        {
            // MeshRenderer模式：检查MeshRenderer（递归）
            var renderers = go.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length > 0)
            {
                if (!sceneObjects.Contains(go))
                {
                    sceneObjects.Add(go);
                    Debug.Log($"已添加场景对象: {go.name} (递归找到 {renderers.Length} 个 MeshRenderer)");

                    // 输出详细的层级结构
                    foreach (var r in renderers)
                    {
                        Debug.Log($"  └─ {GetGameObjectPath(r.gameObject)} [{(r.enabled ? "可见" : "隐藏")}]");
                    }
                }
            }
            else
            {
                Debug.LogWarning($"对象 '{go.name}' 及其所有子对象中没有找到 MeshRenderer 组件");
            }
        }
    }

    private void AddAsset(Object obj)
    {
        if (obj == null) return;

        string path = AssetDatabase.GetAssetPath(obj);
        if (string.IsNullOrEmpty(path)) return;

        if (AssetDatabase.IsValidFolder(path))
        {
            if (currentMode == ToolMode.SmoothNormal)
            {
                // 平滑法线模式：查找模型和Mesh
                string[] guids = AssetDatabase.FindAssets("t:Model t:Mesh t:GameObject", new[] { path });
                foreach (string guid in guids)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                    if (asset != null && ContainsMesh(asset) && !targetAssets.Contains(asset))
                        targetAssets.Add(asset);
                }
            }
            else
            {
                // MeshRenderer模式：查找GameObject/预制体（递归）
                string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { path });
                int addedCount = 0;

                foreach (string guid in guids)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

                    if (asset != null && HasMeshRenderer(asset) && !targetAssets.Contains(asset))
                    {
                        targetAssets.Add(asset);
                        addedCount++;

                        // 输出详细信息
                        var renderers = asset.GetComponentsInChildren<MeshRenderer>(true);
                        Debug.Log($"已添加预制体: {asset.name} (递归找到 {renderers.Length} 个 MeshRenderer)");
                    }
                }

                Debug.Log($"从文件夹 '{path}' 添加了 {addedCount} 个预制体");
            }
        }
        else
        {
            if (currentMode == ToolMode.SmoothNormal)
            {
                if (ContainsMesh(obj) && !targetAssets.Contains(obj))
                    targetAssets.Add(obj);
            }
            else
            {
                if (obj is GameObject go && HasMeshRenderer(go))
                {
                    if (!targetAssets.Contains(obj))
                    {
                        targetAssets.Add(obj);
                        var renderers = go.GetComponentsInChildren<MeshRenderer>(true);
                        Debug.Log($"已添加预制体: {go.name} (递归找到 {renderers.Length} 个 MeshRenderer)");

                        // 输出详细的层级结构
                        foreach (var r in renderers)
                        {
                            Debug.Log($"  └─ {GetGameObjectPath(r.gameObject)} [{(r.enabled ? "可见" : "隐藏")}]");
                        }
                    }
                }
            }
        }
    }

    private bool ContainsMesh(Object obj)
    {
        if (obj is Mesh) return true;
        if (obj is GameObject go)
            return go.GetComponentInChildren<MeshFilter>() != null ||
                   go.GetComponentInChildren<SkinnedMeshRenderer>() != null;

        string path = AssetDatabase.GetAssetPath(obj);
        if (!string.IsNullOrEmpty(path))
            return AssetDatabase.LoadAllAssetsAtPath(path).Any(a => a is Mesh);
        return false;
    }

    /// <summary>
    /// 检查GameObject是否包含MeshRenderer（递归检查所有子对象）
    /// </summary>
    private bool HasMeshRenderer(GameObject go)
    {
        // GetComponentsInChildren(true) 会递归搜索所有子对象
        // true 参数表示包括未激活的GameObject
        return go.GetComponentsInChildren<MeshRenderer>(true).Length > 0;
    }
}