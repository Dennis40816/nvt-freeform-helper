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
    /// Gets or sets a value indicating whether the in-canvas hint should be visible.
    /// </summary>
    [ObservableProperty]
    private bool _hasCanvasHint;

    // --- Status and Selection Display Properties ---

    /// <summary>
    /// Gets or sets the primary status text displayed in the application (e.g., "Ready.", "Importing DXF...").
    /// This text is then processed to update <see cref="StatusDisplayText"/> and <see cref="HasStatusDisplay"/>.
    /// </summary>
    [ObservableProperty]
    private string _statusText = "Ready.";

    /// <summary>
    /// Gets or sets the display version of the status text.
    /// Empty if <see cref="StatusText"/> is "Ready.".
    /// </summary>
    [ObservableProperty]
    private string _statusDisplayText = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether there is a status message to display.
    /// </summary>
    [ObservableProperty]
    private bool _hasStatusDisplay;

    /// <summary>
    /// Gets or sets a concise summary of the current selection (e.g., "Selected: CAD=5, Regular=12").
    /// </summary>
    [ObservableProperty]
    private string _selectionSummary = "No selection.";

    /// <summary>
    /// Gets or sets a descriptive summary of the currently selected regular grid range.
    /// </summary>
    [ObservableProperty]
    private string _selectedGridRangeSummary = "Select regular pads to edit size.";

    /// <summary>
    /// Gets or sets a string indicating the count of selected CAD pads.
    /// </summary>
    [ObservableProperty]
    private string _cadSelectionText = "CAD: 0 pads.";

    /// <summary>
    /// Gets or sets a string indicating the count of selected regular pads.
    /// </summary>
    [ObservableProperty]
    private string _regularSelectionText = "Regular: 0 pads.";

    /// <summary>
    /// Gets or sets a detailed text description of the current selection, including row/col ranges.
    /// </summary>
    [ObservableProperty]
    private string _selectionDetailText = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether there is a detailed selection text to display.
    /// </summary>
    [ObservableProperty]
    private bool _hasSelectionDetail;

    /// <summary>
    /// Gets or sets quick-locate CAD pad id input used by Inspector panel.
    /// </summary>
    [ObservableProperty]
    private decimal _quickLocateCadPadId;

    /// <summary>
    /// Gets or sets quick-locate regular pad index input used by Inspector panel.
    /// </summary>
    [ObservableProperty]
    private decimal _quickLocateRegularPadIndex;

    /// <summary>
    /// Gets or sets the unified quick-focus query used by the Inspector panel.
    /// Examples: CAD 4809, REG 4616, DIFF 72, IC1 DIFF72.
    /// </summary>
    [ObservableProperty]
    private string _quickFocusQueryText = string.Empty;

    /// <summary>
    /// Gets or sets the latest pad inspector snapshot used by popover/CLI/right-side details.
    /// </summary>
    [ObservableProperty]
    private PadInspectorSnapshot? _currentPadInspectorSnapshot;

    /// <summary>
    /// Gets or sets whether the top-strip pad inspector summary is visible.
    /// </summary>
    [ObservableProperty]
    private bool _hasPadInspectorSummary;
    public bool HasNoPadInspectorSummary => !HasPadInspectorSummary;

    /// <summary>
    /// Gets or sets whether CAD inspector notch details are being refreshed in background.
    /// </summary>
    [ObservableProperty]
    private bool _isPadInspectorDeferredRefreshPending;

    /// <summary>
    /// Gets or sets whether right panel is currently on Settings tab.
    /// </summary>
    [ObservableProperty]
    private bool _isRightPanelSettingsTab = true;

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

    /// <summary>
    /// Gets the current right panel tab title.
    /// </summary>
    public string RightPanelActiveTabTitle => IsRightPanelSettingsTab ? "Settings" : "Inspector";

    /// <summary>
    /// Gets or sets top-strip primary text for current inspector snapshot.
    /// </summary>
    [ObservableProperty]
    private string _padInspectorPrimaryText = string.Empty;

    /// <summary>
    /// Gets or sets compact header text (ID + diff idx only) for workspace top strip.
    /// </summary>
    [ObservableProperty]
    private string _padInspectorCompactHeaderText = string.Empty;

    /// <summary>
    /// Gets or sets top-strip secondary text for current inspector snapshot.
    /// </summary>
    [ObservableProperty]
    private string _padInspectorSecondaryText = string.Empty;

    /// <summary>
    /// Gets or sets top-strip match summary text for current inspector snapshot.
    /// </summary>
    [ObservableProperty]
    private string _padInspectorMatchText = string.Empty;

    /// <summary>
    /// Gets or sets top-strip notch/freeform summary text for current inspector snapshot.
    /// </summary>
    [ObservableProperty]
    private string _padInspectorCompensationText = string.Empty;

    /// <summary>
    /// Gets or sets segmented notch/freeform summary lines for inspector panel.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<PadInspectorDisplayLine> _padInspectorCompensationLines = new();

    /// <summary>
    /// Gets or sets rule trace lines for current inspector snapshot.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _padInspectorRuleTraceLines = new();

    /// <summary>
    /// Gets or sets grouped rule trace sections for current inspector snapshot.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<PadInspectorRuleTraceSection> _padInspectorRuleTraceSections = new();

    /// <summary>
    /// Gets or sets whether current inspector snapshot has rule trace lines.
    /// </summary>
    [ObservableProperty]
    private bool _hasPadInspectorRuleTrace;

    /// <summary>
    /// Gets or sets whether pad inspector trace is expanded in top strip.
    /// </summary>
    [ObservableProperty]
    private bool _isPadInspectorTraceExpanded;

    /// <summary>
    /// Gets the toggle text for pad inspector trace expander.
    /// </summary>
    public string PadInspectorTraceToggleText => IsPadInspectorTraceExpanded ? "Hide rule trace" : "Rule trace";

    /// <summary>
    /// Gets or sets whether top-strip rule trace popup is open.
    /// </summary>
    [ObservableProperty]
    private bool _isPadInspectorTracePopupOpen;

    /// <summary>
    /// Gets the toggle text for the top-strip rule trace popup.
    /// </summary>
    public string PadInspectorTracePopupToggleText => IsPadInspectorTracePopupOpen ? "Hide rule trace" : "Rule trace";

    /// <summary>
    /// Gets or sets whether Debug raw section in inspector rule trace is expanded.
    /// </summary>
    [ObservableProperty]
    private bool _isPadInspectorRawTraceExpanded;

    partial void OnHasPadInspectorSummaryChanged(bool value)
    {
        OnPropertyChanged(nameof(HasNoPadInspectorSummary));
    }

    partial void OnIsRightPanelSettingsTabChanged(bool value)
    {
        OnPropertyChanged(nameof(IsRightPanelInspectorTab));
        OnPropertyChanged(nameof(RightPanelActiveTabTitle));
    }

    /// <summary>
    /// Gets or sets a value that, when true, suppresses clearing the current selection
    /// during a grid rebuild operation. Used for undo/redo.
    /// </summary>
    [ObservableProperty]
    private bool _suppressSelectionClearOnRebuild;

    /// <summary>
    /// Partial method invoked when <see cref="StatusText"/> property changes.
    /// It processes the raw status text to update <see cref="StatusDisplayText"/>
    /// and <see cref="HasStatusDisplay"/> for UI binding.
    /// </summary>
    /// <param name="value">The new value of <see cref="StatusText"/>.</param>
    partial void OnStatusTextChanged(string value)
    {
        // If the status text indicates "Ready.", clear the display text and hide the status display.
        if (string.Equals(value, "Ready.", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "Ready", StringComparison.OrdinalIgnoreCase))
        {
            StatusDisplayText = string.Empty;
            HasStatusDisplay = false;
            return;
        }

        // Otherwise, display the new status text.
        StatusDisplayText = value;
        HasStatusDisplay = !string.IsNullOrWhiteSpace(value);
    }

    partial void OnCanvasHintTextChanged(string value)
    {
        HasCanvasHint = !string.IsNullOrWhiteSpace(value);
    }
}
