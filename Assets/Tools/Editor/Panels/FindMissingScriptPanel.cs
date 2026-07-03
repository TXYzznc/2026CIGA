using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 缺失脚本GUID扫描面板 - 可集成到工具箱，也可独立使用
/// </summary>
[ToolHubItem("诊断工具/缺失脚本GUID扫描", "扫描预制体中的缺失脚本GUID引用", 20)]
public class FindMissingScriptPanel : IToolHubPanel
{
    private Vector2 scrollPosition;
    private List<ScanResult> results = new List<ScanResult>();
    private bool isScanning = false;

    // 场景扫描
    private Vector2 sceneScrollPosition;
    private List<GameObject> missingScriptObjects = new List<GameObject>();
    private bool hasScannedScene = false;

    // 匹配类似：m_Script: {fileID: 11500000, guid: xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx, type: 3}
    private static readonly Regex ScriptGuidRegex =
        new Regex(@"m_Script:\s*\{[^}]*guid:\s*([0-9a-fA-F]{32})", RegexOptions.Compiled);

    private class ScanResult
    {
        public string PrefabPath;
        public string ScriptGuid;
        public string ScriptPath;
        public string MonoScriptName;
        public string ClassName;
        public bool IsMissing;
    }

    public void OnEnable()
    {
        results = new List<ScanResult>();
    }

    public void OnDisable()
    {
        results.Clear();
        missingScriptObjects.Clear();
    }

    public void OnDestroy()
    {
        results.Clear();
        missingScriptObjects.Clear();
    }

    public string GetHelpText()
    {
        return "扫描项目中的预制体，查找缺失的脚本GUID引用。";
    }

    public void OnGUI()
    {
        // ── 场景对象缺失组件扫描 ──
        EditorGUILayout.LabelField("场景对象缺失组件扫描", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox("扫描当前已打开场景中的所有 GameObject，列出含有缺失组件的对象。", MessageType.Info);
        EditorGUILayout.Space(4);

        if (GUILayout.Button("扫描当前场景", GUILayout.Height(32)))
        {
            ScanCurrentScene();
        }

        EditorGUILayout.Space(4);
        DrawSceneResults();

        EditorGUILayout.Space(16);

        // ── 预制体 GUID 缺失扫描 ──
        EditorGUILayout.LabelField("缺失脚本GUID扫描（预制体/场景文件）", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        EditorGUILayout.HelpBox("扫描项目中所有预制体，查找缺失的脚本GUID引用。", MessageType.Info);
        EditorGUILayout.Space(4);

        using (new EditorGUI.DisabledScope(isScanning))
        {
            if (GUILayout.Button("开始扫描", GUILayout.Height(32)))
            {
                ExecuteScan();
            }
        }

        if (isScanning)
        {
            EditorGUILayout.LabelField("扫描中...", EditorStyles.miniLabel);
        }

        EditorGUILayout.Space(10);

        DrawResults();
    }

    private void ScanCurrentScene()
    {
        missingScriptObjects.Clear();
        hasScannedScene = true;

        // 遍历所有已加载场景
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            foreach (var root in scene.GetRootGameObjects())
            {
                CollectMissingScriptObjects(root);
            }
        }

        Debug.Log($"[MissingScriptScan] 场景扫描完成，发现 {missingScriptObjects.Count} 个含缺失组件的对象");
    }

    private void CollectMissingScriptObjects(GameObject go)
    {
        var components = go.GetComponents<Component>();
        foreach (var comp in components)
        {
            if (comp == null)
            {
                missingScriptObjects.Add(go);
                break;
            }
        }

        for (int i = 0; i < go.transform.childCount; i++)
        {
            CollectMissingScriptObjects(go.transform.GetChild(i).gameObject);
        }
    }

    private void DrawSceneResults()
    {
        if (!hasScannedScene) return;

        if (missingScriptObjects.Count == 0)
        {
            EditorGUILayout.HelpBox("扫描完成！当前场景未发现含缺失组件的对象。", MessageType.Info);
            return;
        }

        var redStyle = new GUIStyle(EditorStyles.boldLabel);
        redStyle.normal.textColor = Color.red;
        EditorGUILayout.LabelField($"发现 {missingScriptObjects.Count} 个含缺失组件的对象", redStyle);
        EditorGUILayout.Space(4);

        sceneScrollPosition = EditorGUILayout.BeginScrollView(sceneScrollPosition, GUILayout.MaxHeight(240));

        foreach (var go in missingScriptObjects)
        {
            if (go == null) continue;

            using (new EditorGUILayout.HorizontalScope("box"))
            {
                if (GUILayout.Button("选中", GUILayout.Width(48), GUILayout.Height(20)))
                {
                    Selection.activeGameObject = go;
                    EditorGUIUtility.PingObject(go);
                }

                // 显示完整层级路径
                EditorGUILayout.LabelField(GetGameObjectPath(go), EditorStyles.miniLabel);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private static string GetGameObjectPath(GameObject go)
    {
        var sb = new System.Text.StringBuilder(go.name);
        var t = go.transform.parent;
        while (t != null)
        {
            sb.Insert(0, t.name + "/");
            t = t.parent;
        }
        return sb.ToString();
    }

    private void ExecuteScan()
    {
        isScanning = true;
        results.Clear();

        // 扫描预制体和场景文件
        var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        var sceneGuids = AssetDatabase.FindAssets("t:Scene");
        int hitPrefabCount = 0;

        // 扫描预制体
        foreach (var prefabGuid in prefabGuids)
        {
            var prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
            if (string.IsNullOrEmpty(prefabPath) || !prefabPath.EndsWith(".prefab"))
                continue;

            ScanAsset(prefabPath, ref hitPrefabCount);
        }

        // 扫描场景文件
        foreach (var sceneGuid in sceneGuids)
        {
            var scenePath = AssetDatabase.GUIDToAssetPath(sceneGuid);
            if (string.IsNullOrEmpty(scenePath) || !scenePath.EndsWith(".unity"))
                continue;

            ScanAsset(scenePath, ref hitPrefabCount);
        }

        Debug.Log($"[MissingScriptScan] 扫描完成。发现 {hitPrefabCount} 个资源包含缺失脚本，共 {results.Count} 处缺失引用");
        isScanning = false;
    }

    private void ScanAsset(string assetPath, ref int hitPrefabCount)
    {
        string text;
        try
        {
            text = File.ReadAllText(assetPath);
        }
        catch
        {
            return;
        }

        var matches = ScriptGuidRegex.Matches(text);
        if (matches.Count == 0) return;

        // 收集该资源里出现过的脚本 guid
        var uniq = new HashSet<string>();
        foreach (Match m in matches) uniq.Add(m.Groups[1].Value);

        bool anyMissing = false;
        foreach (var guid in uniq)
        {
            var scriptPath = AssetDatabase.GUIDToAssetPath(guid);

            // 只记录缺失的脚本
            if (string.IsNullOrEmpty(scriptPath))
            {
                anyMissing = true;
                var result = new ScanResult
                {
                    PrefabPath = assetPath,
                    ScriptGuid = guid,
                    ScriptPath = "",
                    MonoScriptName = "",
                    ClassName = "",
                    IsMissing = true
                };
                results.Add(result);
                Debug.LogWarning($"[MissingScript] Asset: {assetPath}\n  Script GUID: {guid}\n  (asset not found in project)");
            }
        }

        if (anyMissing) hitPrefabCount++;
    }

    private void DrawResults()
    {
        if (results.Count == 0)
        {
            EditorGUILayout.HelpBox("扫描完成！未发现缺失脚本引用。", MessageType.Info);
            return;
        }

        // 按资源路径分组统计
        var groupedResults = new Dictionary<string, List<ScanResult>>();
        foreach (var result in results)
        {
            if (!groupedResults.ContainsKey(result.PrefabPath))
                groupedResults[result.PrefabPath] = new List<ScanResult>();
            groupedResults[result.PrefabPath].Add(result);
        }

        EditorGUILayout.LabelField($"扫描结果：发现 {groupedResults.Count} 个资源包含缺失脚本，共 {results.Count} 处缺失引用", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);

        // 显示清理按钮
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("清除结果", GUILayout.Height(24)))
            {
                results.Clear();
            }
            if (GUILayout.Button("复制所有GUID", GUILayout.Height(24)))
            {
                CopyAllGuidsToClipboard();
            }
        }
        EditorGUILayout.Space(6);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        foreach (var kvp in groupedResults)
        {
            DrawAssetGroup(kvp.Key, kvp.Value);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawAssetGroup(string assetPath, List<ScanResult> groupResults)
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            // 资源路径标题行
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("定位", GUILayout.Width(48), GUILayout.Height(24)))
                {
                    var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                    if (obj != null)
                    {
                        EditorGUIUtility.PingObject(obj);
                        Selection.activeObject = obj;
                    }
                }

                var style = new GUIStyle(EditorStyles.boldLabel);
                style.normal.textColor = Color.red;
                EditorGUILayout.LabelField($"[{groupResults.Count}处缺失]", style, GUILayout.Width(80));

                EditorGUILayout.LabelField(assetPath, EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(3);

            // 显示该资源下的所有缺失GUID
            foreach (var result in groupResults)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(20);
                    EditorGUILayout.LabelField($"GUID: {result.ScriptGuid}", EditorStyles.miniLabel);
                    
                    if (GUILayout.Button("复制", GUILayout.Width(50)))
                    {
                        EditorGUIUtility.systemCopyBuffer = result.ScriptGuid;
                        Debug.Log($"已复制 GUID: {result.ScriptGuid}");
                    }
                }
            }
        }

        EditorGUILayout.Space(5);
    }

    private void CopyAllGuidsToClipboard()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("缺失脚本GUID列表：");
        sb.AppendLine();
        
        foreach (var result in results)
        {
            sb.AppendLine($"Asset: {result.PrefabPath}");
            sb.AppendLine($"  GUID: {result.ScriptGuid}");
            sb.AppendLine();
        }
        
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log($"已复制 {results.Count} 个缺失GUID到剪贴板");
    }

    private void DrawResultItem(ScanResult result)
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("定位", GUILayout.Width(48)))
                {
                    var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(result.PrefabPath);
                    if (obj != null)
                    {
                        EditorGUIUtility.PingObject(obj);
                        Selection.activeObject = obj;
                    }
                }

                string status = result.IsMissing ? "[缺失]" : "[存在]";
                var style = new GUIStyle(EditorStyles.label);
                style.normal.textColor = result.IsMissing ? Color.red : Color.green;
                EditorGUILayout.LabelField(status, style, GUILayout.Width(50));

                EditorGUILayout.LabelField(result.PrefabPath, EditorStyles.miniLabel);
            }

            EditorGUILayout.LabelField($"GUID: {result.ScriptGuid}", EditorStyles.miniLabel);

            if (!result.IsMissing)
            {
                EditorGUILayout.LabelField($"Path: {result.ScriptPath}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Script: {result.MonoScriptName}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Class: {result.ClassName}", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField("脚本资源未找到", EditorStyles.miniLabel);
            }
        }

        EditorGUILayout.Space(3);
    }
}
