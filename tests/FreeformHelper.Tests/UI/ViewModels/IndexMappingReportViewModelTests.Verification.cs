using FreeformHelper.Application.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class IndexMappingReportViewModelTests
{


    [Fact]
    public void VerificationStepRail_SelectStep5_DoesNotOverrideLeftTableFilter()
    {
        var report = new DxfRegularMappingReport(
            CadCount: 1,
            RegularCount: 2,
            MappedCount: 1,
            UnmappedCadCount: 0,
            UnmappedRegularCount: 1,
            LowConfidenceCount: 0,
            AmbiguousCount: 0,
            HasIssues: true,
            Summary: "summary",
            TotalIssueCount: 1,
            IssuesTruncated: false,
            Issues: new[]
            {
                new DxfRegularMappingIssue(
                    DxfRegularMappingIssueKind.CountMismatch,
                    "count mismatch")
            },
            SampleIssues: Array.Empty<DxfRegularMappingIssue>());

        var vm = new IndexMappingReportViewModel("summary", report);

        Assert.Equal(5, vm.VerificationSteps.Count);
        vm.SelectedFilterMode = IndexMappingDecisionFilterMode.Unmapped;
        vm.SelectVerificationStepCommand.Execute(IndexMappingVerificationStep.Step5);

        Assert.Equal(IndexMappingVerificationStep.Step5, vm.ActiveVerificationStep);
        Assert.Equal(IndexMappingDecisionFilterMode.Unmapped, vm.SelectedFilterMode);
    }


    [Fact]
    public void VerificationStepRail_SelectStep5_WithSyncEnabled_SwitchesPreferredFilter()
    {
        var report = new DxfRegularMappingReport(
            CadCount: 1,
            RegularCount: 2,
            MappedCount: 1,
            UnmappedCadCount: 0,
            UnmappedRegularCount: 1,
            LowConfidenceCount: 0,
            AmbiguousCount: 0,
            HasIssues: true,
            Summary: "summary",
            TotalIssueCount: 1,
            IssuesTruncated: false,
            Issues: new[]
            {
                new DxfRegularMappingIssue(
                    DxfRegularMappingIssueKind.CountMismatch,
                    "count mismatch")
            },
            SampleIssues: Array.Empty<DxfRegularMappingIssue>());

        var vm = new IndexMappingReportViewModel("summary", report)
        {
            SyncFilterWithStepRail = true,
            SelectedFilterMode = IndexMappingDecisionFilterMode.Unmapped,
        };

        vm.SelectVerificationStepCommand.Execute(IndexMappingVerificationStep.Step5);

        Assert.Equal(IndexMappingVerificationStep.Step5, vm.ActiveVerificationStep);
        Assert.Equal(IndexMappingDecisionFilterMode.CountMismatch, vm.SelectedFilterMode);
    }


    [Fact]
    public void VerificationStepRail_LastNode_HasNoConnector()
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
            SampleIssues: Array.Empty<DxfRegularMappingIssue>());

        var vm = new IndexMappingReportViewModel("summary", report);

        Assert.Equal(5, vm.VerificationSteps.Count);
        for (var i = 0; i < vm.VerificationSteps.Count; i++)
        {
            var node = vm.VerificationSteps[i];
            if (i == vm.VerificationSteps.Count - 1)
            {
                Assert.True(node.IsLast);
                Assert.False(node.HasConnector);
            }
            else
            {
                Assert.False(node.IsLast);
                Assert.True(node.HasConnector);
            }
        }
    }


    [Fact]
    public void HeaderSubtitle_UsesCompactStatus_NotRawSummary()
    {
        const string verboseSummary = "Diagnostics warnings: mapped=4838, unmappedCad=0, unmappedRegular=154, lowConf=42, ambiguous=4, duplicateDiff=0, maskChanged=0, maskRemoved=0";
        var report = new DxfRegularMappingReport(
            CadCount: 1,
            RegularCount: 2,
            MappedCount: 1,
            UnmappedCadCount: 0,
            UnmappedRegularCount: 1,
            LowConfidenceCount: 0,
            AmbiguousCount: 0,
            HasIssues: true,
            Summary: verboseSummary,
            TotalIssueCount: 1,
            IssuesTruncated: false,
            Issues: new[]
            {
                new DxfRegularMappingIssue(DxfRegularMappingIssueKind.CountMismatch, "count mismatch"),
            },
            SampleIssues: Array.Empty<DxfRegularMappingIssue>());

        var vm = new IndexMappingReportViewModel(verboseSummary, report);

        Assert.Contains("Open issues", vm.HeaderSubtitle);
        Assert.DoesNotContain("mapped=4838", vm.HeaderSubtitle, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public void StepRail_UsesSelectedEntityJourney_WhenDecisionSelected()
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
                    CadPadId: 4823,
                    OrderedCadIndex: 0,
                    RawSeed: new CadBestMatchSeed(3844, 1, 324, 0.91, 0.89),
                    MaskedSeed: new CadBestMatchSeed(3843, 1, 323, 0.74, 0.72),
                    CsvConfirmedCandidates: new[]
                    {
                        new DxfRegularMaskAuditCandidate(3843, 1, 323, 0.74, 0.72),
                    },
                    CsvConfirmedConfidence: 0.83,
                    SuggestedDiffIndex: 323,
                    CurrentAssignedDiffIndex: 324,
                    Status: DxfRegularMaskAuditStatus.ChangedByMask,
                    Message: "changed",
                    Mode: CadOutputFwDiffAssignmentMode.CsvConstrained,
                    ReasonCode: CadOutputFwDiffAssignmentReasonCode.SegmentOffsetSuspected,
                    DecisionSource: CadOutputFwDiffAssignmentDecisionSource.AutoRepaired,
                    PrimaryAssignedDiffIndex: 324,
                    RepairSuggestionDiffIndex: 323,
                    IcIndex: 1,
                    RowIndex: 6,
                    SegmentIndex: 0,
                    SegmentMemberCount: 2),
            },
            MaskChangedCount: 1,
            MaskRemovedCount: 0);

        var vm = new IndexMappingReportViewModel("summary", report);
        var selected = Assert.Single(vm.DecisionRows);
        vm.SelectDecisionCommand.Execute(selected);

        Assert.Equal("CAD 4823", vm.SelectedEntityText);
        Assert.Contains("raw diff 324 -> assigned diff 324 -> suggested diff 323", vm.SelectedJourneyText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("assigned=diff 324", vm.VerificationSteps[1].KeyChecks, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Apply diff repair for CAD 4823", vm.VerificationSteps[1].OpenActionText, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public async Task AlgorithmTrace_ExpandsAndLoadsForSelectedDecisionAsync()
    {
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
            Issues: new[]
            {
                new DxfRegularMappingIssue(
                    DxfRegularMappingIssueKind.LowConfidence,
                    "cad 777",
                    DxfIndex: 77,
                    CadPadId: 777,
                    RegularPadIndex: 88,
                    DiffIndex: 88)
            },
            SampleIssues: Array.Empty<DxfRegularMappingIssue>());

        var vm = new IndexMappingReportViewModel("summary", report);
        vm.IsAlgorithmTraceExpanded = true;

        var completed = await WaitUntilAsync(
            () => vm.AlgorithmTraceState is IndexMappingAlgorithmTraceState.Ready or IndexMappingAlgorithmTraceState.Failed,
            timeoutMs: 2000);

        Assert.True(completed);
        Assert.Equal(IndexMappingAlgorithmTraceState.Ready, vm.AlgorithmTraceState);
        Assert.NotEmpty(vm.AlgorithmTraceEntries);
    }
}
