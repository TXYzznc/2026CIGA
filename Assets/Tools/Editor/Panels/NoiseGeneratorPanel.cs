using System.IO;
using UnityEngine;
using UnityEditor;

/// <summary>
/// 噪声生成器面板 - 可集成到工具箱，也可独立使用
/// </summary>
[ToolHubItem("纹理工具/FastNoise 噪声生成器", "基于FastNoiseLite的高性能噪声纹理生成工具", 10)]
public class NoiseGeneratorPanel : IToolHubPanel
{
    #region 枚举定义

    /// <summary>
    /// FastNoiseLite支持的噪声类型
    /// </summary>
    public enum NoiseType
    {
        [Tooltip("OpenSimplex2噪声 - 高质量通用噪声")]
        OpenSimplex2,

        [Tooltip("OpenSimplex2S噪声 - 平滑变体")]
        OpenSimplex2S,

        [Tooltip("细胞噪声 - 蜂窝状图案")]
        Cellular,

        [Tooltip("柏林噪声 - 经典算法")]
        Perlin,

        [Tooltip("值噪声 - 简单快速")]
        Value,

        [Tooltip("立方值噪声 - 更平滑的值噪声")]
        ValueCubic
    }

    /// <summary>
    /// 分形类型
    /// </summary>
    public enum FractalType
    {
        [Tooltip("无分形 - 单层噪声")]
        None,

        [Tooltip("FBM分形 - 标准多层次细节")]
        FBm,

        [Tooltip("山脊分形 - 山脉效果")]
        Ridged,

        [Tooltip("乒乓分形 - 波浪起伏")]
        PingPong,

        [Tooltip("域扭曲渐进式")]
        DomainWarpProgressive,

        [Tooltip("域扭曲独立式")]
        DomainWarpIndependent
    }

    /// <summary>
    /// 细胞噪声返回类型
    /// </summary>
    public enum CellularReturnType
    {
        [Tooltip("距离 - 到最近点的距离")]
        Distance,

        [Tooltip("距离2 - 到第二近点的距离")]
        Distance2,

        [Tooltip("距离2添加 - 两个距离相加")]
        Distance2Add,

        [Tooltip("距离2减去 - 两个距离相减")]
        Distance2Sub,

        [Tooltip("距离2乘以 - 两个距离相乘")]
        Distance2Mul,

        [Tooltip("距离2除以 - 两个距离相除")]
        Distance2Div,

        [Tooltip("细胞值 - 细胞的随机值")]
        CellValue
    }

    /// <summary>
    /// 细胞距离函数
    /// </summary>
    public enum CellularDistanceFunction
    {
        [Tooltip("欧几里得距离 - 圆形细胞")]
        Euclidean,

        [Tooltip("欧几里得平方 - 性能更好")]
        EuclideanSq,

        [Tooltip("曼哈顿距离 - 方形细胞")]
        Manhattan,

        [Tooltip("混合距离 - 介于欧氏和曼哈顿之间")]
        Hybrid
    }

    /// <summary>
    /// 域扭曲类型
    /// </summary>
    public enum DomainWarpType
    {
        [Tooltip("OpenSimplex2扭曲")]
        OpenSimplex2,

        [Tooltip("OpenSimplex2缩减")]
        OpenSimplex2Reduced,

        [Tooltip("基础网格扭曲")]
        BasicGrid
    }

    /// <summary>
    /// 3D 旋转类型
    /// </summary>
    public enum RotationType3D
    {
        [Tooltip("无旋转")]
        None,

        [Tooltip("改善XY平面")]
        ImproveXYPlanes,

        [Tooltip("改善XZ平面")]
        ImproveXZPlanes
    }

    #endregion

    #region 成员变量

    // FastNoiseLite实例
    private FastNoiseLite noise;

    // 基础参数
    private NoiseType noiseType = NoiseType.OpenSimplex2;
    private RotationType3D rotationType3D = RotationType3D.None;
    private int seed = 1337;
    private float frequency = 0.01f;

    // 分形参数
    private FractalType fractalType = FractalType.None;
    private int octaves = 4;
    private float lacunarity = 2.0f;
    private float gain = 0.5f;
    private float weightedStrength = 0.0f;
    private float pingPongStrength = 2.0f;

    // 细胞噪声参数
    private CellularDistanceFunction cellularDistanceFunction = CellularDistanceFunction.Euclidean;
    private CellularReturnType cellularReturnType = CellularReturnType.Distance;
    private float cellularJitter = 1.0f;

    // 域扭曲参数
    private bool useDomainWarp = false;
    private DomainWarpType domainWarpType = DomainWarpType.OpenSimplex2;
    private float domainWarpAmp = 30.0f;

    // 纹理参数
    private int width = 512;
    private int height = 512;
    private Vector2 offset = Vector2.zero;

    // 后处理
    private bool invertNoise = false;
    private bool useContrast = false;
    private float contrastPower = 1.0f;
    private bool useColorGradient = false;
    private Gradient colorGradient;
    private bool normalizeOutput = true;

    // UI
    private Texture2D previewTexture;
    private Vector2 scrollPos;
    private bool autoRefresh = true;
    private string saveFolderName = "SaveImages";
    private string customFileName = "";

    // 预设
    private int selectedPreset = 0;
    private readonly string[] presetNames = { "自定义", "经典地形", "细腻云层", "岩石表面", "水波纹", "木纹", "大理石", "细胞组织", "火焰", "山脉" };
    
    // 折叠状态
    private bool showBasic = true;
    private bool showFractal = true;
    private bool showCellular = true;
    private bool showDomainWarp = false;
    private bool showPostProcess = false;
    #endregion

    public void OnEnable()
    {
        InitializeNoise();
        colorGradient = new Gradient();
        colorGradient.SetKeys(
            new[] { new GradientColorKey(Color.black, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
    }

    public void OnDisable() { }
    public void OnDestroy() 
    { 
        if (previewTexture != null) Object.DestroyImmediate(previewTexture);
    }

    public string GetHelpText() => "FastNoiseLite 高性能噪声生成器。支持多种噪声类型、分形和域扭曲效果。";

    private void InitializeNoise()
    {
        noise = new FastNoiseLite(seed);
        UpdateNoiseSettings();
    }

    public void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        DrawPresetSection();
        DrawBasicParameters();
        DrawFractalParameters();
        if (noiseType == NoiseType.Cellular) DrawCellularParameters();
        DrawDomainWarpParameters();
        DrawPostProcessing();
        DrawControlButtons();
        DrawPreview();

        EditorGUILayout.EndScrollView();

        if (autoRefresh && GUI.changed)
        {
            UpdateNoiseSettings();
            GeneratePreview();
        }
    }

    private void DrawPresetSection()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("📦 快速预设", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        selectedPreset = EditorGUILayout.Popup("预设模板", selectedPreset, presetNames);
        if (EditorGUI.EndChangeCheck() && selectedPreset > 0)
        {
            ApplyPreset(selectedPreset);
            selectedPreset = 0;
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(3);
    }

    private void DrawBasicParameters()
    {
        showBasic = EditorGUILayout.BeginFoldoutHeaderGroup(showBasic, "⚙️ 基础参数");
        if (showBasic)
        {
            EditorGUILayout.BeginVertical("box");
            
            // 噪声类型
            noiseType = (NoiseType)EditorGUILayout.EnumPopup(
                new GUIContent("噪声类型", GetNoiseTypeDescription(noiseType)),
                noiseType
            );
            EditorGUILayout.HelpBox(GetNoiseTypeDescription(noiseType), MessageType.None);
            
            // 3D 旋转
            rotationType3D = (RotationType3D)EditorGUILayout.EnumPopup(
                new GUIContent("3D 旋转优化", "改善在 2D 平面上的 3D 噪声采样效果（如消除 OpenSimplex2 的轴向伪影）。启用此项将强制使用 3D 计算模式。"),
                rotationType3D
            );
            
            EditorGUILayout.BeginHorizontal();
            seed = EditorGUILayout.IntField(new GUIContent("随机种子", "改变种子可生成完全不同的噪声图案"), seed);
            if (GUILayout.Button("🎲", GUILayout.Width(30))) seed = Random.Range(0, 99999);
            EditorGUILayout.EndHorizontal();

            frequency = EditorGUILayout.Slider(new GUIContent("频率", "控制噪声的密集程度，值越大图案越密集"), frequency, 0.001f, 0.1f);

            EditorGUILayout.BeginHorizontal();
            width = EditorGUILayout.IntPopup("宽度", width, new[] { "128", "256", "512", "1024", "2048" }, new[] { 128, 256, 512, 1024, 2048 });
            height = EditorGUILayout.IntPopup("高度", height, new[] { "128", "256", "512", "1024", "2048" }, new[] { 128, 256, 512, 1024, 2048 });
            EditorGUILayout.EndHorizontal();

            offset = EditorGUILayout.Vector2Field(new GUIContent("偏移量", "在噪声空间中移动采样位置"), offset);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(3);
    }

    private void DrawFractalParameters()
    {
        showFractal = EditorGUILayout.BeginFoldoutHeaderGroup(showFractal, "🌊 分形参数");
        if (showFractal)
        {
            EditorGUILayout.BeginVertical("box");
            fractalType = (FractalType)EditorGUILayout.EnumPopup(
                new GUIContent("分形类型", "叠加多层不同频率的噪声创建复杂效果"), 
                fractalType
            );

            if (fractalType != FractalType.None)
            {
                EditorGUILayout.HelpBox(GetFractalDescription(fractalType), MessageType.None);

                octaves = EditorGUILayout.IntSlider(new GUIContent("八度数", "叠加的层数，越多细节越丰富但性能越低"), octaves, 1, 10);
                lacunarity = EditorGUILayout.Slider(new GUIContent("间隙度", "每层频率增长倍数，通常为2.0"), lacunarity, 1.0f, 4.0f);
                gain = EditorGUILayout.Slider(new GUIContent("增益", "每层振幅衰减系数，通常为0.5"), gain, 0.0f, 1.0f);
                
                if (fractalType == FractalType.PingPong)
                    pingPongStrength = EditorGUILayout.Slider(new GUIContent("乒乓强度", "控制乒乓效果的强度"), pingPongStrength, 0.0f, 5.0f);
                
                weightedStrength = EditorGUILayout.Slider(new GUIContent("权重强度", "基于前一层输出调整当前层权重"), weightedStrength, 0.0f, 1.0f);
            }
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(3);
    }

    private void DrawCellularParameters()
    {
        showCellular = EditorGUILayout.BeginFoldoutHeaderGroup(showCellular, "🔷 细胞噪声参数");
        if (showCellular)
        {
            EditorGUILayout.BeginVertical("box");
            cellularDistanceFunction = (CellularDistanceFunction)EditorGUILayout.EnumPopup(new GUIContent("距离函数", "决定细胞的形状特征"), cellularDistanceFunction);
            cellularReturnType = (CellularReturnType)EditorGUILayout.EnumPopup(new GUIContent("返回类型", "决定如何计算细胞值"), cellularReturnType);
            cellularJitter = EditorGUILayout.Slider(new GUIContent("抖动强度", "控制细胞点的随机偏移，0为规则网格，1为完全随机"), cellularJitter, 0.0f, 1.0f);
            
            EditorGUILayout.HelpBox(
                "• Distance: 经典Worley噪声\n" +
                "• Distance2Sub: 产生清晰的细胞边界\n" +
                "• CellValue: 每个细胞不同颜色",
                MessageType.None
            );
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(3);
    }

    private void DrawDomainWarpParameters()
    {
        showDomainWarp = EditorGUILayout.BeginFoldoutHeaderGroup(showDomainWarp, "🌀 域扭曲");
        if (showDomainWarp)
        {
            EditorGUILayout.BeginVertical("box");
            useDomainWarp = EditorGUILayout.Toggle(new GUIContent("启用域扭曲", "使用噪声扭曲另一个噪声，创建有机形态"), useDomainWarp);
            if (useDomainWarp)
            {
                domainWarpType = (DomainWarpType)EditorGUILayout.EnumPopup(new GUIContent("扭曲类型", "选择扭曲算法"), domainWarpType);
                domainWarpAmp = EditorGUILayout.Slider(new GUIContent("扭曲强度", "扭曲的剧烈程度"), domainWarpAmp, 1.0f, 200.0f);
                
                EditorGUILayout.HelpBox("适合创建：流体纹理、有机形态、魔法效果", MessageType.Info);
            }
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(3);
    }

    private void DrawPostProcessing()
    {
        showPostProcess = EditorGUILayout.BeginFoldoutHeaderGroup(showPostProcess, "🎨 后处理");
        if (showPostProcess)
        {
            EditorGUILayout.BeginVertical("box");
            normalizeOutput = EditorGUILayout.Toggle("归一化输出", normalizeOutput);
            invertNoise = EditorGUILayout.Toggle("反转噪声", invertNoise);
            useContrast = EditorGUILayout.Toggle("启用对比度", useContrast);
            if (useContrast) contrastPower = EditorGUILayout.Slider("对比度强度", contrastPower, 0.1f, 5.0f);
            useColorGradient = EditorGUILayout.Toggle("颜色渐变", useColorGradient);
            if (useColorGradient) colorGradient = EditorGUILayout.GradientField("渐变色", colorGradient);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(3);
    }

    private void DrawControlButtons()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        autoRefresh = EditorGUILayout.Toggle("自动刷新", autoRefresh);
        if (!autoRefresh && GUILayout.Button("🔄 生成", GUILayout.Height(25)))
        {
            UpdateNoiseSettings();
            GeneratePreview();
        }
        if (GUILayout.Button("💾 保存PNG", GUILayout.Height(25), GUILayout.Width(100)))
        {
            if (previewTexture == null) GeneratePreview();
            SaveTexture();
        }
        EditorGUILayout.EndHorizontal();

        saveFolderName = EditorGUILayout.TextField("保存文件夹", saveFolderName);
        
        EditorGUILayout.BeginHorizontal();
        customFileName = EditorGUILayout.TextField("文件名 (必填)", customFileName);
        if (string.IsNullOrWhiteSpace(customFileName))
        {
            var style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.red } };
            EditorGUILayout.LabelField(" *", style, GUILayout.Width(20));
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
    }

    private void DrawPreview()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("👁️ 预览", EditorStyles.boldLabel);

        if (previewTexture == null) GeneratePreview();

        if (previewTexture != null)
        {
            float previewSize = Mathf.Min(EditorGUIUtility.currentViewWidth - 40, 400);
            Rect rect = GUILayoutUtility.GetRect(previewSize, previewSize);
            EditorGUI.DrawRect(rect, new Color(0.18f, 0.18f, 0.18f, 1f));
            GUI.DrawTexture(rect, previewTexture, ScaleMode.ScaleToFit);
            EditorGUILayout.LabelField($"尺寸: {width}×{height} | 种子: {seed}", EditorStyles.miniLabel);
        }
        EditorGUILayout.EndVertical();
    }

    private void UpdateNoiseSettings()
    {
        noise ??= new FastNoiseLite(seed);
        noise.SetSeed(seed);
        noise.SetNoiseType((FastNoiseLite.NoiseType)(int)noiseType);
        noise.SetRotationType3D((FastNoiseLite.RotationType3D)(int)rotationType3D); // 设置 3D 旋转类型
        noise.SetFrequency(frequency);
        noise.SetFractalType((FastNoiseLite.FractalType)(int)fractalType);
        noise.SetFractalOctaves(octaves);
        noise.SetFractalLacunarity(lacunarity);
        noise.SetFractalGain(gain);
        noise.SetFractalWeightedStrength(weightedStrength);
        noise.SetFractalPingPongStrength(pingPongStrength);

        if (noiseType == NoiseType.Cellular)
        {
            noise.SetCellularDistanceFunction((FastNoiseLite.CellularDistanceFunction)(int)cellularDistanceFunction);
            noise.SetCellularReturnType((FastNoiseLite.CellularReturnType)(int)cellularReturnType);
            noise.SetCellularJitter(cellularJitter);
        }

        if (useDomainWarp)
        {
            noise.SetDomainWarpType((FastNoiseLite.DomainWarpType)(int)domainWarpType);
            noise.SetDomainWarpAmp(domainWarpAmp);
        }
    }

    private void GeneratePreview()
    {
        if (previewTexture == null || previewTexture.width != width || previewTexture.height != height)
            previewTexture = new Texture2D(width, height);

        float minValue = float.MaxValue, maxValue = float.MinValue;
        float[,] noiseValues = new float[width, height];

        // 检查是否应该使用 3D 计算模式
        // 如果开启了 RotationType3D 优化，FastNoiseLite 需要调用 3D 接口才能生效
        bool use3DCompute = rotationType3D != RotationType3D.None;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float xCoord = x + offset.x, yCoord = y + offset.y;
                float zCoord = 0f; // 默认 Z 轴为 0

                if (useDomainWarp)
                {
                    if (use3DCompute) noise.DomainWarp(ref xCoord, ref yCoord, ref zCoord);
                    else noise.DomainWarp(ref xCoord, ref yCoord);
                }

                float val;
                if (use3DCompute)
                {
                    // 调用 3D 接口以启用 RotationType3D 优化，Z 传入 0
                    val = noise.GetNoise(xCoord, yCoord, zCoord);
                }
                else
                {
                    // 标准 2D 接口，性能稍好
                    val = noise.GetNoise(xCoord, yCoord);
                }

                noiseValues[x, y] = val;
                minValue = Mathf.Min(minValue, val);
                maxValue = Mathf.Max(maxValue, val);
            }
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float value = noiseValues[x, y];
                value = normalizeOutput && maxValue > minValue ? (value - minValue) / (maxValue - minValue) : (value + 1f) * 0.5f;
                if (invertNoise) value = 1f - value;
                if (useContrast) value = Mathf.Pow(value, contrastPower);
                value = Mathf.Clamp01(value);
                Color color = useColorGradient ? colorGradient.Evaluate(value) : new Color(value, value, value);
                previewTexture.SetPixel(x, y, color);
            }
        }
        previewTexture.Apply();
    }

    private void ApplyPreset(int preset)
    {
        // 应用预设时重置 RotationType
        rotationType3D = RotationType3D.None;

        switch (preset)
        {
            case 1: noiseType = NoiseType.Perlin; fractalType = FractalType.FBm; octaves = 6; frequency = 0.005f; break;
            case 2: noiseType = NoiseType.OpenSimplex2; fractalType = FractalType.FBm; octaves = 5; frequency = 0.008f; useDomainWarp = true; domainWarpAmp = 50f; break;
            case 3: noiseType = NoiseType.ValueCubic; fractalType = FractalType.Ridged; octaves = 6; frequency = 0.015f; useContrast = true; contrastPower = 1.5f; break;
            case 4: noiseType = NoiseType.Cellular; cellularReturnType = CellularReturnType.Distance2Sub; frequency = 0.02f; break;
            case 5: noiseType = NoiseType.Perlin; fractalType = FractalType.PingPong; octaves = 4; frequency = 0.01f; pingPongStrength = 3.0f; useDomainWarp = true; domainWarpAmp = 100f; break;
            case 6: noiseType = NoiseType.OpenSimplex2S; fractalType = FractalType.Ridged; octaves = 5; frequency = 0.012f; useDomainWarp = true; domainWarpAmp = 80f; useContrast = true; contrastPower = 2.0f; break;
            case 7: noiseType = NoiseType.Cellular; cellularReturnType = CellularReturnType.CellValue; frequency = 0.025f; fractalType = FractalType.FBm; octaves = 3; break;
            case 8: noiseType = NoiseType.OpenSimplex2; fractalType = FractalType.Ridged; octaves = 6; frequency = 0.02f; lacunarity = 3.0f; gain = 0.6f; useDomainWarp = true; domainWarpAmp = 150f; break;
            case 9: noiseType = NoiseType.Perlin; fractalType = FractalType.Ridged; octaves = 7; frequency = 0.004f; useContrast = true; contrastPower = 1.8f; break;
        }
        UpdateNoiseSettings();
        GeneratePreview();
    }

    private void SaveTexture()
    {
        if (string.IsNullOrWhiteSpace(customFileName))
        {
            EditorUtility.DisplayDialog("保存失败", "请先输入文件名！", "确定");
            return;
        }

        string sanitized = customFileName.Trim();
        foreach (char c in Path.GetInvalidFileNameChars()) sanitized = sanitized.Replace(c.ToString(), "");
        if (sanitized.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
            sanitized = sanitized.Substring(0, sanitized.Length - 4);

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            EditorUtility.DisplayDialog("保存失败", "文件名包含非法字符！", "确定");
            return;
        }

        string dirPath = Application.dataPath + "/" + saveFolderName + "/";
        if (!Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);

        string fileName = dirPath + sanitized + ".png";
        if (File.Exists(fileName) && !EditorUtility.DisplayDialog("文件已存在", $"'{sanitized}.png' 已存在，是否覆盖？", "覆盖", "取消"))
            return;

        File.WriteAllBytes(fileName, previewTexture.EncodeToPNG());
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("保存成功", $"文件已保存到:\n{fileName}", "确定");
    }

    #region 描述文本辅助方法

    private string GetNoiseTypeDescription(NoiseType type)
    {
        switch (type)
        {
            case NoiseType.OpenSimplex2: return "OpenSimplex2: 高性能且平滑，适合大多数地形和纹理。";
            case NoiseType.OpenSimplex2S: return "OpenSimplex2S: OpenSimplex2 的变体，形态略有不同。";
            case NoiseType.Cellular: return "Cellular (Voronoi/Worley): 产生细胞、晶体或鹅卵石状图案。";
            case NoiseType.Perlin: return "Perlin: 经典的自然噪声，适合云彩和地形。";
            case NoiseType.Value: return "Value: 基于晶格点的简单随机值插值，块状感较强。";
            case NoiseType.ValueCubic: return "ValueCubic: Value 噪声的更平滑版本。";
            default: return "";
        }
    }

    private string GetFractalDescription(FractalType type)
    {
        switch (type)
        {
            case FractalType.None: return "不使用分形，仅生成单层基础噪声。";
            case FractalType.FBm: return "分形布朗运动 (FBM): 叠加多层噪声，频率增加振幅减小，产生丰富细节。";
            case FractalType.Ridged: return "山脊 (Ridged): 产生类似于山脉尖峰的锐利边缘效果。";
            case FractalType.PingPong: return "乒乓 (PingPong): 使用乒乓函数折叠值，产生波浪状起伏。";
            case FractalType.DomainWarpProgressive: return "渐进式域扭曲: 使用噪声来扭曲坐标域，产生液态流动效果。";
            case FractalType.DomainWarpIndependent: return "独立域扭曲: 类似于渐进式，但扭曲计算方式略有不同。";
            default: return "";
        }
    }

    #endregion
}
