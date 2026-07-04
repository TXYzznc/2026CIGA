using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 挂在 SmackButtonUI GameObject 上，提供：
///   1. 呼吸发光 —— 可交互时循环脉冲（颜色 + 缩放）
///   2. 按下爆闪 —— 白色覆盖 Image 快速闪入/出
///   3. 按下震动 —— RectTransform DOPunchPosition
///   4. 径向模糊 —— 触发挂在 Camera 上的 RadialBlurEffect
///
/// 依赖：DOTween（项目已引入）
/// 配置：将 Main Camera 上的 RadialBlurEffect 拖入 radialBlur 字段；
///       glowImage 可留空，脚本会自动创建一个白色底板作为发光层。
/// </summary>
public class SmackButtonPressEffect : MonoBehaviour
{
    // ── 呼吸发光 ────────────────────────────────────────────────────
    [Header("呼吸发光")]
    [Tooltip("留空则自动在按钮下创建一个覆盖 Image 作为发光层")]
    [SerializeField] private Image glowImage;
    [SerializeField] private Color glowColor      = new Color(0.45f, 0.82f, 1f, 0f);
    [SerializeField] private float glowMaxAlpha   = 0.65f;
    [SerializeField] private float breathPeriod   = 1.5f;   // 单次呼/吸耗时
    [SerializeField] private float breathScaleAmp = 0.06f;  // 缩放幅度 ±

    // ── 按下爆闪 ────────────────────────────────────────────────────
    [Header("按下爆闪")]
    [SerializeField] private float flashDuration  = 0.14f;
    [SerializeField] private float flashPeakAlpha = 0.9f;

    // ── 按下震动 ────────────────────────────────────────────────────
    [Header("按下震动")]
    [SerializeField] private float shakeDuration  = 0.22f;
    [SerializeField] private float shakePunch     = 14f;
    [SerializeField] private int   shakeVibrato   = 8;

    // ── 径向模糊 ────────────────────────────────────────────────────
    [Header("径向模糊（拖入 Main Camera 上的 RadialBlurEffect）")]
    [SerializeField] private RadialBlurEffect radialBlur;
    [SerializeField] private float            blurIntensity = 0.07f;
    [SerializeField] private float            blurDuration  = 0.20f;

    // ── 内部 ────────────────────────────────────────────────────────
    private Button    _button;
    private Image     _flashImage;
    private Sequence  _breathSeq;
    private Vector3   _baseScale;

    private void Awake()
    {
        _button = GetComponentInChildren<Button>(true);
        if (_button != null)
            _button.onClick.AddListener(OnSmackPressed);

        // 自动创建发光层
        if (glowImage == null)
            glowImage = CreateAutoImage("GlowLayer", glowColor, -1);

        // 初始化发光颜色
        if (glowImage != null)
        {
            var c = glowColor; c.a = 0f;
            glowImage.color = c;
            glowImage.raycastTarget = false;
        }

        // 自动创建爆闪层
        _flashImage = CreateAutoImage("FlashLayer", Color.white, 1);
        if (_flashImage != null)
        {
            _flashImage.color = new Color(1f, 1f, 1f, 0f);
            _flashImage.raycastTarget = false;
        }

        _baseScale = transform.localScale;
    }

    private void Start()
    {
        StartBreath();
    }

    // ── 外部接口 ─────────────────────────────────────────────────────

    /// <summary>结算期间由外部（HUDView / Controller）调用，暂停/恢复呼吸动画。</summary>
    public void SetInteractable(bool interactable)
    {
        if (interactable)
            StartBreath();
        else
            StopBreath();
    }

    // ── 按下响应 ─────────────────────────────────────────────────────

    private void OnSmackPressed()
    {
        PlayFlash();
        PlayShake();
        radialBlur?.TriggerBlur(blurIntensity, blurDuration);
    }

    // ── 呼吸动画 ─────────────────────────────────────────────────────

    private void StartBreath()
    {
        _breathSeq?.Kill(true);
        if (glowImage == null) return;

        // 确保从 alpha=0 开始
        var startColor = glowColor; startColor.a = 0f;
        glowImage.color = startColor;
        transform.localScale = _baseScale;

        _breathSeq = DOTween.Sequence().SetLoops(-1, LoopType.Yoyo).SetUpdate(false);

        // 发光 alpha 呼吸
        _breathSeq.Append(
            glowImage.DOFade(glowMaxAlpha, breathPeriod).SetEase(Ease.InOutSine));

        // 同步缩放脉冲
        _breathSeq.Join(
            transform.DOScale(_baseScale * (1f + breathScaleAmp), breathPeriod)
                     .SetEase(Ease.InOutSine));
    }

    private void StopBreath()
    {
        _breathSeq?.Kill(true);
        if (glowImage != null)
            glowImage.DOFade(0f, 0.2f);
        transform.DOScale(_baseScale, 0.15f);
    }

    // ── 爆闪 ─────────────────────────────────────────────────────────

    private void PlayFlash()
    {
        if (_flashImage == null) return;
        _flashImage.DOKill();
        _flashImage.color = new Color(1f, 1f, 1f, flashPeakAlpha);
        _flashImage.DOFade(0f, flashDuration).SetEase(Ease.OutQuad);
    }

    // ── 震动 ─────────────────────────────────────────────────────────

    private void PlayShake()
    {
        ((RectTransform)transform).DOKill();
        ((RectTransform)transform).DOPunchPosition(
            new Vector3(shakePunch, 0f, 0f), shakeDuration, shakeVibrato, 0.4f);
    }

    // ── 工具 ─────────────────────────────────────────────────────────

    /// <summary>在本 Transform 下创建一个全尺寸覆盖 Image。sibling 为 -1 时插到最后。</summary>
    private Image CreateAutoImage(string goName, Color color, int siblingIndex)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(transform, false);

        var rt = go.AddComponent<RectTransform>();
        // 拉伸铺满父级，并稍微向外扩展一圈作为发光边距
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-12f, -12f);
        rt.offsetMax = new Vector2(12f,  12f);

        var img = go.AddComponent<Image>();
        img.color = color;

        if (siblingIndex >= 0)
            go.transform.SetSiblingIndex(siblingIndex);
        else
            go.transform.SetAsFirstSibling();

        return img;
    }

    private void OnDestroy()
    {
        _breathSeq?.Kill();
        if (_button != null)
            _button.onClick.RemoveListener(OnSmackPressed);
    }
}
