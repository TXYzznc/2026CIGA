using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Minimal settlement view: text display, button events, and visibility only.
/// </summary>
public class SettlementView : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text detailText;

    [Header("Buttons")]
    [SerializeField] private Button nextButton;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button mainMenuButton;

    [Header("Events")]
    [SerializeField] private UnityEvent onNextClicked;
    [SerializeField] private UnityEvent onRetryClicked;
    [SerializeField] private UnityEvent onMainMenuClicked;

    public event Action OnNextClicked;
    public event Action OnRetryClicked;
    public event Action OnMainMenuClicked;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        nextButton?.onClick.AddListener(ClickNext);
        retryButton?.onClick.AddListener(ClickRetry);
        mainMenuButton?.onClick.AddListener(ClickMainMenu);
    }

    public void Show(string title, int score, string detail = "")
    {
        if (titleText != null)
            titleText.text = title;

        if (scoreText != null)
            scoreText.text = score.ToString();

        if (detailText != null)
            detailText.text = detail;

        SetVisible(true);
    }

    public void Hide()
    {
        SetVisible(false);
    }

    public void SetNextInteractable(bool interactable)
    {
        if (nextButton != null)
            nextButton.interactable = interactable;
    }

    public void SetButtonVisibility(bool showNext, bool showRetry, bool showMainMenu)
    {
        if (nextButton != null)
            nextButton.gameObject.SetActive(showNext);
        if (retryButton != null)
            retryButton.gameObject.SetActive(showRetry);
        if (mainMenuButton != null)
            mainMenuButton.gameObject.SetActive(showMainMenu);
    }

    public void ClickNext()
    {
        onNextClicked?.Invoke();
        OnNextClicked?.Invoke();
    }

    public void ClickRetry()
    {
        onRetryClicked?.Invoke();
        OnRetryClicked?.Invoke();
    }

    public void ClickMainMenu()
    {
        onMainMenuClicked?.Invoke();
        OnMainMenuClicked?.Invoke();
    }

    private void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);

        if (canvasGroup == null)
            return;

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    private void OnDestroy()
    {
        nextButton?.onClick.RemoveListener(ClickNext);
        retryButton?.onClick.RemoveListener(ClickRetry);
        mainMenuButton?.onClick.RemoveListener(ClickMainMenu);
    }
}
