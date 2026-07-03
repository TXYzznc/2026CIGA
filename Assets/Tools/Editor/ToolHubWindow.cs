using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class ToolHubWindow : EditorWindow
{
    private const string SettingsAssetPath = "Assets/Editor/ToolHubSettings.asset";
    private const float HomeButtonWidth = 72f;
    private const float TabHeight = 28f;
    private const float TopBarHeight = 56f;
    private const float TopBarContentGap = 10f;
    private const float TopBarPadding = 3f;
    private const float TabScrollbarHeight = 16f;
    private const float TopBarButtonWidth = 40f;
    private const float ToolButtonWidth = 150f;
    private const float ToolButtonHeight = 58f;
    private const float ToolButtonGap = 10f;

    private ToolHubSettings settings;
    private readonly Dictionary<string, IToolHubPanel> panelCache = new();
    private readonly Dictionary<string, GUIStyle> toolButtonStyleCache = new();
    private IToolHubPanel currentPanel;
    private string currentTypeName;
    private bool showHome = true;
    private Vector2 tabScrollPosition;
    private Vector2 homeScrollPosition;

    private GUIStyle tabStyle;
    private GUIStyle selectedTabStyle;
    private GUIStyle homeButtonStyle;
    private GUIStyle selectedHomeButtonStyle;
    private GUIStyle categoryStyle;
    private bool stylesInitialized;

    [MenuItem("Tools/Unity开发工具箱 %#T")]
    public static void Open()
    {
        var win = GetWindow<ToolHubWindow>("unity开发工具箱");
        win.minSize = new Vector2(640, 420);
        win.Show();
    }

    private void OnEnable()
    {
        settings = LoadOrCreateSettings();
        stylesInitialized = false;

        if (settings.tools.Count > 0)
        {
            settings.selectedIndex = Mathf.Clamp(settings.selectedIndex, 0, settings.tools.Count - 1);
            showHome = false;
        }
        else
        {
            showHome = true;
        }
    }

    private void OnDisable()
    {
        currentPanel?.OnDisable();
    }

    private void OnDestroy()
    {
        foreach (var panel in panelCache.Values)
        {
            panel.OnDestroy();
        }

        panelCache.Clear();
    }

    private void InitStyles()
    {
        if (stylesInitialized)
            return;

        tabStyle = new GUIStyle(EditorStyles.toolbarButton)
        {
            fixedHeight = TabHeight,
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12,
            clipping = TextClipping.Clip,
        };

        selectedTabStyle = new GUIStyle(tabStyle);
        selectedTabStyle.normal = selectedTabStyle.active;

        homeButtonStyle = new GUIStyle(EditorStyles.toolbarButton)
        {
            fixedHeight = TabHeight,
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12,
        };

        selectedHomeButtonStyle = new GUIStyle(homeButtonStyle);
        selectedHomeButtonStyle.normal = selectedHomeButtonStyle.active;

        categoryStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            margin = new RectOffset(0, 0, 10, 4),
        };

        stylesInitialized = true;
    }

    private void OnGUI()
    {
        InitStyles();

        if (settings == null)
        {
            DrawErrorState();
            return;
        }

        DrawTopBar();
        GUILayout.Space(TopBarContentGap);

        if (showHome)
            DrawHomePage();
        else
            DrawCurrentTool();
    }

    private void DrawErrorState()
    {
        EditorGUILayout.HelpBox("ToolHubSettings 配置丢失", MessageType.Error);
        if (GUILayout.Button("重新创建配置"))
            settings = LoadOrCreateSettings(true);
    }

    private void DrawTopBar()
    {
        Rect topBarRect = EditorGUILayout.GetControlRect(false, TopBarHeight, GUILayout.ExpandWidth(true));
        GUI.Box(topBarRect, GUIContent.none, EditorStyles.toolbar);

        float x = topBarRect.x + TopBarPadding;
        float y = topBarRect.y + TopBarPadding;
        float right = topBarRect.xMax - TopBarPadding;

        Rect homeRect = new Rect(x, y, HomeButtonWidth, TabHeight);
        if (GUI.Button(homeRect, "主页", showHome ? selectedHomeButtonStyle : homeButtonStyle))
            ShowHome();
        x = homeRect.xMax + 4f;

        Rect settingsRect = new Rect(right - TopBarButtonWidth, y, TopBarButtonWidth, TabHeight);
        Rect lockRect = new Rect(settingsRect.x - TopBarButtonWidth, y, TopBarButtonWidth, TabHeight);
        Rect closeRect = new Rect(lockRect.x - TopBarButtonWidth, y, TopBarButtonWidth, TabHeight);
        Rect countRect = new Rect(closeRect.x - 68f, y, 64f, TabHeight);

        float tabAreaWidth = Mathf.Max(1f, countRect.x - x - 4f);
        Rect tabViewRect = new Rect(x, y, tabAreaWidth, TabHeight);
        Rect scrollbarRect = new Rect(x, y + TabHeight + 3f, tabAreaWidth, TabScrollbarHeight);
        DrawScrollableTabs(tabViewRect, scrollbarRect);

        GUI.Label(countRect, $"已打开: {settings.tools.Count}", EditorStyles.miniLabel);

        using (new EditorGUI.DisabledScope(showHome || settings.tools.Count == 0 || settings.locked))
        {
            if (GUI.Button(closeRect, "关闭", EditorStyles.toolbarButton))
                RemoveCurrentTool();
        }

        var lockText = settings.locked ? "解锁" : "锁定";
        if (GUI.Button(lockRect, lockText, EditorStyles.toolbarButton))
        {
            settings.locked = !settings.locked;
            SaveSettings();
        }

        if (GUI.Button(settingsRect, "设置", EditorStyles.toolbarButton))
            ShowSettingsMenu();
    }

    private void DrawScrollableTabs(Rect viewRect, Rect scrollbarRect)
    {
        float contentWidth = CalculateTabsContentWidth();
        float maxScroll = Mathf.Max(0f, contentWidth - viewRect.width);
        tabScrollPosition.x = Mathf.Clamp(tabScrollPosition.x, 0f, maxScroll);

        GUI.BeginGroup(viewRect);
        {
            float x = -tabScrollPosition.x;
            for (int i = 0; i < settings.tools.Count; i++)
            {
                var entry = settings.tools[i];
                string label = GetEntryDisplayName(entry);
                float width = Mathf.Clamp(tabStyle.CalcSize(new GUIContent(label)).x + 18f, 72f, 180f);
                bool selected = !showHome && i == settings.selectedIndex;

                Rect tabRect = new Rect(x, 0f, width, TabHeight);
                if (GUI.Button(tabRect, label, selected ? selectedTabStyle : tabStyle))
                {
                    settings.selectedIndex = i;
                    showHome = false;
                    SwitchToPanel(entry.typeName);
                    SaveSettings();
                }

                x += width;
            }
        }
        GUI.EndGroup();

        using (new EditorGUI.DisabledScope(maxScroll <= 0f))
        {
            tabScrollPosition.x = GUI.HorizontalScrollbar(scrollbarRect, tabScrollPosition.x, viewRect.width, 0f, contentWidth);
        }
    }

    private float CalculateTabsContentWidth()
    {
        float width = 0f;
        for (int i = 0; i < settings.tools.Count; i++)
        {
            string label = GetEntryDisplayName(settings.tools[i]);
            width += Mathf.Clamp(tabStyle.CalcSize(new GUIContent(label)).x + 18f, 72f, 180f);
        }

        return Mathf.Max(width, 1f);
    }

    private void DrawHomePage()
    {
        var tools = FindAllToolPanelTypes().OrderBy(GetCategoryName).ThenBy(GetPriority).ThenBy(GetToolName).ToList();

        homeScrollPosition = EditorGUILayout.BeginScrollView(homeScrollPosition);
        EditorGUILayout.Space(10);

        if (tools.Count == 0)
        {
            EditorGUILayout.HelpBox("没有找到可用工具。", MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        foreach (var group in tools.GroupBy(GetCategoryName))
        {
            EditorGUILayout.LabelField(group.Key, categoryStyle);
            DrawToolButtonGrid(group.ToList());
        }

        EditorGUILayout.Space(16);
        EditorGUILayout.EndScrollView();
    }

    private void DrawToolButtonGrid(List<Type> tools)
    {
        float availableWidth = Mathf.Max(1f, position.width - 24f);
        int columns = Mathf.Max(1, Mathf.FloorToInt((availableWidth + ToolButtonGap) / (ToolButtonWidth + ToolButtonGap)));

        for (int i = 0; i < tools.Count; i += columns)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int column = 0; column < columns; column++)
                {
                    int index = i + column;
                    if (index >= tools.Count)
                    {
                        GUILayout.Space(ToolButtonWidth + ToolButtonGap);
                        continue;
                    }

                    DrawToolButton(tools[index]);
                    GUILayout.Space(ToolButtonGap);
                }
            }

            GUILayout.Space(ToolButtonGap);
        }
    }

    private void DrawToolButton(Type toolType)
    {
        string label = GetToolName(toolType);
        var style = GetToolButtonStyle(label);

        if (
            GUILayout.Button(
                new GUIContent(label, GetDescription(toolType)),
                style,
                GUILayout.Width(ToolButtonWidth),
                GUILayout.Height(ToolButtonHeight)
            )
        )
        {
            OpenTool(toolType);
        }
    }

    private GUIStyle GetToolButtonStyle(string label)
    {
        if (toolButtonStyleCache.TryGetValue(label, out var cached))
            return cached;

        var style = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            clipping = TextClipping.Clip,
            padding = new RectOffset(8, 8, 6, 6),
            fixedWidth = ToolButtonWidth,
            fixedHeight = ToolButtonHeight,
        };

        var content = new GUIContent(label);
        for (int size = 14; size >= 8; size--)
        {
            style.fontSize = size;
            if (style.CalcHeight(content, ToolButtonWidth - style.padding.horizontal) <= ToolButtonHeight - style.padding.vertical)
                break;
        }

        toolButtonStyleCache[label] = style;
        return style;
    }

    private void OpenTool(Type toolType)
    {
        string typeName = toolType.AssemblyQualifiedName;
        int index = settings.tools.FindIndex(x => x.typeName == typeName);

        if (index < 0)
        {
            settings.tools.Add(
                new ToolHubSettings.ToolEntry
                {
                    typeName = typeName,
                    displayName = GetToolName(toolType),
                    description = GetDescription(toolType),
                }
            );
            index = settings.tools.Count - 1;
        }

        settings.selectedIndex = index;
        showHome = false;
        SwitchToPanel(typeName);
        SaveSettings();
    }

    private void ShowHome()
    {
        showHome = true;
        currentPanel?.OnDisable();
        currentPanel = null;
        currentTypeName = null;
    }

    private void DrawCurrentTool()
    {
        if (settings.tools.Count == 0)
        {
            ShowHome();
            DrawHomePage();
            return;
        }

        settings.selectedIndex = Mathf.Clamp(settings.selectedIndex, 0, settings.tools.Count - 1);
        var entry = settings.tools[settings.selectedIndex];

        if (currentPanel == null || currentTypeName != entry.typeName)
            SwitchToPanel(entry.typeName);

        if (currentPanel == null)
        {
            EditorGUILayout.HelpBox(
                $"无法加载工具:\n{entry.typeName}\n\n可能是类已被删除或重命名。",
                MessageType.Error
            );

            if (GUILayout.Button("关闭这个页签"))
                RemoveCurrentTool();

            return;
        }

        try
        {
            currentPanel.OnGUI();
        }
        catch (Exception e)
        {
            EditorGUILayout.HelpBox($"工具绘制出错:\n{e.Message}", MessageType.Error);
            Debug.LogException(e);
        }
    }

    private void SwitchToPanel(string typeName)
    {
        if (currentTypeName == typeName && currentPanel != null)
            return;

        currentPanel?.OnDisable();
        currentPanel = GetOrCreatePanel(typeName);
        currentTypeName = typeName;
        currentPanel?.OnEnable();
    }

    private void RemoveCurrentTool()
    {
        if (settings.tools.Count == 0)
            return;

        int idx = Mathf.Clamp(settings.selectedIndex, 0, settings.tools.Count - 1);
        var entry = settings.tools[idx];

        if (panelCache.TryGetValue(entry.typeName, out var panel))
        {
            panel.OnDisable();
            panel.OnDestroy();
            panelCache.Remove(entry.typeName);
        }

        if (currentTypeName == entry.typeName)
        {
            currentPanel = null;
            currentTypeName = null;
        }

        settings.tools.RemoveAt(idx);
        settings.selectedIndex = Mathf.Clamp(idx, 0, Mathf.Max(0, settings.tools.Count - 1));

        if (settings.tools.Count == 0)
        {
            showHome = true;
        }
        else if (!showHome)
        {
            SwitchToPanel(settings.tools[settings.selectedIndex].typeName);
        }

        SaveSettings();
    }

    private void ShowSettingsMenu()
    {
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("刷新主页"), false, () =>
        {
            toolButtonStyleCache.Clear();
            Repaint();
        });
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("关闭所有页签"), false, CloseAllTabs);
        menu.AddSeparator("");
        menu.AddItem(
            new GUIContent("打开配置文件"),
            false,
            () =>
            {
                Selection.activeObject = settings;
                EditorGUIUtility.PingObject(settings);
            }
        );

        menu.ShowAsContext();
    }

    private void CloseAllTabs()
    {
        if (settings.locked)
            return;

        foreach (var panel in panelCache.Values)
        {
            panel.OnDisable();
            panel.OnDestroy();
        }

        panelCache.Clear();
        currentPanel = null;
        currentTypeName = null;
        settings.tools.Clear();
        settings.selectedIndex = 0;
        showHome = true;
        SaveSettings();
    }

    private IToolHubPanel GetOrCreatePanel(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return null;
        if (panelCache.TryGetValue(typeName, out var cached))
            return cached;

        var type = Type.GetType(typeName);
        if (type == null || !typeof(IToolHubPanel).IsAssignableFrom(type))
            return null;

        try
        {
            var panel = Activator.CreateInstance(type) as IToolHubPanel;
            panelCache[typeName] = panel;
            return panel;
        }
        catch (Exception e)
        {
            Debug.LogError($"创建工具面板失败: {typeName}\n{e}");
            return null;
        }
    }

    private static IEnumerable<Type> FindAllToolPanelTypes()
    {
        return TypeCache
            .GetTypesDerivedFrom<IToolHubPanel>()
            .Where(t =>
                !t.IsAbstract
                && !t.IsInterface
                && t.GetCustomAttributes(typeof(ToolHubItemAttribute), false).Length > 0
            );
    }

    private static string GetEntryDisplayName(ToolHubSettings.ToolEntry entry)
    {
        return string.IsNullOrWhiteSpace(entry.displayName) ? "未命名工具" : entry.displayName;
    }

    private static string GetCategoryName(Type t)
    {
        string menuName = GetMenuName(t);
        int slashIndex = menuName.LastIndexOf('/');
        return slashIndex > 0 ? menuName.Substring(0, slashIndex) : "通用工具";
    }

    private static string GetToolName(Type t)
    {
        string menuName = GetMenuName(t);
        int slashIndex = menuName.LastIndexOf('/');
        return slashIndex >= 0 && slashIndex < menuName.Length - 1
            ? menuName.Substring(slashIndex + 1)
            : menuName;
    }

    private static string GetMenuName(Type t)
    {
        var attr =
            t.GetCustomAttributes(typeof(ToolHubItemAttribute), false).FirstOrDefault()
            as ToolHubItemAttribute;
        return attr?.MenuName ?? t.Name;
    }

    private static string GetDescription(Type t)
    {
        var attr =
            t.GetCustomAttributes(typeof(ToolHubItemAttribute), false).FirstOrDefault()
            as ToolHubItemAttribute;
        return attr?.Description ?? "";
    }

    private static int GetPriority(Type t)
    {
        var attr =
            t.GetCustomAttributes(typeof(ToolHubItemAttribute), false).FirstOrDefault()
            as ToolHubItemAttribute;
        return attr?.Priority ?? 100;
    }

    private ToolHubSettings LoadOrCreateSettings(bool forceRecreate = false)
    {
        if (forceRecreate)
        {
            AssetDatabase.DeleteAsset(SettingsAssetPath);
            AssetDatabase.SaveAssets();
        }

        var asset = AssetDatabase.LoadAssetAtPath<ToolHubSettings>(SettingsAssetPath);
        if (asset != null)
            return asset;

        if (!AssetDatabase.IsValidFolder("Assets/Editor"))
            AssetDatabase.CreateFolder("Assets", "Editor");

        asset = CreateInstance<ToolHubSettings>();
        AssetDatabase.CreateAsset(asset, SettingsAssetPath);
        AssetDatabase.SaveAssets();
        return asset;
    }

    private void SaveSettings()
    {
        if (settings == null)
            return;

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }
}
