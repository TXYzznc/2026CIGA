using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace GameTemplate.EditorTools
{
    /// <summary>
    /// 字体批量替换工具。
    ///
    /// 用法：菜单 Tools/Font Replacer 打开窗口
    /// 1. 拖入目标（文件夹 / Prefab 资产 / 场景中的 GameObject）
    /// 2. 勾选要替换的类型（UGUI Text / TMP_Text）并指定新字体
    /// 3. 先「扫描预览」看会改多少个，再「执行替换」
    ///
    /// 注意：
    /// - 场景对象支持 Undo（Ctrl+Z 可回退），Prefab 资产修改不可撤销，请先提交 Git
    /// - 不可在 Play Mode 下使用
    /// </summary>
    public sealed class FontReplacerWindow : EditorWindow
    {
        Object _target;

        bool _replaceUGUI = true;
        Font _targetFont;

        bool _replaceTMP = true;
        TMP_FontAsset _targetTMPFont;

        bool _recursive = true;
        bool _includeInactive = true;

        Vector2 _scroll;
        string _lastResult = "";

        [MenuItem("Tools/Font Replacer")]
        static void Open()
        {
            var win = GetWindow<FontReplacerWindow>("Font Replacer");
            win.minSize = new Vector2(420, 360);
            win.Show();
        }

        void OnGUI()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox("请退出 Play Mode 后再使用此工具。", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("目标", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("可拖入：文件夹 / Prefab 资产 / 场景中的 GameObject", MessageType.None);
            _target = EditorGUILayout.ObjectField("Target", _target, typeof(Object), true);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("替换类型", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                _replaceUGUI = EditorGUILayout.ToggleLeft("UGUI Text", _replaceUGUI, GUILayout.Width(120));
                using (new EditorGUI.DisabledScope(!_replaceUGUI))
                    _targetFont = (Font)EditorGUILayout.ObjectField(_targetFont, typeof(Font), false);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _replaceTMP = EditorGUILayout.ToggleLeft("TMP_Text", _replaceTMP, GUILayout.Width(120));
                using (new EditorGUI.DisabledScope(!_replaceTMP))
                    _targetTMPFont = (TMP_FontAsset)EditorGUILayout.ObjectField(_targetTMPFont, typeof(TMP_FontAsset), false);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("选项", EditorStyles.boldLabel);
            _recursive = EditorGUILayout.ToggleLeft("递归子节点", _recursive);
            _includeInactive = EditorGUILayout.ToggleLeft("包含 inactive 对象", _includeInactive);

            EditorGUILayout.Space(12);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!CanRun(preview: true)))
                {
                    if (GUILayout.Button("扫描预览", GUILayout.Height(28)))
                        Run(preview: true);
                }
                using (new EditorGUI.DisabledScope(!CanRun(preview: false)))
                {
                    if (GUILayout.Button("执行替换", GUILayout.Height(28)))
                    {
                        if (EditorUtility.DisplayDialog("确认替换",
                                "Prefab 资产替换不可撤销（场景对象可 Ctrl+Z）。确认继续？",
                                "执行", "取消"))
                            Run(preview: false);
                    }
                }
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("结果", EditorStyles.boldLabel);
            using (var s = new EditorGUILayout.ScrollViewScope(_scroll, GUILayout.MinHeight(120)))
            {
                _scroll = s.scrollPosition;
                EditorGUILayout.TextArea(_lastResult, GUILayout.ExpandHeight(true));
            }
        }

        bool CanRun(bool preview)
        {
            if (_target == null) return false;
            if (!_replaceUGUI && !_replaceTMP) return false;
            if (!preview)
            {
                if (_replaceUGUI && _targetFont == null) return false;
                if (_replaceTMP && _targetTMPFont == null) return false;
            }
            return true;
        }

        // ─────────────────────────────────────────────────────────
        // 调度
        // ─────────────────────────────────────────────────────────

        void Run(bool preview)
        {
            var stats = new Stats();
            var path = AssetDatabase.GetAssetPath(_target);

            if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path))
                ProcessFolder(path, preview, stats);
            else if (_target is GameObject go)
            {
                if (PrefabUtility.IsPartOfPrefabAsset(go))
                    ProcessPrefabAsset(AssetDatabase.GetAssetPath(go), preview, stats);
                else
                    ProcessSceneObject(go, preview, stats);
            }
            else
            {
                _lastResult = "不支持的目标类型。请拖入文件夹 / Prefab 资产 / 场景 GameObject。";
                return;
            }

            if (!preview) AssetDatabase.SaveAssets();
            _lastResult = stats.Format(preview);
            Debug.Log($"[FontReplacer] {(preview ? "预览" : "替换")}完成。\n{_lastResult}");
        }

        void ProcessFolder(string folderPath, bool preview, Stats stats)
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
            stats.PrefabFound = guids.Length;
            for (int i = 0; i < guids.Length; i++)
            {
                var p = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (EditorUtility.DisplayCancelableProgressBar("Font Replacer",
                        $"{Path.GetFileName(p)} ({i + 1}/{guids.Length})",
                        (float)i / Mathf.Max(1, guids.Length)))
                    break;
                ProcessPrefabAsset(p, preview, stats);
            }
            EditorUtility.ClearProgressBar();
        }

        void ProcessPrefabAsset(string path, bool preview, Stats stats)
        {
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int beforeUGUI = stats.UGUIReplaced;
                int beforeTMP = stats.TMPReplaced;
                CollectAndReplace(contents, preview, stats);
                bool changed = stats.UGUIReplaced > beforeUGUI || stats.TMPReplaced > beforeTMP;

                if (!preview && changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                    stats.PrefabsModified++;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        void ProcessSceneObject(GameObject root, bool preview, Stats stats)
        {
            int beforeUGUI = stats.UGUIReplaced;
            int beforeTMP = stats.TMPReplaced;
            CollectAndReplace(root, preview, stats);
            bool changed = stats.UGUIReplaced > beforeUGUI || stats.TMPReplaced > beforeTMP;

            if (!preview && changed)
            {
                EditorSceneManager.MarkSceneDirty(root.scene);
                stats.SceneObjectsModified++;
            }
        }

        // ─────────────────────────────────────────────────────────
        // 核心替换
        // ─────────────────────────────────────────────────────────

        void CollectAndReplace(GameObject root, bool preview, Stats stats)
        {
            if (_replaceUGUI)
            {
                var list = _recursive
                    ? new List<Text>(root.GetComponentsInChildren<Text>(_includeInactive))
                    : CollectSelf<Text>(root);
                foreach (var t in list)
                {
                    if (preview)
                    {
                        if (t.font != _targetFont) stats.UGUIReplaced++;
                    }
                    else
                    {
                        if (t.font == _targetFont) continue;
                        Undo.RecordObject(t, "Replace Font (UGUI)");
                        t.font = _targetFont;
                        EditorUtility.SetDirty(t);
                        stats.UGUIReplaced++;
                    }
                }
            }

            if (_replaceTMP)
            {
                var list = _recursive
                    ? new List<TMP_Text>(root.GetComponentsInChildren<TMP_Text>(_includeInactive))
                    : CollectSelf<TMP_Text>(root);
                foreach (var t in list)
                {
                    if (preview)
                    {
                        if (t.font != _targetTMPFont) stats.TMPReplaced++;
                    }
                    else
                    {
                        if (t.font == _targetTMPFont) continue;
                        Undo.RecordObject(t, "Replace Font (TMP)");
                        t.font = _targetTMPFont;
                        EditorUtility.SetDirty(t);
                        stats.TMPReplaced++;
                    }
                }
            }
        }

        static List<T> CollectSelf<T>(GameObject root) where T : Component
        {
            var list = new List<T>();
            root.GetComponents(list);
            return list;
        }

        // ─────────────────────────────────────────────────────────
        // 统计
        // ─────────────────────────────────────────────────────────

        sealed class Stats
        {
            public int PrefabFound;
            public int PrefabsModified;
            public int SceneObjectsModified;
            public int UGUIReplaced;
            public int TMPReplaced;

            public string Format(bool preview)
            {
                var verb = preview ? "将替换" : "已替换";
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"UGUI Text  : {verb} {UGUIReplaced}");
                sb.AppendLine($"TMP_Text   : {verb} {TMPReplaced}");
                if (PrefabFound > 0)
                {
                    sb.AppendLine($"扫描 Prefab: {PrefabFound}");
                    if (!preview) sb.AppendLine($"修改 Prefab: {PrefabsModified}");
                }
                if (SceneObjectsModified > 0)
                    sb.AppendLine($"修改场景对象: {SceneObjectsModified}");
                return sb.ToString();
            }
        }
    }
}
