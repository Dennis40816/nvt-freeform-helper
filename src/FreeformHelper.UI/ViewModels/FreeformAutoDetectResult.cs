using System.Collections.ObjectModel;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Holds the latest freeform auto-detect statistics: the summary text and the table rows.
/// The view model replaces the whole value when the statistics are refreshed or reset.
/// </summary>
/// <param name="Summary">The summary text of the statistics.</param>
/// <param name="Rows">The rows of the statistics table.</param>
public sealed record FreeformAutoDetectResult(
    string Summary,
    ObservableCollection<FreeformAutoDetectStatRow> Rows)
{
    /// <summary>
    /// The summary text before any statistics were built.
    /// </summary>
    public const string NotRunSummary = "Freeform stats: not run.";

    /// <summary>
    /// Gets a value indicating whether the table has rows.
    /// </summary>
    public bool HasRows => Rows.Count > 0;

    /// <summary>
    /// Creates the result before any statistics were built. Each call returns a new row collection.
    /// </summary>
    public static FreeformAutoDetectResult CreateNotRun()
    {
        return new FreeformAutoDetectResult(NotRunSummary, new ObservableCollection<FreeformAutoDetectStatRow>());
    }
}
