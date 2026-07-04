using DG.Tweening;
using UnityEngine;

/// <summary>
/// 运行时动态生成的拍击冲击波效果，包含三层：
///   1. 主扩散环  — 粗、快速向外扩散
///   2. 回声环    — 细、略小、更快消失，制造层次感
///   3. 火花粒子  — 高速向外射出，亮黄白色，短生命周期
///   4. 碎屑粒子  — 中速，受轻微重力影响，暖橙色，飘散感
///
/// 由 SmackImpactVFX 实例化，播放结束后自动销毁。
/// </summary>
public class ShockwaveRingEffect : MonoBehaviour
{
    private const int Segments = 64;

    private Color _color;
    private float _endRadius;
    private float _duration;

    /// <summary>Play() 前调用，设置基础外观参数。</summary>
    public void Init(Color color, float endRadius, float duration)
    {
        _color     = color;
        _endRadius = endRadius;
        _duration  = duration;
    }

    /// <summary>在指定世界坐标播放完整冲击波效果。</summary>
    public void Play(Vector3 worldPos)
    {
        transform.position = new Vector3(worldPos.x, worldPos.y, worldPos.z - 0.5f);

        // 层 1：主扩散环
        BuildRing(gameObject, _color, lineWidth: 0.15f,
                  endRadius: _endRadius, duration: _duration, ease: Ease.OutExpo);

        // 层 2：回声环（细、小、快）
        var echoGo    = new GameObject("EchoRing");
        echoGo.transform.SetParent(transform, false);
        var echoColor = new Color(_color.r * 0.6f, _color.g * 0.85f, 1f, _color.a * 0.55f);
        BuildRing(echoGo, echoColor, lineWidth: 0.055f,
                  endRadius: _endRadius * 0.72f, duration: _duration * 0.6f, ease: Ease.OutCubic);

        // 层 3：火花粒子
        SpawnSparks();

        // 层 4：碎屑粒子
        SpawnDebris();

        // 整体销毁（等最长动画结束）
        DOVirtual.DelayedCall(_duration + 0.2f, () => { if (this != null) Destroy(gameObject); });
    }

    // -------------------------------------------------------
    // 内部：圆环
    // -------------------------------------------------------

    private void BuildRing(GameObject host, Color color, float lineWidth,
                           float endRadius, float duration, Ease ease)
    {
        var lr = host.AddComponent<LineRenderer>();
        lr.loop          = true;
        lr.positionCount = Segments;
        lr.useWorldSpace = false;
        lr.startWidth    = lineWidth;
        lr.endWidth      = lineWidth;
        lr.startColor    = color;
        lr.endColor      = color;
        lr.material      = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
        lr.sortingOrder  = 20;

        const float startRadius = 0.2f;
        float radius = startRadius;
        Color c = color;
        SetCircle(lr, radius);

        var seq = DOTween.Sequence();
        seq.Append(
            DOTween.To(() => radius, v => { radius = v; SetCircle(lr, v); }, endRadius, duration)
                   .SetEase(ease));
        seq.Join(
            DOTween.To(() => c.a, v => { c.a = v; lr.startColor = c; lr.endColor = c; }, 0f, duration)
                   .SetEase(Ease.InQuad));
    }

    private static void SetCircle(LineRenderer lr, float radius)
    {
        for (int i = 0; i < Segments; i++)
        {
            float angle = i * Mathf.PI * 2f / Segments;
            lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }
    }

    // -------------------------------------------------------
    // 内部：火花粒子（高速、短命、亮白/黄）
    // -------------------------------------------------------

    private void SpawnSparks()
    {
        var go = new GameObject("Sparks");
        go.transform.SetParent(transform, false);

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop            = false;
        main.playOnAwake     = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 80;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(_endRadius * 1.8f, _endRadius * 3.5f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.04f, 0.11f);
        main.startRotation   = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.startColor      = new ParticleSystem.MinMaxGradient(
            new Color(1f, 1f, 0.85f, 1f),
            new Color(1f, 0.65f, 0.2f,  1f));

        var emission = ps.emission;
        emission.enabled = false;

        var shape = ps.shape;
        shape.shapeType       = ParticleSystemShapeType.Circle;
        shape.radius          = 0.15f;
        shape.radiusThickness = 1f; // 整个圆面发射，形成放射状

        // 大小：先略膨胀再快速收缩到 0
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size    = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f,   1f),
            new Keyframe(0.2f, 1.3f),
            new Keyframe(1f,   0f)));

        // 颜色+透明度渐变
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad    = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(new Color(1f, 1f, 0.9f),    0f),
                new GradientColorKey(new Color(1f, 0.45f, 0.1f), 1f)
            },
            new[] {
                new GradientAlphaKey(1f,  0f),
                new GradientAlphaKey(0.8f, 0.5f),
                new GradientAlphaKey(0f,  1f)
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.material     = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
        r.sortingOrder = 21;

        ps.Emit(40);
    }

    // -------------------------------------------------------
    // 内部：碎屑粒子（中速、有重力、暖橙/金、飘散感）
    // -------------------------------------------------------

    private void SpawnDebris()
    {
        var go = new GameObject("Debris");
        go.transform.SetParent(transform, false);

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop            = false;
        main.playOnAwake     = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 40;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.55f, 1.1f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(_endRadius * 0.4f, _endRadius * 1.1f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.07f, 0.22f);
        main.gravityModifier = 0.35f; // 轻微重力，碎屑往下坠
        main.startColor      = new ParticleSystem.MinMaxGradient(
            new Color(1f,   0.88f, 0.55f, 0.9f),
            new Color(0.85f, 0.45f, 0.15f, 0.7f));

        var emission = ps.emission;
        emission.enabled = false;

        var shape = ps.shape;
        shape.shapeType       = ParticleSystemShapeType.Circle;
        shape.radius          = 0.3f;
        shape.radiusThickness = 0f; // 从圆周边缘发射

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size    = new ParticleSystem.MinMaxCurve(
            1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var col  = ps.colorOverLifetime;
        col.enabled = true;
        var grad    = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(new Color(1f,  0.9f, 0.6f), 0f),
                new GradientColorKey(new Color(0.6f, 0.3f, 0.1f), 1f)
            },
            new[] {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0f,   1f)
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.material     = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
        r.sortingOrder = 19;

        ps.Emit(18);
    }
}
