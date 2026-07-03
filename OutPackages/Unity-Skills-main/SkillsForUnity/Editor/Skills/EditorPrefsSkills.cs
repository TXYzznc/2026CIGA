using UnityEditor;

namespace UnitySkills
{
    /// <summary>
    /// Read-only access to EditorPrefs. Keys containing sensitive keywords
    /// (token / password / secret / apikey / access) are blocked from value
    /// reads to avoid leaking credentials into AI transcripts. Existence
    /// checks (has_key) are always allowed so callers can verify whether
    /// a credential has been configured without exposing its value.
    /// </summary>
    public static class EditorPrefsSkills
    {
        private static readonly string[] SensitiveKeywords =
        {
            "token", "password", "secret", "apikey", "access"
        };

        private static bool IsSensitive(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            var lower = key.ToLowerInvariant();
            for (int i = 0; i < SensitiveKeywords.Length; i++)
            {
                if (lower.Contains(SensitiveKeywords[i])) return true;
            }
            return false;
        }

        private static object BlockedError(string key)
        {
            return new
            {
                error = $"Key '{key}' contains a sensitive keyword ({string.Join("/", SensitiveKeywords)}) and is blocked from being read. Use editor_prefs_has_key to check existence without exposing the value."
            };
        }

        [UnitySkill("editor_prefs_has_key", "Check whether an EditorPrefs key exists. Safe on sensitive keys (tokens/passwords) because the value itself is never returned.",
            Category = SkillCategory.Editor, Operation = SkillOperation.Query,
            Tags = new[] { "prefs", "config", "exists" },
            Outputs = new[] { "hasKey" },
            ReadOnly = true,
            Mode = SkillMode.SemiAuto)]
        public static object HasKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return new { error = "key is empty" };
            return new { success = true, hasKey = EditorPrefs.HasKey(key) };
        }

        [UnitySkill("editor_prefs_get_string", "Read a string value from EditorPrefs. Blocked for keys containing sensitive keywords to prevent credential leakage.",
            Category = SkillCategory.Editor, Operation = SkillOperation.Query,
            Tags = new[] { "prefs", "config", "read", "string" },
            Outputs = new[] { "value", "hasKey" },
            ReadOnly = true,
            Mode = SkillMode.SemiAuto)]
        public static object GetString(string key, string defaultValue = "")
        {
            if (string.IsNullOrEmpty(key))
                return new { error = "key is empty" };
            if (IsSensitive(key)) return BlockedError(key);
            return new
            {
                success = true,
                hasKey = EditorPrefs.HasKey(key),
                value = EditorPrefs.GetString(key, defaultValue)
            };
        }

        [UnitySkill("editor_prefs_get_int", "Read an int value from EditorPrefs. Blocked for keys containing sensitive keywords to prevent credential leakage.",
            Category = SkillCategory.Editor, Operation = SkillOperation.Query,
            Tags = new[] { "prefs", "config", "read", "int" },
            Outputs = new[] { "value", "hasKey" },
            ReadOnly = true,
            Mode = SkillMode.SemiAuto)]
        public static object GetInt(string key, int defaultValue = 0)
        {
            if (string.IsNullOrEmpty(key))
                return new { error = "key is empty" };
            if (IsSensitive(key)) return BlockedError(key);
            return new
            {
                success = true,
                hasKey = EditorPrefs.HasKey(key),
                value = EditorPrefs.GetInt(key, defaultValue)
            };
        }

        [UnitySkill("editor_prefs_get_float", "Read a float value from EditorPrefs. Blocked for keys containing sensitive keywords to prevent credential leakage.",
            Category = SkillCategory.Editor, Operation = SkillOperation.Query,
            Tags = new[] { "prefs", "config", "read", "float" },
            Outputs = new[] { "value", "hasKey" },
            ReadOnly = true,
            Mode = SkillMode.SemiAuto)]
        public static object GetFloat(string key, float defaultValue = 0f)
        {
            if (string.IsNullOrEmpty(key))
                return new { error = "key is empty" };
            if (IsSensitive(key)) return BlockedError(key);
            return new
            {
                success = true,
                hasKey = EditorPrefs.HasKey(key),
                value = EditorPrefs.GetFloat(key, defaultValue)
            };
        }

        [UnitySkill("editor_prefs_get_bool", "Read a bool value from EditorPrefs. Blocked for keys containing sensitive keywords to prevent credential leakage.",
            Category = SkillCategory.Editor, Operation = SkillOperation.Query,
            Tags = new[] { "prefs", "config", "read", "bool" },
            Outputs = new[] { "value", "hasKey" },
            ReadOnly = true,
            Mode = SkillMode.SemiAuto)]
        public static object GetBool(string key, bool defaultValue = false)
        {
            if (string.IsNullOrEmpty(key))
                return new { error = "key is empty" };
            if (IsSensitive(key)) return BlockedError(key);
            return new
            {
                success = true,
                hasKey = EditorPrefs.HasKey(key),
                value = EditorPrefs.GetBool(key, defaultValue)
            };
        }
    }
}
