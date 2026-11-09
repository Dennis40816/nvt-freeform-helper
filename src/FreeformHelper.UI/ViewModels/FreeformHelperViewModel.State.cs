using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// This partial class of <see cref="FreeformHelperViewModel"/> defines and manages
/// all the observable properties that represent the current state of the application's UI.
/// These properties are typically bound to controls in the View.
/// </summary>
public sealed partial class FreeformHelperViewModel
{
    // --- Observable Collections for Data Display ---

    /// <summary>
    /// Gets or sets the observable collection of <see cref="CadPad"/> objects currently displayed on the canvas.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<CadPad> _cadPads = new();

    /// <summary>
    /// Gets or sets the observable collection of <see cref="RegularPad"/> objects currently displayed on the canvas.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<RegularPad> _regularPads = new();

    /// <summary>
    /// Gets or sets an in-canvas hint shown when there is nothing meaningful to render.
    /// </summary>
    [ObservableProperty]
    private string _canvasHintText = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the in-canvas hint should be visible.
    /// The value follows <see cref="CanvasHintText"/>.
    /// </summary>
    public bool HasCanvasHint => !string.IsNullOrWhiteSpace(CanvasHintText);

    // --- Status and Selection Display Properties ---

    /// <summary>
    /// Gets or sets the primary status text displayed in the application (e.g., "Ready.", "Importing DXF...").
    /// This text is then processed to update <see cref="StatusDisplayText"/> and <see cref="HasStatusDisplay"/>.
    /// </summary>
    [ObservableProperty]
    private string _statusText = "Ready.";

    /// <summary>
    /// Gets the display version of the status text.
    /// Empty if <see cref="StatusText"/> is "Ready.".
    /// </summary>
    public string StatusDisplayText => BuildStatusDisplayText(StatusText);

    /// <summary>
    /// Gets a value indicating whether there is a status message to display.
    /// </summary>
    public bool HasStatusDisplay => HasStatusDisplayFor(StatusText);

    // The inspector panel inputs and flags are stored once. The properties below forward to the record and
    // raise the same notifications and callbacks as the stored properties they replaced.
    private InspectorPanelState _inspectorPanel = InspectorPanelState.Initial;

    /// <summary>
    /// Gets or sets whether right panel is currently on Settings tab.
    /// </summary>
    public bool IsRightPanelSettingsTab
    {
        get => _inspectorPanel.IsRightPanelSettingsTab;
        set
        {
            if (_inspectorPanel.IsRightPanelSettingsTab == value)
            {
                return;
            }

            OnPropertyChanging();
            _inspectorPanel = _inspectorPanel with { IsRightPanelSettingsTab = value };
            OnIsRightPanelSettingsTabChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnIsRightPanelSettingsTabChanged(bool value);

    /// <summary>
    /// Gets or sets whether right panel is currently on Inspector tab.
    /// </summary>
    public bool IsRightPanelInspectorTab
    {
        get => !IsRightPanelSettingsTab;
        set
        {
            if (value == IsRightPanelInspectorTab)
            {
                return;
            }

            IsRightPanelSettingsTab = !value;
        }
    }

    partial void OnIsRightPanelSettingsTabChanged(bool value)
    {
        OnPropertyChanged(nameof(IsRightPanelInspectorTab));
    }

    /// <summary>
    /// Partial method invoked when <see cref="StatusText"/> property changes.
    /// It raises the change notifications of <see cref="StatusDisplayText"/>
    /// and <see cref="HasStatusDisplay"/> for UI binding when their values change.
    /// </summary>
    /// <param name="oldValue">The previous value of <see cref="StatusText"/>.</param>
    /// <param name="newValue">The new value of <see cref="StatusText"/>.</param>
    partial void OnStatusTextChanged(string? oldValue, string newValue)
    {
        if (!string.Equals(BuildStatusDisplayText(oldValue), BuildStatusDisplayText(newValue), StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(StatusDisplayText));
        }

        if (HasStatusDisplayFor(oldValue) != HasStatusDisplayFor(newValue))
        {
            OnPropertyChanged(nameof(HasStatusDisplay));
        }
    }

    partial void OnCanvasHintTextChanged(string? oldValue, string newValue)
    {
        if (string.IsNullOrWhiteSpace(oldValue) != string.IsNullOrWhiteSpace(newValue))
        {
            OnPropertyChanged(nameof(HasCanvasHint));
        }
    }

    private static bool IsReadyStatusText(string? value)
    {
        return string.Equals(value, "Ready.", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "Ready", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildStatusDisplayText(string? value)
    {
        return IsReadyStatusText(value) ? string.Empty : value ?? string.Empty;
    }

    private static bool HasStatusDisplayFor(string? value)
    {
        return !IsReadyStatusText(value) && !string.IsNullOrWhiteSpace(value);
    }
}
