// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class IndexMappingAggregateSelectionBehaviorTests
{
    [Fact]
    public async Task SelectDecision_AggregateAfterEntity_ShowsAggregateJourneyAndClearsEntityTrace()
    {
        using var culture = TestCulture.UseEnUs();
        DxfRegularMappingIssue[] issues =
        [
            new(DxfRegularMappingIssueKind.CountMismatch, "Two CAD pads and three regular pads."),
            new(DxfRegularMappingIssueKind.UnmappedCad, "CAD has no counterpart.", CadPadId: 7),
        ];
        var report = new DxfRegularMappingReport(
            CadCount: 2, RegularCount: 3, MappedCount: 1, UnmappedCadCount: 1, UnmappedRegularCount: 2,
            LowConfidenceCount: 0, AmbiguousCount: 0, HasIssues: true, Summary: "Mapping audit",
            TotalIssueCount: issues.Length, IssuesTruncated: false, Issues: issues, SampleIssues: issues);
        var vm = new IndexMappingReportViewModel("Mapping audit", report);
        var work = new RecordingBackgroundWorkRunner();
        vm.BackgroundWork.Runner = work;
        var entity = Assert.Single(vm.DecisionRows);
        var aggregate = Assert.Single(vm.AggregateDecisionRows);
        vm.IsAlgorithmTraceExpanded = true;
        await work.LastStartedAsync(IndexMappingReportViewModel.TraceWorkName);
        await work.LastStartedAsync(IndexMappingReportViewModel.TracePrefetchWorkName);
        Assert.Equal("CAD 7", vm.SelectedEntityText);
        Assert.NotEmpty(vm.AlgorithmTraceEntries);

        vm.SelectDecisionCommand.Execute(aggregate);
        await work.LastStartedAsync(IndexMappingReportViewModel.TraceWorkName);

        Assert.Same(aggregate, vm.SelectedDecision);
        Assert.Same(aggregate.Issue, vm.SelectedIssue);
        Assert.True(aggregate.IsInspectorSelected);
        Assert.False(entity.IsInspectorSelected);
        Assert.Equal($"aggregate: {aggregate.CategoryText}", vm.SelectedEntityText);
        Assert.Equal("journey: aggregate diagnostics row. Select a CAD/REG entity row to inspect step-by-step attribution.",
            vm.SelectedJourneyText);
        Assert.Empty(vm.AlgorithmTraceEntries);
        Assert.Equal(IndexMappingAlgorithmTraceState.NotRequested, vm.AlgorithmTraceState);
        Assert.Equal("Trace: aggregate row (select CAD/REG entity).", vm.AlgorithmTraceStatusText);
        Assert.False(vm.CanLocateSelectedDecision);
    }
}
