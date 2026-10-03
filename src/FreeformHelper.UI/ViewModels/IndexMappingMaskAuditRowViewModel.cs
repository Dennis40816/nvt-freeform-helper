using System.Globalization;
using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed class IndexMappingMaskAuditRowViewModel
{
    public IndexMappingMaskAuditRowViewModel(DxfRegularMaskAuditRow row, Func<int, int?>? getOverrideRegularIndex = null)
    {
        CadPadId = row.CadPadId;
        OrderedCadIndex = row.OrderedCadIndex;
        RawRegularPadIndex = row.RawSeed.RegularPadIndex;
        RawDiffIndex = row.RawSeed.BestMatchFwDiffIndex;
        RawRegularCoverage = row.RawSeed.RegularCoverage;
        RawCadCoverage = row.RawSeed.CadCoverage;
        MaskedRegularPadIndex = row.MaskedSeed.RegularPadIndex;
        MaskedDiffIndex = row.MaskedSeed.BestMatchFwDiffIndex;
        CsvConfirmedCandidates = row.CsvConfirmedCandidates;
        CsvConfirmedConfidence = row.CsvConfirmedConfidence;
        SuggestedDiffIndex = row.SuggestedDiffIndex;
        CurrentAssignedDiffIndex = row.CurrentAssignedDiffIndex;
        PrimaryAssignedDiffIndex = row.PrimaryAssignedDiffIndex ?? row.CurrentAssignedDiffIndex;
        PassiveCompensationDiffIndex = row.PassiveCompensationDiffIndex;
        RepairSuggestionDiffIndex = row.RepairSuggestionDiffIndex ?? row.SuggestedDiffIndex;
        Mode = row.Mode;
        ReasonCode = row.ReasonCode;
        DecisionSource = row.DecisionSource;
        GeometryCandidates = row.GeometryCandidates ?? Array.Empty<DxfRegularMaskAuditCandidate>();
        RepairCandidateCount = row.RepairCandidateCount > 0
            ? row.RepairCandidateCount
            : (Mode == CadOutputFwDiffAssignmentMode.CsvConstrained
                ? CsvConfirmedCandidates.Count
                : GeometryCandidates.Count);
        DetectedOffset = row.DetectedOffset;
        OffsetSupportCount = row.OffsetSupportCount;
        OffsetSupportRatio = row.OffsetSupportRatio;
        SegmentConfidence = row.SegmentConfidence;
        IcIndex = row.IcIndex;
        RowIndex = row.RowIndex;
        SegmentIndex = row.SegmentIndex;
        SegmentMemberCount = row.SegmentMemberCount;
        Status = row.Status;
        Message = row.Message;
        OverrideRegularPadIndex = getOverrideRegularIndex?.Invoke(row.CadPadId);
    }

    public int CadPadId { get; }
    public int OrderedCadIndex { get; }
    public int? RawRegularPadIndex { get; }
    public int? RawDiffIndex { get; }
    public double? RawRegularCoverage { get; }
    public double? RawCadCoverage { get; }
    public int? MaskedRegularPadIndex { get; }
    public int? MaskedDiffIndex { get; }
    public int? CurrentAssignedDiffIndex { get; }
    public int? PrimaryAssignedDiffIndex { get; }
    public int? PassiveCompensationDiffIndex { get; }
    public int? RepairSuggestionDiffIndex { get; }
    public CadOutputFwDiffAssignmentMode Mode { get; }
    public CadOutputFwDiffAssignmentReasonCode ReasonCode { get; }
    public CadOutputFwDiffAssignmentDecisionSource DecisionSource { get; }
    public DxfRegularMaskAuditStatus Status { get; }
    public string Message { get; }
    public bool IsChangedByMask => Status == DxfRegularMaskAuditStatus.ChangedByMask;
    public bool IsRemovedByMask => Status == DxfRegularMaskAuditStatus.MaskRemovedMatch;
    public bool IsDecisionRelevant => Status != DxfRegularMaskAuditStatus.Unchanged;
    public int CsvConfirmedCandidateCount => CsvConfirmedCandidates.Count;
    public IReadOnlyList<DxfRegularMaskAuditCandidate> CsvConfirmedCandidates { get; }
    public IReadOnlyList<DxfRegularMaskAuditCandidate> GeometryCandidates { get; }
    public double? CsvConfirmedConfidence { get; }
    public int? SuggestedDiffIndex { get; }
    public int RepairCandidateCount { get; }
    public int? DetectedOffset { get; }
    public int OffsetSupportCount { get; }
    public double? OffsetSupportRatio { get; }
    public double? SegmentConfidence { get; }
    public int? IcIndex { get; }
    public int? RowIndex { get; }
    public int? SegmentIndex { get; }
    public int SegmentMemberCount { get; }
    public int? OverrideRegularPadIndex { get; }
    public int? PreferredLocateRegularPadIndex => MaskedRegularPadIndex ?? RawRegularPadIndex ?? (CsvConfirmedCandidates.Count > 0 ? (int?)CsvConfirmedCandidates[0].RegularPadIndex : null);
    public int? PreferredApplyRegularPadIndex => MaskedRegularPadIndex ?? (CsvConfirmedCandidates.Count > 0 ? (int?)CsvConfirmedCandidates[0].RegularPadIndex : null) ?? RawRegularPadIndex;
    public string StatusText => Status switch
    {
        DxfRegularMaskAuditStatus.ChangedByMask => "Changed",
        DxfRegularMaskAuditStatus.MaskRemovedMatch => "Removed",
        DxfRegularMaskAuditStatus.NoRawBestMatch => "No raw geometry seed",
        _ => "Unchanged",
    };
    public string Display =>
        $"CAD {CadPadId}: raw REG {FormatRegular(RawRegularPadIndex, RawDiffIndex)} -> masked REG {FormatRegular(MaskedRegularPadIndex, MaskedDiffIndex)}";
    public string CoverageText =>
        RawRegularCoverage is double regularCoverage && RawCadCoverage is double cadCoverage
            ? $"Coverage: regular={regularCoverage:P1}, cad={cadCoverage:P1}"
            : string.Empty;
    public bool HasCoverageText => CoverageText.Length > 0;
    public string ModeText => Mode.ToContractString();
    public string ReasonCodeText => ReasonCode.ToContractString();
    public string DecisionSourceText => DecisionSource.ToContractString();
    public string AssignmentText => PrimaryAssignedDiffIndex is int diff
        ? $"Primary diff idx: {diff}"
        : "Primary diff idx: none";
    public string PassiveCompensationText => PassiveCompensationDiffIndex is int diff
        ? $"Passive compensation diff idx: {diff}"
        : "Passive compensation diff idx: none";
    public string ConfirmationText => CsvConfirmedCandidateCount == 0
        ? "CSV-confirmed candidates: none"
        : $"CSV-confirmed candidates: {CsvConfirmedCandidateCount}, confidence={CsvConfirmedConfidence.GetValueOrDefault():P1}";
    public string SuggestionText => SuggestedDiffIndex is int diff
        ? $"Suggested diff idx: {diff}"
        : "Suggested diff idx: none";
    public string RepairSuggestionText => RepairSuggestionDiffIndex is int diff
        ? $"Repair suggestion diff idx: {diff}"
        : "Repair suggestion diff idx: none";
    public string SegmentOffsetText => DetectedOffset is int offset
        ? $"Segment offset: {offset:+#;-#;0}, support={OffsetSupportCount}, ratio={OffsetSupportRatio.GetValueOrDefault():P1}, confidence={SegmentConfidence.GetValueOrDefault():P1}"
        : "Segment offset: none";
    public string SegmentKeyText =>
        IcIndex is int icIndex && RowIndex is int rowIndex && SegmentIndex is int segmentIndex
            ? $"IC {icIndex}, row {rowIndex}, segment {segmentIndex}"
            : "IC/row/segment: n/a";
    public string DecisionSummaryText =>
        $"reason={ReasonCodeText}, source={DecisionSourceText}, mode={ModeText}, repairCandidates={RepairCandidateCount}, segment={SegmentKeyText}, size={SegmentMemberCount}, offset={DetectedOffset?.ToString(CultureInfo.InvariantCulture) ?? "none"}, support={OffsetSupportCount}, ratio={(OffsetSupportRatio.HasValue ? OffsetSupportRatio.Value.ToString("P1", CultureInfo.InvariantCulture) : "n/a")}";
    public string RawDetailText => RawRegularPadIndex is int rawReg && RawDiffIndex is int rawDiff
        ? $"Raw geometry seed: REG {rawReg} / diff {rawDiff}"
        : "Raw geometry seed: none";
    public string MaskedDetailText => MaskedRegularPadIndex is int maskedReg && MaskedDiffIndex is int maskedDiff
        ? $"Masked geometry seed: REG {maskedReg} / diff {maskedDiff}"
        : "Masked geometry seed: none";
    public string SuggestedDetailText => PreferredApplyRegularPadIndex is int suggestedReg && SuggestedDiffIndex is int suggestedDiff
        ? $"Suggested: REG {suggestedReg} / diff {suggestedDiff}"
        : SuggestedDiffIndex is int diff
            ? $"Suggested diff idx: {diff}"
            : "Suggested: none";

    private static string FormatRegular(int? regularPadIndex, int? diffIndex)
    {
        if (regularPadIndex is not int reg || diffIndex is not int diff)
        {
            return "none";
        }

        return $"{reg} / diff {diff}";
    }
}
