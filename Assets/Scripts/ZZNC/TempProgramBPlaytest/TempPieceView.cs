using System.Collections;
using UnityEngine;

/// <summary>
/// Temporary visual adapter for Program-B playtest replacement.
/// Delete the whole TempProgramBPlaytest folder when the real Program-B view layer is ready.
/// </summary>
public class TempPieceView : MonoBehaviour, IPieceView
{
    [SerializeField] public float MoveDuration = 0.18f;
    [SerializeField] public float FxDuration = 0.14f;
    private SpriteRenderer _renderer;
    private Coroutine _motion;
    private Vector3 _baseScale;

    /// <summary>队列越长该值越大，动画幅度越夸张。由 SmackResolver 在每次拍击前设置。</summary>
    public static float GlobalAmplitudeScale = 1f;
    /// <summary>队列越长该值越大，协程动画实际速度越快。由 SmackResolver 设置。</summary>
    public static float GlobalSpeedScale = 1f;

    private static readonly int StretchDir    = Shader.PropertyToID("_StretchDir");
    private static readonly int StretchAmount = Shader.PropertyToID("_StretchAmount");

    private MaterialPropertyBlock _mpb;

    public void Init(Sprite sprite, Material material, int sortingOrder)
    {
        _renderer = gameObject.GetComponent<SpriteRenderer>();
        if (_renderer == null)
            _renderer = gameObject.AddComponent<SpriteRenderer>();

        _renderer.sprite = sprite;
        _renderer.sharedMaterial = material;
        _renderer.sortingOrder = sortingOrder;
        _baseScale = transform.localScale;

        _mpb = new MaterialPropertyBlock();
        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetVector(StretchDir,    Vector4.zero);
        _mpb.SetFloat (StretchAmount, 0f);
        _renderer.SetPropertyBlock(_mpb);
    }

    public void SnapTo(Vector3 worldPos)
    {
        if (_motion != null)
        {
            StopCoroutine(_motion);
            _motion = null;
        }
        transform.position = worldPos;
        transform.localScale = _baseScale; // 恢复基准大小，防止中断的动画遗留了缩放
    }

    public float MoveTo(Vector3 worldPos)
    {
        StartMotion(AnimateMove(worldPos));
        return MoveDuration;
    }

    public float PlayHitShake()
    {
        StartMotion(AnimateShake());
        return FxDuration;
    }

    public float PlayAbilityFX()
    {
        StartMotion(AnimatePulse());
        return FxDuration;
    }

    public float PlaySpawn()
    {
        // 确保协程跑第一帧之前不可见（解决 ProcessEventQueue 阶段已可见的问题）
        transform.localScale = Vector3.zero;
        StartMotion(AnimateSpawn());
        return FxDuration;
    }

    public float PlayRemove()
    {
        StartMotion(AnimateRemove());
        return FxDuration;
    }

    private void StartMotion(IEnumerator routine)
    {
        if (_motion != null)
        {
            StopCoroutine(_motion);
        }

        _motion = StartCoroutine(routine);
    }

    private IEnumerator AnimateMove(Vector3 target)
    {
        float dur   = MoveDuration / GlobalSpeedScale;
        var   start = transform.position;

        // 运动方向转换到本地空间（Shader 在 local space 做顶点拉伸）
        Vector3 worldDir = (target - start).normalized;
        Vector3 localDir = transform.InverseTransformDirection(worldDir);

        for (var t = 0f; t < dur; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / dur);
            transform.position = Vector3.Lerp(start, target, k);

            // 速度曲线：中段最快，两端为 0
            // SmoothStep 的导数 ≈ 6t(1-t)，归一化后峰值在 t=0.5
            float speed01 = 6f * (t / dur) * (1f - t / dur); // 0→1→0
            float stretch = speed01 * GlobalAmplitudeScale * 0.8f;
            stretch = Mathf.Clamp01(stretch);

            if (_mpb != null && _renderer != null)
            {
                _mpb.SetVector(StretchDir,    new Vector4(localDir.x, localDir.y, 0f, 0f));
                _mpb.SetFloat (StretchAmount, stretch);
                _renderer.SetPropertyBlock(_mpb);
            }

            yield return null;
        }

        transform.position = target;

        // 落点：清除拉伸，生成撞击粒子
        if (_mpb != null && _renderer != null)
        {
            _mpb.SetFloat(StretchAmount, 0f);
            _renderer.SetPropertyBlock(_mpb);
        }
        SpawnLandParticles(target, worldDir);
    }

    // -------------------------------------------------------
    // 落地撞击粒子
    // -------------------------------------------------------

    private static void SpawnLandParticles(Vector3 worldPos, Vector3 arrivalDir)
    {
        var go = new GameObject("LandParticles") { hideFlags = HideFlags.HideAndDontSave };
        go.transform.position = worldPos;

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop            = false;
        main.playOnAwake     = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 20;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.8f,  2.2f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.04f, 0.11f);
        main.gravityModifier = 0.25f;
        main.startColor      = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.95f, 0.7f, 0.9f),
            new Color(0.9f, 0.55f, 0.2f, 0.7f));

        var emission = ps.emission;
        emission.enabled = false;

        // 形状：半球面，朝向来方向（粒子往撞上去的方向扇形喷出）
        var shape = ps.shape;
        shape.shapeType  = ParticleSystemShapeType.Hemisphere;
        shape.radius     = 0.1f;
        // 把半球对齐到来方向：旋转使 +Y 指向 arrivalDir
        shape.rotation   = Quaternion.FromToRotation(Vector3.up, arrivalDir).eulerAngles;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size    = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.material     = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
        r.sortingOrder = 10;

        ps.Emit(10);

        // 延迟销毁（等粒子寿命结束）
        Object.Destroy(go, 0.6f);
    }

    // -------------------------------------------------------
    // Squash & Stretch 碰撞震动
    // -------------------------------------------------------

    private IEnumerator AnimateShake()
    {
        float dur = FxDuration / GlobalSpeedScale;
        float amp = Mathf.Clamp(GlobalAmplitudeScale, 0.5f, 2.5f);

        // 阶段一：受击瞬间压扁（X 扩张，Y 缩短）
        float squashDur = dur * 0.25f;
        for (var t = 0f; t < squashDur; t += Time.deltaTime)
        {
            float k = t / squashDur; // 0→1
            float x = Mathf.Lerp(1f, 1f + 0.45f * amp, k);
            float y = Mathf.Lerp(1f, 1f - 0.35f * amp, k);
            transform.localScale = new Vector3(_baseScale.x * x, _baseScale.y * y, _baseScale.z);
            yield return null;
        }

        // 阶段二：弹性过冲回弹（Overshoot：先超过基准再收回）
        float stretchDur = dur * 0.75f;
        for (var t = 0f; t < stretchDur; t += Time.deltaTime)
        {
            float k = t / stretchDur; // 0→1
            // 弹性曲线：用衰减正弦模拟弹跳
            float bounce = Mathf.Exp(-k * 5f) * Mathf.Cos(k * Mathf.PI * 3.5f);
            float x = 1f + bounce * 0.25f * amp;
            float y = 1f - bounce * 0.20f * amp;
            transform.localScale = new Vector3(_baseScale.x * x, _baseScale.y * y, _baseScale.z);
            yield return null;
        }

        transform.localScale = _baseScale;
    }

    private IEnumerator AnimatePulse()
    {
        float dur = FxDuration / GlobalSpeedScale;
        float amp = GlobalAmplitudeScale * GlobalAmplitudeScale;
        for (var t = 0f; t < dur; t += Time.deltaTime)
        {
            var k = Mathf.Sin(t / dur * Mathf.PI);
            transform.localScale = _baseScale * (1f + k * 0.22f * amp);
            yield return null;
        }
        transform.localScale = _baseScale;
    }

    private IEnumerator AnimateSpawn()
    {
        float dur = FxDuration / GlobalSpeedScale;
        for (var t = 0f; t < dur; t += Time.deltaTime)
        {
            var k = Mathf.SmoothStep(0f, 1f, t / dur);
            transform.localScale = _baseScale * k;
            yield return null;
        }
        transform.localScale = _baseScale;
    }

    private IEnumerator AnimateRemove()
    {
        float dur = FxDuration / GlobalSpeedScale;
        var startColor = _renderer != null ? _renderer.color : Color.white;
        for (var t = 0f; t < dur; t += Time.deltaTime)
        {
            var k = 1f - Mathf.SmoothStep(0f, 1f, t / dur);
            transform.localScale = _baseScale * k;
            if (_renderer != null)
                _renderer.color = new Color(startColor.r, startColor.g, startColor.b, startColor.a * k);
            yield return null;
        }
        if (_renderer != null)
            _renderer.color = Color.clear;
    }
}
