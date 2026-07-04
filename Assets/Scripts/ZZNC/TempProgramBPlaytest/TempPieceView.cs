using System.Collections;
using UnityEngine;

/// <summary>
/// Temporary visual adapter for Program-B playtest replacement.
/// Delete the whole TempProgramBPlaytest folder when the real Program-B view layer is ready.
/// </summary>
public class TempPieceView : MonoBehaviour, IPieceView
{
    private const float MoveDuration = 0.18f;
    private const float FxDuration = 0.14f;
    private SpriteRenderer _renderer;
    private Coroutine _motion;
    private Vector3 _baseScale;

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
        var start = transform.position;
        for (var t = 0f; t < MoveDuration; t += Time.deltaTime)
        {
            var k = Mathf.SmoothStep(0f, 1f, t / MoveDuration);
            transform.position = Vector3.Lerp(start, target, k);
            yield return null;
        }

        transform.position = target;
    }

    private IEnumerator AnimateShake()
    {
        var start = transform.localPosition;
        for (var t = 0f; t < FxDuration; t += Time.deltaTime)
        {
            var phase = Mathf.Sin(t * 90f) * 0.08f * (1f - t / FxDuration);
            transform.localPosition = start + new Vector3(phase, 0f, 0f);
            yield return null;
        }

        transform.localPosition = start;
    }

    private IEnumerator AnimatePulse()
    {
        for (var t = 0f; t < FxDuration; t += Time.deltaTime)
        {
            var k = Mathf.Sin(t / FxDuration * Mathf.PI);
            transform.localScale = _baseScale * (1f + k * 0.22f);
            yield return null;
        }

        transform.localScale = _baseScale;
    }

    private IEnumerator AnimateSpawn()
    {
        for (var t = 0f; t < FxDuration; t += Time.deltaTime)
        {
            var k = Mathf.SmoothStep(0f, 1f, t / FxDuration);
            transform.localScale = _baseScale * k;
            yield return null;
        }

        transform.localScale = _baseScale;
    }

    private IEnumerator AnimateRemove()
    {
        var startColor = _renderer != null ? _renderer.color : Color.white;
        for (var t = 0f; t < FxDuration; t += Time.deltaTime)
        {
            var k = 1f - Mathf.SmoothStep(0f, 1f, t / FxDuration);
            transform.localScale = _baseScale * k;
            if (_renderer != null)
            {
                _renderer.color = new Color(startColor.r, startColor.g, startColor.b, startColor.a * k);
            }
            yield return null;
        }

        if (_renderer != null)
        {
            _renderer.color = Color.clear;
        }
    }
}
