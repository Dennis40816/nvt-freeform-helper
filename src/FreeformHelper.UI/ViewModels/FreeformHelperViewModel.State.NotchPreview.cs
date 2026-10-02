using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    [ObservableProperty] private ObservableCollection<NotchCanvasPreviewItem> _notchCanvasPreviewItems = new();

    /// <summary>
    /// Gets or sets a value indicating whether Notch 2.2 canvas preview is active.
    /// </summary>
    [ObservableProperty] private bool _showNotchCanvasPreview = true;

    /// <summary>
    /// Gets or sets a value indicating whether To Regular ratio labels should render on AA canvas.
    /// </summary>
    [ObservableProperty] private bool _showNotchToRegularLabels = true;

    /// <summary>
    /// Gets or sets staged To Full visualization step on AA canvas.
    /// 1 = polygon-overlap seed, 2 = boundary candidate regulars, 3 = final To Full result.
    /// </summary>
    [ObservableProperty] private decimal _notchPreviewVisualizationStep = 3m;
    /// <summary>
    /// Gets or sets a value indicating whether Step 3 preview stage should auto-play (1->2->3 loop).
    /// </summary>
    [ObservableProperty] private bool _notchPreviewAutoPlayEnabled = true;
    /// <summary>
    /// Gets or sets Step 3 preview auto-play interval in milliseconds.
    /// </summary>
    [ObservableProperty] private decimal _notchPreviewAutoPlayIntervalMs = 700m;

    /// <summary>
    /// Gets a value indicating whether the quick To Full display toggle is available.
    /// This toggle is display-only and should be disabled when To Full computation is off.
    /// </summary>
    public bool CanShowToFullPreviewToggle => EnableToFull;

    /// <summary>
    /// Gets a value indicating whether To Full preview should actually render on canvas.
    /// User toggle state is respected, but rendering only happens when Step 3 data exists.
    /// </summary>
    public bool IsNotchCanvasPreviewVisible => ResolveNotchOverlayVisibility().ShowAnyToFull;

    /// <summary>
    /// Gets a value indicating whether Step 3 stage controls (prev/next/autoplay) are available.
    /// </summary>
    public bool CanControlNotchPreviewStage => IsNotchCanvasPreviewVisible;

    /// <summary>
    /// Gets the normalized Step 3 stage index (1~3).
    /// </summary>
    public int NotchPreviewStageIndex => Math.Clamp((int)Math.Round((double)NotchPreviewVisualizationStep), 1, 3);

    /// <summary>
    /// Gets a user-facing summary for current To Full stage.
    /// </summary>
    public string NotchPreviewStageSummary => NotchPreviewStageIndex switch
    {
        1 => "Current layer: Stage 1 (Seed)",
        2 => "Current layer: Stage 2 (Candidate)",
        _ => "Current layer: Stage 3 (Final)",
    };

    /// <summary>
    /// Gets a user-facing summary for autoplay state.
    /// </summary>
    public string NotchPreviewAutoPlaySummary => NotchPreviewAutoPlayEnabled
        ? $"AutoPlay ON ({NotchPreviewAutoPlayIntervalMs:0} ms, loop 1->2->3)"
        : $"AutoPlay OFF ({NotchPreviewAutoPlayIntervalMs:0} ms)";

    /// <summary>
    /// Gets a user-facing summary for current To Full rule engine mode.
    /// </summary>
    public string NotchToFullRuleEngineSummary => EnableToFullRuleEngine
        ? (EnableToFullRuleTrace
            ? "To Full rule engine: ON (trace ON)"
            : "To Full rule engine: ON (trace OFF)")
        : "To Full rule engine: OFF (legacy gate path)";

    public string NotchCompensationModelSummary => string.IsNullOrWhiteSpace(SelectedNotchCompensationModelOption.Description)
        ? SelectedNotchCompensationModelOption.Display
        : SelectedNotchCompensationModelOption.Description;

    public string NotchEffectiveModelSummary =>
        $"{SelectedNotchCompensationModelOption.Display} · ToRegular {(EnableToRegular ? "ON" : "OFF")} · ToFull {(EnableToFull ? "ON" : "OFF")}";

    public string NotchAllocationModelSummary =>
        SelectedNotchCompensationModelOption.Value switch
        {
            NotchCompensationModel.CurrentGain => "Allocation model: Stage3 effective-area gain; ToFull-expanded area splits the combined source signal.",
            NotchCompensationModel.ConservativeNoGain => "Allocation model: SourceArea-dominant; ToFull is support/cap, not direct full-area weight.",
            NotchCompensationModel.Disabled => "Allocation model: disabled baseline.",
            _ => string.Empty,
        };

    public string NotchAllocationModelShortText =>
        SelectedNotchCompensationModelOption.Value switch
        {
            NotchCompensationModel.CurrentGain => "Stage3 gain allocation",
            NotchCompensationModel.ConservativeNoGain => "Source-area dominant",
            NotchCompensationModel.Disabled => "Disabled baseline",
            _ => string.Empty,
        };

    public string NotchToRegularSemanticSummary => EnableToRegular
        ? "ToRegular: undo NF area flattening and restore area-proportional signal before redistribution."
        : "ToRegular: disabled for baseline comparison.";

    public string NotchToRegularShortText => EnableToRegular
        ? "ON - restore area signal"
        : "OFF - baseline";

    public string NotchToRegularStateText => EnableToRegular
        ? "ToRegular ON"
        : "ToRegular OFF";

    public string NotchToFullSemanticSummary => EnableToFull
        ? "ToFull: boundary support/cap/allowance only; target amount remains area-proportional."
        : "ToFull: disabled for baseline comparison.";

    public string NotchToFullShortText => EnableToFull
        ? "ON - support/cap only"
        : "OFF - no boundary support";

    public string NotchToFullStateText => EnableToFull
        ? "ToFull ON"
        : "ToFull OFF";

    public string NotchBoundaryVirtualAreaCapSummary => EnableBoundaryVirtualAreaCap
        ? $"Boundary cap: virtual ToFull area <= inside overlap x {BoundaryVirtualAreaCapPercent:0.#}%."
        : "Boundary cap: OFF; Stage3 uses raw ToFull reachable area.";

    public string NotchBoundaryVirtualAreaCapShortText => EnableBoundaryVirtualAreaCap
        ? $"Boundary cap {BoundaryVirtualAreaCapPercent:0.#}%"
        : "Boundary cap OFF";

    public string NotchTargetCoverageGuardSummary =>
        SimulationSafetyTextProjector.BuildNotchTargetCoverageGuardSummary(
            EnableTargetCoverageGuard,
            TargetCoverageCapPercent);

    public string NotchTargetCoverageGuardShortText => EnableTargetCoverageGuard
        ? $"Target guard {TargetCoverageCapPercent:0.#}%"
        : "Target guard OFF";

    public string NotchTargetCoverageCapHelpText =>
        SimulationSafetyTextProjector.BuildNotchTargetCoverageCapHelpText(
            TargetCoverageCapPercent,
            SimulationSafetyOverviewEmsCapText);

    public string NotchEmsSafetyPolicySummary =>
        !string.IsNullOrWhiteSpace(SelectedNotchCompensationModelOption.Display)
            ? SimulationSafetyTextProjector.BuildNotchEmsSafetyPolicySummary(SimulationSafetyOverviewEmsCapText)
            : string.Empty;

    public string NotchEmsSafetyShortText =>
        !string.IsNullOrWhiteSpace(SelectedNotchCompensationModelOption.Display)
            ? SimulationSafetyTextProjector.BuildNotchEmsSafetyShortText(SimulationSafetyOverviewEmsCapText)
            : string.Empty;

    public string Step3ToFullDetailsToggleText => IsStep3ToFullDetailsExpanded
        ? "Hide ToFull details"
        : "Show ToFull details";

    public string NotchExportSafetyPolicySummary =>
        !string.IsNullOrWhiteSpace(SelectedNotchExportFileTypeOption.Display)
            ? SimulationSafetyTextProjector.BuildNotchExportSafetyPolicySummary(SimulationSafetyOverviewEmsCapText)
            : string.Empty;

    public string NotchExportHandoffFileText => !string.IsNullOrWhiteSpace(SelectedNotchExportFileTypeOption.Display)
        ? SelectedNotchExportFileTypeOption.Display
        : "No export type selected";

    public string NotchExportHandoffProfileText => !string.IsNullOrWhiteSpace(SelectedNotchExportProfileOption.Display)
        ? SelectedNotchExportProfileOption.Display
        : "No export profile selected";

    public string NotchExportHandoffVersionText =>
        NotchExportFileTypeMetadata.TryGetPinnedVersion(SelectedNotchExportFileTypeOption, out var pinnedVersion)
            ? pinnedVersion.ToDisplayLabel()
            : "Review window selection";

    public string NotchExportHandoffChecklistText =>
        SimulationSafetyTextProjector.BuildNotchExportHandoffChecklistText(
            !string.IsNullOrWhiteSpace(SelectedNotchExportFileTypeOption.Display));

    /// <summary>
    /// Gets a value indicating whether To Regular ratio labels should render on canvas.
    /// Labels are tied to Step 3 preview data and remain available even when To Full overlay is off.
    /// </summary>
    public bool IsNotchToRegularPreviewVisible => ResolveNotchOverlayVisibility().ShowToRegularLabels;

    /// <summary>
    /// Gets a value indicating whether Step 3 To Full seed overlay should render on canvas.
    /// </summary>
    public bool IsNotchToFullSeedPreviewVisible => ResolveNotchOverlayVisibility().ShowToFullSeed;

    /// <summary>
    /// Gets a value indicating whether Step 3 To Full candidate overlay should render on canvas.
    /// </summary>
    public bool IsNotchToFullCandidatePreviewVisible => ResolveNotchOverlayVisibility().ShowToFullCandidate;

    /// <summary>
    /// Gets a value indicating whether Step 3 To Full final overlay should render on canvas.
    /// </summary>
    public bool IsNotchToFullFinalPreviewVisible => ResolveNotchOverlayVisibility().ShowToFullFinal;

    public string NotchPreviewSeedLayerStateText => IsNotchToFullSeedPreviewVisible ? "Active" : "Hidden";

    public string NotchPreviewCandidateLayerStateText => IsNotchToFullCandidatePreviewVisible ? "Active" : "Hidden";

    public string NotchPreviewFinalLayerStateText => IsNotchToFullFinalPreviewVisible ? "Active" : "Hidden";

    private NotchOverlayVisibilityState ResolveNotchOverlayVisibility()
    {
        return NotchOverlayVisibilityPolicy.Resolve(new NotchOverlayVisibilityInput(
            HasPreviewData: NotchCanvasPreviewItems.Count > 0,
            EnableToFullComputation: EnableToFull,
            ShowToFullOverlay: ShowNotchCanvasPreview,
            ShowToRegularLabels: ShowNotchToRegularLabels,
            PreviewStage: Math.Clamp((int)Math.Round((double)NotchPreviewVisualizationStep), 1, 3)));
    }

    private void NotifyNotchModelSummaryChanged()
    {
        OnPropertyChanged(nameof(NotchCompensationModelSummary));
        OnPropertyChanged(nameof(NotchEffectiveModelSummary));
        OnPropertyChanged(nameof(NotchAllocationModelSummary));
        OnPropertyChanged(nameof(NotchAllocationModelShortText));
        OnPropertyChanged(nameof(NotchToRegularSemanticSummary));
        OnPropertyChanged(nameof(NotchToRegularShortText));
        OnPropertyChanged(nameof(NotchToRegularStateText));
        OnPropertyChanged(nameof(NotchToFullSemanticSummary));
        OnPropertyChanged(nameof(NotchToFullShortText));
        OnPropertyChanged(nameof(NotchToFullStateText));
        OnPropertyChanged(nameof(NotchBoundaryVirtualAreaCapSummary));
        OnPropertyChanged(nameof(NotchBoundaryVirtualAreaCapShortText));
        OnPropertyChanged(nameof(NotchTargetCoverageGuardSummary));
        OnPropertyChanged(nameof(NotchTargetCoverageGuardShortText));
        OnPropertyChanged(nameof(NotchTargetCoverageCapHelpText));
        OnPropertyChanged(nameof(NotchEmsSafetyPolicySummary));
        OnPropertyChanged(nameof(NotchEmsSafetyShortText));
    }

    partial void OnIsStep3ToFullDetailsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(Step3ToFullDetailsToggleText));
    }
}
