using UnityEditor;
using UnityEngine;

[ToolHubItem("特效工具/粒子批量预览", "创建并管理 ParticleEffectBatchPreview 场景组件", 40)]
public class ParticleEffectBatchPreviewPanel : IToolHubPanel
{
    private ParticleEffectBatchPreview selectedPreview;

    public void OnEnable()
    {
        selectedPreview = Object.FindObjectOfType<ParticleEffectBatchPreview>();
    }

    public void OnDisable() { }

    public void OnDestroy() { }

    public string GetHelpText() =>
        "在场景中创建 ParticleEffectBatchPreview 组件后，可在 Inspector 中设置特效目录、网格尺寸和运行时播放控制。";

    public void OnGUI()
    {
        EditorGUILayout.LabelField("粒子批量预览", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "该工具由场景组件 + CustomEditor 组成。先创建或选择 ParticleEffectBatchPreview，然后进入 Play Mode 使用 Inspector 控制批次播放。",
            MessageType.Info
        );

        selectedPreview = (ParticleEffectBatchPreview)EditorGUILayout.ObjectField(
            "预览组件",
            selectedPreview,
            typeof(ParticleEffectBatchPreview),
            true
        );

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("创建预览对象", GUILayout.Height(28)))
                CreatePreviewObject();

            using (new EditorGUI.DisabledScope(selectedPreview == null))
            {
                if (GUILayout.Button("选中对象", GUILayout.Height(28)))
                    Selection.activeObject = selectedPreview.gameObject;
            }
        }

        using (new EditorGUI.DisabledScope(selectedPreview == null || !Application.isPlaying))
        {
            EditorGUILayout.Space(8);
            if (GUILayout.Button("刷新特效资源", GUILayout.Height(28)))
                selectedPreview.RefreshEffects();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("上一批"))
                    selectedPreview.PreviousBatch();
                if (GUILayout.Button("下一批"))
                    selectedPreview.NextBatch();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(selectedPreview != null && selectedPreview.IsPlaying ? "暂停" : "播放"))
                    selectedPreview.TogglePlayPause();
                if (GUILayout.Button("重启"))
                    selectedPreview.RestartAllParticles();
            }
        }
    }

    private void CreatePreviewObject()
    {
        var go = new GameObject("ParticleEffectBatchPreview");
        Undo.RegisterCreatedObjectUndo(go, "Create Particle Effect Batch Preview");
        selectedPreview = go.AddComponent<ParticleEffectBatchPreview>();
        Selection.activeObject = go;
    }
}
