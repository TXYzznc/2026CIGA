using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 平滑法线生成器面板 - 可集成到工具箱，也可独立使用
/// </summary>
[ToolHubItem("模型工具/平滑法线生成器", "批量处理模型的平滑法线并烘焙到切线空间", 20)]
public class SmoothNormalPanel : IToolHubPanel
{
    private List<Object> targetAssets = new List<Object>();
    private List<GameObject> sceneObjects = new List<GameObject>();
    private Vector2 scrollPosition;
    private bool autoApplyToScene = true;
    private bool showSuccessDialog = true;
    
    private int processedCount = 0;
    private int totalCount = 0;
    private bool isProcessing = false;

    public void OnEnable() { }
    public void OnDisable() { }
    public void OnDestroy() { }
    public string GetHelpText() => "将平滑法线烘焙到模型的切线空间，用于描边等效果";

    public void OnGUI()
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox(
            "拖拽方式：\n" +
            "• 从 Project 窗口拖拽：文件夹、模型文件、Prefab\n" +
            "• 从 Hierarchy 窗口拖拽：场景中的游戏对象\n" +
            "• 生成的Mesh会保存在原文件同目录下",
            MessageType.Info
        );

        EditorGUILayout.Space(5);
        DrawDropArea();
        EditorGUILayout.Space(5);
        DrawOptions();
        EditorGUILayout.Space(5);
        DrawAssetList();
        EditorGUILayout.Space(5);
        DrawActionButtons();
        DrawProgress();
    }

    private void DrawDropArea()
    {
        Event evt = Event.current;
        Rect dropArea = GUILayoutUtility.GetRect(0, 60, GUILayout.ExpandWidth(true));
        
        GUI.Box(dropArea, "🎯 拖拽文件夹、模型文件或场景对象到这里", EditorStyles.helpBox);
        
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
                            if (IsSceneObject(go)) AddSceneObject(go);
                            else AddAsset(draggedObject);
                        }
                        else AddAsset(draggedObject);
                    }
                    evt.Use();
                }
                break;
        }
    }

    private void DrawOptions()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("⚙️ 选项", EditorStyles.boldLabel);
        autoApplyToScene = EditorGUILayout.Toggle("自动应用到场景物体", autoApplyToScene);
        showSuccessDialog = EditorGUILayout.Toggle("显示完成提示", showSuccessDialog);
        EditorGUILayout.EndVertical();
    }

    private void DrawAssetList()
    {
        int totalItems = targetAssets.Count + sceneObjects.Count;
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField($"📦 待处理项目 ({totalItems})", EditorStyles.boldLabel);
        
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(120));
        
        if (targetAssets.Count > 0)
        {
            EditorGUILayout.LabelField("📁 资产文件:", EditorStyles.miniLabel);
            for (int i = targetAssets.Count - 1; i >= 0; i--)
            {
                if (targetAssets[i] == null) { targetAssets.RemoveAt(i); continue; }
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(targetAssets[i], typeof(Object), false);
                if (GUILayout.Button("×", GUILayout.Width(25))) targetAssets.RemoveAt(i);
                EditorGUILayout.EndHorizontal();
            }
        }
        
        if (sceneObjects.Count > 0)
        {
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("🎮 场景对象:", EditorStyles.miniLabel);
            for (int i = sceneObjects.Count - 1; i >= 0; i--)
            {
                if (sceneObjects[i] == null) { sceneObjects.RemoveAt(i); continue; }
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(sceneObjects[i], typeof(GameObject), true);
                if (GUILayout.Button("×", GUILayout.Width(25))) sceneObjects.RemoveAt(i);
                EditorGUILayout.EndHorizontal();
            }
        }
        
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawActionButtons()
    {
        EditorGUI.BeginDisabledGroup((targetAssets.Count == 0 && sceneObjects.Count == 0) || isProcessing);
        
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("🗑️ 清空列表", GUILayout.Height(30)))
        {
            targetAssets.Clear();
            sceneObjects.Clear();
        }
        
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("🚀 开始处理", GUILayout.Height(30)))
        {
            ProcessAllAssets();
        }
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUI.EndDisabledGroup();
    }

    private void DrawProgress()
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

    private bool IsSceneObject(GameObject go) => string.IsNullOrEmpty(AssetDatabase.GetAssetPath(go));

    private void AddSceneObject(GameObject go)
    {
        MeshFilter mf = go.GetComponent<MeshFilter>();
        SkinnedMeshRenderer smr = go.GetComponent<SkinnedMeshRenderer>();
        
        if (mf != null || smr != null)
        {
            if (!sceneObjects.Contains(go)) sceneObjects.Add(go);
        }
        else
        {
            var childMF = go.GetComponentsInChildren<MeshFilter>();
            var childSMR = go.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (childMF.Length > 0 || childSMR.Length > 0)
            {
                if (!sceneObjects.Contains(go)) sceneObjects.Add(go);
            }
        }
    }

    private void AddAsset(Object obj)
    {
        string path = AssetDatabase.GetAssetPath(obj);
        if (string.IsNullOrEmpty(path)) return;

        if (AssetDatabase.IsValidFolder(path))
        {
            string[] guids = AssetDatabase.FindAssets("t:Model t:Mesh t:GameObject", new[] { path });
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                if (asset != null && ContainsMesh(asset) && !targetAssets.Contains(asset))
                    targetAssets.Add(asset);
            }
        }
        else if (ContainsMesh(obj) && !targetAssets.Contains(obj))
        {
            targetAssets.Add(obj);
        }
    }

    private bool ContainsMesh(Object obj)
    {
        if (obj is Mesh) return true;
        if (obj is GameObject go)
            return go.GetComponentInChildren<MeshFilter>() != null || go.GetComponentInChildren<SkinnedMeshRenderer>() != null;
        
        string path = AssetDatabase.GetAssetPath(obj);
        if (!string.IsNullOrEmpty(path))
            return AssetDatabase.LoadAllAssetsAtPath(path).Any(a => a is Mesh);
        return false;
    }

    private void ProcessAllAssets()
    {
        isProcessing = true;
        processedCount = 0;
        totalCount = 0;

        var meshMapping = new Dictionary<Mesh, Mesh>();
        var meshesToProcess = new List<Mesh>();
        
        foreach (Object obj in targetAssets) CollectMeshes(obj, meshesToProcess);
        foreach (GameObject go in sceneObjects) CollectMeshesFromSceneObject(go, meshesToProcess);

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
                if (newMesh != null) meshMapping[originalMesh] = newMesh;
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
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
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
            if (!normalDict.ContainsKey(pos)) normalDict[pos] = Vector3.zero;
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
}
