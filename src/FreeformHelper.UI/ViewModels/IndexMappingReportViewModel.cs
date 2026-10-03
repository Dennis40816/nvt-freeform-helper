using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Application.Services;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class IndexMappingReportViewModel : ObservableObject
{
    private const int MaxConcurrentTraceJobs = 2;
    private const int TraceSchemaVersion = 1;
    private readonly Func<int?, int?, bool>? _locateTarget;
    private readonly Func<int, int, bool>? _applyOverride;
    private readonly Func<int, int, bool>? _applyDiffOverride;
    private readonly Func<int, bool>? _clearOverride;
    private readonly IndexMappingVerificationWorkspaceSnapshot _workspaceSnapshot;
    private readonly List<IndexMappingDecisionRowViewModel> _allDecisionSeedRows;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, IReadOnlyList<IndexMappingAlgorithmTraceEntryViewModel>> _traceCache = new(StringComparer.Ordinal);
    private int _traceRequestVersion;

    public IndexMappingReportViewModel(
        string summary,
        DxfRegularMappingReport report,
        Func<int?, int?, bool>? locateTarget = null,
        Func<int, int, bool>? applyOverride = null,
        Func<int, int, bool>? applyDiffOverride = null,
        Func<int, bool>? clearOverride = null,
        Func<int, int?>? getOverrideRegularIndex = null)
    {
        Summary = summary;
        TotalIssueCount = Math.Max(0, report.TotalIssueCount);
        IssuesTruncated = report.IssuesTruncated;
        _locateTarget = locateTarget;
        _applyOverride = applyOverride;
        _applyDiffOverride = applyDiffOverride;
        _clearOverride = clearOverride;

        _workspaceSnapshot = IndexMappingVerificationWorkspaceProjector.Build(report, getOverrideRegularIndex);
        _allDecisionSeedRows = _workspaceSnapshot.DecisionRowContracts
            .Select(IndexMappingDecisionRowViewModel.FromContract)
            .ToList();

        MaskChangedCount = _workspaceSnapshot.Metrics.ChangedByMaskDecisionCount;
        MaskRemovedCount = _workspaceSnapshot.Metrics.RemovedByMaskDecisionCount;
        CountMismatchDecisionCount = _workspaceSnapshot.Metrics.CountMismatchDecisionCount;
        UnmappedDecisionCount = _workspaceSnapshot.Metrics.UnmappedDecisionCount;
        LowConfidenceDecisionCount = _workspaceSnapshot.Metrics.LowConfidenceDecisionCount;
        AmbiguousDecisionCount = _workspaceSnapshot.Metrics.AmbiguousDecisionCount;
        DuplicateDiffDecisionCount = _workspaceSnapshot.Metrics.DuplicateDiffDecisionCount;
        ChangedByMaskDecisionCount = _workspaceSnapshot.Metrics.ChangedByMaskDecisionCount;
        RemovedByMaskDecisionCount = _workspaceSnapshot.Metrics.RemovedByMaskDecisionCount;

        PreviousIssueCommand = new RelayCommand(() => MoveDecision(-1));
        NextIssueCommand = new RelayCommand(() => MoveDecision(1));
        LocateSelectedIssueCommand = new RelayCommand(LocateSelectedDecision);
        ApplyOverrideCommand = new RelayCommand(ApplyOverrideForSelectedDecision);
        ApplyDiffOverrideCommand = new RelayCommand(ApplyDiffOverrideForSelectedDecision);
        ApplySegmentDiffOverridesCommand = new RelayCommand(ApplySegmentDiffOverridesForSelectedDecision);
        ApplyCadOutputFwDiffOverridesCommand = new RelayCommand(ApplyCadOutputFwDiffOverrides);
        ClearOverrideCommand = new RelayCommand(ClearOverrideForSelectedDecision);
        ClearSearchKeywordCommand = new RelayCommand(() => SearchKeyword = string.Empty);
        SetDecisionFilterModeCommand = new RelayCommand<IndexMappingDecisionFilterMode>(SetDecisionFilterMode);
        SelectDecisionCommand = new RelayCommand<IndexMappingDecisionRowViewModel?>(SelectDecision);
        SelectVerificationStepCommand = new RelayCommand<IndexMappingVerificationStep>(SelectVerificationStep);

        RebuildWorkspaceView();
        RefreshVerificationSteps();
    }

    public string Summary { get; }
    public int TotalIssueCount { get; }
    public bool IssuesTruncated { get; }
    public int MaskChangedCount { get; }
    public int MaskRemovedCount { get; }
    public int CountMismatchDecisionCount { get; }
    public int UnmappedDecisionCount { get; }
    public int LowConfidenceDecisionCount { get; }
    public int AmbiguousDecisionCount { get; }
    public int DuplicateDiffDecisionCount { get; }
    public int ChangedByMaskDecisionCount { get; }
    public int RemovedByMaskDecisionCount { get; }

    public bool HasIssues => Issues.Count > 0;
    public bool HasNoIssues => !HasIssues;
    public bool HasMaskAudit => _workspaceSnapshot.MaskAuditRows.Count > 0;
    public bool HasNoMaskAudit => !HasMaskAudit;
    public string IssueTitle => TotalIssueCount > 0
        ? $"Issues ({Issues.Count} shown / {TotalIssueCount} total)"
        : "Issues";
    public string MaskAuditTitle => HasMaskAudit
        ? $"CSV-confirmed geometry seed ({MaskAuditRows.Count} shown / {_workspaceSnapshot.MaskAuditRows.Count} total)"
        : "CSV-confirmed geometry seed";
    public string MaskAuditSummary => !HasMaskAudit
        ? "No SeeRegular-based CSV confirmation data."
        : $"Changed by mask: {MaskChangedCount}, removed by mask: {MaskRemovedCount}.";
    public bool HasTruncationNote => IssuesTruncated && Issues.Count > 0 && TotalIssueCount > Issues.Count;
    public string TruncationNote => HasTruncationNote
        ? $"Showing first {Issues.Count} of {TotalIssueCount} issues."
        : string.Empty;
    public bool HasDecisionRows => DecisionRows.Count > 0;
    public bool HasNoDecisionRows => !HasDecisionRows;
    public bool HasAggregateDecisionRows => AggregateDecisionRows.Count > 0;
    public bool HasNoAggregateDecisionRows => !HasAggregateDecisionRows;
    public int VisibleDecisionCount => DecisionRows.Count;
    public int VisibleAggregateDecisionCount => AggregateDecisionRows.Count;
    public int TotalDecisionCount => _allDecisionSeedRows.Count;
    public int OpenIssueCount => CountMismatchDecisionCount +
                                 UnmappedDecisionCount +
                                 LowConfidenceDecisionCount +
                                 AmbiguousDecisionCount +
                                 DuplicateDiffDecisionCount +
                                 ChangedByMaskDecisionCount +
                                 RemovedByMaskDecisionCount;
    public string OverallStatusText => OpenIssueCount == 0 ? "pass" : "warning";
    public string DecisionTitle => TotalDecisionCount > 0
        ? $"Decision rows ({VisibleDecisionCount} shown / {TotalDecisionCount} total)"
        : "Decision rows";
    public string DecisionSummaryText => HasDecisionRows
        ? $"Showing {VisibleDecisionCount} entity rows under {SelectedFilterLabel}."
        : HasAggregateDecisionRows
            ? $"No entity rows under {SelectedFilterLabel}. Aggregate diagnostics: {VisibleAggregateDecisionCount}."
            : $"No rows under {SelectedFilterLabel}.";
    public string AggregateDecisionTitle => HasAggregateDecisionRows
        ? $"Aggregate diagnostics ({VisibleAggregateDecisionCount})"
        : "Aggregate diagnostics";
    public string AggregateDecisionSummaryText => HasAggregateDecisionRows
        ? "Aggregate signals (for example count mismatch) are listed separately and do not block entity review."
        : "No aggregate diagnostics.";
    public bool HasSearchKeyword => !string.IsNullOrWhiteSpace(SearchKeyword);
    public bool IsFilterAllActive => SelectedFilterMode == IndexMappingDecisionFilterMode.All;
    public bool IsFilterChangedByMaskActive => SelectedFilterMode == IndexMappingDecisionFilterMode.ChangedByMask;
    public bool IsFilterRemovedByMaskActive => SelectedFilterMode == IndexMappingDecisionFilterMode.RemovedByMask;
    public bool IsFilterAmbiguousActive => SelectedFilterMode == IndexMappingDecisionFilterMode.Ambiguous;
    public bool IsFilterDuplicateDiffActive => SelectedFilterMode == IndexMappingDecisionFilterMode.DuplicateDiff;
    public bool IsFilterLowConfidenceActive => SelectedFilterMode == IndexMappingDecisionFilterMode.LowConfidence;
    public bool IsFilterUnmappedActive => SelectedFilterMode == IndexMappingDecisionFilterMode.Unmapped;
    public bool IsFilterCountMismatchActive => SelectedFilterMode == IndexMappingDecisionFilterMode.CountMismatch;
    public bool HasVerificationSteps => VerificationSteps.Count > 0;
    public bool HasAlgorithmTraceEntries => AlgorithmTraceEntries.Count > 0;
    public bool IsAlgorithmTraceNotRequested => AlgorithmTraceState == IndexMappingAlgorithmTraceState.NotRequested;
    public bool IsAlgorithmTraceLoading => AlgorithmTraceState == IndexMappingAlgorithmTraceState.Loading;
    public bool IsAlgorithmTraceReady => AlgorithmTraceState == IndexMappingAlgorithmTraceState.Ready;
    public bool IsAlgorithmTraceStale => AlgorithmTraceState == IndexMappingAlgorithmTraceState.Stale;
    public bool IsAlgorithmTraceFailed => AlgorithmTraceState == IndexMappingAlgorithmTraceState.Failed;
    public string ActiveVerificationStepText => ActiveVerificationStep switch
    {
        IndexMappingVerificationStep.Step3 => "Step 3",
        IndexMappingVerificationStep.Step4 => "Step 4",
        IndexMappingVerificationStep.Step5 => "Step 5",
        IndexMappingVerificationStep.Step6 => "Step 6",
        _ => "Simulation",
    };
    public string TableRevisionText => $"rev {TableRevision.ToString(CultureInfo.InvariantCulture)}";
    public string SelectedFilterLabel => SelectedFilterMode switch
    {
        IndexMappingDecisionFilterMode.ChangedByMask => "Changed by mask",
        IndexMappingDecisionFilterMode.RemovedByMask => "Removed by mask",
        IndexMappingDecisionFilterMode.Ambiguous => "Ambiguous",
        IndexMappingDecisionFilterMode.DuplicateDiff => "Duplicate diff",
        IndexMappingDecisionFilterMode.LowConfidence => "Low confidence",
        IndexMappingDecisionFilterMode.Unmapped => "Unmapped",
        IndexMappingDecisionFilterMode.CountMismatch => "Count mismatch",
        _ => "all diagnostics",
    };
    public string HeaderSubtitle => $"Open issues {OpenIssueCount} · Active {ActiveVerificationStepText} · {TableRevisionText}";
    public string HeaderAuditSummaryText => BuildHeaderAuditSummaryText();
    public bool HasHeaderAuditSummary => HeaderAuditSummaryText.Length > 0;
    public bool IsStep3Active => ActiveVerificationStep == IndexMappingVerificationStep.Step3;
    public bool IsStep4Active => ActiveVerificationStep == IndexMappingVerificationStep.Step4;
    public bool IsStep5Active => ActiveVerificationStep == IndexMappingVerificationStep.Step5;
    public bool IsStep6Active => ActiveVerificationStep == IndexMappingVerificationStep.Step6;
    public bool IsSimulationActive => ActiveVerificationStep == IndexMappingVerificationStep.Simulation;
    public bool ShowStep3ActionButtons => IsStep3Active || IsStep6Active;
    public bool ShowStep4ActionButtons => IsStep4Active;
    public bool ShowStep5ActionButtons => IsStep5Active;
    public bool ShowSimulationActionButtons => IsSimulationActive;
    public bool CanLocateSelectedDecision => SelectedDecision?.CanLocate ?? false;
    public string ActiveStepCardTitle => GetActiveStepNode()?.Title ?? ActiveVerificationStepText;
    public string ActiveStepCardStatusText => $"status: {GetActiveStepNode()?.StatusText ?? OverallStatusText}";
    public string ActiveStepWhatToVerifyText => GetActiveStepNode()?.VerifyHint ?? "Select step.";
    public string ActiveStepKeyChecksText => GetActiveStepNode()?.KeyChecks ?? "key checks: n/a";
    public string ActiveStepOpenActionText => GetActiveStepNode()?.OpenActionText ?? "open action: review selected row.";
    public string ActiveStepInputText => BuildActiveStepInputText();
    public string ActiveStepDecisionText => BuildActiveStepDecisionText();
    public string ActiveStepOutputText => BuildActiveStepOutputText();
    public string ActiveStepImpactText => BuildActiveStepImpactText();
    public string ActiveStepActionText => BuildActiveStepActionText();
    public string SelectedDecisionKeyText => SelectedDecision?.Title ?? "No row selected.";
    public string SelectedDecisionSubtitleText => SelectedDecision?.Subtitle ?? "Select a row in the verification table.";
    public string SelectedEntityText => BuildSelectedEntityText();
    public string SelectedJourneyText => BuildSelectedJourneyText();
    public string Step4ModeText => $"mode: {SelectedDecision?.DecisionModeText ?? "n/a"}";
    public string Step4ReasonCodeText => $"reasonCode: {SelectedDecision?.DecisionReasonCodeText ?? "n/a"}";
    public string Step4DecisionSourceText => $"decisionSource: {SelectedDecision?.DecisionSourceText ?? "n/a"}";
    public string Step4ReassignedByText => $"reassignedBy: {SelectedDecision?.DecisionSourceText ?? "n/a"} / {SelectedDecision?.DecisionReasonCodeText ?? "n/a"}";
    public string Step4WeightBreakdownText => SelectedDecision?.CoverageText is { Length: > 0 } coverage
        ? $"weight: {coverage}"
        : "weight: not instrumented for this row";
    public string Step4RawBestText => SelectedDecision?.RawDetailText ?? "Raw geometry seed: n/a";
    public string Step4MaskedBestText => SelectedDecision?.MaskedDetailText ?? "Masked geometry seed: n/a";
    public string Step4AssignedText => $"primaryAssigned: {SelectedDecision?.CurrentDisplay ?? "-"}";
    public string Step4PassiveCompensationText => SelectedDecision?.PassiveCompensationDiffIndex is int passiveDiff
        ? $"passiveCompensationDiff: diff {passiveDiff}"
        : "passiveCompensationDiff: none";
    public string Step4RepairSuggestionText => SelectedDecision?.ApplyDiffIndex is int repairDiff
        ? $"repairSuggestion: diff {repairDiff}"
        : "repairSuggestion: none";
    public string Step4ConfidenceText => SelectedDecision?.DecisionConfidence is double confidence
        ? $"confidence: {confidence.ToString("P1", CultureInfo.InvariantCulture)}"
        : "confidence: n/a";
    public string Step4OffsetSupportText => SelectedDecision?.OffsetSupportText ?? "Offset support: n/a";
    public string Step4CandidateAttributionTitle => $"candidate attribution ({Step4CandidateAttributionLines.Count.ToString(CultureInfo.InvariantCulture)})";
    public IReadOnlyList<string> Step4CandidateAttributionLines => SelectedDecision?.CandidateLines ?? Array.Empty<string>();
    public string Step5RowNumberText => $"row: {(SelectedDecision?.RowIndex?.ToString(CultureInfo.InvariantCulture) ?? "n/a")}";
    public string Step5VersionText => $"version: {SelectedDecision?.DecisionModeText ?? "n/a"}";
    public string Step5IcDiffText => $"IC: {(SelectedDecision?.IcIndex?.ToString(CultureInfo.InvariantCulture) ?? "n/a")} · diff: {SelectedDecision?.CurrentDisplay ?? "-"}";
    public string Step5RegularCadText => $"regular: {(SelectedDecision?.LocateRegularPadIndex?.ToString(CultureInfo.InvariantCulture) ?? "n/a")} · CAD: {(SelectedDecision?.CadPadId?.ToString(CultureInfo.InvariantCulture) ?? "n/a")}";
    public string Step5PayloadSummaryText => SelectedDecision?.DecisionContractText ?? "payload: n/a";
    public string Step5StatusText => $"status: {SelectedDecision?.StatusDisplay ?? "n/a"}";
    public string Step6ValidationRegularText => $"validationRegularPadId: {(SelectedDecision?.LocateRegularPadIndex?.ToString(CultureInfo.InvariantCulture) ?? "n/a")}";
    public string Step6TraceCountText => $"traceCount: {SelectedDecision?.CandidateLines.Count.ToString(CultureInfo.InvariantCulture) ?? "0"}";
    public string Step6TraceDirectionText => BuildStep6TraceDirectionText();
    public string SimulationViewStateText => $"views: Before / After / Delta / Changed only ({ActiveVerificationStepText})";
    public string SimulationColorStateText => $"color mode: AUTO / TH ({SelectedFilterLabel})";
    public string SimulationScaleText => $"color scale: full visible min/max, rows {VisibleDecisionCount}/{TotalDecisionCount}";
    public string SimulationImpactText => SelectedDecision is null
        ? "impact: select a row to inspect linked regular/notch rows."
        : $"impact: linked CAD {SelectedDecision.CadPadId?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}, suggested {SelectedDecision.SuggestedDisplay}";
    public string SimulationScopeText => $"scope: {SelectedFilterLabel}, visible rows {VisibleDecisionCount}/{TotalDecisionCount}";
    public string StepFilterSyncText => SyncFilterWithStepRail ? "Step sync on" : "Step sync off";

    [ObservableProperty] private IReadOnlyList<IndexMappingReportIssueViewModel> _issues = Array.Empty<IndexMappingReportIssueViewModel>();
    [ObservableProperty] private IReadOnlyList<IndexMappingMaskAuditRowViewModel> _maskAuditRows = Array.Empty<IndexMappingMaskAuditRowViewModel>();
    [ObservableProperty] private IReadOnlyList<IndexMappingDecisionRowViewModel> _decisionRows = Array.Empty<IndexMappingDecisionRowViewModel>();
    [ObservableProperty] private IReadOnlyList<IndexMappingDecisionRowViewModel> _aggregateDecisionRows = Array.Empty<IndexMappingDecisionRowViewModel>();
    [ObservableProperty] private IndexMappingReportIssueViewModel? _selectedIssue;
    [ObservableProperty] private IndexMappingDecisionRowViewModel? _selectedDecision;
    [ObservableProperty] private string _actionStatus = string.Empty;
    [ObservableProperty] private bool _showCountMismatchIssues = true;
    [ObservableProperty] private bool _showUnmappedCadIssues = true;
    [ObservableProperty] private bool _showUnmappedRegularIssues = true;
    [ObservableProperty] private bool _showLowConfidenceIssues = true;
    [ObservableProperty] private bool _showAmbiguousIssues = true;
    [ObservableProperty] private bool _showDuplicateDiffIssues = true;
    [ObservableProperty] private bool _showChangedMaskRowsOnly = true;
    [ObservableProperty] private string _searchWatermarkText = "Search key / reason / source / diff";
    [ObservableProperty] private string _searchKeyword = string.Empty;
    [ObservableProperty] private IndexMappingDecisionFilterMode _selectedFilterMode = IndexMappingDecisionFilterMode.All;
    [ObservableProperty] private IndexMappingVerificationStep _activeVerificationStep = IndexMappingVerificationStep.Step4;
    [ObservableProperty] private IReadOnlyList<IndexMappingVerificationStepNodeViewModel> _verificationSteps = Array.Empty<IndexMappingVerificationStepNodeViewModel>();
    [ObservableProperty] private IndexMappingAlgorithmTraceState _algorithmTraceState = IndexMappingAlgorithmTraceState.NotRequested;
    [ObservableProperty] private string _algorithmTraceStatusText = "Trace: not requested.";
    [ObservableProperty] private IReadOnlyList<IndexMappingAlgorithmTraceEntryViewModel> _algorithmTraceEntries = Array.Empty<IndexMappingAlgorithmTraceEntryViewModel>();
    [ObservableProperty] private bool _isAlgorithmTraceExpanded;
    [ObservableProperty] private bool _syncFilterWithStepRail;
    [ObservableProperty] private int _tableRevision = 1;

    public bool HasSelectedIssue => SelectedIssue is not null;
    public bool HasSelectedDecision => SelectedDecision is not null;
    public bool CanApplySelectedIssueOverride => SelectedDecision?.CanApplyOverride ?? false;
    public bool CanApplySelectedDiffOverride => SelectedDecision?.CanApplyDiffOverride ?? false;
    public bool CanApplySelectedSegmentDiffOverrides => GetSelectedSegmentRepairRows().Count > 0;
    public bool CanApplyCadOutputFwDiffOverrides => GetVisibleRepairRows().Count > 0;
    public string SelectedSegmentPreviewText => BuildSelectedSegmentPreviewText();
    public bool CanClearSelectedIssueOverride => SelectedDecision?.CanClearOverride ?? false;

    public IRelayCommand PreviousIssueCommand { get; }
    public IRelayCommand NextIssueCommand { get; }
    public IRelayCommand LocateSelectedIssueCommand { get; }
    public IRelayCommand ApplyOverrideCommand { get; }
    public IRelayCommand ApplyDiffOverrideCommand { get; }
    public IRelayCommand ApplySegmentDiffOverridesCommand { get; }
    public IRelayCommand ApplyCadOutputFwDiffOverridesCommand { get; }
    public IRelayCommand ClearOverrideCommand { get; }
    public IRelayCommand ClearSearchKeywordCommand { get; }
    public IRelayCommand SetDecisionFilterModeCommand { get; }
    public IRelayCommand<IndexMappingDecisionRowViewModel?> SelectDecisionCommand { get; }
    public IRelayCommand<IndexMappingVerificationStep> SelectVerificationStepCommand { get; }

}
