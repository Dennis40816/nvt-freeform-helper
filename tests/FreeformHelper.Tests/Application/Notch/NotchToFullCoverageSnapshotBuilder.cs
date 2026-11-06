using System.Reflection;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

internal static class NotchToFullCoverageSnapshotBuilder
{
    public static NotchToFullCoverageSnapshot Build(NotchToFullCoverageAudit audit)
    {
        ArgumentNullException.ThrowIfNull(audit);

        return new NotchToFullCoverageSnapshot
        {
            HasExpectations = audit.HasExpectations,
            BucketCount = audit.BucketCount,
            ExpectedTargetDiffCount = audit.ExpectedTargetDiffCount,
            CoveredTargetDiffCount = audit.CoveredTargetDiffCount,
            MissingTargetDiffCount = audit.MissingTargetDiffCount,
            MissingGapSamples = audit.MissingGaps
                .Take(16)
                .Select(gap => new NotchToFullCoverageGapSnapshot
                {
                    Ic = gap.IcIndex + 1,
                    SourceDiff = gap.SourceDiffIndex,
                    TargetDiff = gap.TargetDiffIndex,
                    CadPadIds = gap.CadPadIds.ToArray(),
                })
                .ToList(),
        };
    }
}

internal sealed class NotchToFullCoverageSnapshot
{
    public bool HasExpectations { get; set; }

    public int BucketCount { get; set; }

    public int ExpectedTargetDiffCount { get; set; }

    public int CoveredTargetDiffCount { get; set; }

    public int MissingTargetDiffCount { get; set; }

    public List<NotchToFullCoverageGapSnapshot> MissingGapSamples { get; set; } = new();
}

internal sealed class NotchToFullCoverageGapSnapshot
{
    public int Ic { get; set; }

    public int SourceDiff { get; set; }

    public int TargetDiff { get; set; }

    public int[] CadPadIds { get; set; } = Array.Empty<int>();
}

internal static class NotchCurrentWorkflowTableTestHelper
{
    public static Task<NotchTable> GenerateRawTableAsync(FreeformHelperViewModel vm, string operationLabel)
    {
        return GenerateTableAsync(
            vm,
            operationLabel,
            useExportCad: false,
            projectToCadOutputFwDiff: false,
            resultPropertyName: "RawTable");
    }

    public static Task<NotchTable> GenerateExportTableAsync(FreeformHelperViewModel vm, string operationLabel)
    {
        return GenerateTableAsync(
            vm,
            operationLabel,
            useExportCad: true,
            projectToCadOutputFwDiff: true,
            resultPropertyName: "Table");
    }

    public static CadPadSet GetExportCadPadSet(FreeformHelperViewModel vm)
    {
        ArgumentNullException.ThrowIfNull(vm);

        return Assert.IsType<CadPadSet>(FreeformHelperViewModelPrivateAccess.BuildFilteredCadPadSet(vm));
    }

    private static async Task<NotchTable> GenerateTableAsync(
        FreeformHelperViewModel vm,
        string operationLabel,
        bool useExportCad,
        bool projectToCadOutputFwDiff,
        string resultPropertyName)
    {
        ArgumentNullException.ThrowIfNull(vm);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationLabel);

        var cad = useExportCad
            ? GetExportCadPadSet(vm)
            : Assert.IsType<CadPadSet>(FreeformHelperViewModelPrivateAccess.Cad(vm));
        var grid = Assert.IsType<RegularGrid>(FreeformHelperViewModelPrivateAccess.Grid(vm));

        vm.EnableV21 = true;
        vm.EnableV22 = true;

        var task = Assert.IsAssignableFrom<Task>(FreeformHelperViewModelPrivateAccess.GenerateCurrentNotchTable(
            vm,
            cad,
            grid,
            operationLabel,
            null,
            null,
            false,
            projectToCadOutputFwDiff,
            null));
        await task;

        var resultProperty = task.GetType().GetProperty("Result", BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(resultProperty);
        var result = resultProperty!.GetValue(task);
        Assert.NotNull(result);

        var tableProperty = result.GetType().GetProperty(resultPropertyName, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(tableProperty);
        return Assert.IsType<NotchTable>(tableProperty!.GetValue(result));
    }
}
