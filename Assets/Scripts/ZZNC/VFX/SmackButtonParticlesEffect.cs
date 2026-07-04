using UnityEngine;

/// <summary>
/// 挂在拍击按钮的 GameObject 上。调用 Burst() 触发粒子爆发。
///
/// 粒子以世界坐标系运动。Burst() 时会把自身位置对齐到按钮中心点在世界坐标中的
/// 对应位置（Screen Space - Overlay Canvas → ScreenToWorldPoint），确保粒子
/// 从屏幕上按钮位置飞出，而非停留在 UI 层。
///
/// 若 Inspector 中未手动添加 ParticleSystem，Awake 时自动生成一套默认配置。
/// </summary>
public class SmackButtonParticlesEffect : MonoBehaviour
{
    [SerializeField, Range(5, 80)]       private int   burstCount    = 24;
    [SerializeField]                     private Color particleColor = new Color(1f, 0.92f, 0.5f, 1f);
    [SerializeField, Range(0.5f, 6f)]    private float speed         = 3f;
    [SerializeField, Range(0.1f, 2f)]    private float lifetime      = 0.45f;
    [SerializeField, Range(0.02f, 0.4f)] private float size          = 0.09f;

    [Header("按钮 RectTransform（留空则用父级或自身）")]
    [SerializeField] private RectTransform buttonRect;

    [Header("粒子在世界坐标中的深度（相机近裁面偏移量）")]
    [SerializeField, Range(0.1f, 20f)] private float worldDepth = 5f;

    private ParticleSystem _ps;

    private void Awake()
    {
        if (buttonRect == null)
            buttonRect = GetComponentInParent<RectTransform>();

        _ps = GetComponent<ParticleSystem>();
        if (_ps == null)
            _ps = BuildDefaultParticleSystem();
    }

    /// <summary>触发一次粒子爆发，粒子从按钮屏幕位置对应的世界坐标处飞出。</summary>
    public void Burst()
    {
        AlignToButtonWorldPosition();
        _ps.Emit(burstCount);
    }

    /// <summary>
    /// 将本 GO 移到按钮中心点在世界坐标系中的对应位置。
    /// Screen Space - Overlay：RectTransform.position 即屏幕像素坐标，
    /// 用 Camera.main.ScreenToWorldPoint 转换为世界坐标。
    /// </summary>
    private void AlignToButtonWorldPosition()
    {
        var cam = Camera.main;
        if (cam == null || buttonRect == null) return;

        // Overlay Canvas 传 null camera
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, buttonRect.position);

        // worldDepth：相机到粒子的距离（沿 forward 轴）
        float z = cam.nearClipPlane + worldDepth;
        transform.position = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, z));
    }

    private ParticleSystem BuildDefaultParticleSystem()
    {
        var ps = gameObject.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop             = false;
        main.playOnAwake      = false;
        main.startLifetime    = lifetime;
        main.startSpeed       = speed;
        main.startSize        = size;
        main.startColor       = particleColor;
        main.simulationSpace  = ParticleSystemSimulationSpace.World;
        main.maxParticles     = 200;

        var emission = ps.emission;
        emission.enabled = false; // 只通过 Emit() 手动触发

        var shape = ps.shape;
        shape.enabled         = true;
        shape.shapeType       = ParticleSystemShapeType.Circle;
        shape.radius          = 0.3f;
        shape.radiusThickness = 0f; // 只在圆周上发射，向外扩散感更强

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size    = new ParticleSystem.MinMaxCurve(
            1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.material     = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
        r.sortingOrder = 15;

        return ps;
    }
}
