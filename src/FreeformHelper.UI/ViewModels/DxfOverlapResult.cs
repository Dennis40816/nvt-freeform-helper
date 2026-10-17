using System.Collections.ObjectModel;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Holds the result of one DXF overlap check: the summary text, the issue messages and the CAD pads
/// that are highlighted. The view model replaces the whole value when the check runs, when it is
/// cleared, and when the highlights are cleared or restored.
/// </summary>
/// <param name="Summary">The summary text for the check.</param>
/// <param name="Issues">The detailed issue messages of the latest check.</param>
/// <param name="HighlightedCadIds">The CAD pad IDs that the canvas highlights now.</param>
/// <param name="LastCadIds">
/// The CAD pad IDs of the latest check. They stay when the highlights are cleared, so the pads
/// can be highlighted and selected again.
/// </param>
public sealed record DxfOverlapResult(
    string Summary,
    ObservableCollection<string> Issues,
    ObservableCollection<int> HighlightedCadIds,
    IReadOnlyList<int> LastCadIds)
{
    /// <summary>
    /// The summary text before any check ran.
    /// </summary>
    public const string NotRunSummary = "DXF check: not run.";

    /// <summary>
    /// Creates the result before any check ran. Each call returns new collections.
    /// </summary>
    public static DxfOverlapResult CreateNotRun()
    {
        return new DxfOverlapResult(
            NotRunSummary,
            new ObservableCollection<string>(),
            new ObservableCollection<int>(),
            Array.Empty<int>());
    }
}
