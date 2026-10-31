using CommunityToolkit.Mvvm.ComponentModel;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Holds the pad inspector snapshot and the refresh flag that the inspector views bind to, so that the main view model keeps no field for them.
/// </summary>
public sealed partial class PadInspectorUiState : ObservableObject
{
    private readonly Action<PadInspectorSnapshot?> _snapshotChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="PadInspectorUiState"/> class.
    /// </summary>
    /// <param name="snapshotChanged">The action that the main view model runs after the snapshot changes.</param>
    public PadInspectorUiState(Action<PadInspectorSnapshot?> snapshotChanged)
    {
        _snapshotChanged = snapshotChanged;
    }

    /// <summary>
    /// Gets or sets the latest pad inspector snapshot used by popover/CLI/right-side details.
    /// </summary>
    [ObservableProperty] private PadInspectorSnapshot? _snapshot;

    /// <summary>
    /// Gets or sets whether CAD inspector notch details are being refreshed in background.
    /// </summary>
    [ObservableProperty] private bool _isDeferredRefreshPending;

    /// <summary>
    /// Gets or sets whether the Debug details fold of the inspector is open.
    /// It closes when the inspector shows another pad or no pad, and stays open while the same pad is refreshed.
    /// </summary>
    [ObservableProperty] private bool _isDebugDetailsExpanded;

    partial void OnSnapshotChanged(PadInspectorSnapshot? oldValue, PadInspectorSnapshot? newValue)
    {
        if (!IsSamePad(oldValue, newValue))
        {
            IsDebugDetailsExpanded = false;
        }

        _snapshotChanged(newValue);
    }

    private static bool IsSamePad(PadInspectorSnapshot? first, PadInspectorSnapshot? second) =>
        first is not null
        && second is not null
        && first.Cad?.CadPadId == second.Cad?.CadPadId
        && first.Regular?.RegularPadId == second.Regular?.RegularPadId;
}
