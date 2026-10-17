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
    /// Gets or sets quick-locate CAD pad id input used by Inspector panel.
    /// </summary>
    public decimal QuickLocateCadPadId
    {
        get => _inspectorPanel.QuickLocateCadPadId;
        set
        {
            if (_inspectorPanel.QuickLocateCadPadId == value)
            {
                return;
            }

            OnPropertyChanging();
            _inspectorPanel = _inspectorPanel with { QuickLocateCadPadId = value };
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets quick-locate regular pad index input used by Inspector panel.
    /// </summary>
    public decimal QuickLocateRegularPadIndex
    {
        get => _inspectorPanel.QuickLocateRegularPadIndex;
        set
        {
            if (_inspectorPanel.QuickLocateRegularPadIndex == value)
            {
                return;
            }

            OnPropertyChanging();
            _inspectorPanel = _inspectorPanel with { QuickLocateRegularPadIndex = value };
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets the unified quick-focus query used by the Inspector panel.
    /// Examples: CAD 4809, REG 4616, DIFF 72, IC1 DIFF72.
    /// </summary>
    public string QuickFocusQueryText
    {
        get => _inspectorPanel.QuickFocusQueryText;
        set
        {
            if (_inspectorPanel.QuickFocusQueryText == value)
            {
                return;
            }

            OnPropertyChanging();
            _inspectorPanel = _inspectorPanel with { QuickFocusQueryText = value };
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets the latest pad inspector snapshot used by popover/CLI/right-side details.
    /// </summary>
    [ObservableProperty]
    private PadInspectorSnapshot? _currentPadInspectorSnapshot;

    /// <summary>
    /// Gets or sets the text and lines that the pad inspector panels show for the current snapshot.
    /// The properties below read from this one value.
    /// </summary>
    [ObservableProperty]
    private PadInspectorDisplay _padInspectorDisplay = PadInspectorDisplay.CreateEmpty();

    /// <summary>
    /// Gets whether the top-strip pad inspector summary is visible.
    /// </summary>
    public bool HasPadInspectorSummary => PadInspectorDisplay.HasSummary;
    public bool HasNoPadInspectorSummary => !HasPadInspectorSummary;

    /// <summary>
    /// Gets or sets whether CAD inspector notch details are being refreshed in background.
    /// </summary>
    [ObservableProperty]
    private bool _isPadInspectorDeferredRefreshPending;

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

    /// <summary>
    /// Gets the current right panel tab title.
    /// </summary>
    public string RightPanelActiveTabTitle => IsRightPanelSettingsTab ? "Settings" : "Inspector";

    /// <summary>
    /// Gets top-strip primary text for current inspector snapshot.
    /// </summary>
    public string PadInspectorPrimaryText => PadInspectorDisplay.PrimaryText;

    /// <summary>
    /// Gets compact header text (ID + diff idx only) for workspace top strip.
    /// </summary>
    public string PadInspectorCompactHeaderText => PadInspectorDisplay.CompactHeaderText;

    /// <summary>
    /// Gets top-strip secondary text for current inspector snapshot.
    /// </summary>
    public string PadInspectorSecondaryText => PadInspectorDisplay.SecondaryText;

    /// <summary>
    /// Gets top-strip match summary text for current inspector snapshot.
    /// </summary>
    public string PadInspectorMatchText => PadInspectorDisplay.MatchText;

    /// <summary>
    /// Gets top-strip notch/freeform summary text for current inspector snapshot.
    /// </summary>
    public string PadInspectorCompensationText => PadInspectorDisplay.CompensationText;

    /// <summary>
    /// Gets segmented notch/freeform summary lines for inspector panel.
    /// </summary>
    public ObservableCollection<PadInspectorDisplayLine> PadInspectorCompensationLines => PadInspectorDisplay.CompensationLines;

    /// <summary>
    /// Gets rule trace lines for current inspector snapshot.
    /// </summary>
    public ObservableCollection<string> PadInspectorRuleTraceLines => PadInspectorDisplay.RuleTraceLines;

    /// <summary>
    /// Gets grouped rule trace sections for current inspector snapshot.
    /// </summary>
    public ObservableCollection<PadInspectorRuleTraceSection> PadInspectorRuleTraceSections => PadInspectorDisplay.RuleTraceSections;

    /// <summary>
    /// Gets whether current inspector snapshot has rule trace lines.
    /// </summary>
    public bool HasPadInspectorRuleTrace => PadInspectorDisplay.HasRuleTrace;

    /// <summary>
    /// Gets or sets whether pad inspector trace is expanded in top strip.
    /// </summary>
    public bool IsPadInspectorTraceExpanded
    {
        get => _inspectorPanel.IsPadInspectorTraceExpanded;
        set
        {
            if (_inspectorPanel.IsPadInspectorTraceExpanded == value)
            {
                return;
            }

            OnPropertyChanging();
            _inspectorPanel = _inspectorPanel with { IsPadInspectorTraceExpanded = value };
            OnIsPadInspectorTraceExpandedChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnIsPadInspectorTraceExpandedChanged(bool value);

    /// <summary>
    /// Gets the toggle text for pad inspector trace expander.
    /// </summary>
    public string PadInspectorTraceToggleText => IsPadInspectorTraceExpanded ? "Hide rule trace" : "Rule trace";

    /// <summary>
    /// Gets or sets whether top-strip rule trace popup is open.
    /// </summary>
    public bool IsPadInspectorTracePopupOpen
    {
        get => _inspectorPanel.IsPadInspectorTracePopupOpen;
        set
        {
            if (_inspectorPanel.IsPadInspectorTracePopupOpen == value)
            {
                return;
            }

            OnPropertyChanging();
            _inspectorPanel = _inspectorPanel with { IsPadInspectorTracePopupOpen = value };
            OnIsPadInspectorTracePopupOpenChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnIsPadInspectorTracePopupOpenChanged(bool value);

    /// <summary>
    /// Gets the toggle text for the top-strip rule trace popup.
    /// </summary>
    public string PadInspectorTracePopupToggleText => IsPadInspectorTracePopupOpen ? "Hide rule trace" : "Rule trace";

    /// <summary>
    /// Gets or sets whether Debug raw section in inspector rule trace is expanded.
    /// </summary>
    public bool IsPadInspectorRawTraceExpanded
    {
        get => _inspectorPanel.IsPadInspectorRawTraceExpanded;
        set
        {
            if (_inspectorPanel.IsPadInspectorRawTraceExpanded == value)
            {
                return;
            }

            OnPropertyChanging();
            _inspectorPanel = _inspectorPanel with { IsPadInspectorRawTraceExpanded = value };
            OnPropertyChanged();
        }
    }

    partial void OnIsRightPanelSettingsTabChanged(bool value)
    {
        OnPropertyChanged(nameof(IsRightPanelInspectorTab));
        OnPropertyChanged(nameof(RightPanelActiveTabTitle));
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
