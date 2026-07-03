using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[ToolHubItem("场景工具/PWB 控制台", "PWB 集成控制中心，快速切换笔刷工具。", 5)]
public class PWBControlPanel : IToolHubPanel
{
    private const string PrefSaveKey = "PWB_Apply_Key";

    private static readonly string[] ToolNames =
    {
        "PIN",
        "BRUSH",
        "GRAVITY",
        "LINE",
        "SHAPE",
        "TILING",
        "REPLACER",
        "ERASER",
        "SELECTION",
        "EXTRUDE",
        "MIRROR",
        "FLOOR",
        "WALL",
    };

    private bool initialized;
    private string statusMessage;

    private KeyCode ApplyKey
    {
        get => (KeyCode)EditorPrefs.GetInt(PrefSaveKey, (int)KeyCode.Return);
        set => EditorPrefs.SetInt(PrefSaveKey, (int)value);
    }

    public void OnEnable()
    {
        InitializePWB();
    }

    public void OnDisable() { }

    public void OnDestroy() { }

    public string GetHelpText() =>
        "PWB 控制台会在项目安装 PluginMaster / Prefab World Builder 时启用；未安装时仅显示提示，不影响工具箱编译。";

    public void OnGUI()
    {
        InitializePWB();

        if (!initialized)
        {
            EditorGUILayout.HelpBox(statusMessage, MessageType.Warning);
            if (GUILayout.Button("重试初始化", GUILayout.Height(28)))
            {
                initialized = false;
                statusMessage = null;
                InitializePWB();
            }
            return;
        }

        DrawCustomSettings();
        EditorGUILayout.Space(8);
        DrawHeaderStatus();
        EditorGUILayout.Space(8);
        DrawMenuWindowsButtons();
        EditorGUILayout.Space(8);
        DrawToolsGrid();
        EditorGUILayout.Space(8);
        DrawSettingsToggles();
    }

    private void InitializePWB()
    {
        if (initialized)
            return;

        if (!PWBReflection.IsAvailable)
        {
            statusMessage = "未检测到 PluginMaster / Prefab World Builder。此面板已安全禁用。";
            return;
        }

        try
        {
            PWBReflection.InvokeStatic("PluginMaster.PWBCore", "Initialize");
            initialized = true;
            statusMessage = "PWB 初始化成功";
        }
        catch (Exception ex)
        {
            statusMessage = $"PWB 初始化失败：{ex.Message}";
        }
    }

    private void DrawCustomSettings()
    {
        EditorGUILayout.LabelField("快捷键配置", EditorStyles.boldLabel);
        ApplyKey = (KeyCode)EditorGUILayout.EnumPopup(
            new GUIContent("应用/确认键", "该值仅由此控制台保存，实际热键是否生效取决于 PWB 插件版本。"),
            ApplyKey
        );
    }

    private void DrawHeaderStatus()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            string currentTool = PWBReflection.GetToolName();
            EditorGUILayout.LabelField("PWB 状态", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"当前工具: {currentTool}");

            var selectedPalette = PWBReflection.GetStaticProperty("PluginMaster.PaletteManager", "selectedPalette");
            var selectedBrush = PWBReflection.GetStaticProperty("PluginMaster.PaletteManager", "selectedBrush");
            EditorGUILayout.LabelField($"调色板: {(selectedPalette != null ? selectedPalette.ToString() : "无")}");
            EditorGUILayout.LabelField($"笔刷: {(selectedBrush != null ? selectedBrush.ToString() : "无")}");
        }
    }

    private void DrawMenuWindowsButtons()
    {
        EditorGUILayout.LabelField("PWB 窗口", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            DrawWindowButton("工具栏", "PluginMaster.PWBToolbar", "ShowWindow");
            DrawWindowButton("Palette", "PluginMaster.PaletteManager", "ShowWindow");
            DrawWindowButton("Items", "PluginMaster.PWBItemsWindow", "ShowWindow");
            DrawWindowButton("Preferences", "PluginMaster.PWBPreferences", "ShowWindow");

            if (GUILayout.Button(new GUIContent("文档", "打开 PWB 官方文档"), GUILayout.Height(24)))
                PWBReflection.InvokeStatic("PluginMaster.PWBCore", "OpenDocFile");
        }
    }

    private static void DrawWindowButton(string label, string typeName, string methodName)
    {
        using (new EditorGUI.DisabledScope(PWBReflection.FindType(typeName) == null))
        {
            if (GUILayout.Button(label, GUILayout.Height(24)))
                PWBReflection.InvokeStatic(typeName, methodName);
        }
    }

    private void DrawToolsGrid()
    {
        EditorGUILayout.LabelField("笔刷工具", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            bool isPainting = PWBReflection.GetToolName() != "NONE";
            using (new EditorGUI.DisabledScope(!isPainting))
            {
                if (GUILayout.Button("停止绘制 (Esc)", GUILayout.Height(30)))
                    PWBReflection.InvokeStatic("PluginMaster.ToolManager", "DeselectTool");
            }

            const int columns = 3;
            for (int i = 0; i < ToolNames.Length; i += columns)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int j = 0; j < columns && i + j < ToolNames.Length; j++)
                        DrawToolToggle(ToolNames[i + j]);
                }
            }
        }
    }

    private static void DrawToolToggle(string toolName)
    {
        bool active = PWBReflection.GetToolName() == toolName;
        var oldColor = GUI.backgroundColor;
        if (active)
            GUI.backgroundColor = Color.green;

        if (GUILayout.Button(toolName, EditorStyles.miniButton, GUILayout.Height(28)))
        {
            if (active)
                PWBReflection.InvokeStatic("PluginMaster.ToolManager", "DeselectTool");
            else
                PWBReflection.SetTool(toolName);
        }

        GUI.backgroundColor = oldColor;
    }

    private static void DrawSettingsToggles()
    {
        var settings = PWBReflection.GetStaticProperty("PluginMaster.SnapManager", "settings");
        if (settings == null)
            return;

        EditorGUILayout.LabelField("吸附设置", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope("box"))
        {
            DrawBoolProperty(settings, "snappingEnabled", "启用吸附");
            DrawBoolProperty(settings, "visibleGrid", "显示网格");
        }
    }

    private static void DrawBoolProperty(object target, string propertyName, string label)
    {
        var type = target.GetType();
        var prop = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var field = type.GetField(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        bool current = false;
        if (prop != null && prop.PropertyType == typeof(bool))
            current = (bool)prop.GetValue(target);
        else if (field != null && field.FieldType == typeof(bool))
            current = (bool)field.GetValue(target);
        else
            return;

        bool next = EditorGUILayout.Toggle(label, current);
        if (next == current)
            return;

        if (prop != null && prop.CanWrite)
            prop.SetValue(target, next);
        else
            field?.SetValue(target, next);
    }

    private static class PWBReflection
    {
        public static bool IsAvailable => FindType("PluginMaster.ToolManager") != null;

        public static Type FindType(string fullName)
        {
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(a => a.GetType(fullName))
                .FirstOrDefault(t => t != null);
        }

        public static object GetStaticProperty(string typeName, string propertyName)
        {
            var type = FindType(typeName);
            var prop = type?.GetProperty(propertyName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var field = type?.GetField(propertyName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            return prop != null ? prop.GetValue(null) : field?.GetValue(null);
        }

        public static void InvokeStatic(string typeName, string methodName)
        {
            var type = FindType(typeName);
            var method = type?.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            method?.Invoke(null, null);
        }

        public static string GetToolName()
        {
            var value = GetStaticProperty("PluginMaster.ToolManager", "tool");
            return value?.ToString() ?? "NONE";
        }

        public static void SetTool(string toolName)
        {
            var type = FindType("PluginMaster.ToolManager");
            if (type == null)
                return;

            var prop = type.GetProperty("tool", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var field = type.GetField("tool", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var enumType = prop?.PropertyType ?? field?.FieldType;
            if (enumType == null || !enumType.IsEnum || !Enum.TryParse(enumType, toolName, out var enumValue))
                return;

            if (prop != null && prop.CanWrite)
                prop.SetValue(null, enumValue);
            else
                field?.SetValue(null, enumValue);
        }
    }
}
