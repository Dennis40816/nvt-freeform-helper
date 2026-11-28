// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.ViewModels;

public sealed partial class IndexMappingReportViewModel
{
    public string OverallStatusSummaryText => $"Status: {OverallStatusText}";
    public string OpenIssueSummaryText => $"Open issues: {OpenIssueCount}";
    public string AllFilterText => $"All {TotalDecisionCount}";
    public string ChangedFilterText => $"Changed by mask {ChangedByMaskDecisionCount}";
    public string RemovedFilterText => $"Removed by mask {RemovedByMaskDecisionCount}";
    public string AmbiguousFilterText => $"Ambiguous {AmbiguousDecisionCount}";
    public string DuplicateFilterText => $"Duplicate {DuplicateDiffDecisionCount}";
    public string LowConfidenceFilterText => $"Low confidence {LowConfidenceDecisionCount}";
    public string UnmappedFilterText => $"Unmapped {UnmappedDecisionCount}";
    public string MismatchFilterText => $"Mismatch {CountMismatchDecisionCount}";
    public bool IsAllFilterEmpty => TotalDecisionCount == 0;
    public bool IsUnmappedFilterEmpty => UnmappedDecisionCount == 0;
    public bool IsLowConfidenceFilterEmpty => LowConfidenceDecisionCount == 0;
    public bool IsAmbiguousFilterEmpty => AmbiguousDecisionCount == 0;
    public bool IsMismatchFilterEmpty => CountMismatchDecisionCount == 0;
    public bool IsChangedFilterEmpty => ChangedByMaskDecisionCount == 0;
    public bool IsRemovedFilterEmpty => RemovedByMaskDecisionCount == 0;
    public bool IsDuplicateFilterEmpty => DuplicateDiffDecisionCount == 0;
    public string ActiveStepFieldsTitle => $"{ActiveVerificationStepText} detail";
    public string SelectedMappingTargetText => SelectedDecision is { CadPadId: not null, LocateRegularPadIndex: int regularId }
        ? $"{SelectedDecisionKeyText} · REG {regularId}"
        : SelectedDecisionKeyText;
    public string SelectedDecisionSourceText => SelectedDecision?.DecisionSourceText ?? "n/a";
    public string SelectedDecisionConfidenceText => SelectedDecision?.ConfidenceDisplayText ?? "-";

    // Attribution is already shown next to the impact, including when the trace is open.
    public IReadOnlyList<IndexMappingAlgorithmTraceEntryViewModel> AlgorithmTraceDetailEntries =>
        AlgorithmTraceEntries.Where(static entry => entry.Label is not ("source" or "confidence")).ToArray();

    partial void OnAlgorithmTraceEntriesChanged(IReadOnlyList<IndexMappingAlgorithmTraceEntryViewModel> value)
    {
        OnPropertyChanged(nameof(AlgorithmTraceDetailEntries));
    }
}
