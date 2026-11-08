// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class IndexMappingSelectionTraceBehaviorTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AlgorithmTrace_SwitchingUnmappedAndMatchedEntities_KeepsEachExplanation(bool useCad)
    {
        using var culture = TestCulture.UseEnUs();
        var vm = new IndexMappingReportViewModel("Mapping audit", CreateReport(useCad));
        var first = Assert.Single(vm.DecisionRows, row => (row.CadPadId ?? row.LocateRegularPadIndex) == 7);
        var second = Assert.Single(vm.DecisionRows, row => (row.CadPadId ?? row.LocateRegularPadIndex) == 8);
        var work = new RecordingBackgroundWorkRunner();
        vm.BackgroundWork.Runner = work;
        vm.SelectDecisionCommand.Execute(first);

        vm.IsAlgorithmTraceExpanded = true;
        await work.LastStartedAsync(IndexMappingReportViewModel.TraceWorkName);
        await work.LastStartedAsync(IndexMappingReportViewModel.TracePrefetchWorkName);

        Assert.Equal("First pad has no counterpart", Assert.Single(vm.AlgorithmTraceEntries, entry => entry.Label == "final reason").Value);

        vm.SelectedIssue = second.Issue;
        await work.LastStartedAsync(IndexMappingReportViewModel.TraceWorkName);
        await work.LastStartedAsync(IndexMappingReportViewModel.TracePrefetchWorkName);

        Assert.Same(second, vm.SelectedDecision);
        Assert.True(second.IsInspectorSelected);
        Assert.False(first.IsInspectorSelected);
        Assert.Equal(useCad ? "CAD 8" : "REG 8", vm.SelectedEntityText);
        Assert.Equal(IndexMappingAlgorithmTraceState.Ready, vm.AlgorithmTraceState);
        Assert.Equal("Second pad has no counterpart",
            Assert.Single(vm.AlgorithmTraceEntries, entry => entry.Label == "final reason").Value);

        vm.SelectDecisionCommand.Execute(Assert.Single(vm.DecisionRows, row => row.CadPadId == 9));
        await work.LastStartedAsync(IndexMappingReportViewModel.TraceWorkName);
        await work.LastStartedAsync(IndexMappingReportViewModel.TracePrefetchWorkName);

        Assert.Equal("CAD 9", vm.SelectedEntityText);
        Assert.Null(vm.SelectedIssue);
        Assert.Equal("IC 2, row 0, segment 0",
            Assert.Single(vm.AlgorithmTraceEntries, entry => entry.Label == "segment").Value);
        Assert.Equal("diff 91", Assert.Single(vm.AlgorithmTraceEntries, entry => entry.Label == "input.current").Value);

        vm.SelectDecisionCommand.Execute(first);
        await work.LastStartedAsync(IndexMappingReportViewModel.TraceWorkName);
        await work.LastStartedAsync(IndexMappingReportViewModel.TracePrefetchWorkName);

        Assert.Same(first.Issue, vm.SelectedIssue);
        Assert.True(first.IsInspectorSelected);
        Assert.False(second.IsInspectorSelected);
        Assert.Equal(useCad ? "CAD 7" : "REG 7", vm.SelectedEntityText);
        Assert.Equal("First pad has no counterpart", Assert.Single(vm.AlgorithmTraceEntries, entry => entry.Label == "final reason").Value);
    }

    private static DxfRegularMappingReport CreateReport(bool useCad)
    {
        var kind = useCad ? DxfRegularMappingIssueKind.UnmappedCad : DxfRegularMappingIssueKind.UnmappedRegular;
        DxfRegularMappingIssue[] issues =
        [
            new(kind, "First pad has no counterpart", CadPadId: useCad ? 7 : null, RegularPadIndex: useCad ? null : 7),
            new(kind, "Second pad has no counterpart", CadPadId: useCad ? 8 : null, RegularPadIndex: useCad ? null : 8),
        ];
        var grid = TestGeometryFactory.CreateLinearRegularGrid([90], icIndexSelector: _ => 2);
        var cad = TestGeometryFactory.CreateCadPad(9, "PAD", 0, 0, 1, 1);
        var matches = PadMatcher.Match(new CadPadSet([cad]), grid);
        var audit = DxfRegularMaskAuditService.BuildAuditRows(
            [cad], matches.CadToRegular, grid.Pads.ToDictionary(pad => pad.RegularPadId),
            new Dictionary<int, int> { [9] = 2 }, activeRegularPadIds: new HashSet<int>(),
            currentAssignedDiffByCadId: new Dictionary<int, int> { [9] = 91 });
        return new DxfRegularMappingReport(
            CadCount: useCad ? 3 : 1, RegularCount: useCad ? 1 : 3, MappedCount: 1,
            UnmappedCadCount: useCad ? 2 : 0, UnmappedRegularCount: useCad ? 0 : 2,
            LowConfidenceCount: 0, AmbiguousCount: 0, HasIssues: true, Summary: "Mapping audit",
            TotalIssueCount: 2, IssuesTruncated: false, Issues: issues, SampleIssues: issues, MaskAuditRows: audit);
    }
}
