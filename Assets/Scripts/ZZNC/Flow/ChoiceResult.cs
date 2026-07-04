using System.Collections.Generic;

/// <summary>
/// Result of a three-choice step: displayed options and final selection.
/// </summary>
public readonly struct ChoiceResult
{
    public readonly IReadOnlyList<ChoiceOption> DisplayedOptions;
    public readonly ChoiceOption SelectedOption;
    public readonly int SelectedIndex;
    public readonly bool WasAutoSelected;

    public ChoiceResult(
        IReadOnlyList<ChoiceOption> displayedOptions,
        ChoiceOption selectedOption,
        int selectedIndex,
        bool wasAutoSelected)
    {
        DisplayedOptions = displayedOptions;
        SelectedOption = selectedOption;
        SelectedIndex = selectedIndex;
        WasAutoSelected = wasAutoSelected;
    }
}
