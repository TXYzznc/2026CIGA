using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

/// <summary>
/// Scene-backed three-choice popup. Put this under the modal canvas and wire three option buttons.
/// </summary>
public class ThreeChoiceView : MonoBehaviour, IThreeChoiceService
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button[] optionButtons = Array.Empty<Button>();
    [SerializeField] private TMP_Text[] optionTitleTexts = Array.Empty<TMP_Text>();
    [SerializeField] private TMP_Text[] optionDescriptionTexts = Array.Empty<TMP_Text>();
    [SerializeField] private Image[] optionIconImages = Array.Empty<Image>();
    [SerializeField] private bool closeOnCancel;

    private readonly List<ChoiceOption> _currentOptions = new List<ChoiceOption>(3);
    private bool _isWaitingForInput;
    private bool _hasSubmittedChoice;
    private int _submittedIndex = -1;
    private bool _isInitialized;

    public bool IsWaitingForInput => _isWaitingForInput;
    public IReadOnlyList<ChoiceOption> CurrentOptions => _currentOptions;

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (_isInitialized)
            return;

        _isInitialized = true;

        for (var i = 0; i < optionButtons.Length; i++)
        {
            var index = i;
            if (optionButtons[i] != null)
                optionButtons[i].onClick.AddListener(() => SubmitChoice(index));
        }
    }

    public IEnumerator TriggerChoice(ChoiceRequest request, Action<ChoiceResult> onCompleted)
    {
        Initialize();

        if (_isWaitingForInput)
        {
            Debug.LogWarning("[ZZNC.Flow] Three choice request ignored because another choice is active.");
            yield break;
        }

        _currentOptions.Clear();
        PickOptions(request, _currentOptions);

        _isWaitingForInput = true;
        _hasSubmittedChoice = false;
        _submittedIndex = -1;

        Show(request);
        Debug.Log($"[ZZNC.Flow] Scene choice opened: {request.Title}, options={_currentOptions.Count}");

        while (_isWaitingForInput && !_hasSubmittedChoice)
            yield return null;

        var selectedIndex = Mathf.Clamp(_submittedIndex, 0, Mathf.Max(0, _currentOptions.Count - 1));
        var selectedOption = _currentOptions.Count > 0 ? _currentOptions[selectedIndex] : null;
        var result = new ChoiceResult(_currentOptions.ToArray(), selectedOption, selectedIndex, false);

        _isWaitingForInput = false;
        _hasSubmittedChoice = false;
        _submittedIndex = -1;
        Hide();

        onCompleted?.Invoke(result);
    }

    public void SubmitChoice(int optionIndex)
    {
        if (!_isWaitingForInput)
            return;

        _submittedIndex = Mathf.Clamp(optionIndex, 0, Mathf.Max(0, _currentOptions.Count - 1));
        _hasSubmittedChoice = true;
    }

    public void CancelChoice()
    {
        if (!_isWaitingForInput)
            return;

        if (closeOnCancel)
            SubmitChoice(0);
    }

    public void Show(ChoiceRequest request)
    {
        if (titleText != null)
            titleText.text = string.IsNullOrEmpty(request.Title) ? "Choose a Piece" : request.Title;

        if (messageText != null)
            messageText.text = request.Message ?? string.Empty;

        for (var i = 0; i < optionButtons.Length; i++)
        {
            var hasOption = i < _currentOptions.Count && _currentOptions[i] != null;
            var option = hasOption ? _currentOptions[i] : null;

            if (optionButtons[i] != null)
            {
                optionButtons[i].gameObject.SetActive(hasOption);
                optionButtons[i].interactable = hasOption;
            }

            if (i < optionTitleTexts.Length && optionTitleTexts[i] != null)
                optionTitleTexts[i].text = option?.Title ?? string.Empty;

            if (i < optionDescriptionTexts.Length && optionDescriptionTexts[i] != null)
                optionDescriptionTexts[i].text = option?.Description ?? string.Empty;

            if (i < optionIconImages.Length && optionIconImages[i] != null)
            {
                optionIconImages[i].sprite = option?.Icon;
                optionIconImages[i].enabled = option?.Icon != null;
            }
        }

        SetVisible(true);
    }

    public void Hide()
    {
        SetVisible(false);
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

    private static void PickOptions(ChoiceRequest request, List<ChoiceOption> results)
    {
        var pool = request.CandidatePool;
        var displayCount = Mathf.Max(1, request.DisplayCount);

        if (pool == null || pool.Count == 0)
            return;

        var remaining = new List<ChoiceOption>(pool);
        while (results.Count < displayCount && remaining.Count > 0)
        {
            var index = request.UseWeights ? PickWeightedIndex(remaining) : Random.Range(0, remaining.Count);
            results.Add(remaining[index]);
            remaining.RemoveAt(index);
        }

        while (results.Count < displayCount && results.Count > 0)
            results.Add(results[Random.Range(0, results.Count)]);
    }

    private static int PickWeightedIndex(IReadOnlyList<ChoiceOption> options)
    {
        var totalWeight = 0f;
        for (var i = 0; i < options.Count; i++)
            totalWeight += Mathf.Max(0f, options[i]?.Weight ?? 0f);

        if (totalWeight <= 0f)
            return Random.Range(0, options.Count);

        var roll = Random.value * totalWeight;
        for (var i = 0; i < options.Count; i++)
        {
            roll -= Mathf.Max(0f, options[i]?.Weight ?? 0f);
            if (roll <= 0f)
                return i;
        }

        return options.Count - 1;
    }

    private void OnDestroy()
    {
        for (var i = 0; i < optionButtons.Length; i++)
            if (optionButtons[i] != null)
                optionButtons[i].onClick.RemoveAllListeners();
    }
}
