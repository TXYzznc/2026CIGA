using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// 局内 HUD：分数面板显示。
/// 挂在 HUD 节点，拖入两个 TMP Text 引用（当前分数 / 目标分数）。
/// </summary>
public class HUDView : MonoBehaviour
{
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text targetScoreText;

    [Header("动效参数")]
    [SerializeField] private float rollDuration = 0.4f;
    [SerializeField] private float punchScale = 0.3f;
    [SerializeField] private float punchDuration = 0.2f;

    private int _displayedScore;
    private Tweener _rollTween;

    private void Awake()
    {
        _displayedScore = 0;
    }

    /// <summary>更新当前分数，带滚动+弹跳动效。</summary>
    public void SetScore(int score)
    {
        // 终止上一次未完成的滚动
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

        // Scale punch 立即触发，不等滚动完
        if (scoreText != null)
            scoreText.transform.DOPunchScale(Vector3.one * punchScale, punchDuration, 1, 0.5f);
    }

    /// <summary>设置目标分数（静态显示，无动效）。</summary>
    public void SetTargetScore(int target)
    {
        if (targetScoreText != null)
            targetScoreText.text = NumText.ToSpriteTags(target);
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
    }
}
