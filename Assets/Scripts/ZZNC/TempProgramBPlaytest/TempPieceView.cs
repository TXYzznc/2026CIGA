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

    public void Init(Sprite sprite, Material material, int sortingOrder)
    {
        _renderer = gameObject.GetComponent<SpriteRenderer>();
        if (_renderer == null)
        {
            _renderer = gameObject.AddComponent<SpriteRenderer>();
        }

        _renderer.sprite = sprite;
        _renderer.sharedMaterial = material;
        _renderer.sortingOrder = sortingOrder;
        _baseScale = transform.localScale;
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
        float dur = MoveDuration / GlobalSpeedScale;
        var start = transform.position;
        for (var t = 0f; t < dur; t += Time.deltaTime)
        {
            var k = Mathf.SmoothStep(0f, 1f, t / dur);
            transform.position = Vector3.Lerp(start, target, k);
            yield return null;
        }
        transform.position = target;
    }

    private IEnumerator AnimateShake()
    {
        float dur = FxDuration / GlobalSpeedScale;
        var start = transform.localPosition;
        float amp = GlobalAmplitudeScale * GlobalAmplitudeScale;
        for (var t = 0f; t < dur; t += Time.deltaTime)
        {
            var phase = Mathf.Sin(t * 90f) * 0.08f * amp * (1f - t / dur);
            transform.localPosition = start + new Vector3(phase, 0f, 0f);
            yield return null;
        }
        transform.localPosition = start;
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
