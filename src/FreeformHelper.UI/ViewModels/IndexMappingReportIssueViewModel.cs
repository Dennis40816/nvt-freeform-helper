using CommunityToolkit.Mvvm.ComponentModel;
using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class IndexMappingReportIssueViewModel : ObservableObject
{
    public IndexMappingReportIssueViewModel(
        DxfRegularMappingIssue issue,
        Func<int, int?>? getOverrideRegularIndex = null)
    {
        Kind = issue.Kind;
        KindText = issue.Kind switch
        {
            DxfRegularMappingIssueKind.CountMismatch => "Count mismatch",
            DxfRegularMappingIssueKind.UnmappedCad => "Unmapped CAD",
            DxfRegularMappingIssueKind.UnmappedRegular => "Unmapped Regular",
            DxfRegularMappingIssueKind.LowConfidence => "Low confidence",
            DxfRegularMappingIssueKind.Ambiguous => "Ambiguous",
            DxfRegularMappingIssueKind.DuplicateDiff => "Duplicate diff",
            _ => issue.Kind.ToString(),
        };

        ActionHint = issue.Kind switch
        {
            DxfRegularMappingIssueKind.CountMismatch => "Action: verify layer filters, grid channels, and bounds layer.",
            DxfRegularMappingIssueKind.UnmappedCad => "Action: increase candidate number, check bounds, and confirm CAD visibility.",
            DxfRegularMappingIssueKind.UnmappedRegular => "Action: confirm bounds/padding and layer filters; review grid build inputs.",
            DxfRegularMappingIssueKind.LowConfidence => "Action: inspect geometry, tune weights/threshold, or add an override.",
            DxfRegularMappingIssueKind.Ambiguous => "Action: inspect best/second candidates, tune weights, or add an override.",
            DxfRegularMappingIssueKind.DuplicateDiff => "Action: inspect the duplicated current diff idx, then confirm whether one CAD should move to its raw or masked geometry seed.",
            _ => string.Empty,
        };

        Detail = issue.Message;
        DxfIndex = issue.DxfIndex;
        CadPadId = issue.CadPadId;
        RegularPadIndex = issue.RegularPadIndex;
        IcIndex = issue.IcIndex;
        DiffIndex = issue.DiffIndex;
        Score = issue.Score;
        BestScore = issue.BestScore;
        SecondScore = issue.SecondScore;
        Margin = issue.Margin;
        TopCandidates = (issue.TopCandidates ?? Array.Empty<DxfRegularMappingCandidate>())
            .Select((candidate, index) => new IndexMappingCandidateViewModel(index + 1, candidate))
            .ToList();

        if (CadPadId is int cadPadId)
        {
            SetOverride(getOverrideRegularIndex?.Invoke(cadPadId));
        }
        else
        {
            SetOverride(null);
        }
    }

    public DxfRegularMappingIssueKind Kind { get; }
    public string KindText { get; }
    public string Detail { get; }
    public string ActionHint { get; }
    public int? DxfIndex { get; }
    public int? CadPadId { get; }
    public int? RegularPadIndex { get; }
    public int? IcIndex { get; }
    public int? DiffIndex { get; }
    public double? Score { get; }
    public double? BestScore { get; }
    public double? SecondScore { get; }
    public double? Margin { get; }
    public IReadOnlyList<IndexMappingCandidateViewModel> TopCandidates { get; }

    [ObservableProperty] private int? _overrideRegularPadIndex;

    public void SetOverride(int? regularPadIndex)
    {
        OverrideRegularPadIndex = regularPadIndex;
    }
}
