using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ParticleEffectBatchPreview))]
public class ParticleEffectBatchPreviewEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var preview = (ParticleEffectBatchPreview)target;

        if (!Application.isPlaying)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox("运行时才可使用控制面板", MessageType.Info);
            return;
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("运行时控制", EditorStyles.boldLabel);

        // 刷新资源
        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("刷新资源", GUILayout.Height(30)))
        {
            preview.RefreshEffects();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(5);

        // 批次信息
        EditorGUILayout.LabelField("批次信息", EditorStyles.boldLabel);
        int batchSize = serializedObject.FindProperty("m_GridColumns").intValue * serializedObject.FindProperty("m_GridRows").intValue;
        int startIndex = preview.CurrentBatch * batchSize + 1;
        int endIndex = Mathf.Min((preview.CurrentBatch + 1) * batchSize, preview.TotalEffects);
        EditorGUILayout.LabelField($"当前批次: {preview.CurrentBatch + 1} / {preview.TotalBatches}");
        EditorGUILayout.LabelField($"显示特效: {startIndex}-{endIndex} / {preview.TotalEffects}");

        // 批次切换
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("◄ 上一批", GUILayout.Height(25)))
        {
            preview.PreviousBatch();
        }
        if (GUILayout.Button("下一批 ►", GUILayout.Height(25)))
        {
            preview.NextBatch();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // 播放控制
        EditorGUILayout.LabelField("播放控制", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"状态: {(preview.IsPlaying ? "播放中" : "已暂停")}");
        EditorGUILayout.LabelField($"粒子系统数: {preview.ParticleSystemCount}");

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(preview.IsPlaying ? "暂停" : "播放", GUILayout.Height(25)))
        {
            preview.TogglePlayPause();
        }
        if (GUILayout.Button("重启", GUILayout.Height(25)))
        {
            preview.RestartAllParticles();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox("快捷键: A/← 上一批 | D/→ 下一批 | Space 播放/暂停 | R 重启", MessageType.None);

        Repaint();
    }
}
