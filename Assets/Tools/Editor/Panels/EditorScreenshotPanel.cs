using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 编辑器截图工具面板 - 可集成到工具箱，也可独立使用
/// </summary>
[ToolHubItem("资源工具/编辑器截图", "编辑器截图工具，支持场景视图和自定义相机", 10)]
public class EditorScreenshotPanel : IToolHubPanel
{
    public enum SourceMode
    {
        SceneViewCamera,
        SelectedCamera
    }

    private SourceMode sourceMode = SourceMode.SceneViewCamera;
    private Camera selectedCamera;

    private int width = 1920;
    private int height = 1080;

    private bool usePNG = true;
    private bool transparentBackground = false;
    private int jpgQuality = 95;

    private string defaultFolder = "Screenshots";
    private string filePrefix = "shot_";
    private string timeFormat = "yyyyMMdd_HHmmss_fff";

    public void OnEnable()
    {
        // 初始化字段（已在声明时初始化）
    }

    public void OnDisable()
    {
        // 无需清理
    }

    public void OnDestroy()
    {
        // 无需清理
    }

    public string GetHelpText()
    {
        return "编辑器截图工具。支持场景视图相机和自定义相机截图，可配置分辨率和格式。";
    }

    public void OnGUI()
    {
        EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
        sourceMode = (SourceMode)EditorGUILayout.EnumPopup("Mode", sourceMode);

        using (new EditorGUI.DisabledScope(sourceMode != SourceMode.SelectedCamera))
        {
            selectedCamera = (Camera)EditorGUILayout.ObjectField("Camera", selectedCamera, typeof(Camera), true);
        }

        if (sourceMode == SourceMode.SceneViewCamera)
        {
            EditorGUILayout.HelpBox("将使用当前激活的 SceneView 相机进行截图。", MessageType.Info);
        }

        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField("Resolution", EditorStyles.boldLabel);
        width = EditorGUILayout.IntField("Width", width);
        height = EditorGUILayout.IntField("Height", height);
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);

        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField("Format", EditorStyles.boldLabel);
        usePNG = EditorGUILayout.Toggle("Use PNG", usePNG);

        using (new EditorGUI.DisabledScope(!usePNG))
        {
            transparentBackground = EditorGUILayout.Toggle("Transparent Background", transparentBackground);
        }

        using (new EditorGUI.DisabledScope(usePNG))
        {
            jpgQuality = EditorGUILayout.IntSlider("JPG Quality", jpgQuality, 1, 100);
        }

        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        defaultFolder = EditorGUILayout.TextField("Default Folder", defaultFolder);
        filePrefix = EditorGUILayout.TextField("File Prefix", filePrefix);
        timeFormat = EditorGUILayout.TextField("Time Format", timeFormat);

        EditorGUILayout.Space(12);

        using (new EditorGUI.DisabledScope(!CanCapture(out _)))
        {
            if (GUILayout.Button("Capture Screenshot...", GUILayout.Height(32)))
            {
                CaptureWithSavePanel();
            }
        }

        if (!CanCapture(out string reason))
            EditorGUILayout.HelpBox(reason, MessageType.Warning);
    }

    private bool CanCapture(out string reason)
    {
        reason = null;

        if (sourceMode == SourceMode.SceneViewCamera)
        {
            if (SceneView.lastActiveSceneView == null || SceneView.lastActiveSceneView.camera == null)
            {
                reason = "没有可用的 SceneView 相机：请打开 Scene 视图并点击激活它。";
                return false;
            }
        }
        else
        {
            if (selectedCamera == null)
            {
                reason = "请选择一个 Camera。";
                return false;
            }
        }

        return true;
    }

    private Camera GetSourceCamera()
    {
        if (sourceMode == SourceMode.SceneViewCamera)
            return SceneView.lastActiveSceneView.camera;
        return selectedCamera;
    }

    private void CaptureWithSavePanel()
    {
        var cam = GetSourceCamera();
        if (cam == null)
        {
            EditorUtility.DisplayDialog("Screenshot", "Camera is null.", "OK");
            return;
        }

        string ext = usePNG ? "png" : "jpg";
        string folder = Path.Combine(Application.dataPath, defaultFolder);
        Directory.CreateDirectory(folder);

        string defaultName = $"{filePrefix}{DateTime.Now.ToString(timeFormat)}_{width}x{height}.{ext}";

        string savePath = EditorUtility.SaveFilePanel(
            "Save Screenshot",
            folder,
            defaultName,
            ext);

        if (string.IsNullOrEmpty(savePath))
            return;

        byte[] bytes = CaptureToBytes(cam, width, height, usePNG, transparentBackground, jpgQuality);

        File.WriteAllBytes(savePath, bytes);

        // 刷新 Project（如果存到了 Assets 目录里）
        if (savePath.Replace("\\", "/").StartsWith(Application.dataPath.Replace("\\", "/")))
            AssetDatabase.Refresh();

        EditorUtility.RevealInFinder(savePath);
        Debug.Log($"[EditorScreenshotPanel] Saved: {savePath}");
    }
    public static byte[] CaptureToBytes(Camera cam, int width, int height, bool png, bool transparentBg, int jpgQuality)
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);

        RenderTexture prevActive = RenderTexture.active;
        RenderTexture prevCamTarget = cam.targetTexture;

        CameraClearFlags oldFlags = cam.clearFlags;
        Color oldBg = cam.backgroundColor;

        if (transparentBg)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
        }

        // 关键：编辑器下也可以 Camera.Render() -> RT -> ReadPixels
        RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false, false);

        try
        {
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            if (png) return tex.EncodeToPNG();
            return tex.EncodeToJPG(Mathf.Clamp(jpgQuality, 1, 100));
        }
        finally
        {
            cam.targetTexture = prevCamTarget;
            RenderTexture.active = prevActive;

            cam.clearFlags = oldFlags;
            cam.backgroundColor = oldBg;

            rt.Release();
            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}
