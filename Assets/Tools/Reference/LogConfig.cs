using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 日志配置数据类 - 存储每个脚本的日志启用状态
/// 支持 JSON 序列化，便于保存和加载配置
/// </summary>
[Serializable]
public class LogConfig
{
    [Serializable]
    public class ScriptLogEntry
    {
        public string scriptName;
        public bool isEnabled;

        public ScriptLogEntry() { }

        public ScriptLogEntry(string name, bool enabled)
        {
            scriptName = name;
            isEnabled = enabled;
        }
    }

    public List<ScriptLogEntry> entries = new List<ScriptLogEntry>();

    /// <summary>
    /// 从字典创建配置
    /// </summary>
    public static LogConfig FromDictionary(Dictionary<string, bool> states)
    {
        var config = new LogConfig();
        foreach (var kvp in states)
        {
            config.entries.Add(new ScriptLogEntry(kvp.Key, kvp.Value));
        }
        return config;
    }

    /// <summary>
    /// 转换为字典
    /// </summary>
    public Dictionary<string, bool> ToDictionary()
    {
        var dict = new Dictionary<string, bool>();
        foreach (var entry in entries)
        {
            dict[entry.scriptName] = entry.isEnabled;
        }
        return dict;
    }

    /// <summary>
    /// 转换为 JSON 字符串
    /// </summary>
    public string ToJson()
    {
        return JsonUtility.ToJson(this, true);
    }

    /// <summary>
    /// 从 JSON 字符串加载
    /// </summary>
    public static LogConfig FromJson(string json)
    {
        try
        {
            return JsonUtility.FromJson<LogConfig>(json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to load LogConfig from JSON: {ex.Message}");
            return new LogConfig();
        }
    }
}
