using CommunityToolkit.Mvvm.ComponentModel;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Holds the regular visibility mask values that the Step 1 and Step 4 views bind to, so that the main view model keeps no field for them.
/// </summary>
public sealed partial class RegularVisibilityMaskUiState : ObservableObject
{
    private readonly Action<bool> _enabledChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegularVisibilityMaskUiState"/> class.
    /// </summary>
    /// <param name="enabledChanged">The action that the main view model runs after the enabled flag changes.</param>
    public RegularVisibilityMaskUiState(Action<bool> enabledChanged)
    {
        _enabledChanged = enabledChanged;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the imported regular visibility mask should constrain Step 4 and Simulation.
    /// </summary>
    [ObservableProperty] private bool _isEnabled;

    /// <summary>
    /// Gets or sets a summary message for the externally measured regular visibility mask.
    /// </summary>
    [ObservableProperty] private string _summary = "Regular visibility mask: not loaded. Import Regular Visibility Mask (SeeRegular.csv) in Step 1 to constrain Step 4 and Simulation.";

    partial void OnIsEnabledChanged(bool value) => _enabledChanged(value);
}
