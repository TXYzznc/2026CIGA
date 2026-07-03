using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace GameTemplate.EditorTools
{
    /// <summary>
    /// 给 Dynamic 模式的 TMP_FontAsset「预烘」atlas。
    ///
    /// 用法：菜单 Tools/TMP Atlas Preheater
    /// 1. 选择要扫描的根目录（默认 Assets/Resources/Prefab，可改成场景/全工程）
    /// 2. 选择要预烘的目标 TMP_FontAsset（默认勾选当前所有 prefab 用到的字体）
    /// 3. 点「预烘 Atlas」→ 工具扫所有 prefab 里的 TMP_Text.text → 收集 Unicode 集合 → TryAddCharacters → SaveAssets
    ///
    /// 修复场景：编辑模式下 TMP_Text 显示方块（Dynamic Atlas 未注册字符），运行时正常。
    /// 预烘后编辑模式与运行时都正常显示。
    /// </summary>
    public sealed class TMPAtlasPreheater : EditorWindow
    {
        DefaultAsset _scanRoot;
        readonly List<TMP_FontAsset> _targetFonts = new();
        Vector2 _scrollFonts;
        Vector2 _scrollResult;
        string _result = "";

        [MenuItem("Tools/TMP Atlas Preheater")]
        static void Open()
        {
            var win = GetWindow<TMPAtlasPreheater>("TMP Atlas Preheater");
            win.minSize = new Vector2(460, 420);
            win.Show();
        }

        void OnEnable()
        {
            _scanRoot = AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets/Resources/Prefab");
            if (_targetFonts.Count == 0) AutoCollectFontsFromScanRoot();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("扫描根目录", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("拖入要扫的文件夹（递归找所有 prefab）", MessageType.None);
            _scanRoot = (DefaultAsset)EditorGUILayout.ObjectField(_scanRoot, typeof(DefaultAsset), false);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("自动收集字体（从根目录的 prefab）"))
                    AutoCollectFontsFromScanRoot();
                if (GUILayout.Button("清空字体列表"))
                    _targetFonts.Clear();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField($"目标字体 ({_targetFonts.Count})", EditorStyles.boldLabel);
            using (var s = new EditorGUILayout.ScrollViewScope(_scrollFonts, GUILayout.Height(120)))
            {
                _scrollFonts = s.scrollPosition;
                for (int i = 0; i < _targetFonts.Count; i++)
                    _targetFonts[i] = (TMP_FontAsset)EditorGUILayout.ObjectField(_targetFonts[i], typeof(TMP_FontAsset), false);
                if (GUILayout.Button("+ 添加一行"))
                    _targetFonts.Add(null);
            }

            EditorGUILayout.Space(12);
            using (new EditorGUI.DisabledScope(_scanRoot == null || _targetFonts.Count == 0))
            {
                if (GUILayout.Button("预烘 Atlas", GUILayout.Height(32)))
                    Preheat();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("结果", EditorStyles.boldLabel);
            using (var s = new EditorGUILayout.ScrollViewScope(_scrollResult, GUILayout.MinHeight(120)))
            {
                _scrollResult = s.scrollPosition;
                EditorGUILayout.TextArea(_result, GUILayout.ExpandHeight(true));
            }
        }

        // ─────────────────────────────────────────────────────────

        void AutoCollectFontsFromScanRoot()
        {
            _targetFonts.Clear();
            if (_scanRoot == null) return;
            var path = AssetDatabase.GetAssetPath(_scanRoot);
            if (!AssetDatabase.IsValidFolder(path)) return;

            var seen = new HashSet<TMP_FontAsset>();
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { path });
            foreach (var g in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var contents = PrefabUtility.LoadPrefabContents(p);
                try
                {
                    foreach (var t in contents.GetComponentsInChildren<TMP_Text>(true))
                        if (t.font != null) seen.Add(t.font);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
            _targetFonts.AddRange(seen);
        }

        void Preheat()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                _result = "退出 Play Mode 后再跑。";
                return;
            }

            var rootPath = AssetDatabase.GetAssetPath(_scanRoot);
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { rootPath });

            // 1) 收集所有 prefab 里 TMP_Text.text 的字符集合
            var allChars = new HashSet<int>();
            int textCount = 0;
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                var p = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (EditorUtility.DisplayCancelableProgressBar("收集字符",
                        $"{System.IO.Path.GetFileName(p)} ({i + 1}/{prefabGuids.Length})",
                        (float)i / Mathf.Max(1, prefabGuids.Length)))
                    break;

                var contents = PrefabUtility.LoadPrefabContents(p);
                try
                {
                    foreach (var t in contents.GetComponentsInChildren<TMP_Text>(true))
                    {
                        if (string.IsNullOrEmpty(t.text)) continue;
                        textCount++;
                        foreach (var c in t.text) allChars.Add(c);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
            EditorUtility.ClearProgressBar();

            // 2) 给每个目标字体 TryAddCharacters
            var sb = new StringBuilder();
            sb.AppendLine($"扫描 prefab: {prefabGuids.Length}");
            sb.AppendLine($"TMP_Text 数: {textCount}");
            sb.AppendLine($"唯一字符数: {allChars.Count}");
            sb.AppendLine();

            var unicodes = new uint[allChars.Count];
            int idx = 0;
            foreach (var c in allChars) unicodes[idx++] = (uint)c;

            int totalAdded = 0;
            foreach (var font in _targetFonts)
            {
                if (font == null) continue;
                if (font.atlasPopulationMode != AtlasPopulationMode.Dynamic)
                {
                    sb.AppendLine($"[跳过] {font.name}: AtlasPopulationMode={font.atlasPopulationMode}（非 Dynamic）");
                    continue;
                }

                bool ok = font.TryAddCharacters(unicodes, out var missing);
                int added = unicodes.Length - (missing?.Length ?? 0);
                totalAdded += added;
                sb.AppendLine($"[{(ok ? "OK" : "PART")}] {font.name}: 已添加 {added} / {unicodes.Length}，缺失 {missing?.Length ?? 0}");

                EditorUtility.SetDirty(font);
            }

            AssetDatabase.SaveAssets();
            sb.AppendLine();
            sb.AppendLine($"总计添加字形: {totalAdded}");
            sb.AppendLine("提示：重新打开 prefab，编辑模式应该不再显示方块。");

            _result = sb.ToString();
            Debug.Log("[TMPAtlasPreheater] " + _result);
        }
    }
}
