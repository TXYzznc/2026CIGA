using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Three-choice service interface.
/// TriggerChoice opens/shows options; SubmitChoice is called by UI after player input.
/// </summary>
public interface IThreeChoiceService
{
    bool IsWaitingForInput { get; }
    IReadOnlyList<ChoiceOption> CurrentOptions { get; }

    IEnumerator TriggerChoice(ChoiceRequest request, Action<ChoiceResult> onCompleted);
    void SubmitChoice(int optionIndex);
    void CancelChoice();
}
