using System.Collections.Generic;
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

    private readonly List<Material> _ownedMaterials = new List<Material>();

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

        // 层 1：主冲击环 — 最粗最快，立即出现
        BuildRing(gameObject, _color,
                  lineWidth: 0.18f, endRadius: _endRadius,
                  duration: _duration, delay: 0f, ease: Ease.OutExpo);

        // 层 2：第二波 — 略细，稍慢，偏暖橙，延迟 0.06s
        var ring2Go    = new GameObject("Ring2");
        ring2Go.transform.SetParent(transform, false);
        var ring2Color = new Color(1f, _color.g * 0.75f, _color.b * 0.3f, _color.a * 0.75f);
        BuildRing(ring2Go, ring2Color,
                  lineWidth: 0.10f, endRadius: _endRadius * 0.82f,
                  duration: _duration * 0.88f, delay: 0.06f, ease: Ease.OutCubic);

        // 层 3：第三波 — 更细，偏冷蓝白，延迟 0.13s
        var ring3Go    = new GameObject("Ring3");
        ring3Go.transform.SetParent(transform, false);
        var ring3Color = new Color(_color.r * 0.55f, _color.g * 0.85f, 1f, _color.a * 0.50f);
        BuildRing(ring3Go, ring3Color,
                  lineWidth: 0.055f, endRadius: _endRadius * 0.65f,
                  duration: _duration * 0.70f, delay: 0.13f, ease: Ease.OutQuad);

        // 层 4：尾波 — 最细最小，纯白，延迟 0.20s，消失最快
        var ring4Go    = new GameObject("Ring4");
        ring4Go.transform.SetParent(transform, false);
        var ring4Color = new Color(1f, 1f, 1f, _color.a * 0.35f);
        BuildRing(ring4Go, ring4Color,
                  lineWidth: 0.03f, endRadius: _endRadius * 0.48f,
                  duration: _duration * 0.55f, delay: 0.20f, ease: Ease.OutQuad);

        // 粒子
        SpawnSparks();
        SpawnDebris();

        // 整体销毁：先手动释放动态材质，再销毁 GO
        DOVirtual.DelayedCall(_duration + 0.35f, () =>
        {
            if (this == null) return;
            foreach (var mat in _ownedMaterials)
                if (mat != null) Destroy(mat);
            _ownedMaterials.Clear();
            Destroy(gameObject);
        });
    }

    // -------------------------------------------------------
    // 内部：圆环
    // -------------------------------------------------------

    private void BuildRing(GameObject host, Color color, float lineWidth,
                           float endRadius, float duration, float delay, Ease ease)
    {
        var lr = host.AddComponent<LineRenderer>();
        lr.loop          = true;
        lr.positionCount = Segments;
        lr.useWorldSpace = false;
        lr.startWidth    = lineWidth;
        lr.endWidth      = lineWidth;
        lr.startColor    = new Color(color.r, color.g, color.b, 0f); // 延迟前隐藏
        lr.endColor      = new Color(color.r, color.g, color.b, 0f);
        var mat = new Material(Shader.Find("Sprites/Default"));
        _ownedMaterials.Add(mat);
        lr.material = mat;
        lr.sortingOrder  = 20;

        const float startRadius = 0.15f;
        float radius = startRadius;
        Color c = color;
        c.a = 0f;
        SetCircle(lr, radius);

        var seq = DOTween.Sequence();
        seq.AppendInterval(delay);
        // 出现瞬间把 alpha 打满
        seq.AppendCallback(() => { c.a = color.a; lr.startColor = c; lr.endColor = c; });
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
        var sparkMat = new Material(Shader.Find("Sprites/Default"));
        _ownedMaterials.Add(sparkMat);
        r.material     = sparkMat;
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
        var debrisMat = new Material(Shader.Find("Sprites/Default"));
        _ownedMaterials.Add(debrisMat);
        r.material     = debrisMat;
        r.sortingOrder = 19;

        ps.Emit(18);
    }
}
