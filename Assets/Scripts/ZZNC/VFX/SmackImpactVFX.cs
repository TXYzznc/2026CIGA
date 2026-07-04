using UnityEngine;

/// <summary>
/// 拍击冲击感特效总入口（Facade）。
///
/// 使用方式：
///   1. 在场景中任意 GameObject 上挂载此脚本。
///   2. 在 Inspector 中将 Camera 上的 ScreenShakeEffect 和
///      ChromaticAberrationEffect 组件拖入对应槽位。
///   3. 如需按钮粒子，将拍击按钮 GameObject 上的 SmackButtonParticlesEffect 拖入。
///   4. 在需要触发效果的代码中调用：
///        impactVFX.PlaySmackImpact(boardWorldCenter);
/// </summary>
public class SmackImpactVFX : MonoBehaviour
{
    [Header("摄像机效果（挂 Main Camera 上，拖进来）")]
    [SerializeField] private ScreenShakeEffect          screenShake;
    [SerializeField] private ChromaticAberrationEffect  chromaticAberration;

    [Header("冲击波（运行时自动生成圆环，无需 Prefab）")]
    [SerializeField] private Color shockwaveColor     = new Color(1f, 0.95f, 0.8f, 0.9f);
    [SerializeField, Range(0.5f, 12f)] private float shockwaveEndRadius = 4.5f;
    [SerializeField, Range(0.1f, 1f)]  private float shockwaveDuration  = 0.45f;

    [Header("按钮粒子（拍击按钮 GameObject 上的 SmackButtonParticlesEffect）")]
    [SerializeField] private SmackButtonParticlesEffect buttonParticles;

    // -------------------------------------------------------
    // 对外接口
    // -------------------------------------------------------

    /// <summary>
    /// 触发完整的拍击冲击感特效。
    /// <param name="boardWorldCenter">棋盘世界空间中心，冲击波从此处扩散。</param>
    /// </summary>
    public void PlaySmackImpact(Vector3 boardWorldCenter)
    {
        screenShake?.Shake();
        chromaticAberration?.Pulse();
        SpawnShockwave(boardWorldCenter);
        buttonParticles?.Burst();
    }

    /// <summary>仅触发屏幕震动。</summary>
    public void PlayScreenShakeOnly() => screenShake?.Shake();

    /// <summary>仅触发色差脉冲。</summary>
    public void PlayChromaticPulseOnly() => chromaticAberration?.Pulse();

    /// <summary>仅在指定位置生成冲击波圆环。</summary>
    public void PlayShockwaveOnly(Vector3 worldPos) => SpawnShockwave(worldPos);

    /// <summary>仅触发按钮粒子爆发。</summary>
    public void PlayButtonParticlesOnly() => buttonParticles?.Burst();

    // -------------------------------------------------------
    // 内部
    // -------------------------------------------------------

    private void SpawnShockwave(Vector3 worldPos)
    {
        var go   = new GameObject($"ShockwaveRing_{Time.frameCount}");
        var ring = go.AddComponent<ShockwaveRingEffect>();
        ring.Init(shockwaveColor, shockwaveEndRadius, shockwaveDuration);
        ring.Play(worldPos);
    }
}
