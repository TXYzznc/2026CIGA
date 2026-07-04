using System;
using System.Collections.Generic;

/// <summary>
/// Request for a three-choice step.
/// Flow provides a pool; service displays displayCount options and returns the selection.
/// </summary>
public readonly struct ChoiceRequest
{
    public readonly string Title;
    public readonly string Message;
    public readonly IReadOnlyList<ChoiceOption> CandidatePool;
    public readonly int DisplayCount;
    public readonly bool UseWeights;

    public ChoiceRequest(
        string title,
        string message,
        IReadOnlyList<ChoiceOption> candidatePool,
        int displayCount = 3,
        bool useWeights = true)
    {
        Title = title;
        Message = message;
        CandidatePool = candidatePool ?? Array.Empty<ChoiceOption>();
        DisplayCount = displayCount;
        UseWeights = useWeights;
    }
}
