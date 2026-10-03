using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using FreeformHelper.Application.Services;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class IndexMappingDecisionRowViewModel : ObservableObject
{
    private IndexMappingDecisionRowViewModel(
        bool isAggregateRow,
        IndexMappingDecisionFilterMode filterMode,
        int sortPriority,
        string categoryText,
        string title,
        string subtitle,
        string currentDisplay,
        string rawDisplay,
        string suggestedDisplay,
        string statusDisplay,
        string reasonText,
        string actionHint,
        string rawDetailText,
        string maskedDetailText,
        string suggestedDetailText,
        string coverageText,
        string offsetSupportText,
        IReadOnlyList<string> candidateLines,
        int? cadPadId,
        int? locateRegularPadIndex,
        int? applyRegularPadIndex,
        int? applyDiffIndex,
        int? passiveCompensationDiffIndex,
        int? icIndex,
        int? rowIndex,
        int? segmentIndex,
        int segmentMemberCount,
        double? decisionConfidence,
        string decisionModeText,
        string decisionReasonCodeText,
        string decisionSourceText,
        IndexMappingReportIssueViewModel? issue,
        int? overrideRegularPadIndex)
    {
        IsAggregateRow = isAggregateRow;
        FilterMode = filterMode;
        SortPriority = sortPriority;
        CategoryText = categoryText;
        Title = title;
        Subtitle = subtitle;
        CurrentDisplay = currentDisplay;
        RawDisplay = rawDisplay;
        SuggestedDisplay = suggestedDisplay;
        StatusDisplay = statusDisplay;
        ReasonText = reasonText;
        ActionHint = actionHint;
        RawDetailText = rawDetailText;
        MaskedDetailText = maskedDetailText;
        SuggestedDetailText = suggestedDetailText;
        CoverageText = coverageText;
        OffsetSupportText = offsetSupportText;
        CandidateLines = candidateLines;
        CadPadId = cadPadId;
        LocateRegularPadIndex = locateRegularPadIndex;
        ApplyRegularPadIndex = applyRegularPadIndex;
        ApplyDiffIndex = applyDiffIndex;
        PassiveCompensationDiffIndex = passiveCompensationDiffIndex;
        IcIndex = icIndex;
        RowIndex = rowIndex;
        SegmentIndex = segmentIndex;
        SegmentMemberCount = segmentMemberCount;
        DecisionConfidence = decisionConfidence;
        DecisionModeText = decisionModeText;
        DecisionReasonCodeText = decisionReasonCodeText;
        DecisionSourceText = decisionSourceText;
        Issue = issue;
        SetOverride(overrideRegularPadIndex);
    }

    internal static IndexMappingDecisionRowViewModel FromContract(IndexMappingDecisionRowContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        return new IndexMappingDecisionRowViewModel(
            contract.IsAggregateRow,
            contract.FilterMode,
            contract.SortPriority,
            contract.CategoryText,
            contract.Title,
            contract.Subtitle,
            contract.CurrentDisplay,
            contract.RawDisplay,
            contract.SuggestedDisplay,
            contract.StatusDisplay,
            contract.ReasonText,
            contract.ActionHint,
            contract.RawDetailText,
            contract.MaskedDetailText,
            contract.SuggestedDetailText,
            contract.CoverageText,
            contract.OffsetSupportText,
            contract.CandidateLines,
            contract.CadPadId,
            contract.LocateRegularPadIndex,
            contract.ApplyRegularPadIndex,
            contract.ApplyDiffIndex,
            contract.PassiveCompensationDiffIndex,
            contract.IcIndex,
            contract.RowIndex,
            contract.SegmentIndex,
            contract.SegmentMemberCount,
            contract.DecisionConfidence,
            contract.DecisionModeText,
            contract.DecisionReasonCodeText,
            contract.DecisionSourceText,
            contract.Issue,
            contract.OverrideRegularPadIndex);
    }

    public IndexMappingDecisionFilterMode FilterMode { get; }
    public bool IsAggregateRow { get; }
    public bool IsEntityRow => !IsAggregateRow;
    public int SortPriority { get; }
    public string CategoryText { get; }
    public string Title { get; }
    public string Subtitle { get; }
    public string CurrentDisplay { get; }
    public string RawDisplay { get; }
    public string SuggestedDisplay { get; }
    public string StatusDisplay { get; }
    public string ReasonText { get; }
    public string ActionHint { get; }
    public string RawDetailText { get; }
    public string MaskedDetailText { get; }
    public string SuggestedDetailText { get; }
    public string CoverageText { get; }
    public string OffsetSupportText { get; }
    public IReadOnlyList<string> CandidateLines { get; }
    public int? CadPadId { get; }
    public int? LocateRegularPadIndex { get; }
    public int? ApplyRegularPadIndex { get; }
    public int? ApplyDiffIndex { get; }
    public int? PassiveCompensationDiffIndex { get; }
    public int? IcIndex { get; }
    public int? RowIndex { get; }
    public int? SegmentIndex { get; }
    public int SegmentMemberCount { get; }
    public double? DecisionConfidence { get; }
    public string DecisionModeText { get; }
    public string DecisionReasonCodeText { get; }
    public string DecisionSourceText { get; }
    public IndexMappingReportIssueViewModel? Issue { get; }
    public bool HasCoverageText => CoverageText.Length > 0;
    public bool HasOffsetSupportText => OffsetSupportText.Length > 0;
    public bool HasCandidates => CandidateLines.Count > 0;
    public bool HasNoCandidates => !HasCandidates;
    public bool CanLocate => CadPadId.HasValue || LocateRegularPadIndex.HasValue;
    public bool CanApplyOverride => CadPadId.HasValue && ApplyRegularPadIndex.HasValue;
    public bool CanApplyDiffOverride => CadPadId.HasValue && ApplyDiffIndex.HasValue;
    public bool HasSegmentScope => IcIndex.HasValue && RowIndex.HasValue && SegmentIndex.HasValue;
    public bool CanClearOverride => CadPadId.HasValue;
    public string SegmentKey => HasSegmentScope
        ? $"IC {IcIndex}, row {RowIndex}, segment {SegmentIndex}"
        : "IC/row/segment: n/a";
    public string SegmentScopeText => HasSegmentScope
        ? $"Segment scope: {SegmentKey}, members={SegmentMemberCount}"
        : "Segment scope: n/a";
    public string IcDisplayText => IcIndex?.ToString(CultureInfo.InvariantCulture) ?? "-";
    public string CadDisplayText => CadPadId?.ToString(CultureInfo.InvariantCulture) ?? "-";
    public string RegularDisplayText => LocateRegularPadIndex?.ToString(CultureInfo.InvariantCulture) ?? "-";
    public string ConfidenceDisplayText => DecisionConfidence.HasValue
        ? DecisionConfidence.Value.ToString("0.00", CultureInfo.InvariantCulture)
        : "-";
    public string StepTagText => FilterMode switch
    {
        IndexMappingDecisionFilterMode.ChangedByMask or IndexMappingDecisionFilterMode.RemovedByMask => "SIM",
        IndexMappingDecisionFilterMode.DuplicateDiff => "S6",
        IndexMappingDecisionFilterMode.CountMismatch or IndexMappingDecisionFilterMode.Unmapped => "S5",
        _ => "S4",
    };
    public bool IsReasonTagIncoming => FilterMode == IndexMappingDecisionFilterMode.ChangedByMask;
    public bool IsReasonTagOutgoing => FilterMode is IndexMappingDecisionFilterMode.Ambiguous or
        IndexMappingDecisionFilterMode.LowConfidence or
        IndexMappingDecisionFilterMode.DuplicateDiff or
        IndexMappingDecisionFilterMode.RemovedByMask;
    public bool IsReasonTagNoCad => FilterMode == IndexMappingDecisionFilterMode.Unmapped;
    public bool IsReasonTagLegacy => FilterMode == IndexMappingDecisionFilterMode.CountMismatch;
    public bool IsReasonTagDirect => !IsReasonTagIncoming && !IsReasonTagOutgoing && !IsReasonTagNoCad && !IsReasonTagLegacy;
    public string DecisionContractText =>
        $"reason={DecisionReasonCodeText}, source={DecisionSourceText}, mode={DecisionModeText}, confidence={(DecisionConfidence.HasValue ? DecisionConfidence.Value.ToString("P1", CultureInfo.InvariantCulture) : "n/a")}";
    public string DiffRepairPreviewText => ApplyDiffIndex is int diff
        ? $"Diff preview: {CurrentDisplay} -> diff {diff} (passive={(PassiveCompensationDiffIndex.HasValue ? "yes" : "no")})."
        : PassiveCompensationDiffIndex is int passiveDiff
            ? $"Diff preview: no repair suggestion, passive compensation diff {passiveDiff}."
            : "Diff preview: no repair suggestion.";

    internal IndexMappingDecisionRowContract ToContract()
    {
        return new IndexMappingDecisionRowContract(
            IsAggregateRow,
            FilterMode,
            SortPriority,
            CategoryText,
            Title,
            Subtitle,
            CurrentDisplay,
            RawDisplay,
            SuggestedDisplay,
            StatusDisplay,
            ReasonText,
            ActionHint,
            RawDetailText,
            MaskedDetailText,
            SuggestedDetailText,
            CoverageText,
            OffsetSupportText,
            CandidateLines.ToArray(),
            CadPadId,
            LocateRegularPadIndex,
            ApplyRegularPadIndex,
            ApplyDiffIndex,
            PassiveCompensationDiffIndex,
            IcIndex,
            RowIndex,
            SegmentIndex,
            SegmentMemberCount,
            DecisionConfidence,
            DecisionModeText,
            DecisionReasonCodeText,
            DecisionSourceText,
            Issue,
            OverrideRegularPadIndex);
    }

    [ObservableProperty] private bool _hasOverride;
    [ObservableProperty] private int? _overrideRegularPadIndex;
    [ObservableProperty] private bool _isInspectorSelected;
    [ObservableProperty] private string _overrideText = "Override: none.";

    public static IndexMappingDecisionRowViewModel FromIssue(IndexMappingReportIssueViewModel issue)
    {
        var isAggregate = issue.Kind == DxfRegularMappingIssueKind.CountMismatch ||
            (!issue.CadPadId.HasValue && !issue.RegularPadIndex.HasValue);
        var filterMode = issue.Kind switch
        {
            DxfRegularMappingIssueKind.CountMismatch => IndexMappingDecisionFilterMode.CountMismatch,
            DxfRegularMappingIssueKind.UnmappedCad or DxfRegularMappingIssueKind.UnmappedRegular => IndexMappingDecisionFilterMode.Unmapped,
            DxfRegularMappingIssueKind.LowConfidence => IndexMappingDecisionFilterMode.LowConfidence,
            DxfRegularMappingIssueKind.Ambiguous => IndexMappingDecisionFilterMode.Ambiguous,
            DxfRegularMappingIssueKind.DuplicateDiff => IndexMappingDecisionFilterMode.DuplicateDiff,
            _ => IndexMappingDecisionFilterMode.All,
        };
        var sortPriority = filterMode switch
        {
            IndexMappingDecisionFilterMode.CountMismatch => 0,
            IndexMappingDecisionFilterMode.DuplicateDiff => 1,
            IndexMappingDecisionFilterMode.Unmapped => 2,
            IndexMappingDecisionFilterMode.Ambiguous => 3,
            IndexMappingDecisionFilterMode.LowConfidence => 4,
            _ => 9,
        };

        var title = issue.CadPadId is int cadPadId
            ? $"CAD {cadPadId}"
            : issue.RegularPadIndex is int regularPadIndex
                ? $"REG {regularPadIndex}"
                : issue.KindText;
        var subtitle = issue.Kind switch
        {
            DxfRegularMappingIssueKind.CountMismatch => "Visible CAD/Regular counts diverge.",
            DxfRegularMappingIssueKind.UnmappedCad => issue.DxfIndex is int unmappedCadDiff ? $"Current diff idx {unmappedCadDiff} has no regular match." : "CAD has no regular match.",
            DxfRegularMappingIssueKind.UnmappedRegular => issue.RegularPadIndex is int unmappedReg && issue.DiffIndex is int unmappedRegDiff ? $"REG {unmappedReg} / diff {unmappedRegDiff} has no CAD." : "Regular pad has no CAD.",
            DxfRegularMappingIssueKind.LowConfidence => issue.DiffIndex is int assignedDiff ? $"Assigned diff {assignedDiff} is below confidence threshold." : "Assigned diff is below confidence threshold.",
            DxfRegularMappingIssueKind.Ambiguous => issue.DiffIndex is int ambiguousDiff ? $"Assigned diff {ambiguousDiff} is close to another candidate." : "Assigned diff is close to another candidate.",
            DxfRegularMappingIssueKind.DuplicateDiff => issue.DxfIndex is int duplicateDiff ? $"Current diff idx {duplicateDiff} is assigned more than once." : "Current diff idx is assigned more than once.",
            _ => issue.Detail,
        };

        var topCandidate = issue.TopCandidates.Count > 0 ? issue.TopCandidates[0] : null;
        var candidateLines = issue.TopCandidates.Select(candidate => candidate.Display).ToList();

        return new IndexMappingDecisionRowViewModel(
            isAggregate,
            filterMode,
            sortPriority,
            issue.KindText,
            title,
            subtitle,
            issue.DxfIndex is int currentDiff ? $"diff {currentDiff}" : "-",
            issue.DiffIndex is int rawDiff ? $"diff {rawDiff}" : topCandidate is not null ? $"diff {topCandidate.DiffIndex}" : "-",
            topCandidate is not null ? $"diff {topCandidate.DiffIndex}" : issue.DiffIndex is int suggestedDiff ? $"diff {suggestedDiff}" : "-",
            issue.KindText,
            issue.Detail,
            issue.ActionHint,
            issue.DiffIndex is int issueDiff && issue.RegularPadIndex is int issueReg
                ? $"Raw geometry seed: REG {issueReg} / diff {issueDiff}"
                : "Raw geometry seed: none",
            "Masked geometry seed: not available for this diagnostics row.",
            topCandidate is not null
                ? $"Suggested: REG {topCandidate.RegularPadIndex} / diff {topCandidate.DiffIndex}"
                : issue.DiffIndex is int diff && issue.RegularPadIndex is int reg
                    ? $"Suggested: REG {reg} / diff {diff}"
                    : "Suggested: none",
            string.Empty,
            "Offset support: not instrumented for issue rows.",
            candidateLines,
            issue.CadPadId,
            issue.RegularPadIndex,
            issue.RegularPadIndex ?? topCandidate?.RegularPadIndex,
            applyDiffIndex: null,
            passiveCompensationDiffIndex: null,
            icIndex: null,
            rowIndex: null,
            segmentIndex: null,
            segmentMemberCount: 0,
            decisionConfidence: null,
            decisionModeText: "n/a",
            decisionReasonCodeText: issue.KindText,
            decisionSourceText: "n/a",
            issue,
            issue.OverrideRegularPadIndex);
    }

    public static IndexMappingDecisionRowViewModel FromMaskAudit(IndexMappingMaskAuditRowViewModel row)
    {
        var filterMode = row.Status switch
        {
            DxfRegularMaskAuditStatus.ChangedByMask => IndexMappingDecisionFilterMode.ChangedByMask,
            DxfRegularMaskAuditStatus.MaskRemovedMatch => IndexMappingDecisionFilterMode.RemovedByMask,
            _ => IndexMappingDecisionFilterMode.All,
        };
        var sortPriority = filterMode switch
        {
            IndexMappingDecisionFilterMode.ChangedByMask => 4,
            IndexMappingDecisionFilterMode.RemovedByMask => 5,
            _ => 8,
        };

        var candidateLines = row.CsvConfirmedCandidates
            .Select((candidate, index) => $"#{index + 1} diff{candidate.DiffIndex} (IC{candidate.IcIndex}, regId {candidate.RegularPadIndex}), regular={candidate.RegularCoverage:P1}, cad={candidate.CadCoverage:P1}")
            .ToList();
        if (row.Mode == CadOutputFwDiffAssignmentMode.GeometryOnly && row.GeometryCandidates.Count > 0)
        {
            var geometryLines = row.GeometryCandidates
                .Take(3)
                .Select((candidate, index) =>
                    $"G#{index + 1} diff{candidate.DiffIndex} (IC{candidate.IcIndex}, regId {candidate.RegularPadIndex}), regular={candidate.RegularCoverage:P1}, cad={candidate.CadCoverage:P1}");
            candidateLines.AddRange(geometryLines);
        }

        return new IndexMappingDecisionRowViewModel(
            isAggregateRow: false,
            filterMode,
            sortPriority,
            row.StatusText,
            $"CAD {row.CadPadId}",
            row.PrimaryAssignedDiffIndex is int currentDiff
                ? $"Primary diff idx {currentDiff}."
                : "Primary diff idx none.",
            row.PrimaryAssignedDiffIndex is int current ? $"diff {current}" : "-",
            row.RawDiffIndex is int rawDiff ? $"diff {rawDiff}" : "-",
            row.RepairSuggestionDiffIndex is int repairedDiff
                ? $"diff {repairedDiff}"
                : row.SuggestedDiffIndex is int suggestedDiff
                    ? $"diff {suggestedDiff}"
                    : row.MaskedDiffIndex is int maskedDiff
                        ? $"diff {maskedDiff}"
                        : "-",
            row.ReasonCodeText,
            $"{row.Message} [{row.DecisionSummaryText}]",
            row.IsRemovedByMask
                ? "Action: inspect CSV-active candidates before overriding Step 4."
                : "Action: verify whether masked geometry seed should become the CAD Output FW Diff.",
            row.RawDetailText,
            row.MaskedDetailText,
            row.SuggestedDetailText,
            row.CoverageText,
            row.SegmentOffsetText,
            candidateLines,
            row.CadPadId,
            row.PreferredLocateRegularPadIndex,
            row.PreferredApplyRegularPadIndex,
            row.RepairSuggestionDiffIndex ?? row.PassiveCompensationDiffIndex,
            row.PassiveCompensationDiffIndex,
            row.IcIndex,
            row.RowIndex,
            row.SegmentIndex,
            row.SegmentMemberCount,
            row.CsvConfirmedConfidence,
            row.ModeText,
            row.ReasonCodeText,
            row.DecisionSourceText,
            issue: null,
            row.OverrideRegularPadIndex);
    }

    public bool Matches(string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return true;
        }

        return SearchBlob.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public void SetOverride(int? regularPadIndex)
    {
        OverrideRegularPadIndex = regularPadIndex;
        if (regularPadIndex is int index)
        {
            HasOverride = true;
            OverrideText = $"Override: CAD -> regular id {index}.";
        }
        else
        {
            HasOverride = false;
            OverrideText = "Override: none.";
        }
    }

    private string SearchBlob => string.Join(
        " | ",
        new[]
        {
            CategoryText,
            Title,
            Subtitle,
            CurrentDisplay,
            RawDisplay,
            SuggestedDisplay,
            StatusDisplay,
            ReasonText,
            ActionHint,
            RawDetailText,
            MaskedDetailText,
            SuggestedDetailText,
            CoverageText,
            OffsetSupportText,
            SegmentScopeText,
            DecisionContractText,
            DiffRepairPreviewText,
            string.Join(" | ", CandidateLines),
            OverrideText,
        }.Where(part => !string.IsNullOrWhiteSpace(part)));
}
