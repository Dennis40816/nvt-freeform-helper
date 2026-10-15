using System.Runtime.CompilerServices;
using FreeformHelper.Application.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class IndexMappingReportViewModelTests
{
    [Fact]
    public async Task AlgorithmTrace_WhenBuildingTheTraceFails_ShowsTheFailedState()
    {
        var vm = CreateSingleIssueViewModel();
        vm.TraceLoadStartedForTests = static () => throw new InvalidOperationException("trace rejected");

        await vm.EnsureAlgorithmTraceForTestsAsync();

        Assert.Equal(IndexMappingAlgorithmTraceState.Failed, vm.AlgorithmTraceState);
        Assert.Equal("Trace: failed (InvalidOperationException).", vm.AlgorithmTraceStatusText);
    }

    [Fact]
    public async Task AlgorithmTrace_WhenBuildingTheTraceRunsOutOfMemory_Propagates()
    {
        var vm = CreateSingleIssueViewModel();
        var failure = (OutOfMemoryException)RuntimeHelpers.GetUninitializedObject(typeof(OutOfMemoryException));
        vm.TraceLoadStartedForTests = () => throw failure;

        await Assert.ThrowsAsync<OutOfMemoryException>(vm.EnsureAlgorithmTraceForTestsAsync);
    }

    private static IndexMappingReportViewModel CreateSingleIssueViewModel()
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

        return new IndexMappingReportViewModel("summary", report);
    }
}
