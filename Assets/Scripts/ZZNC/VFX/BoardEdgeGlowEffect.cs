using UnityEngine;

/// <summary>
/// 棋盘旋转时边缘发出的蓝色流光。
/// 挂在棋盘 Transform 的子物体上，随棋盘一同旋转。
/// 调用 Setup() 初始化外形，每帧调用 SetSpeed() 驱动亮度。
/// </summary>
public class BoardEdgeGlowEffect : MonoBehaviour
{
    [SerializeField] private Color glowColor = new Color(0.35f, 0.78f, 1f, 1f);
    [SerializeField, Range(0.02f, 0.5f)] private float lineWidth = 0.12f;
    [SerializeField, Range(10f, 200f)] private float maxSpeed = 70f;
    [SerializeField, Range(0.01f, 0.3f)] private float smoothTime = 0.08f;

    private LineRenderer _line;
    private float _currentAlpha;
    private float _smoothVelocity;

    private void Awake()
    {
        EnsureLine();
        ApplyAlpha(0f);
    }

    /// <summary>BuildLayout 完成后调用，依棋盘半径和格子大小重建六边形轮廓。</summary>
    public void Setup(int boardRadius, float cellSize)
    {
        EnsureLine();

        // The board cells stay pointy-top, while the large board outline is flat-top.
        float outer = Mathf.Sqrt(3f) * boardRadius * cellSize + cellSize * 0.68f;

        _line.positionCount = 6;
        for (int i = 0; i < 6; i++)
        {
            // 本地尖顶角点（30°起），board -30° 后世界空间呈平顶轮廓
            float a = (30f + 60f * i) * Mathf.Deg2Rad;
            _line.SetPosition(i, new Vector3(Mathf.Cos(a) * outer, Mathf.Sin(a) * outer, -0.03f));
        }

        _line.widthMultiplier = lineWidth;
    }

    /// <summary>每帧由 TempPlaytestController.Update() 传入弹簧速度绝对值。</summary>
    public void SetSpeed(float absSpeed)
    {
        float target = Mathf.Clamp01(absSpeed / maxSpeed);
        _currentAlpha = Mathf.SmoothDamp(_currentAlpha, target, ref _smoothVelocity, smoothTime);
        ApplyAlpha(_currentAlpha);
    }

    private void ApplyAlpha(float alpha)
    {
        EnsureLine();
        var c = glowColor;
        c.a = alpha;
        _line.startColor = c;
        _line.endColor = c;
        // 宽度随速度微微放大，增强弹性感
        _line.widthMultiplier = lineWidth * (1f + alpha * 0.6f);
    }

    private void EnsureLine()
    {
        if (_line != null) return;

        _line = GetComponent<LineRenderer>();
        if (_line == null)
            _line = gameObject.AddComponent<LineRenderer>();

        var shader = Shader.Find("Sprites/Default");
        if (_line.sharedMaterial == null && shader != null)
            _line.material = new Material(shader);

        _line.useWorldSpace = false;
        _line.loop = true;
        _line.numCapVertices = 4;
        _line.numCornerVertices = 4;
        _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _line.receiveShadows = false;
        _line.sortingOrder = 20;
    }
}
