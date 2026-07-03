#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 曲线对象放置工具 - 工具箱集成面板（无 MonoBehaviour 依赖）
/// </summary>
[ToolHubItem("场景工具/曲线对象放置器", "沿 Catmull-Rom 曲线批量放置预制体", 25)]
public class CurvePlacerPanel : IToolHubPanel
{
    // ── 工具模式 ──────────────────────────────────────────────────────────
    private enum ToolMode
    {
        None,
        Draw,
        Edit,
    }

    private ToolMode m_Mode = ToolMode.None;

    // ── 曲线数据（原 CurvePlacerData 字段，内嵌到 Panel）────────────────
    private List<Vector3> m_ControlPoints = new();

    private GameObject m_PrefabToPlace;
    private float m_ObjectSpacing = 2f;
    private float m_SegmentStep = 0.05f;
    private bool m_IsClosed = false;

    private bool m_LockAxis = false;
    private int m_LockAxisIndex = 1; // 0=X 1=Y 2=Z
    private float m_LockAxisValue = 0f;

    private bool m_AlignToTangent = true;
    private Vector3 m_RotationOffset = Vector3.zero;

    // ── 批次管理 ──────────────────────────────────────────────────────────
    // 当前未保存批次的对象
    private readonly List<GameObject> m_CurrentBatch = new();

    // 已保存批次的根 GameObject（名称 Batch_N）
    private readonly List<GameObject> m_SavedBatches = new();

    // 所有批次的父容器
    private GameObject m_RootContainer;

    // ── 编辑状态 ──────────────────────────────────────────────────────────
    private int m_SelectedIndex = -1;
    private bool m_IsDragging = false;

    // ── UI 滚动 ───────────────────────────────────────────────────────────
    private Vector2 m_Scroll;

    // ── 样式 ──────────────────────────────────────────────────────────────
    private GUIStyle m_SectionStyle;
    private GUIStyle m_HeaderStyle;
    private bool m_StylesInit;

    // ── 常量 ──────────────────────────────────────────────────────────────
    private const float NODE_HANDLE_SIZE = 0.15f;
    private const float NODE_PICK_DISTANCE = 20f;
    private static readonly Color COLOR_CURVE = new(0.2f, 0.9f, 0.4f);
    private static readonly Color COLOR_NODE = new(1f, 0.8f, 0.1f);
    private static readonly Color COLOR_SELECTED = new(1f, 0.3f, 0.3f);

    // ─────────────────────────────────────────────────────────────────────
    public void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        // 恢复对已存在批次容器的引用
        m_RootContainer = GameObject.Find("CurvePlacer_Root");
        if (m_RootContainer != null)
            RefreshSavedBatchList();
    }

    public void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        SetMode(ToolMode.None);
    }

    public void OnDestroy()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    public string GetHelpText() => "沿 Catmull-Rom 曲线批量放置预制体，无需场景组件";

    // ─────────────────────────────────────────────────────────────────────
    // Inspector UI
    // ─────────────────────────────────────────────────────────────────────
    public void OnGUI()
    {
        InitStyles();
        m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);

        DrawHeader();
        EditorGUILayout.Space(4);
        DrawModeButtons();
        EditorGUILayout.Space(4);
        DrawCurveSettings();
        EditorGUILayout.Space(4);
        DrawPlacementSettings();
        EditorGUILayout.Space(4);
        DrawActions();
        EditorGUILayout.Space(4);
        DrawPointList();

        EditorGUILayout.EndScrollView();
    }

    private void InitStyles()
    {
        if (m_StylesInit)
            return;
        m_HeaderStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter,
        };
        m_SectionStyle = new GUIStyle(EditorStyles.helpBox);
        m_StylesInit = true;
    }

    private void DrawHeader()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("✦ Curve Object Placer", m_HeaderStyle);
        EditorGUILayout.Space(4);
    }

    private void DrawModeButtons()
    {
        EditorGUILayout.BeginVertical(m_SectionStyle);
        EditorGUILayout.LabelField("编辑模式", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        DrawModeButton("✏ 绘制节点", ToolMode.Draw);
        DrawModeButton("⊙ 编辑节点", ToolMode.Edit);
        if (GUILayout.Button("退出", GUILayout.Height(28)))
            SetMode(ToolMode.None);
        EditorGUILayout.EndHorizontal();

        string hint = m_Mode switch
        {
            ToolMode.Draw => "左键：添加节点  |  Backspace：删除最后节点  |  Esc：退出",
            ToolMode.Edit => "左键拖拽：移动节点  |  右键：删除节点  |  Esc：退出",
            _ => "选择模式开始编辑",
        };
        EditorGUILayout.HelpBox(hint, MessageType.None);
        EditorGUILayout.EndVertical();
    }

    private void DrawModeButton(string label, ToolMode target)
    {
        bool active = m_Mode == target;
        GUI.backgroundColor = active ? new Color(0.4f, 0.9f, 0.5f) : Color.white;
        if (GUILayout.Button(label, GUILayout.Height(28)))
            SetMode(active ? ToolMode.None : target);
        GUI.backgroundColor = Color.white;
    }

    private void DrawCurveSettings()
    {
        EditorGUILayout.BeginVertical(m_SectionStyle);
        EditorGUILayout.LabelField("曲线设置", EditorStyles.boldLabel);

        m_IsClosed = EditorGUILayout.Toggle("闭合曲线", m_IsClosed);
        m_SegmentStep = EditorGUILayout.Slider("采样精度（步长）", m_SegmentStep, 0.01f, 0.5f);

        EditorGUILayout.Space(4);
        m_LockAxis = EditorGUILayout.Toggle("锁定轴（强制所有节点一致）", m_LockAxis);
        if (m_LockAxis)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("锁定轴", GUILayout.Width(60));
            m_LockAxisIndex = GUILayout.Toolbar(m_LockAxisIndex, new[] { "X", "Y", "Z" });
            EditorGUILayout.EndHorizontal();
            m_LockAxisValue = EditorGUILayout.FloatField("锁定值", m_LockAxisValue);

            if (GUILayout.Button("将所有节点对齐到当前锁定值"))
            {
                for (int i = 0; i < m_ControlPoints.Count; i++)
                    m_ControlPoints[i] = ApplyAxisLock(m_ControlPoints[i]);
                SceneView.RepaintAll();
            }
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawPlacementSettings()
    {
        EditorGUILayout.BeginVertical(m_SectionStyle);
        EditorGUILayout.LabelField("对象放置设置", EditorStyles.boldLabel);

        m_PrefabToPlace = (GameObject)
            EditorGUILayout.ObjectField("预制体", m_PrefabToPlace, typeof(GameObject), false);
        m_ObjectSpacing = EditorGUILayout.FloatField("对象间隔", m_ObjectSpacing);
        m_AlignToTangent = EditorGUILayout.Toggle("朝向切线方向", m_AlignToTangent);
        if (m_AlignToTangent)
            m_RotationOffset = EditorGUILayout.Vector3Field("旋转偏移", m_RotationOffset);

        EditorGUILayout.EndVertical();
    }

    private void DrawActions()
    {
        EditorGUILayout.BeginVertical(m_SectionStyle);
        EditorGUILayout.LabelField("操作", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("生成对象", GUILayout.Height(30)))
            PlaceObjects();
        if (GUILayout.Button("清除当前批次", GUILayout.Height(30)))
            ClearCurrentBatch();
        EditorGUILayout.EndHorizontal();

        GUI.backgroundColor = new Color(0.4f, 0.8f, 1f);
        if (GUILayout.Button("✦ 保存当前批次", GUILayout.Height(30)))
            SaveCurrentBatch();
        GUI.backgroundColor = Color.white;

        if (m_SavedBatches.Count > 0)
        {
            EditorGUILayout.LabelField(
                $"已保存批次：{m_SavedBatches.Count} 个",
                EditorStyles.miniLabel
            );
            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button("清除所有已保存批次", GUILayout.Height(24)))
            {
                if (
                    EditorUtility.DisplayDialog(
                        "确认",
                        "删除所有已保存的批次对象？此操作不可撤销。",
                        "确认",
                        "取消"
                    )
                )
                    ClearAllBatches();
            }
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.Space(2);
        if (GUILayout.Button("清除所有节点", GUILayout.Height(24)))
        {
            if (EditorUtility.DisplayDialog("确认", "清除所有曲线节点？", "确认", "取消"))
            {
                m_ControlPoints.Clear();
                m_SelectedIndex = -1;
                SceneView.RepaintAll();
            }
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawPointList()
    {
        if (m_ControlPoints.Count == 0)
            return;

        EditorGUILayout.BeginVertical(m_SectionStyle);
        EditorGUILayout.LabelField(
            $"节点列表（共 {m_ControlPoints.Count} 个）",
            EditorStyles.boldLabel
        );

        for (int i = 0; i < m_ControlPoints.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            Vector3 newPos = EditorGUILayout.Vector3Field($"  [{i}]", m_ControlPoints[i]);
            if (EditorGUI.EndChangeCheck())
            {
                m_ControlPoints[i] = newPos;
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("✕", GUILayout.Width(24)))
            {
                m_ControlPoints.RemoveAt(i);
                if (m_SelectedIndex >= m_ControlPoints.Count)
                    m_SelectedIndex = -1;
                SceneView.RepaintAll();
                break;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndVertical();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Scene GUI
    // ─────────────────────────────────────────────────────────────────────
    private void OnSceneGUI(SceneView sceneView)
    {
        if (m_Mode == ToolMode.None)
            return;

        DrawCurveInScene();
        DrawNodeHandles();
        HandleInput();

        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
    }

    private void DrawCurveInScene()
    {
        if (m_ControlPoints.Count < 2)
            return;

        var samples = CatmullRomSpline.Sample(m_ControlPoints, m_SegmentStep, m_IsClosed);
        if (samples.Count < 2)
            return;

        Handles.color = COLOR_CURVE;
        for (int i = 1; i < samples.Count; i++)
            Handles.DrawLine(samples[i - 1].pos, samples[i].pos, 2f);

        if (m_IsClosed)
            Handles.DrawLine(samples[samples.Count - 1].pos, samples[0].pos, 2f);
    }

    private void DrawNodeHandles()
    {
        for (int i = 0; i < m_ControlPoints.Count; i++)
        {
            float size = HandleUtility.GetHandleSize(m_ControlPoints[i]) * NODE_HANDLE_SIZE;
            Handles.color = (i == m_SelectedIndex) ? COLOR_SELECTED : COLOR_NODE;
            Handles.SphereHandleCap(
                0,
                m_ControlPoints[i],
                Quaternion.identity,
                size,
                EventType.Repaint
            );
            Handles.Label(
                m_ControlPoints[i] + Vector3.up * size * 1.5f,
                $"{i}",
                EditorStyles.boldLabel
            );
        }
    }

    private void HandleInput()
    {
        Event e = Event.current;

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            SetMode(ToolMode.None);
            e.Use();
            return;
        }

        if (m_Mode == ToolMode.Draw)
            HandleDrawMode(e);
        else if (m_Mode == ToolMode.Edit)
            HandleEditMode(e);
    }

    private void HandleDrawMode(Event e)
    {
        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            m_ControlPoints.Add(ApplyAxisLock(GetWorldPosition(e.mousePosition)));
            SceneView.RepaintAll();
            e.Use();
        }
        if (
            e.type == EventType.KeyDown
            && e.keyCode == KeyCode.Backspace
            && m_ControlPoints.Count > 0
        )
        {
            m_ControlPoints.RemoveAt(m_ControlPoints.Count - 1);
            SceneView.RepaintAll();
            e.Use();
        }
    }

    private void HandleEditMode(Event e)
    {
        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            int nearest = GetNearestPointIndex(e.mousePosition);
            m_SelectedIndex = nearest;
            m_IsDragging = nearest >= 0;
            e.Use();
        }
        if (e.type == EventType.MouseDrag && e.button == 0 && m_IsDragging && m_SelectedIndex >= 0)
        {
            m_ControlPoints[m_SelectedIndex] = ApplyAxisLock(GetWorldPosition(e.mousePosition));
            SceneView.RepaintAll();
            e.Use();
        }
        if (e.type == EventType.MouseUp && e.button == 0)
            m_IsDragging = false;

        if (e.type == EventType.MouseDown && e.button == 1 && !e.alt)
        {
            int nearest = GetNearestPointIndex(e.mousePosition);
            if (nearest >= 0)
            {
                m_ControlPoints.RemoveAt(nearest);
                m_SelectedIndex = -1;
                SceneView.RepaintAll();
                e.Use();
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // 放置 / 批次管理
    // ─────────────────────────────────────────────────────────────────────
    private void PlaceObjects()
    {
        if (m_PrefabToPlace == null)
        {
            EditorUtility.DisplayDialog("提示", "请先设置要放置的预制体。", "OK");
            return;
        }
        if (m_ControlPoints.Count < 2)
        {
            EditorUtility.DisplayDialog("提示", "至少需要 2 个节点才能生成对象。", "OK");
            return;
        }

        ClearCurrentBatch();
        EnsureRootContainer();

        // 当前批次容器
        var batchGo = new GameObject("PlacedObjects_Current");
        batchGo.transform.SetParent(m_RootContainer.transform);
        Undo.RegisterCreatedObjectUndo(batchGo, "Create Batch Container");

        var samples = CatmullRomSpline.Sample(m_ControlPoints, m_SegmentStep, m_IsClosed);
        var points = CatmullRomSpline.GetEvenlySpacedPoints(samples, m_ObjectSpacing);

        foreach (var (pos, tangent) in points)
        {
            var go = (GameObject)
                PrefabUtility.InstantiatePrefab(m_PrefabToPlace, batchGo.transform);
            go.transform.position = pos;

            if (m_AlignToTangent && tangent.sqrMagnitude > 0.001f)
                go.transform.rotation =
                    Quaternion.LookRotation(tangent.normalized, Vector3.up)
                    * Quaternion.Euler(m_RotationOffset);
            else
                go.transform.rotation = Quaternion.Euler(m_RotationOffset);

            Undo.RegisterCreatedObjectUndo(go, "Place Object");
            m_CurrentBatch.Add(go);
        }

        Debug.Log($"[CurvePlacer] 已生成 {points.Count} 个对象。");
    }

    private void SaveCurrentBatch()
    {
        if (m_CurrentBatch.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "没有可保存的对象，请先生成对象。", "OK");
            return;
        }

        EnsureRootContainer();
        int idx = m_SavedBatches.Count + 1;
        var batchContainer = m_RootContainer.transform.Find("PlacedObjects_Current");
        if (batchContainer != null)
        {
            Undo.RecordObject(batchContainer.gameObject, "Save Batch");
            batchContainer.name = $"Batch_{idx}";
            m_SavedBatches.Add(batchContainer.gameObject);
        }

        m_CurrentBatch.Clear();
        Debug.Log($"[CurvePlacer] 批次已保存为 Batch_{idx}");
    }

    private void ClearCurrentBatch()
    {
        foreach (var go in m_CurrentBatch)
            if (go != null)
                Undo.DestroyObjectImmediate(go);
        m_CurrentBatch.Clear();

        // 清理容器
        if (m_RootContainer != null)
        {
            var cur = m_RootContainer.transform.Find("PlacedObjects_Current");
            if (cur != null)
                Undo.DestroyObjectImmediate(cur.gameObject);
        }
    }

    private void ClearAllBatches()
    {
        foreach (var go in m_SavedBatches)
            if (go != null)
                Undo.DestroyObjectImmediate(go);
        m_SavedBatches.Clear();
        ClearCurrentBatch();
    }

    private void EnsureRootContainer()
    {
        if (m_RootContainer != null)
            return;
        m_RootContainer = GameObject.Find("CurvePlacer_Root");
        if (m_RootContainer == null)
        {
            m_RootContainer = new GameObject("CurvePlacer_Root");
            Undo.RegisterCreatedObjectUndo(m_RootContainer, "Create CurvePlacer Root");
        }
    }

    private void RefreshSavedBatchList()
    {
        m_SavedBatches.Clear();
        if (m_RootContainer == null)
            return;
        for (int i = 0; i < m_RootContainer.transform.childCount; i++)
        {
            var child = m_RootContainer.transform.GetChild(i);
            if (child.name.StartsWith("Batch_"))
                m_SavedBatches.Add(child.gameObject);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // 工具方法
    // ─────────────────────────────────────────────────────────────────────
    private void SetMode(ToolMode mode)
    {
        m_Mode = mode;
        m_SelectedIndex = -1;
        m_IsDragging = false;
        SceneView.RepaintAll();
    }

    private Vector3 GetWorldPosition(Vector2 mousePos)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);
        if (Physics.Raycast(ray, out RaycastHit hit))
            return hit.point;
        if (ray.direction.y != 0f)
        {
            float t = -ray.origin.y / ray.direction.y;
            if (t > 0f)
                return ray.origin + ray.direction * t;
        }
        return ray.origin + ray.direction * 10f;
    }

    private Vector3 ApplyAxisLock(Vector3 pos)
    {
        if (!m_LockAxis)
            return pos;
        switch (m_LockAxisIndex)
        {
            case 0:
                pos.x = m_LockAxisValue;
                break;
            case 1:
                pos.y = m_LockAxisValue;
                break;
            case 2:
                pos.z = m_LockAxisValue;
                break;
        }
        return pos;
    }

    private int GetNearestPointIndex(Vector2 mousePos)
    {
        float minDist = NODE_PICK_DISTANCE;
        int index = -1;
        for (int i = 0; i < m_ControlPoints.Count; i++)
        {
            float dist = Vector2.Distance(
                HandleUtility.WorldToGUIPoint(m_ControlPoints[i]),
                mousePos
            );
            if (dist < minDist)
            {
                minDist = dist;
                index = i;
            }
        }
        return index;
    }
}
#endif
