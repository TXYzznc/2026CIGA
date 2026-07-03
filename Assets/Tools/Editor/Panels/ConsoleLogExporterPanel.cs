using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Unity Console 日志导出面板 - 集成到开发工具箱
/// </summary>
[ToolHubItem("调试工具/Console日志导出", "导出 Unity Console 当前日志到本地文件", 31)]
public class ConsoleLogExporterPanel : IToolHubPanel
{
    private const float ButtonHeight = 28f;
    private const string OutputPathPrefsKey = "ConsoleLogExporter_OutputPath";

    private string outputPath;
    private bool includeLog = true;
    private bool includeWarning = true;
    private bool includeError = true;
    private bool includeException = true;
    private bool firstLineOnly = true;
    private bool revealAfterExport = true;
    private Vector2 scrollPosition;

    public void OnEnable()
    {
        outputPath = EditorPrefs.GetString(OutputPathPrefsKey, GetDefaultOutputPath());
    }

    public void OnDisable()
    {
        SaveOutputPath();
    }

    public void OnDestroy()
    {
        SaveOutputPath();
    }

    public string GetHelpText()
    {
        return "导出当前 Unity Console 中的日志，支持按类型过滤、首行导出和自定义输出目录。";
    }

    public void OnGUI()
    {
        EditorGUILayout.LabelField("Console 日志导出", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("直接读取当前 Unity Console 内容并导出为 .log 文件，不依赖运行时日志捕获。", MessageType.Info);
        EditorGUILayout.Space(6);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        DrawOutputSettings();
        EditorGUILayout.Space(10);
        DrawFilterSettings();
        EditorGUILayout.Space(10);
        DrawActions();
        EditorGUILayout.EndScrollView();
    }

    private void DrawOutputSettings()
    {
        EditorGUILayout.LabelField("输出路径", EditorStyles.boldLabel);
        outputPath = EditorGUILayout.TextField("Folder", outputPath);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("浏览...", GUILayout.Height(ButtonHeight)))
            {
                string selectedPath = EditorUtility.OpenFolderPanel("选择 Console 日志输出目录", GetValidPanelPath(), "");
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    outputPath = selectedPath;
                    SaveOutputPath();
                }
            }

            if (GUILayout.Button("重置默认", GUILayout.Height(ButtonHeight)))
            {
                outputPath = GetDefaultOutputPath();
                SaveOutputPath();
            }
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            EditorGUILayout.HelpBox("输出路径为空，将使用默认目录。", MessageType.Warning);
        }
    }

    private void DrawFilterSettings()
    {
        EditorGUILayout.LabelField("导出选项", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("日志类型", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                includeLog = EditorGUILayout.ToggleLeft("Log", includeLog, GUILayout.Width(90));
                includeWarning = EditorGUILayout.ToggleLeft("Warning", includeWarning, GUILayout.Width(110));
                includeError = EditorGUILayout.ToggleLeft("Error / Assert", includeError, GUILayout.Width(120));
                includeException = EditorGUILayout.ToggleLeft("Exception", includeException, GUILayout.Width(110));
            }

            EditorGUILayout.Space(4);
            firstLineOnly = EditorGUILayout.ToggleLeft("只导出首行消息", firstLineOnly);
            revealAfterExport = EditorGUILayout.ToggleLeft("导出后在文件管理器中定位", revealAfterExport);
        }

        if (!includeLog && !includeWarning && !includeError && !includeException)
        {
            EditorGUILayout.HelpBox("至少选择一种日志类型。", MessageType.Warning);
        }
    }

    private void DrawActions()
    {
        using (new EditorGUI.DisabledScope(!HasAnyTypeSelected()))
        {
            if (GUILayout.Button("导出 Console 日志", GUILayout.Height(34)))
            {
                ExportConsoleLogsToFile();
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("打开输出目录", GUILayout.Height(ButtonHeight)))
            {
                OpenOutputFolder();
            }

            GUI.color = new Color(1f, 0.65f, 0.65f);
            if (GUILayout.Button("清空 Unity Console", GUILayout.Height(ButtonHeight)))
            {
                ClearConsoleWithConfirm();
            }
            GUI.color = Color.white;
        }
    }

    private void ExportConsoleLogsToFile()
    {
        try
        {
            if (!TryGetConsoleApi(out var api, out string error))
            {
                EditorUtility.DisplayDialog("导出失败", error, "确定");
                return;
            }

            int count = (int)api.GetCountMethod.Invoke(null, null);
            if (count == 0)
            {
                EditorUtility.DisplayDialog("提示", "Console 中没有日志。", "确定");
                return;
            }

            string folder = GetValidPanelPath();
            Directory.CreateDirectory(folder);

            string fileName = $"ConsoleLog_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log";
            string filePath = Path.Combine(folder, fileName);

            int exportedCount = 0;
            var stats = new ExportStats();

            api.StartGettingEntriesMethod?.Invoke(null, null);
            try
            {
                using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    WriteHeader(writer, count);

                    object entry = Activator.CreateInstance(api.LogEntryType);
                    for (int i = 0; i < count; i++)
                    {
                        api.GetEntryInternalMethod.Invoke(null, new[] { (object)i, entry });

                        var kind = GetEntryKind(api, entry);
                        stats.Add(kind);
                        if (!ShouldExport(kind))
                            continue;

                        string message = GetEntryMessage(api, entry);
                        if (firstLineOnly)
                            message = GetFirstLine(message);

                        writer.WriteLine($"{GetTypeLabel(kind)} {message}");
                        exportedCount++;
                    }

                    writer.WriteLine();
                    writer.WriteLine(new string('=', 80));
                    writer.WriteLine($"Console 总数: {count}");
                    writer.WriteLine($"导出数量: {exportedCount}");
                    writer.WriteLine($"统计: Log {stats.Log} | Warning {stats.Warning} | Error {stats.Error} | Exception {stats.Exception}");
                }
            }
            finally
            {
                api.EndGettingEntriesMethod?.Invoke(null, null);
            }

            Debug.Log($"[ConsoleLogExporter] Console 日志已导出: {filePath}");
            if (revealAfterExport)
                EditorUtility.RevealInFinder(filePath);

            EditorUtility.DisplayDialog("导出成功", $"已导出 {exportedCount}/{count} 条日志\n{filePath}", "确定");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ConsoleLogExporter] 导出 Console 日志失败: {ex}");
            EditorUtility.DisplayDialog("导出失败", ex.Message, "确定");
        }
    }

    private void WriteHeader(StreamWriter writer, int consoleCount)
    {
        writer.WriteLine($"Unity Console 日志导出 - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        writer.WriteLine(new string('=', 80));
        writer.WriteLine($"Console 总数: {consoleCount}");
        writer.WriteLine($"过滤: Log={includeLog}, Warning={includeWarning}, Error={includeError}, Exception={includeException}");
        writer.WriteLine($"只导出首行: {firstLineOnly}");
        writer.WriteLine(new string('=', 80));
        writer.WriteLine();
    }

    private static bool TryGetConsoleApi(out ConsoleApi api, out string error)
    {
        api = null;
        error = null;

        Type logEntriesType = Type.GetType("UnityEditor.LogEntries, UnityEditor");
        Type logEntryType = Type.GetType("UnityEditor.LogEntry, UnityEditor");
        if (logEntriesType == null || logEntryType == null)
        {
            error = "无法访问 Unity Console 日志类型。当前 Unity 版本可能调整了内部 API。";
            return false;
        }

        const BindingFlags staticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags instanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        MethodInfo getCountMethod = logEntriesType.GetMethod("GetCount", staticFlags);
        MethodInfo getEntryInternalMethod = logEntriesType.GetMethod("GetEntryInternal", staticFlags);
        if (getCountMethod == null || getEntryInternalMethod == null)
        {
            error = "无法访问 Unity Console 日志读取方法。当前 Unity 版本可能调整了内部 API。";
            return false;
        }

        api = new ConsoleApi
        {
            LogEntryType = logEntryType,
            GetCountMethod = getCountMethod,
            GetEntryInternalMethod = getEntryInternalMethod,
            StartGettingEntriesMethod = logEntriesType.GetMethod("StartGettingEntries", staticFlags),
            EndGettingEntriesMethod = logEntriesType.GetMethod("EndGettingEntries", staticFlags),
            ClearMethod = logEntriesType.GetMethod("Clear", staticFlags),
            MessageField = logEntryType.GetField("message", instanceFlags) ?? logEntryType.GetField("condition", instanceFlags),
            StackTraceField = logEntryType.GetField("stackTrace", instanceFlags),
            ModeField = logEntryType.GetField("mode", instanceFlags),
        };

        if (api.MessageField == null || api.ModeField == null)
        {
            error = "无法读取 Unity Console 日志字段。当前 Unity 版本可能调整了内部 API。";
            return false;
        }

        return true;
    }

    private static EntryKind GetEntryKind(ConsoleApi api, object entry)
    {
        int mode = api.ModeField != null ? (int)api.ModeField.GetValue(entry) : 0;

        const int errorFlags = 1 | 2 | 256 | 2048 | 4194304;
        const int warningFlags = 4 | 8 | 128 | 512 | 4096;
        const int exceptionFlags = 16 | 131072;

        if ((mode & exceptionFlags) != 0)
            return EntryKind.Exception;
        if ((mode & errorFlags) != 0)
            return EntryKind.Error;
        if ((mode & warningFlags) != 0)
            return EntryKind.Warning;

        return EntryKind.Log;
    }

    private static string GetEntryMessage(ConsoleApi api, object entry)
    {
        string message = api.MessageField?.GetValue(entry) as string ?? string.Empty;
        string stackTrace = api.StackTraceField?.GetValue(entry) as string;

        if (!string.IsNullOrEmpty(stackTrace) && !message.Contains(stackTrace))
            message = $"{message}{Environment.NewLine}{stackTrace}";

        return message.Replace("\r\n", "\n").Replace('\r', '\n');
    }

    private static string GetFirstLine(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        int newline = value.IndexOf('\n');
        return newline >= 0 ? value.Substring(0, newline) : value;
    }

    private bool ShouldExport(EntryKind kind)
    {
        return kind switch
        {
            EntryKind.Log => includeLog,
            EntryKind.Warning => includeWarning,
            EntryKind.Error => includeError,
            EntryKind.Exception => includeException,
            _ => true,
        };
    }

    private bool HasAnyTypeSelected()
    {
        return includeLog || includeWarning || includeError || includeException;
    }

    private static string GetTypeLabel(EntryKind kind)
    {
        return kind switch
        {
            EntryKind.Warning => "[Warning]",
            EntryKind.Error => "[Error]",
            EntryKind.Exception => "[Exception]",
            _ => "[Log]",
        };
    }

    private void OpenOutputFolder()
    {
        string folder = GetValidPanelPath();
        Directory.CreateDirectory(folder);
        EditorUtility.RevealInFinder(folder);
    }

    private static void ClearConsoleWithConfirm()
    {
        if (!EditorUtility.DisplayDialog("确认清空", "确定要清空 Unity Console 吗？", "确定", "取消"))
            return;

        if (!TryGetConsoleApi(out var api, out string error))
        {
            EditorUtility.DisplayDialog("清空失败", error, "确定");
            return;
        }

        if (api.ClearMethod == null)
        {
            EditorUtility.DisplayDialog("清空失败", "当前 Unity 版本没有暴露 Console 清空方法。", "确定");
            return;
        }

        api.ClearMethod.Invoke(null, null);
    }

    private string GetValidPanelPath()
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            return GetDefaultOutputPath();

        return outputPath;
    }

    private static string GetDefaultOutputPath()
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        return Path.Combine(projectRoot, "Logs", "EditorConsole");
    }

    private void SaveOutputPath()
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            EditorPrefs.DeleteKey(OutputPathPrefsKey);
        else
            EditorPrefs.SetString(OutputPathPrefsKey, outputPath);
    }

    private enum EntryKind
    {
        Log,
        Warning,
        Error,
        Exception,
    }

    private sealed class ConsoleApi
    {
        public Type LogEntryType;
        public MethodInfo GetCountMethod;
        public MethodInfo GetEntryInternalMethod;
        public MethodInfo StartGettingEntriesMethod;
        public MethodInfo EndGettingEntriesMethod;
        public MethodInfo ClearMethod;
        public FieldInfo MessageField;
        public FieldInfo StackTraceField;
        public FieldInfo ModeField;
    }

    private struct ExportStats
    {
        public int Log;
        public int Warning;
        public int Error;
        public int Exception;

        public void Add(EntryKind kind)
        {
            switch (kind)
            {
                case EntryKind.Warning:
                    Warning++;
                    break;
                case EntryKind.Error:
                    Error++;
                    break;
                case EntryKind.Exception:
                    Exception++;
                    break;
                default:
                    Log++;
                    break;
            }
        }
    }
}
