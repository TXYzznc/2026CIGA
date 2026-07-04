using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Minimal main menu view. Put it on the menu root and wire buttons in Inspector.
/// </summary>
public class MainMenuView : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    [Header("Events")]
    [SerializeField] private UnityEvent onStartClicked;
    [SerializeField] private UnityEvent onContinueClicked;
    [SerializeField] private UnityEvent onSettingsClicked;
    [SerializeField] private UnityEvent onQuitClicked;

    public event Action OnStartClicked;
    public event Action OnContinueClicked;
    public event Action OnSettingsClicked;
    public event Action OnQuitClicked;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        startButton?.onClick.AddListener(ClickStart);
        continueButton?.onClick.AddListener(ClickContinue);
        settingsButton?.onClick.AddListener(ClickSettings);
        quitButton?.onClick.AddListener(ClickQuit);
    }

    public void Show()
    {
        SetVisible(true);
    }

    public void Hide()
    {
        SetVisible(false);
    }

    public void SetContinueInteractable(bool interactable)
    {
        if (continueButton != null)
            continueButton.interactable = interactable;
    }

    public void ClickStart()
    {
        onStartClicked?.Invoke();
        OnStartClicked?.Invoke();
    }

    public void ClickContinue()
    {
        onContinueClicked?.Invoke();
        OnContinueClicked?.Invoke();
    }

    public void ClickSettings()
    {
        onSettingsClicked?.Invoke();
        OnSettingsClicked?.Invoke();
    }

    public void ClickQuit()
    {
        onQuitClicked?.Invoke();
        OnQuitClicked?.Invoke();
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
        startButton?.onClick.RemoveListener(ClickStart);
        continueButton?.onClick.RemoveListener(ClickContinue);
        settingsButton?.onClick.RemoveListener(ClickSettings);
        quitButton?.onClick.RemoveListener(ClickQuit);
    }
}
