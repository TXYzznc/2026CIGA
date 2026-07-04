using DG.Tweening;
using UnityEngine;

/// <summary>
/// 挂在 Main Camera 上，提供内置渲染管线色差后处理脉冲效果。
/// 需要在 Inspector 中将 ZZNC_ChromaticAberration.shader 拖入 aberrationShader 槽，
/// 或者把该 shader 加入 Project Settings → Graphics → Always Included Shaders。
/// </summary>
[RequireComponent(typeof(Camera))]
[ImageEffectAllowedInSceneView]
public class ChromaticAberrationEffect : MonoBehaviour
{
    [SerializeField] private Shader aberrationShader;
    [SerializeField, Range(0f, 0.1f)]  private float peakIntensity = 0.035f;
    [SerializeField, Range(0.1f, 1f)]  private float duration      = 0.4f;

    private Material _mat;
    private float    _intensity;
    private Tweener  _tween;

    private void Awake()
    {
        if (aberrationShader == null)
            aberrationShader = Shader.Find("ZZNC/ChromaticAberration");

        if (aberrationShader != null)
            _mat = new Material(aberrationShader) { hideFlags = HideFlags.HideAndDontSave };
    }

    private void OnDestroy()
    {
        if (_mat != null)
            DestroyImmediate(_mat);
    }

    [ImageEffectOpaque]
    private void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (!_mat || _intensity < 0.0001f)   // !_mat 能正确检测 Unity 已销毁对象
        {
            Graphics.Blit(src, dst);
            return;
        }
        _mat.SetFloat("_Intensity", _intensity);
        Graphics.Blit(src, dst, _mat);
    }

    /// <summary>触发一次色差脉冲：快速升到峰值，再缓慢归零。</summary>
    public void Pulse()
    {
        if (!_mat) return;

        _tween?.Kill();
        _intensity = 0f;

        float rise = duration * 0.15f;
        float fall = duration * 0.85f;

        _tween = DOTween.To(() => _intensity, v => _intensity = v, peakIntensity, rise)
            .SetEase(Ease.OutCubic)
            .OnComplete(() =>
                _tween = DOTween.To(() => _intensity, v => _intensity = v, 0f, fall)
                    .SetEase(Ease.InCubic));
    }
}
