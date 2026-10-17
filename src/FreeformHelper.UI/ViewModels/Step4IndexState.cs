using CommunityToolkit.Mvvm.ComponentModel;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Holds the Step 4 diff-index values that the Step 4 view binds to, so that the main view model keeps no field for them.
/// </summary>
public sealed partial class Step4IndexState : ObservableObject
{
    private readonly Action _shiftDeltaChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="Step4IndexState"/> class.
    /// </summary>
    /// <param name="shiftDeltaChanged">The action that the main view model runs after the shift delta changes.</param>
    public Step4IndexState(Action shiftDeltaChanged)
    {
        _shiftDeltaChanged = shiftDeltaChanged;
    }

    /// <summary>
    /// Gets or sets the shared diff-index shift delta applied to all selected CAD pads in Step 4.
    /// </summary>
    [ObservableProperty] private decimal _shiftDelta = -1m;

    /// <summary>
    /// Gets or sets the current duplicate diff summary for Step 4.
    /// </summary>
    [ObservableProperty] private string _duplicateDiffSummary = "Duplicate CAD Output FW Diff: none.";

    partial void OnShiftDeltaChanged(decimal value) => _shiftDeltaChanged();
}
