using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HUDView))]
public class HUDViewEditor : Editor
{
    private int _testScore = 320;
    private int _testTarget = 500;
    private int _addDelta = 50;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("测试按钮仅在运行时可用", MessageType.Info);
            return;
        }

        var hud = (HUDView)target;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("── 测试 ──", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            _testScore = EditorGUILayout.IntField("当前分数", _testScore);
            if (GUILayout.Button("SetScore（动效）", GUILayout.Width(140)))
                hud.SetScore(_testScore);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            _testTarget = EditorGUILayout.IntField("目标分数", _testTarget);
            if (GUILayout.Button("SetTargetScore", GUILayout.Width(140)))
                hud.SetTargetScore(_testTarget);
        }

        if (GUILayout.Button("SetScoreImmediate（无动效）"))
            hud.SetScoreImmediate(_testScore);

        using (new EditorGUILayout.HorizontalScope())
        {
            _addDelta = EditorGUILayout.IntField("增加分数", _addDelta);
            if (GUILayout.Button("AddScore", GUILayout.Width(140)))
                hud.AddScore(_addDelta);
        }
    }
}
