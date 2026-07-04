using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 局内 HUD：分数面板显示。
/// 挂在 HUD 节点，拖入两个 TMP Text 引用（当前分数 / 目标分数）。
/// </summary>
public class HUDView : MonoBehaviour
{
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text targetScoreText;

    [Header("拍击按钮")]
    [SerializeField] private Button smackButton;

    /// <summary>GamePlay 程序注册此事件以响应拍击按钮点击。</summary>
    public event Action OnSmackClicked;

    [Header("动效参数 - 滚动")]
    [SerializeField] private float rollDuration = 0.4f;

    [Header("动效参数 - 弹跳（log2 映射，每×2 均匀递增）")]
    [SerializeField] private int minDelta = 2;
    [SerializeField] private int maxDelta = 512;
    [SerializeField] private float minPunch = 0.1f;
    [SerializeField] private float maxPunch = 1.0f;
    [SerializeField] private float minPunchDuration = 0.24f;
    [SerializeField] private float maxPunchDuration = 0.80f;

    private int _displayedScore;
    private Tweener _rollTween;

    private void Awake()
    {
        _displayedScore = 0;
        if (smackButton != null)
            smackButton.onClick.AddListener(() => OnSmackClicked?.Invoke());
    }

    /// <summary>设置拍击按钮是否可交互（结算中由 GamePlay 置灰）。</summary>
    public void SetSmackButtonInteractable(bool interactable)
    {
        if (smackButton != null)
            smackButton.interactable = interactable;
    }

    /// <summary>更新当前分数，带滚动+弹跳动效。</summary>
    public void SetScore(int score)
    {
        int delta = Mathf.Abs(score - _displayedScore);
        float punch = CalcPunch(delta);

        _rollTween?.Kill();

        int from = _displayedScore;
        _rollTween = DOTween.To(
            () => from,
            v =>
            {
                from = v;
                _displayedScore = v;
                if (scoreText != null)
                    scoreText.text = NumText.ToSpriteTags(v);
            },
            score,
            rollDuration
        ).SetEase(Ease.OutCubic);

        if (scoreText != null && punch > 0f)
            scoreText.transform.DOPunchScale(Vector3.one * punch, CalcPunchDuration(delta), 1, 0.5f);
    }

    private float Log2T(int delta)
    {
        if (delta <= 0 || minDelta <= 0 || maxDelta <= minDelta) return 0f;
        float logMin = Mathf.Log(minDelta, 2f);
        float logMax = Mathf.Log(maxDelta, 2f);
        return Mathf.Clamp01((Mathf.Log(delta, 2f) - logMin) / (logMax - logMin));
    }

    private float CalcPunch(int delta) =>
        Mathf.Lerp(minPunch, maxPunch, Log2T(delta));

    private float CalcPunchDuration(int delta) =>
        Mathf.Lerp(minPunchDuration, maxPunchDuration, Log2T(delta));

    /// <summary>设置目标分数（静态显示，无动效）。</summary>
    public void SetTargetScore(int target)
    {
        if (targetScoreText != null)
            targetScoreText.text = "/  " + NumText.ToSpriteTags(target);
    }

    /// <summary>在当前显示分数基础上增加 delta，带动效。</summary>
    public void AddScore(int delta)
    {
        SetScore(_displayedScore + delta);
    }

    /// <summary>弹的次数越多震得越猛（小丑牌风格），抖完恢复原始大小。</summary>
    public void ShakeScore(float intensity)
    {
        if (scoreText != null)
        {
            scoreText.transform.DOKill();
            scoreText.transform.localScale = Vector3.one;
            scoreText.transform.DOPunchScale(Vector3.one * intensity, 0.3f, 3, 0.5f);
        }
    }

    /// <summary>不带动效直接刷新（初始化场景时用）。</summary>
    public void SetScoreImmediate(int score)
    {
        _rollTween?.Kill();
        _displayedScore = score;
        if (scoreText != null)
            scoreText.text = NumText.ToSpriteTags(score);
    }

    private void OnDestroy()
    {
        _rollTween?.Kill();
        if (smackButton != null)
            smackButton.onClick.RemoveAllListeners();
    }
}
