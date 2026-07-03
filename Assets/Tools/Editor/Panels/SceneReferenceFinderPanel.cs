#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景引用查找器（ToolHub 面板版）
/// </summary>
[ToolHubItem("场景工具/场景引用查找器", "查找当前已加载场景内，哪些对象/组件引用了指定对象/组件", 35)]
public class SceneReferenceFinderPanel : IToolHubPanel
{
    [Serializable]
    private class ReferenceInfo
    {
        public Component holder;                 // 谁持有这个字段（哪个组件）
        public GameObject holderGO;              // holder 所在的 GameObject
        public string propertyPath;              // 引用字段路径（含数组路径）
        public UnityEngine.Object referencedTarget; // 被引用的目标（用于显示）
    }

    private UnityEngine.Object _target;  // GameObject 或 Component
    private bool _includeComponentsOnGameObject = true;
    private bool _showOnlyUniqueHolders = false;

    private Vector2 _scroll;
    private readonly List<ReferenceInfo> _results = new();
    private Vector2 _topScroll;

    public void OnEnable()
    {
        // 默认用当前选择，方便开箱即用
        if (_target == null)
            _target = Selection.activeObject;
    }

    public void OnDisable() { }
    public void OnDestroy() { }

    public string GetHelpText() =>
        "说明：\n" +
        "1) 选择一个 GameObject 或其某个 Component。\n" +
        "2) 点击“开始查找”，扫描所有组件的序列化 ObjectReference 字段。\n" +
        "限制：仅能查到 Unity 序列化字段中的引用（public / [SerializeField] 等）。";

    public void OnGUI()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("查找场景内引用（Serialized Fields/ObjectReference）", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                _target = EditorGUILayout.ObjectField(
                    new GUIContent("目标 (GameObject 或 Component)", "拖入一个 GameObject 或某个组件"),
                    _target,
                    typeof(UnityEngine.Object),
                    true);

                if (GUILayout.Button("使用当前选择", GUILayout.Width(110)))
                {
                    _target = Selection.activeObject;
                }
            }

            _includeComponentsOnGameObject = EditorGUILayout.ToggleLeft(
                new GUIContent("目标为 GameObject 时，也匹配其所有组件引用", "字段类型常是组件类型，引用可能存的是组件而不是 GO"),
                _includeComponentsOnGameObject);

            _showOnlyUniqueHolders = EditorGUILayout.ToggleLeft(
                new GUIContent("仅显示唯一引用者(按组件去重)", "同一组件多个字段引用时可只显示一次"),
                _showOnlyUniqueHolders);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = _target != null;
                if (GUILayout.Button("开始查找", GUILayout.Height(28)))
                {
                    FindReferencesInLoadedScenes();
                }
                GUI.enabled = true;

                if (GUILayout.Button("清空结果", GUILayout.Height(28)))
                {
                    _results.Clear();
                }
            }
        }

        EditorGUILayout.Space(6);
        DrawResultHeader();
        DrawResults();
    }

    private void DrawResultHeader()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField($"结果：{_results.Count}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            GUI.enabled = _results.Count > 0;
            if (GUILayout.Button("选中所有引用者(GameObject)", GUILayout.Width(190)))
            {
                var gos = new List<UnityEngine.Object>();
                foreach (var r in _results)
                {
                    if (r != null && r.holderGO != null)
                        gos.Add(r.holderGO);
                }
                Selection.objects = gos.ToArray();
            }
            GUI.enabled = true;
        }
    }

    private void DrawResults()
    {
        using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
        {
            _scroll = scroll.scrollPosition;

            if (_results.Count == 0)
            {
                EditorGUILayout.HelpBox("没有结果。选择目标后点击“开始查找”。", MessageType.Info);
                return;
            }

            for (int i = 0; i < _results.Count; i++)
            {
                var r = _results[i];
                if (r == null || r.holder == null || r.holderGO == null) continue;

                using (new EditorGUILayout.VerticalScope("box"))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("选中", GUILayout.Width(52)))
                        {
                            Selection.activeObject = r.holderGO;
                            EditorGUIUtility.PingObject(r.holderGO);
                        }

                        if (GUILayout.Button("选中组件", GUILayout.Width(78)))
                        {
                            Selection.activeObject = r.holder;
                            EditorGUIUtility.PingObject(r.holder);
                        }

                        GUILayout.Space(6);

                        string holderPath = GetHierarchyPath(r.holderGO);
                        string title = $"{r.holder.GetType().Name}  @  {holderPath}";
                        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                    }

                    EditorGUILayout.LabelField("字段路径", r.propertyPath);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("引用目标", r.referencedTarget != null ? r.referencedTarget.name : "null");
                        GUILayout.FlexibleSpace();

                        GUI.enabled = r.referencedTarget != null;
                        if (GUILayout.Button("Ping目标", GUILayout.Width(80)))
                            EditorGUIUtility.PingObject(r.referencedTarget);
                        GUI.enabled = true;
                    }
                }
            }
        }
    }

    private void FindReferencesInLoadedScenes()
    {
        _results.Clear();
        if (_target == null) return;

        var targets = BuildTargetSet(_target, _includeComponentsOnGameObject);

        try
        {
            var allComponents = GatherAllComponentsInLoadedScenes();
            int total = allComponents.Count;

            for (int i = 0; i < total; i++)
            {
                var c = allComponents[i];
                if (c == null) continue;

                if (EditorUtility.DisplayCancelableProgressBar(
                        "Reference Finder",
                        $"Scanning: {c.GetType().Name}  ({i + 1}/{total})",
                        total <= 0 ? 1f : (float)(i + 1) / total))
                {
                    break;
                }

                ScanComponentSerializedObjectReferences(c, targets);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (_showOnlyUniqueHolders)
        {
            var unique = new List<ReferenceInfo>();
            var seen = new HashSet<int>();
            foreach (var r in _results)
            {
                if (r == null || r.holder == null) continue;
                int id = r.holder.GetInstanceID();
                if (seen.Add(id))
                    unique.Add(r);
            }
            _results.Clear();
            _results.AddRange(unique);
        }
    }

    private static HashSet<UnityEngine.Object> BuildTargetSet(UnityEngine.Object target, bool includeComponentsIfGO)
    {
        var set = new HashSet<UnityEngine.Object>();

        if (target is GameObject go)
        {
            set.Add(go);

            if (includeComponentsIfGO)
            {
                var comps = go.GetComponents<Component>();
                foreach (var c in comps)
                    if (c != null) set.Add(c);
            }
        }
        else if (target is Component comp)
        {
            set.Add(comp);
        }
        else
        {
            set.Add(target);
        }

        return set;
    }

    private static List<Component> GatherAllComponentsInLoadedScenes()
    {
        var list = new List<Component>(4096);

        int sceneCount = SceneManager.sceneCount;
        for (int s = 0; s < sceneCount; s++)
        {
            var scene = SceneManager.GetSceneAt(s);
            if (!scene.isLoaded) continue;

            var roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                if (root == null) continue;
                var comps = root.GetComponentsInChildren<Component>(true); // true: 包含 inactive
                foreach (var c in comps)
                    if (c != null) list.Add(c); // 跳过丢失脚本（null）
            }
        }

        return list;
    }

    private void ScanComponentSerializedObjectReferences(Component component, HashSet<UnityEngine.Object> targets)
    {
        SerializedObject so;
        try
        {
            so = new SerializedObject(component);
        }
        catch
        {
            return;
        }

        var iterator = so.GetIterator();
        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (iterator.propertyType != SerializedPropertyType.ObjectReference)
                continue;

            var objRef = iterator.objectReferenceValue;
            if (objRef == null) continue;

            if (!targets.Contains(objRef))
                continue;

            _results.Add(new ReferenceInfo
            {
                holder = component,
                holderGO = component.gameObject,
                propertyPath = iterator.propertyPath,
                referencedTarget = objRef
            });
        }
    }

    private static string GetHierarchyPath(GameObject go)
    {
        if (go == null) return "(null)";
        var t = go.transform;
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }

        var sceneName = go.scene.IsValid() ? go.scene.name : "(NoScene)";
        return $"{sceneName}: {path}";
    }
}
#endif