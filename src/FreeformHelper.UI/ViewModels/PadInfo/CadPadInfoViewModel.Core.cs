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
    public bool IsMulti { get; }
    /// <summary>
    /// Gets a value indicating whether a single CAD pad is being displayed/edited.
    /// </summary>
    public bool IsSingle => !IsMulti;
    /// <summary>
    /// Gets the number of CAD pads currently selected.
    /// </summary>
    public int SelectedCount { get; }

    // --- Display Properties ---
    public string Title { get; }
    public string Subtitle { get; }
    public string LayerText { get; }
    public string NameText { get; }
    public string CadIdText { get; }
    public string BoundsText { get; }
    public string CentroidText { get; }
    public string AreaText { get; }
    public string VertexText { get; }
    public string IdentityText { get; }
    public string MatchText { get; }
    public string MatchDetailsText { get; }
    public string MatchConfidenceText { get; }
    public string GeometrySeedValueText { get; }
    public bool HasNotchCompensationPreview { get; }
    public string NotchRowSummaryText { get; }
    public string ToRegularRatioText { get; }
    public string ToFullRatioText { get; }
    public string CombinedRatioText { get; }
    public string ToRegularValueText { get; }
    public string ToFullValueText { get; }
    public string CombinedValueText { get; }
    public string ToFullReasonShortText { get; }
    public string ToFullReasonFullText { get; }
    public bool HasToFullReason => !string.IsNullOrWhiteSpace(ToFullReasonFullText);
    public string Stage3AreaText { get; }
    public string Stage3AreaCompactText => StripKnownPrefix(Stage3AreaText, "Stage3 area:");
    public string TargetAllocationSummaryText { get; }
    public string TargetAllocationCompactText => StripKnownPrefix(TargetAllocationSummaryText, "Targets:");
    public IReadOnlyList<string> TargetAllocationLines { get; }
    public bool HasTargetAllocationLines => TargetAllocationLines.Count > 0;
    public IReadOnlyList<PadInfoTargetAllocationViewModel> TargetAllocationItems { get; }
    public bool HasTargetAllocationItems => TargetAllocationItems.Count > 0;
    public string OwnerSummaryText { get; }
    public string OwnerSummaryCompactText => string.IsNullOrWhiteSpace(OwnerSummaryText) ? "-" : OwnerSummaryText;
    public bool HasOwnerSummary => !string.IsNullOrWhiteSpace(OwnerSummaryText);
    public bool HasOwnerDetailNavigation => HasOwnerSummary && HasFocusMatchAction;
    public static string OwnerDetailNavigationText => "Open owner details in Regular";
    public string ToFullDiagnosticsText { get; }
    public bool HasToFullDiagnosticsText => !string.IsNullOrWhiteSpace(ToFullDiagnosticsText);
    public bool HasNotchDiagnosticsToggle => HasToFullDiagnosticsText;
    public string NotchDiagnosticsToggleText => ShowNotchDiagnostics ? "Hide diagnostics" : "Show diagnostics";
    public IReadOnlyList<NotchOwnerLinkViewModel> NotchOwnerLinks { get; }
    public bool HasNotchOwnerLinks => NotchOwnerLinks.Count > 0;
    public bool HasHighlightAllNotchOwners => HasNotchOwnerLinks && _allNotchOwnerCadIds.Count > 1;
    public string HighlightAllNotchOwnersText => $"Highlight all owners ({_allNotchOwnerCadIds.Count})";
    public string HighlightAllNotchOwnersShortText => $"Owners ({_allNotchOwnerCadIds.Count})";
    public IReadOnlyList<string> RuleTraceLines { get; }
    public bool HasRuleTrace => RuleTraceLines.Count > 0;
    public string RuleTraceToggleText => ShowRuleTrace ? "Hide Detail" : "Show Detail";
    public string NotchRowCompactText => StripKnownPrefix(NotchRowSummaryText, "Notch rows:");
    public string DebugStatusText => BuildDebugStatusText();
    /// <summary>
    /// Gets a value indicating whether the custom value is mixed across multiple selected pads.
    /// </summary>
    public bool IsCustomValueMixed { get; }
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
    public IRelayCommand FocusMatchCommand { get; }
    public IRelayCommand LocateMatchCommand { get; }
    public IRelayCommand OpenOwnerDetailCommand { get; }
    public IRelayCommand HighlightMatchesCommand { get; }
    public IRelayCommand HighlightAllNotchOwnersCommand { get; }
    public IRelayCommand ToggleNotchDiagnosticsCommand { get; }
    public IRelayCommand ToggleRuleTraceCommand { get; }
    public IRelayCommand ToggleGeometryDetailsCommand { get; }
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

