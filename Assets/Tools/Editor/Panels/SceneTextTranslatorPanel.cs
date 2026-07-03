#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

[ToolHubItem("本地化工具/场景文本翻译(Baidu)", "批量扫描->分批请求->统一写回->保存场景（包含未激活对象）", 40)]
public class SceneTextTranslatorPanel : IToolHubPanel
{
    // ====== EditorPrefs 持久化 ======
    private const string Pref_AppId = "ToolHub.SceneTextTranslator.BaiduAppId";
    private const string Pref_Secret = "ToolHub.SceneTextTranslator.BaiduSecretKey";
    private const string Pref_ToLangIndex = "ToolHub.SceneTextTranslator.ToLangIndex";
    private const string Pref_AutoSaveScene = "ToolHub.SceneTextTranslator.AutoSaveScene";

    private string baiduAppId = "";
    private string baiduSecretKey = "";

    private bool autoSaveScene = true;

    // 限流（每批一次请求）
    private const float REQUEST_INTERVAL = 0.35f;

    // 百度单次 q 的安全上限（字节）。建议留余量，避免签名/编码后超限
    private const int MAX_Q_BYTES = 4500;

    // ====== 语言选项 ======
    [Serializable]
    private struct LangOption { public string Code; public string Label; public LangOption(string c, string l) { Code = c; Label = l; } }
    private static readonly LangOption[] LangOptions = new LangOption[]
    {
        new LangOption("zh","中文(简体)"),
        new LangOption("cht","中文(繁体)"),
        new LangOption("en","英语"),
        new LangOption("jp","日语"),
        new LangOption("kor","韩语"),
        new LangOption("fra","法语"),
        new LangOption("de","德语"),
        new LangOption("ru","俄语"),
        new LangOption("spa","西班牙语"),
        new LangOption("pt","葡萄牙语"),
        new LangOption("it","意大利语"),
        new LangOption("th","泰语"),
        new LangOption("vie","越南语"),
        new LangOption("ara","阿拉伯语"),
    };

    private int toLangIndex = 0;
    private string ToLangCode => LangOptions[Mathf.Clamp(toLangIndex, 0, LangOptions.Length - 1)].Code;

    // ====== 批量任务状态 ======
    private bool isTranslating;
    private bool isRequesting;
    private double lastRequestTime;

    private int totalUnique;
    private int doneUnique;

    // 扫描到的条目（组件 -> 原文）
    private struct Entry { public Component comp; public string original; }
    private List<Entry> entries = new();

    // 去重后的原文（保持顺序）
    private List<string> uniqueTexts = new();
    private Dictionary<string, int> textToIndex = new();

    // 翻译结果（index -> dst）
    private string[] translated;

    // chunk 队列：每个 chunk 存一段 uniqueTexts 的索引范围
    private Queue<Chunk> chunks = new();

    private struct Chunk
    {
        public int startIndex;     // 在 uniqueTexts 中的起始 index
        public List<string> lines; // 要发送的行
    }

    // 用于保留原文内部换行：把内部 \n 替换成一个“不可见分隔符”，批量 join 时再用 \n 当行分隔
    private const char INNER_NEWLINE_TOKEN = '\u001F';

    public void OnEnable()
    {
        baiduAppId = EditorPrefs.GetString(Pref_AppId, "");
        baiduSecretKey = EditorPrefs.GetString(Pref_Secret, "");
        toLangIndex = EditorPrefs.GetInt(Pref_ToLangIndex, 0);
        autoSaveScene = EditorPrefs.GetBool(Pref_AutoSaveScene, true);

        EditorApplication.update += Update;
    }

    public void OnDisable()
    {
        EditorApplication.update -= Update;
        Stop(clearProgressBar: true);
        SavePrefs();
    }

    public void OnDestroy()
    {
        EditorApplication.update -= Update;
        Stop(clearProgressBar: true);
    }

    public string GetHelpText() =>
        "流程：扫描场景所有 Text/TMP（含未激活）-> 去重 -> 按长度分批请求 -> 全部返回后统一写回 -> 保存场景。\n" +
        "注意：字体需支持目标语言；百度接口有长度/QPS限制，所以会自动分批。";

    public void OnGUI()
    {
        EditorGUILayout.LabelField("场景文本翻译（批量模式）", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUI.BeginChangeCheck();
            baiduAppId = EditorGUILayout.TextField("App ID", baiduAppId);
            baiduSecretKey = EditorGUILayout.PasswordField("Secret Key", baiduSecretKey);
            if (EditorGUI.EndChangeCheck()) SavePrefs();

            EditorGUI.BeginChangeCheck();
            string[] labels = LangOptions.Select(x => x.Label).ToArray();
            toLangIndex = EditorGUILayout.Popup("目标语言 (to)", toLangIndex, labels);
            autoSaveScene = EditorGUILayout.ToggleLeft("翻译完成后自动保存场景", autoSaveScene);
            if (EditorGUI.EndChangeCheck()) SavePrefs();

            EditorGUILayout.LabelField("源语言 (from)：auto（自动识别）", EditorStyles.miniLabel);
        }

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUI.BeginDisabledGroup(isTranslating);
            if (GUILayout.Button("扫描并开始批量翻译（含未激活对象）", GUILayout.Height(32)))
            {
                if (string.IsNullOrEmpty(baiduAppId) || string.IsNullOrEmpty(baiduSecretKey))
                {
                    EditorUtility.DisplayDialog("错误", "请先填写 App ID 和 Secret Key", "确定");
                }
                else
                {
                    StartBatch();
                }
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(!isTranslating);
            if (GUILayout.Button("停止", GUILayout.Height(22))) Stop(clearProgressBar: true);
            EditorGUI.EndDisabledGroup();
        }

        if (isTranslating)
        {
            float p = totalUnique > 0 ? (float)doneUnique / totalUnique : 0f;
            EditorGUILayout.HelpBox($"翻译中：unique {doneUnique}/{totalUnique} | chunk 剩余 {chunks.Count} | 目标语言 {LangOptions[toLangIndex].Label}", MessageType.Info);
            EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 18), p, $"进度 {doneUnique}/{totalUnique}");
        }
    }

    private void SavePrefs()
    {
        EditorPrefs.SetString(Pref_AppId, baiduAppId ?? "");
        EditorPrefs.SetString(Pref_Secret, baiduSecretKey ?? "");
        EditorPrefs.SetInt(Pref_ToLangIndex, toLangIndex);
        EditorPrefs.SetBool(Pref_AutoSaveScene, autoSaveScene);
    }

    // ========= 1) 扫描 + 去重 + 分批 =========
    private void StartBatch()
    {
        Stop(clearProgressBar: true);

        entries.Clear();
        uniqueTexts.Clear();
        textToIndex.Clear();
        chunks.Clear();

        // 扫描（含未激活）
        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var root in roots)
        {
            foreach (var t in root.GetComponentsInChildren<Text>(true)) CollectEntry(t, t.text);
            foreach (var t in root.GetComponentsInChildren<TextMeshProUGUI>(true)) CollectEntry(t, t.text);
            foreach (var t in root.GetComponentsInChildren<TextMeshPro>(true)) CollectEntry(t, t.text);
        }

        // 生成 uniqueTexts
        foreach (var e in entries)
        {
            if (!textToIndex.ContainsKey(e.original))
            {
                textToIndex[e.original] = uniqueTexts.Count;
                uniqueTexts.Add(e.original);
            }
        }

        totalUnique = uniqueTexts.Count;
        doneUnique = 0;

        if (totalUnique == 0)
        {
            EditorUtility.DisplayDialog("提示", "没有找到需要翻译的文本（可能全是空/数字）。", "确定");
            return;
        }

        translated = new string[totalUnique];

        // 分批（按 UTF8 字节）
        BuildChunks();

        isTranslating = true;
        isRequesting = false;
        lastRequestTime = 0;

        Debug.Log($"[Translator] 扫描到组件条目 {entries.Count} 个，去重后 {totalUnique} 条，将分 {chunks.Count} 批请求。");
    }

    private void CollectEntry(Component comp, string text)
    {
        if (comp == null) return;
        if (string.IsNullOrWhiteSpace(text)) return;
        if (double.TryParse(text, out _)) return;

        // 规范化换行：把内部换行替换成 token，防止破坏“按行拆分”的语义
        string normalized = NormalizeForBatch(text);
        entries.Add(new Entry { comp = comp, original = normalized });
    }

    private string NormalizeForBatch(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        s = s.Replace("\r\n", "\n").Replace("\r", "\n");
        s = s.Replace('\n', INNER_NEWLINE_TOKEN);
        return s;
    }

    private string RestoreFromBatch(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        s = s.Replace(INNER_NEWLINE_TOKEN, '\n');
        return s;
    }

    private void BuildChunks()
    {
        int idx = 0;
        while (idx < uniqueTexts.Count)
        {
            int start = idx;
            int bytes = 0;
            var lines = new List<string>();

            while (idx < uniqueTexts.Count)
            {
                string line = uniqueTexts[idx] ?? "";
                // 作为一行发送
                int lineBytes = Encoding.UTF8.GetByteCount(line);
                // +1 是 join 的 '\n'
                int extra = (lines.Count > 0 ? 1 : 0) + lineBytes;

                if (lines.Count > 0 && bytes + extra > MAX_Q_BYTES)
                    break;

                // 单条过长：也必须单独发（可能仍会超限，这种只能提示用户）
                if (lines.Count == 0 && lineBytes > MAX_Q_BYTES)
                {
                    lines.Add(line);
                    idx++;
                    bytes += lineBytes;
                    break;
                }

                lines.Add(line);
                idx++;
                bytes += extra;
            }

            chunks.Enqueue(new Chunk { startIndex = start, lines = lines });
        }
    }

    // ========= 2) Update 驱动：按 chunk 依次请求 =========
    private void Update()
    {
        if (!isTranslating) return;

        if (isRequesting) return;

        if (chunks.Count == 0)
        {
            // 所有请求完成 -> 写回 -> 保存
            ApplyAllResults();
            Finish();
            return;
        }

        if (EditorApplication.timeSinceStartup - lastRequestTime < REQUEST_INTERVAL)
            return;

        var chunk = chunks.Dequeue();
        lastRequestTime = EditorApplication.timeSinceStartup;

        isRequesting = true;
        RequestChunk(chunk);
    }

    // ========= 3) 请求一批 =========
    private void RequestChunk(Chunk chunk)
    {
        // q = line1\nline2\n...
        string q = string.Join("\n", chunk.lines);

        string salt = DateTime.Now.Ticks.ToString();
        string sign = MD5Encrypt(baiduAppId + q + salt + baiduSecretKey);

        string url =
            $"https://api.fanyi.baidu.com/api/trans/vip/translate" +
            $"?q={UnityWebRequest.EscapeURL(q)}" +
            $"&from=auto" +
            $"&to={ToLangCode}" +
            $"&appid={baiduAppId}" +
            $"&salt={salt}" +
            $"&sign={sign}";

        var req = UnityWebRequest.Get(url);
        req.SendWebRequest().completed += _ =>
        {
            try
            {
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[Translator] 网络错误: {req.error}");
                    Stop(clearProgressBar: true);
                    return;
                }

                HandleChunkResult(chunk, req.downloadHandler.text);
            }
            finally
            {
                req.Dispose();
                isRequesting = false;
            }
        };
    }

    private void HandleChunkResult(Chunk chunk, string json)
    {
        var result = JsonUtility.FromJson<BaiduResult>(json);
        if (result == null)
        {
            Debug.LogError("[Translator] 解析失败：返回为空");
            Stop(clearProgressBar: true);
            return;
        }

        if (!string.IsNullOrEmpty(result.error_code))
        {
            Debug.LogWarning($"[Translator] API 错误: {result.error_code} - {result.error_msg}");
            Stop(clearProgressBar: true);
            return;
        }

        if (result.trans_result == null || result.trans_result.Length == 0)
        {
            Debug.LogError("[Translator] API 返回 trans_result 为空");
            Stop(clearProgressBar: true);
            return;
        }

        // 关键：trans_result 数量应当和 lines 数一致（按行对应）
        int n = Mathf.Min(result.trans_result.Length, chunk.lines.Count);

        for (int i = 0; i < n; i++)
        {
            int globalIndex = chunk.startIndex + i;
            if (globalIndex < 0 || globalIndex >= translated.Length) continue;

            string dst = result.trans_result[i].dst;
            translated[globalIndex] = dst;
            doneUnique++;
        }

        // 如果数量不一致，记录一下（通常意味着接口按自己的规则拆分了文本）
        if (result.trans_result.Length != chunk.lines.Count)
        {
            Debug.LogWarning($"[Translator] 返回条数({result.trans_result.Length})与请求行数({chunk.lines.Count})不一致，可能文本包含特殊分隔符导致拆分。建议减少单条文本中的换行/特殊字符。");
        }
    }

    // ========= 4) 全部完成后写回 =========
    private void ApplyAllResults()
    {
        // 建立原文 -> 译文 的映射（注意还原内部换行 token）
        var map = new Dictionary<string, string>(uniqueTexts.Count);
        for (int i = 0; i < uniqueTexts.Count; i++)
        {
            string src = uniqueTexts[i];
            string dst = translated[i];

            // 若 dst 为空（失败/漏回），则保持原文
            if (string.IsNullOrEmpty(dst)) dst = src;

            map[src] = dst;
        }

        // 批量 Undo
        var comps = entries.Select(e => e.comp).Where(c => c != null).Distinct().ToArray();
        Undo.RecordObjects(comps, "Translate Scene Text (Batch)");

        foreach (var e in entries)
        {
            if (e.comp == null) continue;
            if (!map.TryGetValue(e.original, out var dst)) continue;

            // 还原内部换行
            string restored = RestoreFromBatch(dst);

            SetTextContent(e.comp, restored);
        }

        // 标记并保存场景
        var scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);

        if (autoSaveScene)
        {
            bool ok = EditorSceneManager.SaveScene(scene);
            Debug.Log(ok
                ? "[Translator] 场景已保存。"
                : "[Translator] 场景保存失败（可能场景未在磁盘上有路径或被锁定）。");
        }
        else
        {
            Debug.Log("[Translator] 已写回文本并标记场景 Dirty（未自动保存）。");
        }
    }

    private void SetTextContent(Component comp, string newText)
    {
        if (comp is Text ui) ui.text = newText;
        else if (comp is TextMeshProUGUI tmpUgui) tmpUgui.text = newText;
        else if (comp is TextMeshPro tmp) tmp.text = newText;

        EditorUtility.SetDirty(comp);
    }

    private void Finish()
    {
        Stop(clearProgressBar: true);
        EditorUtility.DisplayDialog("完成", "批量翻译完成！", "确定");
    }

    private void Stop(bool clearProgressBar)
    {
        isTranslating = false;
        isRequesting = false;
        entries.Clear();
        uniqueTexts.Clear();
        textToIndex.Clear();
        chunks.Clear();

        translated = null;

        totalUnique = 0;
        doneUnique = 0;

        if (clearProgressBar) EditorUtility.ClearProgressBar();
    }

    private string MD5Encrypt(string str)
    {
        using (MD5 md5 = MD5.Create())
        {
            byte[] bytes = Encoding.UTF8.GetBytes(str);
            byte[] hash = md5.ComputeHash(bytes);
            var sb = new StringBuilder();
            foreach (byte b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    [Serializable]
    private class BaiduResult
    {
        public string from;
        public string to;
        public TransResult[] trans_result;

        public string error_code;
        public string error_msg;
    }

    [Serializable]
    private class TransResult
    {
        public string src;
        public string dst;
    }
}
#endif