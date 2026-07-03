using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;

/// <summary>
/// 日志配置编辑器工具面板 - 集成到开发工具箱
/// 支持按脚本控制日志输出、批量操作、多配置预设管理
/// </summary>
[ToolHubItem("调试工具/日志配置管理器", "按脚本名称控制日志输出，支持批量操作和配置保存", 30)]
public class LogConfigPanel : IToolHubPanel
{
    private Vector2 scrollPosition = Vector2.zero;
    private Vector2 presetScrollPosition = Vector2.zero;
    private string searchFilter = "";
    private Dictionary<string, bool> scriptLogStates = new Dictionary<string, bool>();
    private bool isDirty = false;
    private GUIStyle headerStyle;
    private GUIStyle toggleStyle;
    private List<string> filteredScripts = new List<string>();
    private bool stylesInitialized = false;

    // 预设管理
    private List<string> presetNames = new List<string>();
    private string newPresetName = "";
    private bool showPresetPanel = true;
    private float presetPanelHeight = 150f;
    private bool isDraggingHeight = false;
    private Dictionary<string, bool> presetSelectionState = new Dictionary<string, bool>();
    private const string REMEMBERED_PRESETS_KEY = "LogPanel_RememberedPresets";

    public void OnEnable()
    {
        RefreshScriptStates();
        RefreshPresetList();
        AutoLoadRememberedPresets();
    }

    public void OnDisable()
    {
        if (isDirty)
        {
            SaveConfig();
        }
    }

    public void OnGUI()
    {
        if (!stylesInitialized)
        {
            InitializeStyles();
            stylesInitialized = true;
        }

        DrawHeader();
        DrawToolbar();
        DrawSearchBar();
        DrawScriptList();
        EditorGUILayout.Space(5);
        DrawPresetPanel();
        DrawFooter();
    }

    public void OnDestroy() { }

    public string GetHelpText()
    {
        return "按脚本名称控制日志输出。支持多个预设管理、可调节高度、批量删除预设、同时启用多个预设。";
    }

    #region 样式初始化

    private void InitializeStyles()
    {
        headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            padding = new RectOffset(5, 5, 5, 5)
        };

        toggleStyle = new GUIStyle(GUI.skin.toggle)
        {
            padding = new RectOffset(5, 5, 3, 3)
        };
    }

    #endregion

    #region UI 绘制

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("日志配置管理器", headerStyle);

        int enabledCount = scriptLogStates.Count(kv => kv.Value);
        int totalCount = scriptLogStates.Count;
        string statsText = $"当前启用 {enabledCount}/{totalCount} 个日志节点";

        var statsColor = enabledCount == 0 ? Color.gray :
                         enabledCount == totalCount ? Color.green : Color.yellow;
        GUI.color = statsColor;
        EditorGUILayout.LabelField(statsText, EditorStyles.helpBox);
        GUI.color = Color.white;
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("全启用", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            SetAllScriptsEnabled(true);
        }

        if (GUILayout.Button("全禁用", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            SetAllScriptsEnabled(false);
        }

        GUILayout.Space(10);

        if (GUILayout.Button("恢复默认", EditorStyles.toolbarButton, GUILayout.Width(70)))
        {
            ResetToDefault();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("扫描DebugEx脚本", EditorStyles.toolbarButton, GUILayout.Width(100)))
        {
            ScanDebugExScripts();
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            RefreshScriptStates();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawSearchBar()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("搜索:", GUILayout.Width(50));
        string newFilter = EditorGUILayout.TextField(searchFilter);
        if (newFilter != searchFilter)
        {
            searchFilter = newFilter;
            UpdateFilteredScripts();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
    }

    private void DrawScriptList()
    {
        EditorGUILayout.LabelField($"脚本列表 ({filteredScripts.Count}/{scriptLogStates.Count})", EditorStyles.boldLabel);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));

        if (filteredScripts.Count == 0 && scriptLogStates.Count > 0)
        {
            EditorGUILayout.HelpBox($"没有找到匹配 \"{searchFilter}\" 的脚本", MessageType.Info);
        }
        else if (scriptLogStates.Count == 0)
        {
            EditorGUILayout.HelpBox("暂无已注册的脚本。运行游戏后日志调用会自动注册脚本。", MessageType.Info);
        }

        foreach (var scriptName in filteredScripts)
        {
            DrawScriptToggle(scriptName);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawScriptToggle(string scriptName)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

        bool currentState = scriptLogStates[scriptName];
        bool newState = EditorGUILayout.Toggle(currentState, toggleStyle, GUILayout.Width(20));

        EditorGUILayout.LabelField(scriptName, GUILayout.ExpandWidth(true));

        if (newState != currentState)
        {
            scriptLogStates[scriptName] = newState;
            isDirty = true;
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawPresetPanel()
    {
        showPresetPanel = EditorGUILayout.Foldout(showPresetPanel, $"配置预设 ({presetNames.Count})", true, EditorStyles.foldoutHeader);
        if (!showPresetPanel) return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // 新建预设
        EditorGUILayout.BeginHorizontal();
        newPresetName = EditorGUILayout.TextField(newPresetName, GUILayout.ExpandWidth(true));
        GUI.enabled = !string.IsNullOrWhiteSpace(newPresetName);
        if (GUILayout.Button("保存为预设", GUILayout.Width(80)))
        {
            SaveAsPreset();
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(3);

        // 预设列表
        if (presetNames.Count == 0)
        {
            EditorGUILayout.LabelField("暂无预设，输入名称保存当前配置", EditorStyles.centeredGreyMiniLabel);
        }
        else
        {
            // 显示批量操作按钮
            int selectedCount = presetSelectionState.Count(kv => kv.Value);
            if (selectedCount > 0)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"已选中 {selectedCount} 个预设", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("叠加加载", GUILayout.Width(70)))
                {
                    MergeLoadPresets();
                }

                GUI.color = new Color(1f, 0.6f, 0.6f);
                if (GUILayout.Button("批量删除", GUILayout.Width(70)))
                {
                    if (EditorUtility.DisplayDialog("确认批量删除", $"确定要删除选中的 {selectedCount} 个预设吗？", "确定", "取消"))
                    {
                        BatchDeletePresets();
                    }
                }
                GUI.color = Color.white;

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(3);
            }

            // 绘制预设列表
            presetScrollPosition = EditorGUILayout.BeginScrollView(presetScrollPosition, GUILayout.Height(presetPanelHeight));

            foreach (var presetName in presetNames)
            {
                DrawPresetItem(presetName);
            }

            EditorGUILayout.EndScrollView();

            // 绘制高度调节分割线
            DrawHeightResizer();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawPresetItem(string presetName)
    {
        EditorGUILayout.BeginHorizontal();

        // 复选框用于批量操作
        bool isSelected = false;
        if (presetSelectionState.ContainsKey(presetName))
            isSelected = presetSelectionState[presetName];

        bool newSelected = EditorGUILayout.Toggle(isSelected, GUILayout.Width(20));
        if (newSelected != isSelected)
        {
            presetSelectionState[presetName] = newSelected;
        }

        EditorGUILayout.LabelField(presetName, GUILayout.ExpandWidth(true));

        if (GUILayout.Button("加载", GUILayout.Width(45)))
        {
            LoadPreset(presetName);
        }

        if (GUILayout.Button("覆盖", GUILayout.Width(45)))
        {
            if (EditorUtility.DisplayDialog("确认覆盖", $"确定要用当前配置覆盖预设 \"{presetName}\" 吗？", "确定", "取消"))
            {
                LogConfigManager.SavePreset(presetName, scriptLogStates);
            }
        }

        GUI.color = new Color(1f, 0.6f, 0.6f);
        if (GUILayout.Button("删除", GUILayout.Width(45)))
        {
            if (EditorUtility.DisplayDialog("确认删除", $"确定要删除预设 \"{presetName}\" 吗？", "确定", "取消"))
            {
                LogConfigManager.DeletePreset(presetName);
                RefreshPresetList();
            }
        }
        GUI.color = Color.white;

        EditorGUILayout.EndHorizontal();
    }

    private void DrawHeightResizer()
    {
        EditorGUILayout.Space(2);
        Rect resizerRect = GUILayoutUtility.GetRect(1, 6, GUILayout.ExpandWidth(true));

        GUI.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        GUI.DrawTexture(resizerRect, EditorGUIUtility.whiteTexture);
        GUI.color = Color.white;

        EditorGUIUtility.AddCursorRect(resizerRect, MouseCursor.ResizeVertical);

        if (Event.current.type == EventType.MouseDown && resizerRect.Contains(Event.current.mousePosition))
        {
            isDraggingHeight = true;
            Event.current.Use();
        }

        if (isDraggingHeight)
        {
            presetPanelHeight += Event.current.delta.y;
            presetPanelHeight = Mathf.Clamp(presetPanelHeight, 80f, 400f);
            GUI.changed = true;
            Event.current.Use();
        }

        if (Event.current.type == EventType.MouseUp)
        {
            isDraggingHeight = false;
        }
    }

    private void DrawFooter()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.BeginHorizontal();

        string statusText = isDirty ? "★ 有未保存的更改" : "✓ 已保存";
        GUI.color = isDirty ? Color.yellow : Color.green;
        EditorGUILayout.LabelField(statusText, EditorStyles.helpBox, GUILayout.Width(150));
        GUI.color = Color.white;

        GUILayout.FlexibleSpace();

        GUI.color = isDirty ? Color.green : Color.gray;
        if (GUILayout.Button("保存配置", GUILayout.Width(80), GUILayout.Height(25)))
        {
            SaveConfig();
        }
        GUI.color = Color.white;

        if (GUILayout.Button("加载配置", GUILayout.Width(80), GUILayout.Height(25)))
        {
            LoadConfig();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("导出为JSON", GUILayout.Width(100)))
        {
            ExportToJSON();
        }

        if (GUILayout.Button("清除配置", GUILayout.Width(100)))
        {
            if (EditorUtility.DisplayDialog("确认", "确定要清除所有日志配置吗？", "确定", "取消"))
            {
                ClearConfig();
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    #endregion

    #region 功能方法

    private void ScanDebugExScripts()
    {
        var debugExScripts = GetDebugExScripts();

        if (debugExScripts.Count == 0)
        {
            EditorUtility.DisplayDialog("扫描结果", "未找到使用 DebugEx 的脚本", "确定");
            return;
        }

        foreach (var scriptName in debugExScripts)
        {
            if (!scriptLogStates.ContainsKey(scriptName))
            {
                scriptLogStates[scriptName] = true;
            }
        }

        UpdateFilteredScripts();
        isDirty = true;
        ApplyChangesToDebugEx();
        EditorUtility.DisplayDialog("扫描完成", $"发现 {debugExScripts.Count} 个使用 DebugEx 的脚本\n已添加到配置列表", "确定");
    }

    private List<string> GetDebugExScripts()
    {
        var scriptNames = new HashSet<string>();
        string[] guids = AssetDatabase.FindAssets("t:Script");

        foreach (var guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!assetPath.EndsWith(".cs"))
                continue;

            try
            {
                string fileContent = System.IO.File.ReadAllText(assetPath);
                if (!fileContent.Contains("DebugEx."))
                    continue;
            }
            catch
            {
                continue;
            }

            var scriptAsset = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
            if (scriptAsset == null)
                continue;

            System.Type[] types = scriptAsset.GetClass() != null
                ? new[] { scriptAsset.GetClass() }
                : System.Type.EmptyTypes;

            foreach (var type in types)
            {
                if (type != null && !string.IsNullOrEmpty(type.Name))
                {
                    scriptNames.Add(type.Name);
                }
            }
        }

        return new List<string>(scriptNames);
    }

    private void RefreshScriptStates()
    {
        scriptLogStates = DebugEx.GetAllScriptLogStates();
        UpdateFilteredScripts();
    }

    private void RefreshPresetList()
    {
        presetNames = LogConfigManager.GetAllPresetNames();
        presetSelectionState.Clear();
    }

    private void UpdateFilteredScripts()
    {
        filteredScripts.Clear();

        if (string.IsNullOrEmpty(searchFilter))
        {
            filteredScripts.AddRange(scriptLogStates.Keys.OrderBy(x => x));
        }
        else
        {
            string filter = searchFilter.ToLower();
            filteredScripts.AddRange(
                scriptLogStates.Keys
                    .Where(x => x.ToLower().Contains(filter))
                    .OrderBy(x => x)
            );
        }
    }

    private void SetAllScriptsEnabled(bool enabled)
    {
        foreach (var key in scriptLogStates.Keys.ToList())
        {
            scriptLogStates[key] = enabled;
        }
        isDirty = true;
        ApplyChangesToDebugEx();
    }

    private void ResetToDefault()
    {
        if (EditorUtility.DisplayDialog("确认", "确定要恢复所有脚本的默认配置吗？", "确定", "取消"))
        {
            DebugEx.ClearScriptLogConfig();
            RefreshScriptStates();
            isDirty = false;
        }
    }

    private void ApplyChangesToDebugEx()
    {
        DebugEx.SetAllScriptLogEnabled(new Dictionary<string, bool>(scriptLogStates));
    }

    private void SaveConfig()
    {
        ApplyChangesToDebugEx();
        LogConfigManager.SaveConfigToFile(scriptLogStates);
        isDirty = false;
    }

    private void LoadConfig()
    {
        var loadedStates = LogConfigManager.LoadConfigFromFile();
        if (loadedStates.Count > 0)
        {
            scriptLogStates = loadedStates;
            ApplyChangesToDebugEx();
            UpdateFilteredScripts();
            isDirty = false;
            EditorUtility.DisplayDialog("加载成功", $"已加载 {loadedStates.Count} 个脚本的配置", "确定");
        }
        else
        {
            EditorUtility.DisplayDialog("加载失败", "没有找到保存的配置文件", "确定");
        }
    }

    private void SaveAsPreset()
    {
        string name = newPresetName.Trim();
        if (string.IsNullOrEmpty(name)) return;

        if (presetNames.Contains(name))
        {
            if (!EditorUtility.DisplayDialog("确认覆盖", $"预设 \"{name}\" 已存在，确定覆盖？", "确定", "取消"))
                return;
        }

        if (LogConfigManager.SavePreset(name, scriptLogStates))
        {
            newPresetName = "";
            RefreshPresetList();
            Debug.Log($"[日志配置] ✓ 预设已保存: {name}");
        }
    }

    private void LoadPreset(string presetName)
    {
        var loadedStates = LogConfigManager.LoadPreset(presetName);
        if (loadedStates.Count > 0)
        {
            scriptLogStates = loadedStates;
            ApplyChangesToDebugEx();
            UpdateFilteredScripts();
            isDirty = false;
            Debug.Log($"[日志配置] ✓ 已加载预设: {presetName}");
        }
        else
        {
            Debug.LogWarning($"[日志配置] ⚠ 预设 \"{presetName}\" 为空或加载失败");
        }
    }

    private void MergeLoadPresets()
    {
        var selectedPresets = presetSelectionState.Where(kv => kv.Value).Select(kv => kv.Key).ToList();
        if (selectedPresets.Count == 0) return;

        var mergedStates = new Dictionary<string, bool>(scriptLogStates);

        foreach (var presetName in selectedPresets)
        {
            var presetStates = LogConfigManager.LoadPreset(presetName);
            foreach (var kvp in presetStates)
            {
                if (kvp.Value)
                    mergedStates[kvp.Key] = true;
            }
        }

        scriptLogStates = mergedStates;
        ApplyChangesToDebugEx();
        UpdateFilteredScripts();
        isDirty = false;
        RememberPresetSelection(selectedPresets);
        Debug.Log($"[日志配置] ✓ 已叠加加载 {selectedPresets.Count} 个预设: {string.Join(", ", selectedPresets)}\n下次启动时会自动加载");
    }

    private void RememberPresetSelection(List<string> presetNames)
    {
        string json = JsonUtility.ToJson(new PresetList { presets = presetNames });
        EditorPrefs.SetString(REMEMBERED_PRESETS_KEY, json);
    }

    private void AutoLoadRememberedPresets()
    {
        if (!EditorPrefs.HasKey(REMEMBERED_PRESETS_KEY)) return;

        try
        {
            string json = EditorPrefs.GetString(REMEMBERED_PRESETS_KEY);
            var presetList = JsonUtility.FromJson<PresetList>(json);

            if (presetList?.presets == null || presetList.presets.Count == 0) return;

            // 检查保存的预设是否仍然存在
            var validPresets = presetList.presets.Where(p => presetNames.Contains(p)).ToList();
            if (validPresets.Count == 0) return;

            // 选中这些预设
            foreach (var presetName in validPresets)
            {
                presetSelectionState[presetName] = true;
            }

            // 自动加载（输出在 MergeLoadPresets 中）
            MergeLoadPresets();
        }
        catch { }
    }

    [System.Serializable]
    private class PresetList
    {
        public List<string> presets = new List<string>();
    }

    private void BatchDeletePresets()
    {
        var presetsToDelete = presetSelectionState.Where(kv => kv.Value).Select(kv => kv.Key).ToList();
        foreach (var presetName in presetsToDelete)
        {
            LogConfigManager.DeletePreset(presetName);
        }
        RefreshPresetList();
    }

    private void ClearConfig()
    {
        LogConfigManager.DeleteConfigFile();
        DebugEx.ClearScriptLogConfig();
        RefreshScriptStates();
        isDirty = false;
    }

    private void ExportToJSON()
    {
        string json = LogConfig.FromDictionary(scriptLogStates).ToJson();
        string path = EditorUtility.SaveFilePanel("导出日志配置", "", "log_config.json", "json");

        if (!string.IsNullOrEmpty(path))
        {
            System.IO.File.WriteAllText(path, json);
            EditorUtility.DisplayDialog("导出成功", $"配置已导出到:\n{path}", "确定");
        }
    }

    #endregion
}
