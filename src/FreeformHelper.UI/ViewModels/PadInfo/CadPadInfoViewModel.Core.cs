using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// ViewModel for displaying and editing information about one or more <see cref="CadPad" /> objects.
/// Implements <see cref="IPadInfoChangeTracking" /> to manage custom value edits.
/// </summary>
public sealed partial class CadPadInfoViewModel : ObservableObject, IPadInfoChangeTracking
{
    private readonly CadOutputFwDiffAutoMode _cadOutputFwDiffAutoMode;
    private readonly Func<int, int?> _getDxfIndex;
    private readonly Func<int, bool>? _getIsDxfIndexAnchor;
    // Actions provided by the main ViewModel to apply changes or close the info popup.
    private readonly Action<IReadOnlyList<int>, decimal>? _applyCustomValue;
    private readonly Action? _closePadInfo;
    private readonly Action<int>? _focusCadMatches;
    private readonly Action<int>? _highlightCadMatches;
    private readonly Action<IReadOnlyCollection<int>>? _highlightCadOwnerPads;
    private readonly Action<IReadOnlyCollection<int>>? _highlightRegularPads;
    private readonly Action<int, int>? _setCadOutputFwDiffOverride;
    private readonly Action<int>? _clearCadOutputFwDiffOverride;
    private readonly Action<int>? _setDxfIndexAnchor;
    private readonly Action<int>? _clearDxfIndexAnchor;
    private readonly int? _singleCadPadId;
    private readonly IReadOnlyList<int> _padIds; // IDs of the CAD pads being displayed.

    // Internal flags for change tracking.
    private bool _isLoading; // Prevents change tracking during initial loading.
    private decimal _customValueInitial; // The value of CustomValue when the ViewModel was loaded.
    private bool _customValueDirty; // True if CustomValue has been changed from its initial state.
    private readonly IReadOnlyList<int> _allNotchOwnerCadIds = Array.Empty<int>();

    /// <summary>
    /// Gets a value indicating whether multiple CAD pads are being displayed/edited.
    /// </summary>
    public bool IsMulti => _display.Identity.IsMulti;
    /// <summary>
    /// Gets the number of CAD pads currently selected.
    /// </summary>
    public int SelectedCount => _display.Identity.SelectedCount;

    // --- Display Properties ---
    public string Title => _display.Identity.Title;
    public string Subtitle => _display.Identity.Subtitle;
    public string LayerText => _display.Identity.LayerText;
    public string NameText => _display.Identity.NameText;
    public string CadIdText => _display.Identity.CadIdText;
    public string CentroidText => _display.Identity.CentroidText;
    public string AreaText => _display.Identity.AreaText;
    public string VertexText => _display.Identity.VertexText;
    public string IdentityText => _display.Identity.IdentityText;
    public string MatchText => _display.Match.MatchText;
    public string MatchDetailsText => _display.Match.MatchDetailsText;
    public string MatchConfidenceText => _display.Match.MatchConfidenceText;
    public string GeometrySeedValueText => _display.Match.GeometrySeedValueText;
    public string NotchRowSummaryText => _display.Notch.NotchRowSummaryText;
    public string ToRegularRatioText => _display.Notch.ToRegularRatioText;
    public string ToFullRatioText => _display.Notch.ToFullRatioText;
    public string ToRegularValueText => _display.Notch.ToRegularValueText;
    public string ToFullValueText => _display.Notch.ToFullValueText;
    public string CombinedValueText => _display.Notch.CombinedValueText;
    public string ToFullReasonShortText => _display.Notch.ToFullReasonShortText;
    public string ToFullReasonFullText => _display.Notch.ToFullReasonFullText;
    public bool HasToFullReason => !string.IsNullOrWhiteSpace(ToFullReasonFullText);
    public string Stage3AreaText => _display.Notch.Stage3AreaText;
    public string Stage3AreaCompactText => StripKnownPrefix(Stage3AreaText, "Stage3 area:");
    public string TargetAllocationSummaryText => _display.Notch.TargetAllocationSummaryText;
    public string TargetAllocationCompactText => StripKnownPrefix(TargetAllocationSummaryText, "Targets:");
    public IReadOnlyList<string> TargetAllocationLines => _display.Notch.TargetAllocationLines;
    public IReadOnlyList<PadInfoTargetAllocationViewModel> TargetAllocationItems => _display.Notch.TargetAllocationItems;
    public bool HasTargetAllocationItems => TargetAllocationItems.Count > 0;
    public string OwnerSummaryText => _display.Notch.OwnerSummaryText;
    public string OwnerSummaryCompactText => string.IsNullOrWhiteSpace(OwnerSummaryText) ? "-" : OwnerSummaryText;
    public bool HasOwnerSummary => !string.IsNullOrWhiteSpace(OwnerSummaryText);
    public string ToFullDiagnosticsText => _display.Notch.ToFullDiagnosticsText;
    public bool HasToFullDiagnosticsText => !string.IsNullOrWhiteSpace(ToFullDiagnosticsText);
    public IReadOnlyList<NotchOwnerLinkViewModel> NotchOwnerLinks => _display.Notch.NotchOwnerLinks;
    public IReadOnlyList<string> RuleTraceLines => _display.Notch.RuleTraceLines;
    public bool HasRuleTrace => RuleTraceLines.Count > 0;
    public string NotchRowCompactText => StripKnownPrefix(NotchRowSummaryText, "Notch rows:");
    public string DebugStatusText => BuildDebugStatusText();
    public ICommand? NotchDetailCommand { get; }
    public ICommand? SelectAreaBucketCommand { get; }
    public bool HasSelectAreaBucketCommand => SelectAreaBucketCommand is not null;
    public bool HasFocusMatchAction { get; }
    public bool HasHighlightMatchAction { get; }
    public bool HasAnyMatchAction => HasFocusMatchAction || HasHighlightMatchAction;
    public bool HasLocateMatchAction => HasAnyMatchAction;
    public bool HasCadOutputFwDiffOverrideEditor { get; }
    public bool HasDxfIndexAnchorEditor { get; }
    public bool CanClearCadOutputFwDiffOverride => HasCadOutputFwDiffOverride;
    public bool CanClearDxfIndexAnchor => IsDxfIndexAnchor;
    public string GeometryDetailsToggleText => ShowGeometryDetails ? "Hide details" : "Show details";
    public bool ShowLowFrequencyDetailSection => ShowNotchDiagnostics || ShowRuleTrace || ShowGeometryDetails;
    public string CadOutputFwDiffAssignmentModeText => HasCadOutputFwDiffOverride
            ? "Manual override"
        : IsDxfIndexAnchor
            ? "Anchor saved (inactive in current mode)"
            : _cadOutputFwDiffAutoMode == CadOutputFwDiffAutoMode.BestMatchDirect
                ? "Auto (geometry seed)"
                : "Auto (geometry seed, unique per IC)";

    /// <inheritdoc />
    public bool HasPendingChanges => _customValueDirty;

    /// <summary>
    /// Gets or sets the custom numeric value associated with the CAD pad(s).
    /// </summary>
    [ObservableProperty]
    private decimal _customValue;

    [ObservableProperty]
    private string _dxfIndexText = "-";

    [ObservableProperty]
    private decimal _cadOutputFwDiffOverride;

    [ObservableProperty]
    private bool _hasCadOutputFwDiffOverride;

    [ObservableProperty]
    private bool _isDxfIndexAnchor;

    [ObservableProperty]
    private bool _showNotchDiagnostics;

    [ObservableProperty]
    private bool _showRuleTrace;

    [ObservableProperty]
    private bool _showGeometryDetails;

    /// <inheritdoc />
    public ICommand ApplyChangesCommand { get; }
    /// <inheritdoc />
    public ICommand DiscardChangesCommand { get; }
    public IRelayCommand ApplyCadOutputFwDiffOverrideCommand { get; }
    public IRelayCommand ClearCadOutputFwDiffOverrideCommand { get; }
    public IRelayCommand SetDxfIndexAnchorCommand { get; }
    public IRelayCommand ClearDxfIndexAnchorCommand { get; }
    public IRelayCommand LocateMatchCommand { get; }
    public IRelayCommand HighlightAllNotchOwnersCommand { get; }
    public IRelayCommand ToggleLowFrequencyDetailSectionCommand { get; }

    private static string StripKnownPrefix(string value, string prefix)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? value[prefix.Length..].Trim()
            : value;
    }

    private string BuildDebugStatusText()
    {
        var parts = new List<string>();
        if (HasToFullDiagnosticsText)
        {
            parts.Add("diagnostics");
        }

        if (HasRuleTrace)
        {
            parts.Add("trace");
        }

        parts.Add("geometry");
        return string.Join(" + ", parts);
    }
}

