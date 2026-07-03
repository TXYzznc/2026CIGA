#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class ToolPanelWindow<T> : EditorWindow
    where T : class, IToolHubPanel, new()
{
    private T panel;
    private Vector2 scrollPosition;

    protected virtual void OnEnable()
    {
        panel = new T();
        panel.OnEnable();
    }

    protected virtual void OnDisable()
    {
        panel?.OnDisable();
    }

    protected virtual void OnDestroy()
    {
        panel?.OnDestroy();
    }

    protected virtual void OnGUI()
    {
        if (panel == null)
        {
            panel = new T();
            panel.OnEnable();
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        panel.OnGUI();
        EditorGUILayout.EndScrollView();
    }
}

public class BatchMeshColliderWindow : ToolPanelWindow<BatchMeshColliderPanel>
{
    public static void ShowWindow()
    {
        var window = GetWindow<BatchMeshColliderWindow>("批量添加 MeshCollider");
        window.minSize = new Vector2(480, 520);
        window.Show();
    }
}

public class CurvePlacerWindow : ToolPanelWindow<CurvePlacerPanel>
{
    public static void ShowWindow()
    {
        var window = GetWindow<CurvePlacerWindow>("曲线对象放置器");
        window.minSize = new Vector2(520, 560);
        window.Show();
    }
}

public class EditorScreenshotToolWindow : ToolPanelWindow<EditorScreenshotPanel>
{
    public static void ShowWindow()
    {
        var window = GetWindow<EditorScreenshotToolWindow>("编辑器截图");
        window.minSize = new Vector2(360, 260);
        window.Show();
    }
}

public class FindMissingScriptWindow : ToolPanelWindow<FindMissingScriptPanel>
{
    public static void ShowWindow()
    {
        var window = GetWindow<FindMissingScriptWindow>("缺失脚本 GUID 扫描");
        window.minSize = new Vector2(560, 420);
        window.Show();
    }
}

public class FontReplacerWindow : ToolPanelWindow<FontReplacerPanel>
{
    public static void ShowWindow()
    {
        var window = GetWindow<FontReplacerWindow>("字体资源替换");
        window.minSize = new Vector2(420, 400);
        window.Show();
    }
}

public class LogConfigWindow : ToolPanelWindow<LogConfigPanel>
{
    public static void ShowWindow()
    {
        var window = GetWindow<LogConfigWindow>("日志配置管理器");
        window.minSize = new Vector2(600, 520);
        window.Show();
    }
}

public class ConsoleLogExporterWindow : ToolPanelWindow<ConsoleLogExporterPanel>
{
    public static void ShowWindow()
    {
        var window = GetWindow<ConsoleLogExporterWindow>("Console 日志导出");
        window.minSize = new Vector2(520, 360);
        window.Show();
    }
}

public class MaterialConverterToolWindow : ToolPanelWindow<MaterialConverterPanel>
{
    public static void ShowWindow()
    {
        var window = GetWindow<MaterialConverterToolWindow>("材质批量转换器");
        window.minSize = new Vector2(600, 520);
        window.Show();
    }
}

public class NoiseGeneratorWindow : ToolPanelWindow<NoiseGeneratorPanel>
{
    public static void ShowWindow()
    {
        var window = GetWindow<NoiseGeneratorWindow>("FastNoise 噪声生成器");
        window.minSize = new Vector2(450, 600);
        window.Show();
    }
}

public class NormalProcessingWindow : ToolPanelWindow<NormalProcessingPanel>
{
    public static void ShowWindow()
    {
        var window = GetWindow<NormalProcessingWindow>("法线处理工具");
        window.minSize = new Vector2(520, 520);
        window.Show();
    }
}

public class SmoothNormalWindow : ToolPanelWindow<SmoothNormalPanel>
{
    public static void ShowWindow()
    {
        var window = GetWindow<SmoothNormalWindow>("平滑法线生成器");
        window.minSize = new Vector2(420, 460);
        window.Show();
    }
}

public class PWBControlWindow : ToolPanelWindow<PWBControlPanel>
{
    public static void ShowWindow()
    {
        var window = GetWindow<PWBControlWindow>("PWB 控制台");
        window.minSize = new Vector2(360, 480);
        window.Show();
    }
}

public class SceneReferenceFinderWindow : ToolPanelWindow<SceneReferenceFinderPanel>
{
    public static void ShowWindow()
    {
        var window = GetWindow<SceneReferenceFinderWindow>("场景引用查找器");
        window.minSize = new Vector2(560, 420);
        window.Show();
    }
}

public class SceneTextTranslatorWindow : ToolPanelWindow<SceneTextTranslatorPanel>
{
    public static void ShowWindow()
    {
        var window = GetWindow<SceneTextTranslatorWindow>("场景文本翻译(Baidu)");
        window.minSize = new Vector2(560, 420);
        window.Show();
    }
}

public class SceneDependencyAnalyzerWindow : ToolPanelWindow<SceneDependencyAnalyzerPanel>
{
    public static void ShowWindow()
    {
        var window = GetWindow<SceneDependencyAnalyzerWindow>("场景依赖分析器");
        window.minSize = new Vector2(760, 540);
        window.Show();
    }
}

public class ParticleEffectBatchPreviewWindow : ToolPanelWindow<ParticleEffectBatchPreviewPanel>
{
    public static void ShowWindow()
    {
        var window = GetWindow<ParticleEffectBatchPreviewWindow>("粒子批量预览");
        window.minSize = new Vector2(420, 280);
        window.Show();
    }
}
#endif
