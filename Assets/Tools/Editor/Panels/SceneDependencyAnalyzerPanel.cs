using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Collections.Generic;

[ToolHubItem("场景工具/场景依赖分析器", "分析场景依赖资源，支持筛选、移动和导出报告", 45)]
public class SceneDependencyAnalyzerPanel : IToolHubPanel
{
    private Object sceneAsset;
    private string targetFolder = "Assets/SceneAssets";
    private Vector2 scrollPos;
    private List<AssetEntry> assetEntries = new();
    private bool scanned;
    private long totalSize;
    private long selectedSize;
    private int selectedCount;

    // 分组折叠状态
    private Dictionary<string, bool> groupFoldouts = new();

    private enum SortMode { SizeDesc, SizeAsc, Name }
    private SortMode sortMode = SortMode.SizeDesc;

    public void OnEnable() { }

    public void OnDisable() { }

    public void OnDestroy() { }

    public string GetHelpText() => "分析指定场景的资源依赖，可按类型选择资源、移动到目标目录并导出报告。移动使用 AssetDatabase.MoveAsset，会保持 GUID。";

    public void OnGUI()
    {
        DrawToolbar();
        EditorGUILayout.Space(4);
        DrawAssetList();
        DrawFooter();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        EditorGUILayout.LabelField("场景文件:", GUILayout.Width(60));
        var newScene = EditorGUILayout.ObjectField(sceneAsset, typeof(SceneAsset), false, GUILayout.Width(200));
        if (newScene != sceneAsset)
        {
            sceneAsset = newScene;
            scanned = false;
            assetEntries.Clear();
        }

        if (GUILayout.Button("扫描依赖", EditorStyles.toolbarButton, GUILayout.Width(70)))
        {
            ScanDependencies();
        }

        GUILayout.FlexibleSpace();

        EditorGUILayout.LabelField("排序:", GUILayout.Width(30));
        if (GUILayout.Button("大小↓", sortMode == SortMode.SizeDesc ? EditorStyles.toolbarButton : EditorStyles.toolbarButton, GUILayout.Width(45)))
        {
            sortMode = SortMode.SizeDesc;
            SortEntries();
        }
        if (GUILayout.Button("大小↑", EditorStyles.toolbarButton, GUILayout.Width(45)))
        {
            sortMode = SortMode.SizeAsc;
            SortEntries();
        }
        if (GUILayout.Button("名称", EditorStyles.toolbarButton, GUILayout.Width(40)))
        {
            sortMode = SortMode.Name;
            SortEntries();
        }

        EditorGUILayout.EndHorizontal();

        // 目标文件夹
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("目标文件夹:", GUILayout.Width(70));
        targetFolder = EditorGUILayout.TextField(targetFolder);
        if (GUILayout.Button("浏览", GUILayout.Width(50)))
        {
            string selected = EditorUtility.OpenFolderPanel("选择目标文件夹", Application.dataPath, "");
            if (!string.IsNullOrEmpty(selected) && selected.Contains("Assets"))
            {
                int assetsIndex = selected.IndexOf("Assets");
                targetFolder = "Assets" + selected.Substring(assetsIndex + 6);
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawAssetList()
    {
        if (!scanned)
        {
            EditorGUILayout.HelpBox("请选择一个场景文件并点击「扫描依赖」", MessageType.Info);
            return;
        }

        // 全选/取消全选按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("全选", GUILayout.Width(50)))
            assetEntries.ForEach(e => e.selected = true);
        if (GUILayout.Button("全不选", GUILayout.Width(60)))
            assetEntries.ForEach(e => e.selected = false);
        if (GUILayout.Button("仅选贴图", GUILayout.Width(70)))
        {
            assetEntries.ForEach(e => e.selected = false);
            assetEntries.Where(e => e.type == "贴图 Texture").ToList().ForEach(e => e.selected = true);
        }
        if (GUILayout.Button("仅选模型", GUILayout.Width(70)))
        {
            assetEntries.ForEach(e => e.selected = false);
            assetEntries.Where(e => e.type == "模型 Model").ToList().ForEach(e => e.selected = true);
        }
        if (GUILayout.Button("选 >1MB", GUILayout.Width(70)))
        {
            assetEntries.ForEach(e => e.selected = e.size > 1024 * 1024);
        }
        if (GUILayout.Button("反选", GUILayout.Width(50)))
            assetEntries.ForEach(e => e.selected = !e.selected);
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField($"共 {assetEntries.Count} 项", GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2);

        // 分组显示
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        var groups = assetEntries.GroupBy(e => e.type);
        foreach (var group in groups)
        {
            string key = group.Key;
            if (!groupFoldouts.ContainsKey(key))
                groupFoldouts[key] = true;

            long groupSize = group.Sum(e => e.size);
            int groupSelected = group.Count(e => e.selected);
            string header = $"{key} ({group.Count()} 个, {FormatSize(groupSize)}) [已选 {groupSelected}]";

            EditorGUILayout.BeginHorizontal();
            groupFoldouts[key] = EditorGUILayout.Foldout(groupFoldouts[key], header, true);

            // 组级别全选/取消
            if (GUILayout.Button("✓", GUILayout.Width(22)))
                group.ToList().ForEach(e => e.selected = true);
            if (GUILayout.Button("✗", GUILayout.Width(22)))
                group.ToList().ForEach(e => e.selected = false);

            EditorGUILayout.EndHorizontal();

            if (groupFoldouts[key])
            {
                EditorGUI.indentLevel++;
                foreach (var entry in group)
                {
                    EditorGUILayout.BeginHorizontal();
                    entry.selected = EditorGUILayout.Toggle(entry.selected, GUILayout.Width(20));
                    EditorGUILayout.LabelField(FormatSize(entry.size), GUILayout.Width(75));

                    // 点击路径可以 Ping 到资源
                    if (GUILayout.Button(entry.path, EditorStyles.linkLabel))
                    {
                        var obj = AssetDatabase.LoadAssetAtPath<Object>(entry.path);
                        if (obj != null)
                            EditorGUIUtility.PingObject(obj);
                    }

                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawFooter()
    {
        UpdateSelectedStats();

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal("box");

        EditorGUILayout.LabelField($"总计: {FormatSize(totalSize)}", GUILayout.Width(130));
        EditorGUILayout.LabelField($"已选: {selectedCount} 个, {FormatSize(selectedSize)}", GUILayout.Width(180));

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("导出分析报告", GUILayout.Width(100)))
        {
            ExportReport();
        }

        GUI.enabled = selectedCount > 0;
        if (GUILayout.Button($"移动选中资源 ({selectedCount})", GUILayout.Width(150)))
        {
            MoveSelectedAssets();
        }
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();
    }

    private void ScanDependencies()
    {
        if (sceneAsset == null)
        {
            EditorUtility.DisplayDialog("提示", "请先选择一个场景文件", "确定");
            return;
        }

        string scenePath = AssetDatabase.GetAssetPath(sceneAsset);
        string[] allDeps = AssetDatabase.GetDependencies(scenePath, true);

        assetEntries = allDeps
            .Where(p => p != scenePath)
            .Where(p => !p.EndsWith(".cs") && !p.EndsWith(".dll"))
            .Where(p => !p.StartsWith("Packages/"))
            .Where(p => !IsBuiltinAsset(p))
            .Select(p => new AssetEntry
            {
                path = p,
                size = GetFileSize(p),
                type = GetAssetType(p),
                selected = true
            })
            .ToList();

        SortEntries();
        scanned = true;
        groupFoldouts.Clear();
    }

    private void SortEntries()
    {
        assetEntries = sortMode switch
        {
            SortMode.SizeDesc => assetEntries.OrderBy(e => e.type).ThenByDescending(e => e.size).ToList(),
            SortMode.SizeAsc => assetEntries.OrderBy(e => e.type).ThenBy(e => e.size).ToList(),
            SortMode.Name => assetEntries.OrderBy(e => e.type).ThenBy(e => e.path).ToList(),
            _ => assetEntries
        };
    }

    private void UpdateSelectedStats()
    {
        selectedCount = assetEntries.Count(e => e.selected);
        selectedSize = assetEntries.Where(e => e.selected).Sum(e => e.size);
        totalSize = assetEntries.Sum(e => e.size);
    }

    private void MoveSelectedAssets()
    {
        var toMove = assetEntries.Where(e => e.selected).ToList();
        if (toMove.Count == 0) return;

        if (string.IsNullOrEmpty(targetFolder))
        {
            EditorUtility.DisplayDialog("错误", "请指定目标文件夹", "确定");
            return;
        }

        bool confirm = EditorUtility.DisplayDialog(
            "确认移动",
            $"将移动 {toMove.Count} 个资源 ({FormatSize(selectedSize)}) 到:\n{targetFolder}\n\n" +
            "AssetDatabase.MoveAsset 保持 GUID 不变，引用不会断开。",
            "确定移动", "取消");

        if (!confirm) return;

        EnsureFolderExists(targetFolder);

        int moved = 0;
        int skipped = 0;
        var errors = new List<string>();

        try
        {
            AssetDatabase.StartAssetEditing();

            for (int i = 0; i < toMove.Count; i++)
            {
                var entry = toMove[i];
                string fileName = Path.GetFileName(entry.path);
                string subFolder = GetSubFolder(entry.type);
                string destFolder = $"{targetFolder}/{subFolder}";

                EnsureFolderExists(destFolder);
                string destPath = $"{destFolder}/{fileName}";

                if (entry.path == destPath || File.Exists(Path.GetFullPath(destPath)))
                {
                    skipped++;
                    continue;
                }

                string result = AssetDatabase.MoveAsset(entry.path, destPath);
                if (string.IsNullOrEmpty(result))
                {
                    entry.path = destPath;
                    moved++;
                }
                else
                {
                    errors.Add($"{entry.path} → {result}");
                }

                if (i % 20 == 0)
                {
                    EditorUtility.DisplayProgressBar("移动资源中...",
                        $"{i}/{toMove.Count} - {fileName}",
                        (float)i / toMove.Count);
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
        }

        string message = $"完成!\n成功: {moved}\n跳过: {skipped}";
        if (errors.Count > 0)
            message += $"\n失败: {errors.Count}\n\n" + string.Join("\n", errors.Take(5));

        EditorUtility.DisplayDialog("结果", message, "确定");
        Debug.Log($"<color=green>资源移动完成</color> 成功:{moved} 跳过:{skipped} 失败:{errors.Count}");
    }

    private void ExportReport()
    {
        var lines = new List<string>();
        string sceneName = sceneAsset != null ? sceneAsset.name : "Unknown";
        lines.Add($"# 场景依赖分析: {sceneName}");
        lines.Add($"资源总大小 (源文件): {FormatSize(totalSize)}");
        lines.Add($"资源总数: {assetEntries.Count}");
        lines.Add("");

        var groups = assetEntries.GroupBy(e => e.type).OrderByDescending(g => g.Sum(e => e.size));
        foreach (var group in groups)
        {
            long groupSize = group.Sum(e => e.size);
            lines.Add($"## {group.Key} ({group.Count()} 个, {FormatSize(groupSize)})");
            lines.Add("");
            foreach (var entry in group.OrderByDescending(e => e.size))
            {
                lines.Add($"  {FormatSize(entry.size),10}  {entry.path}");
            }
            lines.Add("");
        }

        string outputPath = "Assets/SceneDependencyReport.txt";
        File.WriteAllText(outputPath, string.Join("\n", lines));
        AssetDatabase.Refresh();
        Debug.Log($"<color=green>报告已导出:</color> {outputPath}");
        EditorUtility.RevealInFinder(outputPath);
    }

    private static string GetSubFolder(string type)
    {
        if (type.Contains("Texture") || type.Contains("贴图")) return "Textures";
        if (type.Contains("Model") || type.Contains("模型")) return "Models";
        if (type.Contains("Material") || type.Contains("材质")) return "Materials";
        if (type.Contains("Shader") || type.Contains("着色器")) return "Shaders";
        if (type.Contains("Animation") || type.Contains("动画")) return "Animations";
        if (type.Contains("Prefab") || type.Contains("预制体")) return "Prefabs";
        if (type.Contains("Audio") || type.Contains("音频")) return "Audio";
        if (type.Contains("Asset") || type.Contains("资产")) return "Assets";
        return "Other";
    }

    private static string GetAssetType(string path)
    {
        string ext = Path.GetExtension(path).ToLower();
        return ext switch
        {
            ".png" or ".jpg" or ".jpeg" or ".tga" or ".psd" or ".exr" or ".hdr" => "贴图 Texture",
            ".fbx" or ".obj" or ".blend" or ".max" => "模型 Model",
            ".mat" => "材质 Material",
            ".shader" or ".shadergraph" or ".hlsl" => "着色器 Shader",
            ".anim" => "动画片段 Animation",
            ".controller" or ".overridecontroller" => "动画控制器 Animator",
            ".prefab" => "预制体 Prefab",
            ".unity" => "场景 Scene",
            ".asset" => "资产文件 Asset",
            ".lighting" => "光照 Lighting",
            ".wav" or ".mp3" or ".ogg" => "音频 Audio",
            ".ttf" or ".otf" => "字体 Font",
            ".mesh" => "网格 Mesh",
            _ => $"其他 ({ext})"
        };
    }

    private static bool IsBuiltinAsset(string path)
    {
        return path.StartsWith("Resources/") ||
               path.Contains("/unity_builtin_extra") ||
               path.Contains("/unity default resources");
    }

    private static long GetFileSize(string assetPath)
    {
        string fullPath = Path.GetFullPath(assetPath);
        if (File.Exists(fullPath))
            return new FileInfo(fullPath).Length;
        return 0;
    }

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0):F2} MB";
        if (bytes >= 1024)
            return $"{bytes / 1024.0:F1} KB";
        return $"{bytes} B";
    }

    private static void EnsureFolderExists(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parent = Path.GetDirectoryName(folderPath).Replace("\\", "/");
        string folderName = Path.GetFileName(folderPath);

        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolderExists(parent);

        AssetDatabase.CreateFolder(parent, folderName);
    }

    private class AssetEntry
    {
        public string path;
        public long size;
        public string type;
        public bool selected;
    }
}
