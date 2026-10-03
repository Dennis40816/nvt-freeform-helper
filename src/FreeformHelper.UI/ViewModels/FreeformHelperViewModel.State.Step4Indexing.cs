using CommunityToolkit.Mvvm.ComponentModel;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    /// <summary>
    /// Gets or sets the shared diff-index shift delta applied to all selected CAD pads in Step 4.
    /// </summary>
    [ObservableProperty] private decimal _cadOutputFwDiffIndexShiftDelta = -1m;

    /// <summary>
    /// Gets or sets the current duplicate diff summary for Step 4.
    /// </summary>
    [ObservableProperty] private string _step4DuplicateDiffSummary = "Duplicate CAD Output FW Diff: none.";

    /// <summary>
    /// Gets or sets a value indicating whether duplicate diff groups are currently present in Step 4.
    /// </summary>
    [ObservableProperty] private bool _hasStep4DuplicateDiffGroups;
}
