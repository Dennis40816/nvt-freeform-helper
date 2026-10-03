using FreeformHelper.Application.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class IndexMappingReportViewModelTests
{


    [Fact]
    public void SelectDecisionCommand_UpdatesInspectorActiveRow_WithoutLocate()
    {
        var issue = new DxfRegularMappingIssue(
            DxfRegularMappingIssueKind.LowConfidence,
            "cad 777",
            DxfIndex: 77,
            CadPadId: 777,
            RegularPadIndex: 88,
            DiffIndex: 88);
        var report = new DxfRegularMappingReport(
            CadCount: 1,
            RegularCount: 1,
            MappedCount: 1,
            UnmappedCadCount: 0,
            UnmappedRegularCount: 0,
            LowConfidenceCount: 1,
            AmbiguousCount: 0,
            HasIssues: true,
            Summary: "summary",
            TotalIssueCount: 1,
            IssuesTruncated: false,
            Issues: new[] { issue },
            SampleIssues: new[] { issue });

        var vm = new IndexMappingReportViewModel("summary", report);
        var row = Assert.Single(vm.DecisionRows);

        vm.SelectDecisionCommand.Execute(row);

        Assert.Same(row, vm.SelectedDecision);
        Assert.True(row.IsInspectorSelected);
        Assert.Equal(string.Empty, vm.ActionStatus);
    }


    [Fact]
    public void SelectDecisionCommand_LocatesImmediately_WhenLocateHandlerAvailable()
    {
        var issue = new DxfRegularMappingIssue(
            DxfRegularMappingIssueKind.DuplicateDiff,
            "Current diff idx 324 is shared by CAD 4823, 4848.",
            DxfIndex: 324,
            CadPadId: 4823,
            RegularPadIndex: 3844,
            DiffIndex: 324);
        var report = new DxfRegularMappingReport(
            CadCount: 2,
            RegularCount: 2,
            MappedCount: 2,
            UnmappedCadCount: 0,
            UnmappedRegularCount: 0,
            LowConfidenceCount: 0,
            AmbiguousCount: 0,
            HasIssues: true,
            Summary: "summary",
            TotalIssueCount: 1,
            IssuesTruncated: false,
            Issues: new[] { issue },
            SampleIssues: new[] { issue },
            DuplicateDiffCount: 1);

        var locateCalls = 0;
        var vm = new IndexMappingReportViewModel(
            "summary",
            report,
            locateTarget: (cadId, regularId) =>
            {
                locateCalls++;
                return cadId == 4823 && regularId == 3844;
            });
        var row = Assert.Single(vm.DecisionRows);

        vm.SelectDecisionCommand.Execute(row);

        Assert.Same(row, vm.SelectedDecision);
        Assert.Equal(1, locateCalls);
        Assert.Equal("Diagnostic row focused on canvas.", vm.ActionStatus);
    }


    [Fact]
    public void ApplyDiffOverrideCommand_UsesRepairSuggestionDiff_WhenAvailable()
    {
        var report = new DxfRegularMappingReport(
            CadCount: 1,
            RegularCount: 1,
            MappedCount: 1,
            UnmappedCadCount: 0,
            UnmappedRegularCount: 0,
            LowConfidenceCount: 0,
            AmbiguousCount: 0,
            HasIssues: false,
            Summary: "summary",
            TotalIssueCount: 0,
            IssuesTruncated: false,
            Issues: Array.Empty<DxfRegularMappingIssue>(),
            SampleIssues: Array.Empty<DxfRegularMappingIssue>(),
            MaskAuditRows: new[]
            {
                new DxfRegularMaskAuditRow(
                    CadPadId: 9011,
                    OrderedCadIndex: 0,
                    RawSeed: new CadBestMatchSeed(411, 0, 20, 0.91, 0.89),
                    MaskedSeed: new CadBestMatchSeed(412, 0, 21, 0.72, 0.70),
                    CsvConfirmedCandidates: new[]
                    {
                        new DxfRegularMaskAuditCandidate(412, 0, 21, 0.72, 0.70),
                    },
                    CsvConfirmedConfidence: 1.0,
                    SuggestedDiffIndex: 21,
                    CurrentAssignedDiffIndex: 25,
                    Status: DxfRegularMaskAuditStatus.ChangedByMask,
                    Message: "changed",
                    Mode: CadOutputFwDiffAssignmentMode.CsvConstrained,
                    ReasonCode: CadOutputFwDiffAssignmentReasonCode.SegmentOffsetSuspected,
                    DecisionSource: CadOutputFwDiffAssignmentDecisionSource.Seed,
                    PrimaryAssignedDiffIndex: 25,
                    PassiveCompensationDiffIndex: null,
                    RepairSuggestionDiffIndex: 22),
            },
            MaskChangedCount: 1,
            MaskRemovedCount: 0);

        var applyCalls = new List<(int CadId, int DiffIndex)>();
        var vm = new IndexMappingReportViewModel(
            "summary",
            report,
            applyDiffOverride: (cadId, diffIndex) =>
            {
                applyCalls.Add((cadId, diffIndex));
                return true;
            });
        var row = Assert.Single(vm.DecisionRows);
        vm.SelectDecisionCommand.Execute(row);

        Assert.True(vm.CanApplySelectedDiffOverride);

        vm.ApplyDiffOverrideCommand.Execute(null);

        var applied = Assert.Single(applyCalls);
        Assert.Equal(9011, applied.CadId);
        Assert.Equal(22, applied.DiffIndex);
        Assert.Equal("Diff repair override applied.", vm.ActionStatus);
    }


    [Fact]
    public void ApplySegmentDiffOverridesCommand_AppliesRowsInSelectedSegment()
    {
        var report = new DxfRegularMappingReport(
            CadCount: 3,
            RegularCount: 3,
            MappedCount: 3,
            UnmappedCadCount: 0,
            UnmappedRegularCount: 0,
            LowConfidenceCount: 0,
            AmbiguousCount: 0,
            HasIssues: false,
            Summary: "summary",
            TotalIssueCount: 0,
            IssuesTruncated: false,
            Issues: Array.Empty<DxfRegularMappingIssue>(),
            SampleIssues: Array.Empty<DxfRegularMappingIssue>(),
            MaskAuditRows: new[]
            {
                new DxfRegularMaskAuditRow(
                    CadPadId: 9101,
                    OrderedCadIndex: 0,
                    RawSeed: new CadBestMatchSeed(501, 1, 100, 0.91, 0.89),
                    MaskedSeed: new CadBestMatchSeed(501, 1, 100, 0.91, 0.89),
                    CsvConfirmedCandidates: new[] { new DxfRegularMaskAuditCandidate(501, 1, 100, 0.91, 0.89) },
                    CsvConfirmedConfidence: 0.92,
                    SuggestedDiffIndex: 100,
                    CurrentAssignedDiffIndex: 98,
                    Status: DxfRegularMaskAuditStatus.ChangedByMask,
                    Message: "row1",
                    Mode: CadOutputFwDiffAssignmentMode.CsvConstrained,
                    ReasonCode: CadOutputFwDiffAssignmentReasonCode.SegmentOffsetSuspected,
                    DecisionSource: CadOutputFwDiffAssignmentDecisionSource.AutoRepaired,
                    PrimaryAssignedDiffIndex: 98,
                    PassiveCompensationDiffIndex: null,
                    RepairSuggestionDiffIndex: 100,
                    IcIndex: 1,
                    RowIndex: 7,
                    SegmentIndex: 0,
                    SegmentMemberCount: 3),
                new DxfRegularMaskAuditRow(
                    CadPadId: 9102,
                    OrderedCadIndex: 1,
                    RawSeed: new CadBestMatchSeed(502, 1, 101, 0.90, 0.87),
                    MaskedSeed: new CadBestMatchSeed(502, 1, 101, 0.90, 0.87),
                    CsvConfirmedCandidates: new[] { new DxfRegularMaskAuditCandidate(502, 1, 101, 0.90, 0.87) },
                    CsvConfirmedConfidence: 0.90,
                    SuggestedDiffIndex: 101,
                    CurrentAssignedDiffIndex: 99,
                    Status: DxfRegularMaskAuditStatus.ChangedByMask,
                    Message: "row2",
                    Mode: CadOutputFwDiffAssignmentMode.CsvConstrained,
                    ReasonCode: CadOutputFwDiffAssignmentReasonCode.SegmentOffsetSuspected,
                    DecisionSource: CadOutputFwDiffAssignmentDecisionSource.AutoRepaired,
                    PrimaryAssignedDiffIndex: 99,
                    PassiveCompensationDiffIndex: null,
                    RepairSuggestionDiffIndex: 101,
                    IcIndex: 1,
                    RowIndex: 7,
                    SegmentIndex: 0,
                    SegmentMemberCount: 3),
                new DxfRegularMaskAuditRow(
                    CadPadId: 9103,
                    OrderedCadIndex: 2,
                    RawSeed: new CadBestMatchSeed(503, 1, 110, 0.88, 0.83),
                    MaskedSeed: new CadBestMatchSeed(503, 1, 110, 0.88, 0.83),
                    CsvConfirmedCandidates: new[] { new DxfRegularMaskAuditCandidate(503, 1, 110, 0.88, 0.83) },
                    CsvConfirmedConfidence: 0.88,
                    SuggestedDiffIndex: 110,
                    CurrentAssignedDiffIndex: 108,
                    Status: DxfRegularMaskAuditStatus.ChangedByMask,
                    Message: "row3",
                    Mode: CadOutputFwDiffAssignmentMode.CsvConstrained,
                    ReasonCode: CadOutputFwDiffAssignmentReasonCode.SegmentOffsetSuspected,
                    DecisionSource: CadOutputFwDiffAssignmentDecisionSource.AutoRepaired,
                    PrimaryAssignedDiffIndex: 108,
                    PassiveCompensationDiffIndex: null,
                    RepairSuggestionDiffIndex: 110,
                    IcIndex: 1,
                    RowIndex: 7,
                    SegmentIndex: 1,
                    SegmentMemberCount: 1),
            },
            MaskChangedCount: 3,
            MaskRemovedCount: 0);

        var applyCalls = new List<(int CadId, int DiffIndex)>();
        var vm = new IndexMappingReportViewModel(
            "summary",
            report,
            applyDiffOverride: (cadId, diffIndex) =>
            {
                applyCalls.Add((cadId, diffIndex));
                return true;
            });
        var selected = Assert.Single(vm.DecisionRows, row => row.CadPadId == 9101);
        vm.SelectDecisionCommand.Execute(selected);

        Assert.True(vm.CanApplySelectedSegmentDiffOverrides);
        Assert.Contains("repair rows 2/3", vm.SelectedSegmentPreviewText, StringComparison.OrdinalIgnoreCase);

        vm.ApplySegmentDiffOverridesCommand.Execute(null);

        Assert.Equal(2, applyCalls.Count);
        Assert.Contains(applyCalls, call => call.CadId == 9101 && call.DiffIndex == 100);
        Assert.Contains(applyCalls, call => call.CadId == 9102 && call.DiffIndex == 101);
        Assert.DoesNotContain(applyCalls, call => call.CadId == 9103);
        Assert.Equal("Segment diff repair applied for 2/2 rows.", vm.ActionStatus);
    }


    [Fact]
    public void ApplyCadOutputFwDiffOverridesCommand_AppliesOnlyVisibleRows()
    {
        var report = new DxfRegularMappingReport(
            CadCount: 2,
            RegularCount: 2,
            MappedCount: 2,
            UnmappedCadCount: 0,
            UnmappedRegularCount: 0,
            LowConfidenceCount: 0,
            AmbiguousCount: 0,
            HasIssues: false,
            Summary: "summary",
            TotalIssueCount: 0,
            IssuesTruncated: false,
            Issues: Array.Empty<DxfRegularMappingIssue>(),
            SampleIssues: Array.Empty<DxfRegularMappingIssue>(),
            MaskAuditRows: new[]
            {
                new DxfRegularMaskAuditRow(
                    CadPadId: 9201,
                    OrderedCadIndex: 0,
                    RawSeed: new CadBestMatchSeed(611, 1, 200, 0.8, 0.8),
                    MaskedSeed: new CadBestMatchSeed(611, 1, 200, 0.8, 0.8),
                    CsvConfirmedCandidates: new[] { new DxfRegularMaskAuditCandidate(611, 1, 200, 0.8, 0.8) },
                    CsvConfirmedConfidence: 0.8,
                    SuggestedDiffIndex: 200,
                    CurrentAssignedDiffIndex: 199,
                    Status: DxfRegularMaskAuditStatus.ChangedByMask,
                    Message: "row-a",
                    Mode: CadOutputFwDiffAssignmentMode.CsvConstrained,
                    ReasonCode: CadOutputFwDiffAssignmentReasonCode.GapCompensationCandidate,
                    DecisionSource: CadOutputFwDiffAssignmentDecisionSource.AutoRepaired,
                    PrimaryAssignedDiffIndex: 199,
                    RepairSuggestionDiffIndex: 200,
                    IcIndex: 1,
                    RowIndex: 9,
                    SegmentIndex: 0,
                    SegmentMemberCount: 2),
                new DxfRegularMaskAuditRow(
                    CadPadId: 9202,
                    OrderedCadIndex: 1,
                    RawSeed: new CadBestMatchSeed(612, 1, 201, 0.8, 0.8),
                    MaskedSeed: new CadBestMatchSeed(612, 1, 201, 0.8, 0.8),
                    CsvConfirmedCandidates: new[] { new DxfRegularMaskAuditCandidate(612, 1, 201, 0.8, 0.8) },
                    CsvConfirmedConfidence: 0.8,
                    SuggestedDiffIndex: 201,
                    CurrentAssignedDiffIndex: 200,
                    Status: DxfRegularMaskAuditStatus.ChangedByMask,
                    Message: "row-b",
                    Mode: CadOutputFwDiffAssignmentMode.CsvConstrained,
                    ReasonCode: CadOutputFwDiffAssignmentReasonCode.GapCompensationCandidate,
                    DecisionSource: CadOutputFwDiffAssignmentDecisionSource.AutoRepaired,
                    PrimaryAssignedDiffIndex: 200,
                    RepairSuggestionDiffIndex: 201,
                    IcIndex: 1,
                    RowIndex: 9,
                    SegmentIndex: 0,
                    SegmentMemberCount: 2),
            },
            MaskChangedCount: 2,
            MaskRemovedCount: 0);

        var applyCalls = new List<(int CadId, int DiffIndex)>();
        var vm = new IndexMappingReportViewModel(
            "summary",
            report,
            applyDiffOverride: (cadId, diffIndex) =>
            {
                applyCalls.Add((cadId, diffIndex));
                return true;
            });
        vm.SearchKeyword = "9202";

        Assert.True(vm.CanApplyCadOutputFwDiffOverrides);
        Assert.Single(vm.DecisionRows);

        vm.ApplyCadOutputFwDiffOverridesCommand.Execute(null);

        var applied = Assert.Single(applyCalls);
        Assert.Equal(9202, applied.CadId);
        Assert.Equal(201, applied.DiffIndex);
        Assert.Equal("Visible diff repairs applied for 1/1 rows.", vm.ActionStatus);
    }
}
