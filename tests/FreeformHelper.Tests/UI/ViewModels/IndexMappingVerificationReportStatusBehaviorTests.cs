// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class IndexMappingVerificationReportStatusBehaviorTests
{
    [Theory]
    [InlineData(DxfRegularMappingIssueKind.UnmappedCad, "warning", "pass")]
    [InlineData(DxfRegularMappingIssueKind.DuplicateDiff, "pass", "warning")]
    public void SearchHidesSelection_StepStatusesUseRelevantReportCounts(
        DxfRegularMappingIssueKind kind, string rowIntegrityStatus, string traceStatus)
    {
        using var culture = TestCulture.UseEnUs();
        var isUnmapped = kind == DxfRegularMappingIssueKind.UnmappedCad;
        DxfRegularMappingIssue[] issues = isUnmapped ?
        [
            new(DxfRegularMappingIssueKind.UnmappedCad, "CAD 7 has no counterpart", CadPadId: 7),
            new(DxfRegularMappingIssueKind.UnmappedRegular, "REG 12 has no counterpart", RegularPadIndex: 12, DiffIndex: 4),
        ] :
        [
            new(DxfRegularMappingIssueKind.DuplicateDiff, "Two CAD pads share diff 3", CadPadId: 7,
                RegularPadIndex: 11, DiffIndex: 3),
        ];
        var report = new DxfRegularMappingReport(
            CadCount: 2, RegularCount: 2, MappedCount: isUnmapped ? 1 : 2,
            UnmappedCadCount: isUnmapped ? 1 : 0, UnmappedRegularCount: isUnmapped ? 1 : 0,
            LowConfidenceCount: 0, AmbiguousCount: 0, HasIssues: true, Summary: "Mapping audit",
            TotalIssueCount: issues.Length, IssuesTruncated: false, Issues: issues, SampleIssues: issues,
            DuplicateDiffCount: isUnmapped ? 0 : 1);
        var vm = new IndexMappingReportViewModel("Mapping audit", report);
        Assert.NotNull(vm.SelectedDecision);

        vm.SearchKeyword = "no matching entity";

        Assert.Null(vm.SelectedDecision);
        Assert.Empty(vm.DecisionRows);
        Assert.Equal(0, vm.CountMismatchDecisionCount);
        Assert.Equal("warning", vm.OverallStatusText);
        Assert.Equal("warning", vm.VerificationSteps.Single(
            static node => node.Step == IndexMappingVerificationStep.Step3).StatusText);
        Assert.Equal("warning", vm.VerificationSteps.Single(
            static node => node.Step == IndexMappingVerificationStep.Step4).StatusText);
        Assert.Equal("warning", vm.VerificationSteps.Single(
            static node => node.Step == IndexMappingVerificationStep.Simulation).StatusText);

        vm.SelectVerificationStepCommand.Execute(IndexMappingVerificationStep.Step5);
        Assert.Equal($"status: {rowIntegrityStatus}", vm.ActiveStepCardStatusText);
        Assert.Equal(isUnmapped ? "mismatch=0, unmapped=2" : "mismatch=0, unmapped=0",
            vm.VerificationSteps.Single(static node => node.Step == IndexMappingVerificationStep.Step5).KeyChecks);

        vm.SelectVerificationStepCommand.Execute(IndexMappingVerificationStep.Step6);
        Assert.Equal($"status: {traceStatus}", vm.ActiveStepCardStatusText);
        Assert.Equal(isUnmapped ? "duplicate=0" : "duplicate=1", vm.ActiveStepOutputText);
    }
}
