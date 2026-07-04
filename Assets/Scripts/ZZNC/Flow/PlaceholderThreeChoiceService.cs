using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Placeholder three-choice service.
/// Defaults to auto submit; turn autoSubmit off when a real popup calls SubmitChoice.
/// </summary>
public class PlaceholderThreeChoiceService : MonoBehaviour, IThreeChoiceService
{
    [Header("Placeholder")]
    [SerializeField] private bool autoSubmit = true;
    [SerializeField, Min(0f)] private float autoSubmitDelay = 0.15f;
    [SerializeField] private bool autoSelectByWeight = false;

    private readonly List<ChoiceOption> _currentOptions = new List<ChoiceOption>(3);
    private bool _isWaitingForInput;
    private bool _hasSubmittedChoice;
    private bool _submittedByAuto;
    private int _submittedIndex = -1;

    public bool IsWaitingForInput => _isWaitingForInput;
    public IReadOnlyList<ChoiceOption> CurrentOptions => _currentOptions;

    public IEnumerator TriggerChoice(ChoiceRequest request, Action<ChoiceResult> onCompleted)
    {
        if (_isWaitingForInput)
        {
            Debug.LogWarning("[ZZNC.Flow] Three choice request ignored because another choice is active.");
            yield break;
        }

        _currentOptions.Clear();
        PickOptions(request, _currentOptions);

        _isWaitingForInput = true;
        _hasSubmittedChoice = false;
        _submittedByAuto = false;
        _submittedIndex = -1;

        Debug.Log($"[ZZNC.Flow] Choice opened: {request.Title}, options={_currentOptions.Count}");

        if (autoSubmit)
        {
            if (autoSubmitDelay > 0f)
                yield return new WaitForSeconds(autoSubmitDelay);

            SubmitAutoChoice();
        }

        while (_isWaitingForInput && !_hasSubmittedChoice)
            yield return null;

        var selectedIndex = Mathf.Clamp(_submittedIndex, 0, Mathf.Max(0, _currentOptions.Count - 1));
        var selectedOption = _currentOptions.Count > 0 ? _currentOptions[selectedIndex] : null;
        var result = new ChoiceResult(_currentOptions.ToArray(), selectedOption, selectedIndex, _submittedByAuto);

        _isWaitingForInput = false;
        _hasSubmittedChoice = false;
        _submittedByAuto = false;
        _submittedIndex = -1;

        onCompleted?.Invoke(result);
    }

    public void SubmitChoice(int optionIndex)
    {
        if (!_isWaitingForInput)
            return;

        if (_currentOptions.Count == 0)
        {
            _submittedIndex = -1;
            _hasSubmittedChoice = true;
            return;
        }

        _submittedIndex = Mathf.Clamp(optionIndex, 0, _currentOptions.Count - 1);
        _hasSubmittedChoice = true;
    }

    public void CancelChoice()
    {
        if (!_isWaitingForInput)
            return;

        SubmitChoice(0);
    }

    private void SubmitAutoChoice()
    {
        _submittedByAuto = true;
        SubmitChoice(PickAutoSelectedIndex());
    }

    private void PickOptions(ChoiceRequest request, List<ChoiceOption> results)
    {
        var pool = request.CandidatePool;
        var displayCount = Mathf.Max(1, request.DisplayCount);

        if (pool == null || pool.Count == 0)
        {
            for (var i = 0; i < displayCount; i++)
                results.Add(new ChoiceOption($"placeholder_{i + 1}", $"Option {i + 1}", "Placeholder choice option", 1f));
            return;
        }

        var remaining = new List<ChoiceOption>(pool);
        while (results.Count < displayCount && remaining.Count > 0)
        {
            var index = request.UseWeights ? PickWeightedIndex(remaining) : Random.Range(0, remaining.Count);
            results.Add(remaining[index]);
            remaining.RemoveAt(index);
        }

        while (results.Count < displayCount)
            results.Add(results[Random.Range(0, results.Count)]);
    }

    private int PickAutoSelectedIndex()
    {
        if (_currentOptions.Count <= 1)
            return 0;

        return autoSelectByWeight ? PickWeightedIndex(_currentOptions) : Random.Range(0, _currentOptions.Count);
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
}
