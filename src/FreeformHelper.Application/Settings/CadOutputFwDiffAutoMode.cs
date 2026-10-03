namespace FreeformHelper.Application.Settings;

/// <summary>
/// Defines how CAD pads auto-resolve CAD Output FW Diff when no manual override exists.
/// </summary>
public enum CadOutputFwDiffAutoMode
{
    /// <summary>
    /// Use direct best-match FW diff and keep per-IC CAD Output FW Diff unique.
    /// Conflicts or unmatched pads remain unresolved instead of shifting other pads.
    /// </summary>
    StrictUnique = 0,

    /// <summary>
    /// Use direct best-match FW diff for each CAD pad.
    /// Does not enforce per-IC uniqueness or row-sequence smoothing.
    /// </summary>
    BestMatchDirect = 1,
}
