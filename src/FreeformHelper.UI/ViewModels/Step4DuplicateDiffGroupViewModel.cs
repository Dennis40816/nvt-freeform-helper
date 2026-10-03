namespace FreeformHelper.UI.ViewModels;

public sealed record Step4DuplicateDiffGroupViewModel(
    int IcIndex,
    int DiffIndex,
    IReadOnlyList<int> CadPadIds,
    string TitleText,
    string CadSummaryText,
    string SuggestionText);
