using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

/// <summary>
/// 增强版 Debug 工具类
/// 所有方法均支持按脚本级别控制日志输出
/// 通过 scriptName 参数标识日志来源脚本，LogConfigPanel 可对每个脚本的日志进行独立控制
/// </summary>
public static class DebugEx
{
    #region 颜色常量

    public static class Color
    {
        public const string Red = "#FF0000";
        public const string Green = "#00FF00";
        public const string Blue = "#0000FF";
        public const string Yellow = "#FFFF00";
        public const string Cyan = "#00FFFF";
        public const string Magenta = "#FF00FF";
        public const string Orange = "#FFA500";
        public const string Purple = "#800080";
        public const string Pink = "#FFC0CB";
        public const string White = "#FFFFFF";
        public const string Gray = "#808080";
        public const string LightGreen = "#90EE90";
        public const string LightBlue = "#ADD8E6";
    }

    #endregion

    #region 配置

    /// <summary>
    /// 全局总开关
    /// </summary>
    public static bool EnableLog = false;

    /// <summary>
    /// 脚本级别的日志控制字典（由 LogConfigPanel 管理）
    /// </summary>
    private static Dictionary<string, bool> scriptLogEnabled = new();

    #endregion

    #region 脚本日志控制方法

    /// <summary>
    /// 获取所有脚本的日志启用状态
    /// </summary>
    public static Dictionary<string, bool> GetAllScriptLogStates()
    {
        return new Dictionary<string, bool>(scriptLogEnabled);
    }

    /// <summary>
    /// 设置所有脚本的日志启用状态
    /// </summary>
    public static void SetAllScriptLogEnabled(Dictionary<string, bool> states)
    {
        scriptLogEnabled = new Dictionary<string, bool>(states);
    }

    /// <summary>
    /// 清除脚本日志配置
    /// </summary>
    public static void ClearScriptLogConfig()
    {
        scriptLogEnabled.Clear();
    }

    /// <summary>
    /// 检查某个脚本的日志是否启用
    /// </summary>
    private static bool IsScriptLogEnabled(string scriptName)
    {
        if (!EnableLog)
            return false;

        if (!scriptLogEnabled.ContainsKey(scriptName))
        {
            scriptLogEnabled[scriptName] = true;
        }

        return scriptLogEnabled[scriptName];
    }

    #endregion

    #region Log 方法

    /// <summary>
    /// 普通日志（仅限于脚本）
    /// </summary>
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Log(string scriptName, object message)
    {
        if (!IsScriptLogEnabled(scriptName))
            return;
        UnityEngine.Debug.Log($"[{scriptName}] {message}");
    }

    /// <summary>
    /// 带颜色的日志（仅限于脚本）
    /// </summary>
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void LogColor(string scriptName, object message, string color)
    {
        if (!IsScriptLogEnabled(scriptName))
            return;
        UnityEngine.Debug.Log($"<color={color}>[{scriptName}] {message}</color>");
    }

    /// <summary>
    /// 带 Context 的日志（仅限于脚本，可在 Console 中点击定位到对象）
    /// </summary>
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Log(string scriptName, object message, Object context)
    {
        if (!IsScriptLogEnabled(scriptName))
            return;
        UnityEngine.Debug.Log($"[{scriptName}] {message}", context);
    }

    #endregion

    #region Warning 方法

    /// <summary>
    /// 警告日志（仅限于脚本）
    /// </summary>
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Warning(string scriptName, object message)
    {
        if (!IsScriptLogEnabled(scriptName))
            return;
        UnityEngine.Debug.LogWarning($"[{scriptName}] {message}");
    }

    /// <summary>
    /// 带 Context 的警告日志（仅限于脚本）
    /// </summary>
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Warning(string scriptName, object message, Object context)
    {
        if (!IsScriptLogEnabled(scriptName))
            return;
        UnityEngine.Debug.LogWarning($"[{scriptName}] {message}", context);
    }

    #endregion

    #region Error 方法

    /// <summary>
    /// 错误日志（仅限于脚本）
    /// </summary>
    public static void Error(string scriptName, object message)
    {
        if (!IsScriptLogEnabled(scriptName))
            return;
        UnityEngine.Debug.LogError($"[{scriptName}] {message}");
    }

    /// <summary>
    /// 带 Context 的错误日志（仅限于脚本）
    /// </summary>
    public static void Error(string scriptName, object message, Object context)
    {
        if (!IsScriptLogEnabled(scriptName))
            return;
        UnityEngine.Debug.LogError($"[{scriptName}] {message}", context);
    }

    #endregion

    #region 便捷方法

    /// <summary>
    /// 成功日志（绿色）
    /// </summary>
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Success(string scriptName, object message)
    {
        LogColor(scriptName, message, Color.Green);
    }

    /// <summary>
    /// 失败日志（红色）
    /// </summary>
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Fail(string scriptName, object message)
    {
        LogColor(scriptName, message, Color.Red);
    }

    #endregion

    #region 断言

    /// <summary>
    /// 断言（条件为 false 时输出错误）
    /// </summary>
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Assert(string scriptName, bool condition, string message)
    {
        if (!condition)
        {
            Error(scriptName, $"断言失败: {message}");
        }
    }

    #endregion
}
