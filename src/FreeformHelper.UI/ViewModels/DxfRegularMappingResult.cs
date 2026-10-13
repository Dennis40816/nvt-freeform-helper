using System.Collections.ObjectModel;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Holds the result of one DXF and regular pad mapping diagnostics run. The view model replaces
/// the whole value when the diagnostics start, finish or are cleared.
/// </summary>
/// <param name="Summary">The summary text of the diagnostics.</param>
/// <param name="Issues">The detailed issue messages of the latest run.</param>
/// <param name="HasIssues">
/// Whether the run found issues. A mask change or removal counts as an issue, so this is not
/// only the length of <paramref name="Issues"/>.
/// </param>
public sealed record DxfRegularMappingResult(
    string Summary,
    ObservableCollection<string> Issues,
    bool HasIssues)
{
    /// <summary>
    /// The summary text before any run.
    /// </summary>
    public const string NotRunSummary = "Diagnostics: not run.";

    /// <summary>
    /// Creates the result before any run. Each call returns a new issue collection.
    /// </summary>
    public static DxfRegularMappingResult CreateNotRun()
    {
        return new DxfRegularMappingResult(NotRunSummary, new ObservableCollection<string>(), HasIssues: false);
    }
}
