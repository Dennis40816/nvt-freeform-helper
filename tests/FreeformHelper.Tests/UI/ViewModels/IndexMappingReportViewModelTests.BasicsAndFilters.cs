using FreeformHelper.Application.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class IndexMappingReportViewModelTests
{
    [Theory]
    [InlineData(IndexMappingDecisionFilterMode.All, 7, 1)]
    [InlineData(IndexMappingDecisionFilterMode.ChangedByMask, 1, 0)]
    [InlineData(IndexMappingDecisionFilterMode.RemovedByMask, 1, 0)]
    [InlineData(IndexMappingDecisionFilterMode.Ambiguous, 1, 0)]
    [InlineData(IndexMappingDecisionFilterMode.DuplicateDiff, 1, 0)]
    [InlineData(IndexMappingDecisionFilterMode.LowConfidence, 1, 0)]
    [InlineData(IndexMappingDecisionFilterMode.Unmapped, 2, 0)]
    [InlineData(IndexMappingDecisionFilterMode.CountMismatch, 0, 1)]
    public void SetDecisionFilterModeCommand_SelectsMatchingDecisions(
        IndexMappingDecisionFilterMode mode,
        int expectedEntityCount,
        int expectedAggregateCount)
    {
        var report = CreateDecisionFilterReport();
        var vm = new IndexMappingReportViewModel("summary", report);
        if (mode == IndexMappingDecisionFilterMode.All)
        {
            vm.SetDecisionFilterModeCommand.Execute(IndexMappingDecisionFilterMode.LowConfidence);
            Assert.Single(vm.DecisionRows);
        }

        vm.SetDecisionFilterModeCommand.Execute(mode);

        Assert.Equal(mode, vm.SelectedFilterMode);
        Assert.Equal(expectedEntityCount, vm.DecisionRows.Count);
        Assert.Equal(expectedAggregateCount, vm.AggregateDecisionRows.Count);
        var visibleRows = vm.DecisionRows.Concat(vm.AggregateDecisionRows).ToList();
        Assert.NotNull(vm.SelectedDecision);
        Assert.Contains(vm.SelectedDecision, visibleRows);
        if (mode == IndexMappingDecisionFilterMode.All)
        {
            Assert.Equal([301, 302, 303, 304, 306, 307],
                vm.DecisionRows.Where(static row => row.CadPadId.HasValue).Select(static row => row.CadPadId!.Value).Order());
            Assert.Contains(vm.DecisionRows, static row => row.CadPadId is null && row.LocateRegularPadIndex == 305);
            Assert.Equal(IndexMappingDecisionFilterMode.CountMismatch, Assert.Single(vm.AggregateDecisionRows).FilterMode);
        }
        else
        {
            Assert.All(visibleRows, row => Assert.Equal(mode, row.FilterMode));

            vm.SetDecisionFilterModeCommand.Execute(mode);

            Assert.Equal(IndexMappingDecisionFilterMode.All, vm.SelectedFilterMode);
            Assert.Equal(7, vm.DecisionRows.Count);
            Assert.Single(vm.AggregateDecisionRows);
        }
    }

    private static DxfRegularMappingReport CreateDecisionFilterReport()
    {
        var issues = new[]
        {
            new DxfRegularMappingIssue(DxfRegularMappingIssueKind.CountMismatch, "count mismatch"),
            new DxfRegularMappingIssue(DxfRegularMappingIssueKind.LowConfidence, "low confidence", CadPadId: 301),
            new DxfRegularMappingIssue(DxfRegularMappingIssueKind.Ambiguous, "ambiguous", CadPadId: 302),
            new DxfRegularMappingIssue(DxfRegularMappingIssueKind.DuplicateDiff, "duplicate diff", CadPadId: 303),
            new DxfRegularMappingIssue(DxfRegularMappingIssueKind.UnmappedCad, "unmapped CAD", CadPadId: 304),
            new DxfRegularMappingIssue(DxfRegularMappingIssueKind.UnmappedRegular, "unmapped REG", RegularPadIndex: 305),
        };
        var report = new DxfRegularMappingReport(
            CadCount: 6,
            RegularCount: 7,
            MappedCount: 5,
            UnmappedCadCount: 1,
            UnmappedRegularCount: 1,
            LowConfidenceCount: 1,
            AmbiguousCount: 1,
            HasIssues: true,
            Summary: "summary",
            TotalIssueCount: issues.Length,
            IssuesTruncated: false,
            Issues: issues,
            SampleIssues: issues,
            MaskAuditRows:
            [
                new DxfRegularMaskAuditRow(
                    CadPadId: 306,
                    OrderedCadIndex: 4,
                    RawSeed: new CadBestMatchSeed(400, 0, 40, 0.9, 0.9),
                    MaskedSeed: new CadBestMatchSeed(401, 0, 41, 0.7, 0.7),
                    CsvConfirmedCandidates: Array.Empty<DxfRegularMaskAuditCandidate>(),
                    CsvConfirmedConfidence: null,
                    SuggestedDiffIndex: 41,
                    CurrentAssignedDiffIndex: 41,
                    Status: DxfRegularMaskAuditStatus.ChangedByMask,
                    Message: "changed"),
                new DxfRegularMaskAuditRow(
                    CadPadId: 307,
                    OrderedCadIndex: 5,
                    RawSeed: new CadBestMatchSeed(402, 0, 42, 0.9, 0.9),
                    MaskedSeed: CadBestMatchSeed.Empty,
                    CsvConfirmedCandidates: Array.Empty<DxfRegularMaskAuditCandidate>(),
                    CsvConfirmedConfidence: null,
                    SuggestedDiffIndex: null,
                    CurrentAssignedDiffIndex: null,
                    Status: DxfRegularMaskAuditStatus.MaskRemovedMatch,
                    Message: "removed"),
            ],
            MaskChangedCount: 1,
            MaskRemovedCount: 1,
            DuplicateDiffCount: 1);

        return report;
    }

    [Fact]
    public void IssueViewModel_ExposesScoreDiagnosticsAndTopCandidates()
    {
        var issue = new DxfRegularMappingIssue(
            DxfRegularMappingIssueKind.LowConfidence,
            "Low confidence",
            DxfIndex: 7,
            CadPadId: 107,
            RegularPadIndex: 45,
            IcIndex: 2,
            DiffIndex: 45,
            Score: 0.21,
            BestScore: 0.33,
            SecondScore: 0.29,
            Margin: 0.04,
            TopCandidates: new[]
            {
                new DxfRegularMappingCandidate(45, 2, 45, 0.33),
                new DxfRegularMappingCandidate(46, 2, 46, 0.29),
            });

        var report = new DxfRegularMappingReport(
            CadCount: 10,
            RegularCount: 10,
            MappedCount: 9,
            UnmappedCadCount: 1,
            UnmappedRegularCount: 1,
            LowConfidenceCount: 1,
            AmbiguousCount: 0,
            HasIssues: true,
            Summary: "summary",
            TotalIssueCount: 1,
            IssuesTruncated: false,
            Issues: new[] { issue },
            SampleIssues: new[] { issue });

        var vm = new IndexMappingReportViewModel("summary", report);
        var selected = Assert.Single(vm.Issues);

        Assert.True(selected.Score.HasValue || selected.BestScore.HasValue || selected.SecondScore.HasValue || selected.Margin.HasValue);
        Assert.Equal(0.21, selected.Score);
        Assert.Equal(0.33, selected.BestScore);
        Assert.Equal(0.29, selected.SecondScore);
        Assert.Equal(0.04, selected.Margin);
        Assert.NotEmpty(selected.TopCandidates);
        Assert.Equal(2, selected.TopCandidates.Count);
        Assert.Contains("#1 diff45", selected.TopCandidates[0].Display, StringComparison.Ordinal);
    }


    [Fact]
    public void Filters_UpdateVisibleIssues_ByKind()
    {
        var issues = new[]
        {
            new DxfRegularMappingIssue(DxfRegularMappingIssueKind.CountMismatch, "count"),
            new DxfRegularMappingIssue(DxfRegularMappingIssueKind.LowConfidence, "low", CadPadId: 1, RegularPadIndex: 10),
            new DxfRegularMappingIssue(DxfRegularMappingIssueKind.Ambiguous, "amb", CadPadId: 2, RegularPadIndex: 11),
        };

        var report = new DxfRegularMappingReport(
            CadCount: 3,
            RegularCount: 3,
            MappedCount: 2,
            UnmappedCadCount: 0,
            UnmappedRegularCount: 0,
            LowConfidenceCount: 1,
            AmbiguousCount: 1,
            HasIssues: true,
            Summary: "summary",
            TotalIssueCount: issues.Length,
            IssuesTruncated: false,
            Issues: issues,
            SampleIssues: issues);

        var vm = new IndexMappingReportViewModel("summary", report);
        Assert.Equal(3, vm.Issues.Count);

        vm.ShowCountMismatchIssues = false;
        Assert.Equal(2, vm.Issues.Count);
        Assert.DoesNotContain(vm.Issues, i => i.Kind == DxfRegularMappingIssueKind.CountMismatch);

        vm.ShowLowConfidenceIssues = false;
        Assert.Single(vm.Issues);
        Assert.Equal(DxfRegularMappingIssueKind.Ambiguous, vm.Issues[0].Kind);
    }


    [Fact]
    public void MaskAudit_Filter_DefaultsToChangedOnly()
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
                    CadPadId: 384,
                    OrderedCadIndex: 0,
                    RawSeed: new CadBestMatchSeed(583, 0, 43, 0.993, 0.991),
                    MaskedSeed: new CadBestMatchSeed(579, 0, 39, 0.210, 0.205),
                    CsvConfirmedCandidates: new[]
                    {
                        new DxfRegularMaskAuditCandidate(579, 0, 39, 0.210, 0.205),
                    },
                    CsvConfirmedConfidence: 1.0,
                    SuggestedDiffIndex: 39,
                    CurrentAssignedDiffIndex: 39,
                    Status: DxfRegularMaskAuditStatus.ChangedByMask,
                    Message: "changed"),
                new DxfRegularMaskAuditRow(
                    CadPadId: 385,
                    OrderedCadIndex: 1,
                    RawSeed: new CadBestMatchSeed(584, 0, 44, 0.990, 0.989),
                    MaskedSeed: new CadBestMatchSeed(584, 0, 44, 0.990, 0.989),
                    CsvConfirmedCandidates: new[]
                    {
                        new DxfRegularMaskAuditCandidate(584, 0, 44, 0.990, 0.989),
                    },
                    CsvConfirmedConfidence: 1.0,
                    SuggestedDiffIndex: 44,
                    CurrentAssignedDiffIndex: 44,
                    Status: DxfRegularMaskAuditStatus.Unchanged,
                    Message: "unchanged"),
            },
            MaskChangedCount: 1,
            MaskRemovedCount: 0);

        var vm = new IndexMappingReportViewModel("summary", report);

        Assert.True(vm.HasMaskAudit);
        Assert.Single(vm.MaskAuditRows);
        Assert.Equal(384, vm.MaskAuditRows[0].CadPadId);
        Assert.Equal(39, vm.MaskAuditRows[0].SuggestedDiffIndex);

        vm.ShowChangedMaskRowsOnly = false;
        Assert.Equal(2, vm.MaskAuditRows.Count);
    }


    [Fact]
    public void DecisionRows_IncludeIssuesAndMaskAuditInSingleList()
    {
        var issues = new[]
        {
            new DxfRegularMappingIssue(
                DxfRegularMappingIssueKind.Ambiguous,
                "ambiguous",
                DxfIndex: 42,
                CadPadId: 1001,
                RegularPadIndex: 321,
                DiffIndex: 321,
                TopCandidates: new[]
                {
                    new DxfRegularMappingCandidate(321, 1, 321, 0.44),
                }),
        };

        var report = new DxfRegularMappingReport(
            CadCount: 2,
            RegularCount: 2,
            MappedCount: 2,
            UnmappedCadCount: 0,
            UnmappedRegularCount: 0,
            LowConfidenceCount: 0,
            AmbiguousCount: 1,
            HasIssues: true,
            Summary: "summary",
            TotalIssueCount: 1,
            IssuesTruncated: false,
            Issues: issues,
            SampleIssues: issues,
            MaskAuditRows: new[]
            {
                new DxfRegularMaskAuditRow(
                    CadPadId: 1002,
                    OrderedCadIndex: 1,
                    RawSeed: new CadBestMatchSeed(400, 1, 400, 0.81, 0.79),
                    MaskedSeed: new CadBestMatchSeed(401, 1, 401, 0.55, 0.51),
                    CsvConfirmedCandidates: new[]
                    {
                        new DxfRegularMaskAuditCandidate(401, 1, 401, 0.55, 0.51),
                    },
                    CsvConfirmedConfidence: 1.0,
                    SuggestedDiffIndex: 401,
                    CurrentAssignedDiffIndex: 401,
                    Status: DxfRegularMaskAuditStatus.ChangedByMask,
                    Message: "changed"),
            },
            MaskChangedCount: 1,
            MaskRemovedCount: 0);

        var vm = new IndexMappingReportViewModel("summary", report);

        Assert.Equal(2, vm.DecisionRows.Count);
        Assert.Contains(vm.DecisionRows, row => row.FilterMode == IndexMappingDecisionFilterMode.Ambiguous);
        Assert.Contains(vm.DecisionRows, row => row.FilterMode == IndexMappingDecisionFilterMode.ChangedByMask);
    }


    [Fact]
    public void DecisionRows_SplitEntityRows_AndAggregateRows()
    {
        var issues = new[]
        {
            new DxfRegularMappingIssue(DxfRegularMappingIssueKind.CountMismatch, "count mismatch"),
            new DxfRegularMappingIssue(
                DxfRegularMappingIssueKind.LowConfidence,
                "low confidence",
                DxfIndex: 18,
                CadPadId: 2001,
                RegularPadIndex: 301,
                DiffIndex: 301),
        };

        var report = new DxfRegularMappingReport(
            CadCount: 2,
            RegularCount: 3,
            MappedCount: 1,
            UnmappedCadCount: 0,
            UnmappedRegularCount: 1,
            LowConfidenceCount: 1,
            AmbiguousCount: 0,
            HasIssues: true,
            Summary: "summary",
            TotalIssueCount: issues.Length,
            IssuesTruncated: false,
            Issues: issues,
            SampleIssues: issues);

        var vm = new IndexMappingReportViewModel("summary", report);

        var entityRow = Assert.Single(vm.DecisionRows);
        Assert.Equal(2001, entityRow.CadPadId);
        var aggregateRow = Assert.Single(vm.AggregateDecisionRows);
        Assert.True(aggregateRow.IsAggregateRow);
        Assert.Equal(IndexMappingDecisionFilterMode.CountMismatch, aggregateRow.FilterMode);
        Assert.Same(entityRow, vm.SelectedDecision);
    }


    [Fact]
    public void DecisionRows_SearchAndFilter_UpdateVisibleRows()
    {
        var report = new DxfRegularMappingReport(
            CadCount: 2,
            RegularCount: 2,
            MappedCount: 2,
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
                    DiffIndex: 88),
            },
            SampleIssues: Array.Empty<DxfRegularMappingIssue>(),
            MaskAuditRows: new[]
            {
                new DxfRegularMaskAuditRow(
                    CadPadId: 888,
                    OrderedCadIndex: 0,
                    RawSeed: new CadBestMatchSeed(99, 1, 99, 0.9, 0.9),
                    MaskedSeed: new CadBestMatchSeed(98, 1, 98, 0.7, 0.7),
                    CsvConfirmedCandidates: Array.Empty<DxfRegularMaskAuditCandidate>(),
                    CsvConfirmedConfidence: null,
                    SuggestedDiffIndex: 98,
                    CurrentAssignedDiffIndex: 98,
                    Status: DxfRegularMaskAuditStatus.ChangedByMask,
                    Message: "mask changed"),
            },
            MaskChangedCount: 1,
            MaskRemovedCount: 0);

        var vm = new IndexMappingReportViewModel("summary", report);

        vm.SelectedFilterMode = IndexMappingDecisionFilterMode.ChangedByMask;
        Assert.Single(vm.DecisionRows);
        Assert.Equal(888, vm.DecisionRows[0].CadPadId);

        vm.SelectedFilterMode = IndexMappingDecisionFilterMode.All;
        vm.SearchKeyword = "777";
        Assert.Single(vm.DecisionRows);
        Assert.Equal(777, vm.DecisionRows[0].CadPadId);
    }
}
