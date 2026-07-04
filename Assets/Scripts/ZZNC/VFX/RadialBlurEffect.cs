using System.Collections;
using UnityEngine;

/// <summary>
/// 挂在 Main Camera 上。提供一次性径向模糊脉冲效果。
/// TriggerBlur() 触发后自动完整淡出，无需外部管理。
/// 要求 Built-in RP（非 URP），依赖 OnRenderImage。
/// </summary>
[RequireComponent(typeof(Camera))]
public class RadialBlurEffect : MonoBehaviour
{
    [SerializeField] private Shader blurShader;
    [SerializeField, Range(4, 32)] private int samples = 12;

    private Material _mat;
    private float    _intensity;
    private Coroutine _routine;

    private void Awake()
    {
        if (blurShader == null)
            blurShader = Shader.Find("ZZNC/RadialBlur");

        if (blurShader != null)
            _mat = new Material(blurShader) { hideFlags = HideFlags.HideAndDontSave };
        else
            Debug.LogWarning("[RadialBlurEffect] 找不到 ZZNC/RadialBlur shader，请检查路径。");
    }

    private void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (_mat == null || _intensity < 0.001f)
        {
            Graphics.Blit(src, dst);
            return;
        }
        _mat.SetFloat("_Intensity", _intensity);
        _mat.SetFloat("_Samples",   samples);
        _mat.SetFloat("_CenterX",   0.5f);
        _mat.SetFloat("_CenterY",   0.5f);
        Graphics.Blit(src, dst, _mat);
    }

    /// <summary>触发一次径向模糊脉冲：快速升至峰值，然后在 duration 内淡出。</summary>
    public void TriggerBlur(float peakIntensity = 0.07f, float duration = 0.22f)
    {
        if (_mat == null) return;
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(BlurRoutine(peakIntensity, duration));
    }

    private IEnumerator BlurRoutine(float peak, float duration)
    {
        // 立即设为峰值（一帧感）
        _intensity = peak;
        yield return null;

        // 剩余时间线性淡出
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed    += Time.unscaledDeltaTime;
            _intensity  = Mathf.Lerp(peak, 0f, elapsed / duration);
            yield return null;
        }
        _intensity = 0f;
    }

    private void OnDestroy()
    {
        if (_mat != null) Destroy(_mat);
    }
}
