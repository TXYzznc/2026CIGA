using UnityEngine;

/// <summary>
/// Combo score pulse that follows the board outline.
/// </summary>
public class BoardHexPulseEffect : MonoBehaviour
{
    private const int CornerCount = 6;

    [SerializeField] private Color lowComboColor = new Color(1f, 0.48f, 0.08f, 1f);
    [SerializeField] private Color highComboColor = new Color(1f, 0.05f, 0.03f, 1f);
    [SerializeField, Range(0.02f, 0.5f)] private float baseLineWidth = 0.13f;
    [SerializeField, Range(0.1f, 1.2f)] private float duration = 0.45f;
    [SerializeField, Range(0f, 1f)] private float minAlpha = 0.35f;
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.9f;
    [SerializeField, Range(0f, 0.8f)] private float expandDistance = 0.32f;
    [SerializeField, Range(0f, 0.5f)] private float irregularity = 0.16f;
    [SerializeField] private int fullCombo = 8;

    private readonly LineRenderer[] _rings = new LineRenderer[3];
    private readonly float[] _ringDelay = { 0f, 0.07f, 0.14f };
    private readonly float[] _ringScale = { 1f, 0.72f, 0.48f };
    private readonly Vector3[] _basePoints = new Vector3[CornerCount];
    private readonly float[] _cornerNoise = new float[CornerCount];
    private readonly Material[] _materials = new Material[3];

    private float _outerRadius;
    private float _elapsed = 999f;
    private float _comboT;
    private Color _pulseColor;

    public void Setup(int boardRadius, float cellSize)
    {
        EnsureRings();

        _outerRadius = 2f * boardRadius * cellSize;
        for (int i = 0; i < CornerCount; i++)
        {
            float angle = (30f + 60f * i) * Mathf.Deg2Rad;
            _basePoints[i] = new Vector3(Mathf.Cos(angle) * _outerRadius, Mathf.Sin(angle) * _outerRadius, -0.04f);
        }

        ApplyHidden();
    }

    public void Pulse(int combo)
    {
        EnsureRings();
        if (_outerRadius <= 0f)
            Setup(3, 1.45f);

        int comboSpan = Mathf.Max(1, fullCombo - 1);
        _comboT = Mathf.Clamp01((combo - 1f) / comboSpan);
        _pulseColor = Color.Lerp(lowComboColor, highComboColor, _comboT);
        _elapsed = 0f;

        for (int i = 0; i < CornerCount; i++)
            _cornerNoise[i] = Random.Range(-1f, 1f);

        UpdateRings();
    }

    private void Update()
    {
        if (_elapsed > duration + _ringDelay[_ringDelay.Length - 1])
            return;

        _elapsed += Time.deltaTime;
        UpdateRings();
    }

    private void UpdateRings()
    {
        for (int i = 0; i < _rings.Length; i++)
        {
            float localT = Mathf.InverseLerp(_ringDelay[i], _ringDelay[i] + duration, _elapsed);
            if (localT <= 0f || localT >= 1f)
            {
                SetRingAlpha(_rings[i], 0f);
                continue;
            }

            float alphaCurve = 1f - Mathf.SmoothStep(0f, 1f, localT);
            float alpha = Mathf.Lerp(minAlpha, maxAlpha, _comboT) * alphaCurve * _ringScale[i];
            float width = baseLineWidth * Mathf.Lerp(1f, 2.1f, _comboT) * (1f + alphaCurve * 0.7f) * _ringScale[i];
            float expand = expandDistance * (0.25f + _comboT) * localT * (1f + i * 0.45f);
            float noiseScale = irregularity * (0.5f + _comboT) * alphaCurve;

            var ring = _rings[i];
            ring.widthMultiplier = width;

            var color = _pulseColor;
            color.a = alpha;
            ring.startColor = color;
            ring.endColor = color;

            for (int p = 0; p < CornerCount; p++)
            {
                Vector3 dir = _basePoints[p].normalized;
                float jitter = _cornerNoise[p] * noiseScale * (1f + i * 0.35f);
                ring.SetPosition(p, _basePoints[p] + dir * (expand + jitter));
            }
        }
    }

    private void ApplyHidden()
    {
        EnsureRings();
        for (int i = 0; i < _rings.Length; i++)
        {
            for (int p = 0; p < CornerCount; p++)
                _rings[i].SetPosition(p, _basePoints[p]);

            SetRingAlpha(_rings[i], 0f);
        }
    }

    private static void SetRingAlpha(LineRenderer ring, float alpha)
    {
        var start = ring.startColor;
        var end = ring.endColor;
        start.a = alpha;
        end.a = alpha;
        ring.startColor = start;
        ring.endColor = end;
    }

    private void EnsureRings()
    {
        var shader = Shader.Find("Sprites/Default");

        for (int i = 0; i < _rings.Length; i++)
        {
            if (_rings[i] != null) continue;

            var child = transform.Find($"ComboPulseRing_{i}");
            var go = child != null ? child.gameObject : new GameObject($"ComboPulseRing_{i}");
            go.transform.SetParent(transform, false);

            var line = go.GetComponent<LineRenderer>();
            if (line == null)
                line = go.AddComponent<LineRenderer>();

            if (line.sharedMaterial == null && shader != null)
            {
                _materials[i] = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                line.material = _materials[i];
            }

            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = CornerCount;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sortingOrder = 24 + i;
            line.widthMultiplier = baseLineWidth;

            _rings[i] = line;
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _materials.Length; i++)
        {
            if (_materials[i] != null)
                Destroy(_materials[i]);
        }
    }
}
