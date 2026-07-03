using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// 日志配置管理器 - 负责配置的持久化保存和加载
/// 支持 JSON 文件和 PlayerPrefs 两种方式，支持多配置预设管理
/// </summary>
public static class LogConfigManager
{
    private const string CONFIG_FOLDER = "LogConfig";
    private const string CONFIG_FILENAME = "log_settings.json";
    private const string PRESETS_FOLDER = "Presets";
    private const string PLAYERPREFS_KEY = "LogConfig_Settings";

    private static string GetConfigFilePath()
    {
        string folder = Path.Combine(Application.persistentDataPath, CONFIG_FOLDER);
        return Path.Combine(folder, CONFIG_FILENAME);
    }

    private static string GetPresetsFolderPath()
    {
        return Path.Combine(Application.persistentDataPath, CONFIG_FOLDER, PRESETS_FOLDER);
    }

    private static string GetPresetFilePath(string presetName)
    {
        return Path.Combine(GetPresetsFolderPath(), $"{presetName}.json");
    }

    #region 预设管理

    public static bool SavePreset(string presetName, Dictionary<string, bool> states)
    {
        try
        {
            string folderPath = GetPresetsFolderPath();
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            var config = LogConfig.FromDictionary(states);
            string json = config.ToJson();
            File.WriteAllText(GetPresetFilePath(presetName), json);
            Debug.Log($"[LogConfigManager] 预设 \"{presetName}\" 已保存");
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LogConfigManager] 保存预设失败: {ex.Message}");
            return false;
        }
    }

    public static Dictionary<string, bool> LoadPreset(string presetName)
    {
        try
        {
            string filePath = GetPresetFilePath(presetName);
            if (!File.Exists(filePath))
                return new Dictionary<string, bool>();

            string json = File.ReadAllText(filePath);
            var config = LogConfig.FromJson(json);
            Debug.Log($"[LogConfigManager] 预设 \"{presetName}\" 已加载");
            return config.ToDictionary();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LogConfigManager] 加载预设失败: {ex.Message}");
            return new Dictionary<string, bool>();
        }
    }

    public static List<string> GetAllPresetNames()
    {
        var names = new List<string>();
        string folderPath = GetPresetsFolderPath();
        if (!Directory.Exists(folderPath))
            return names;

        foreach (var file in Directory.GetFiles(folderPath, "*.json"))
        {
            names.Add(Path.GetFileNameWithoutExtension(file));
        }
        names.Sort();
        return names;
    }

    public static bool DeletePreset(string presetName)
    {
        try
        {
            string filePath = GetPresetFilePath(presetName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                Debug.Log($"[LogConfigManager] 预设 \"{presetName}\" 已删除");
                return true;
            }
            return false;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LogConfigManager] 删除预设失败: {ex.Message}");
            return false;
        }
    }

    public static bool RenamePreset(string oldName, string newName)
    {
        try
        {
            string oldPath = GetPresetFilePath(oldName);
            string newPath = GetPresetFilePath(newName);
            if (!File.Exists(oldPath) || File.Exists(newPath))
                return false;

            File.Move(oldPath, newPath);
            Debug.Log($"[LogConfigManager] 预设 \"{oldName}\" 已重命名为 \"{newName}\"");
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LogConfigManager] 重命名预设失败: {ex.Message}");
            return false;
        }
    }

    #endregion

    /// <summary>
    /// 保存配置到 JSON 文件
    /// </summary>
    public static bool SaveConfigToFile(Dictionary<string, bool> states)
    {
        try
        {
            var config = LogConfig.FromDictionary(states);
            string json = config.ToJson();

            string folderPath = Path.Combine(Application.persistentDataPath, CONFIG_FOLDER);
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string filePath = GetConfigFilePath();
            File.WriteAllText(filePath, json);

            Debug.Log($"[LogConfigManager] 日志配置已保存到: {filePath}");
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LogConfigManager] 保存配置失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 从 JSON 文件加载配置
    /// </summary>
    public static Dictionary<string, bool> LoadConfigFromFile()
    {
        try
        {
            string filePath = GetConfigFilePath();
            if (!File.Exists(filePath))
            {
                Debug.Log($"[LogConfigManager] 配置文件不存在: {filePath}");
                return new Dictionary<string, bool>();
            }

            string json = File.ReadAllText(filePath);
            var config = LogConfig.FromJson(json);

            Debug.Log($"[LogConfigManager] 日志配置已加载");
            return config.ToDictionary();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LogConfigManager] 加载配置失败: {ex.Message}");
            return new Dictionary<string, bool>();
        }
    }

    /// <summary>
    /// 保存配置到 PlayerPrefs
    /// </summary>
    public static void SaveConfigToPlayerPrefs(Dictionary<string, bool> states)
    {
        try
        {
            var config = LogConfig.FromDictionary(states);
            string json = config.ToJson();
            PlayerPrefs.SetString(PLAYERPREFS_KEY, json);
            PlayerPrefs.Save();

            Debug.Log($"[LogConfigManager] 日志配置已保存到 PlayerPrefs");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LogConfigManager] 保存到 PlayerPrefs 失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 从 PlayerPrefs 加载配置
    /// </summary>
    public static Dictionary<string, bool> LoadConfigFromPlayerPrefs()
    {
        try
        {
            if (!PlayerPrefs.HasKey(PLAYERPREFS_KEY))
            {
                Debug.Log($"[LogConfigManager] PlayerPrefs 中无配置数据");
                return new Dictionary<string, bool>();
            }

            string json = PlayerPrefs.GetString(PLAYERPREFS_KEY);
            var config = LogConfig.FromJson(json);

            Debug.Log($"[LogConfigManager] 日志配置已从 PlayerPrefs 加载");
            return config.ToDictionary();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LogConfigManager] 从 PlayerPrefs 加载失败: {ex.Message}");
            return new Dictionary<string, bool>();
        }
    }

    /// <summary>
    /// 删除保存的配置文件
    /// </summary>
    public static bool DeleteConfigFile()
    {
        try
        {
            string filePath = GetConfigFilePath();
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                Debug.Log($"[LogConfigManager] 配置文件已删除");
                return true;
            }
            return false;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LogConfigManager] 删除配置文件失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 清除 PlayerPrefs 中的配置
    /// </summary>
    public static void ClearPlayerPrefsConfig()
    {
        if (PlayerPrefs.HasKey(PLAYERPREFS_KEY))
        {
            PlayerPrefs.DeleteKey(PLAYERPREFS_KEY);
            PlayerPrefs.Save();
            Debug.Log($"[LogConfigManager] PlayerPrefs 配置已清除");
        }
    }

    /// <summary>
    /// 检查配置文件是否存在
    /// </summary>
    public static bool ConfigFileExists()
    {
        return File.Exists(GetConfigFilePath());
    }

    /// <summary>
    /// 获取配置文件路径（编辑器用）
    /// </summary>
    public static string GetConfigFilePathForEditor()
    {
        return GetConfigFilePath();
    }
}
