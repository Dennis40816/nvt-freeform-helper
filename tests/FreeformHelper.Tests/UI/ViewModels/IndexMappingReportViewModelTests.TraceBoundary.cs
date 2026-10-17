using System.Runtime.CompilerServices;
using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class IndexMappingReportViewModelTests
{
    [Fact]
    public async Task AlgorithmTrace_WhenBuildingTheTraceFails_ShowsTheFailedState()
    {
        var vm = CreateSingleIssueViewModel();
        var work = new RecordingBackgroundWorkRunner();
        vm.BackgroundWork.Runner = work;
        vm.TraceBuilder.Builder = new FailsOnFirstBuildTraceBuilder(new InvalidOperationException("trace rejected"));

        vm.IsAlgorithmTraceExpanded = true;
        await work.LastStartedAsync(IndexMappingReportViewModel.TraceWorkName);
        await work.LastStartedAsync(IndexMappingReportViewModel.TracePrefetchWorkName);

        Assert.Equal(IndexMappingAlgorithmTraceState.Failed, vm.AlgorithmTraceState);
        Assert.Equal("Trace: failed (InvalidOperationException).", vm.AlgorithmTraceStatusText);
    }

    [Fact]
    public async Task AlgorithmTrace_WhenBuildingTheTraceRunsOutOfMemory_Propagates()
    {
        var vm = CreateSingleIssueViewModel();
        var failure = (OutOfMemoryException)RuntimeHelpers.GetUninitializedObject(typeof(OutOfMemoryException));
        var work = new RecordingBackgroundWorkRunner();
        vm.BackgroundWork.Runner = work;
        vm.TraceBuilder.Builder = new FailsOnFirstBuildTraceBuilder(failure);

        vm.IsAlgorithmTraceExpanded = true;

        await Assert.ThrowsAsync<OutOfMemoryException>(() =>
            work.LastStartedAsync(IndexMappingReportViewModel.TraceWorkName));
        Assert.Equal(0, work.StartedCount(IndexMappingReportViewModel.TracePrefetchWorkName));
    }

    // Fails the first build with the given exception, then builds an empty trace.
    private sealed class FailsOnFirstBuildTraceBuilder : IIndexMappingTraceBuilder
    {
        private readonly Exception _failure;
        private int _builds;

        public FailsOnFirstBuildTraceBuilder(Exception failure)
        {
            _failure = failure;
        }

        public IReadOnlyList<IndexMappingAlgorithmTraceEntryViewModel> Build(IndexMappingDecisionRowViewModel row, int traceSchemaVersion)
        {
            if (Interlocked.Increment(ref _builds) == 1)
            {
                throw _failure;
            }

            return Array.Empty<IndexMappingAlgorithmTraceEntryViewModel>();
        }
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
