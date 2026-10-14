using FreeformHelper.Application.Settings;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Holds the CAD Output FW Diff assignment settings of the main view model: the start index, the auto
/// assignment mode, the anchor CAD id, the policy summary text and the overlay switch. The view model
/// replaces the whole value when one of them changes and forwards one property per member so the
/// bindings keep their names.
/// </summary>
/// <param name="CadOutputFwDiffIndexStart">The starting expected diff idx value for CAD scan-order indexing.</param>
/// <param name="CadOutputFwDiffAutoMode">The CAD auto CAD Output FW Diff assignment mode.</param>
/// <param name="CadOutputFwDiffIndexAnchorCadId">The CAD id used as diff idx start anchor. Use -1 to indicate "first CAD in scan order".</param>
/// <param name="CadOutputFwDiffAssignmentSummary">The summary text for current CAD Output FW Diff assignment policy.</param>
/// <param name="ShowDiffIndexOverlay">Whether to display CAD Output FW Diff overlay hints on canvas.</param>
public sealed record FwDiffAssignmentSettings(
    decimal CadOutputFwDiffIndexStart,
    CadOutputFwDiffAutoMode CadOutputFwDiffAutoMode,
    decimal CadOutputFwDiffIndexAnchorCadId,
    string CadOutputFwDiffAssignmentSummary,
    bool ShowDiffIndexOverlay)
{
    /// <summary>
    /// Gets the value of a new view model before any project settings are loaded.
    /// </summary>
    public static FwDiffAssignmentSettings Initial { get; } = new(
        0m,
        CadOutputFwDiffAutoMode.BestMatchDirect,
        -1m,
        "CAD Output FW Diff assignment (per-IC): auto, scan-order based.",
        true);
}
