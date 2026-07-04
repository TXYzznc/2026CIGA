using DG.Tweening;
using UnityEngine;

/// <summary>
/// 挂在 Main Camera 上。调用 Shake() 触发屏幕震动。
/// 依赖 DOTween。
/// </summary>
[RequireComponent(typeof(Camera))]
public class ScreenShakeEffect : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)]  private float strength   = 0.18f;
    [SerializeField, Range(0.05f, 1f)] private float duration = 0.35f;
    [SerializeField, Range(5, 50)]   private int   vibrato    = 22;
    [SerializeField, Range(0f, 90f)] private float randomness = 60f;

    private Vector3 _restPosition;

    private void Awake()
    {
        _restPosition = transform.localPosition;
    }

    public void Shake()
    {
        // 先 Kill 残留 tween，并归位，防止连续调用时基点累积漂移
        transform.DOKill(complete: false);
        transform.localPosition = _restPosition;

        transform.DOShakePosition(
            duration,
            new Vector3(strength, strength, 0f),
            vibrato,
            randomness,
            snapping: false,
            fadeOut: true);
    }
}
