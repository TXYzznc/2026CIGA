using UnityEngine;

/// <summary>
/// 拍击冲击感特效测试驱动。
///
/// 搭建方式（纯脚本，无需额外 Prefab）：
///   1. 创建一个空 GameObject，挂载此脚本。
///   2. 将 impactVFX 拖入（挂有 SmackImpactVFX 的对象）。
///   3. 各子效果引用留空时，会在 Start() 中尝试自动查找场景内组件。
///   4. Play，用键盘触发效果。
///
/// 快捷键：
///   [Space] / [Enter]  完整拍击效果（全部同时触发）
///   [1]                仅屏幕震动
///   [2]                仅色差脉冲
///   [3]                仅冲击波圆环
///   [4]                仅按钮粒子爆发
///   [R]                重置测试棋盘中心坐标为 (0,0,0)
/// </summary>
public class SmackVFXTestRunner : MonoBehaviour
{
    [Header("被测对象")]
    [SerializeField] private SmackImpactVFX             impactVFX;
    [SerializeField] private ScreenShakeEffect          screenShake;
    [SerializeField] private ChromaticAberrationEffect  chromaticAberration;
    [SerializeField] private SmackButtonParticlesEffect buttonParticles;

    [Header("冲击波测试位置（世界空间）")]
    [SerializeField] private Vector3 testBoardCenter = Vector3.zero;

    private void Start()
    {
        // 未手动拖入时，尝试自动查找
        if (impactVFX          == null) impactVFX         = FindFirstObjectByType<SmackImpactVFX>();
        if (screenShake        == null) screenShake       = FindFirstObjectByType<ScreenShakeEffect>();
        if (chromaticAberration == null) chromaticAberration = FindFirstObjectByType<ChromaticAberrationEffect>();
        if (buttonParticles    == null) buttonParticles   = FindFirstObjectByType<SmackButtonParticlesEffect>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
        {
            if (impactVFX != null)
                impactVFX.PlaySmackImpact(testBoardCenter);
            else
                Debug.LogWarning("[SmackVFXTest] impactVFX 未赋值");
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            if (screenShake != null)   screenShake.Shake();
            else if (impactVFX != null) impactVFX.PlayScreenShakeOnly();
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            if (chromaticAberration != null)   chromaticAberration.Pulse();
            else if (impactVFX != null)         impactVFX.PlayChromaticPulseOnly();
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            if (impactVFX != null)
                impactVFX.PlayShockwaveOnly(testBoardCenter);
            else
                SpawnShockwaveStandalone();
        }

        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            if (buttonParticles != null)  buttonParticles.Burst();
            else if (impactVFX != null)   impactVFX.PlayButtonParticlesOnly();
        }

        if (Input.GetKeyDown(KeyCode.R))
            testBoardCenter = Vector3.zero;
    }

    private void OnGUI()
    {
        var bg = new GUIStyle(GUI.skin.box);
        var label = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            normal   = { textColor = Color.white }
        };
        var title = new GUIStyle(label)
        {
            fontSize  = 17,
            fontStyle = FontStyle.Bold
        };

        GUILayout.BeginArea(new Rect(12, 12, 330, 185), bg);
        GUILayout.Label("SmackImpactVFX  测试面板", title);
        GUILayout.Space(4);
        GUILayout.Label("[Space / Enter]  完整拍击效果（全部）", label);
        GUILayout.Label("[1]              屏幕震动",              label);
        GUILayout.Label("[2]              色差脉冲",              label);
        GUILayout.Label("[3]              冲击波圆环",            label);
        GUILayout.Label("[4]              按钮粒子爆发",          label);
        GUILayout.Label("[R]              重置棋盘中心到原点",    label);
        GUILayout.EndArea();

        // 右下角状态行
        var statusStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            normal   = { textColor = impactVFX != null ? Color.green : Color.red }
        };
        var statusText = impactVFX != null
            ? "SmackImpactVFX: OK"
            : "SmackImpactVFX: 未找到，请在 Inspector 中赋值";
        GUI.Label(new Rect(12, Screen.height - 28, 400, 24), statusText, statusStyle);
    }

    // 无 impactVFX 时的后备冲击波，直接实例化
    private void SpawnShockwaveStandalone()
    {
        var go   = new GameObject("TestShockwave");
        var ring = go.AddComponent<ShockwaveRingEffect>();
        ring.Init(new Color(1f, 0.9f, 0.5f, 1f), 4f, 0.45f);
        ring.Play(testBoardCenter);
    }
}
