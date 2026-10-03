using System.Reflection;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class FreeformHelperViewModelTests
{
    [Fact]
    public void BuildWorkflowDataSnapshot_ReusesCacheUntilInvalidated()
    {
        var vm = new FreeformHelperViewModel();

        var first = BuildWorkflowDataSnapshot(vm);
        var second = BuildWorkflowDataSnapshot(vm);

        Assert.Same(first, second);
        Assert.Equal(1, vm.WorkflowDataSnapshotBuildCount);
        var revisionBeforeInvalidate = vm.WorkflowDataSnapshotRevision;

        InvalidateWorkflowDataSnapshot(vm);
        var third = BuildWorkflowDataSnapshot(vm);

        Assert.NotSame(first, third);
        Assert.Equal(revisionBeforeInvalidate + 1, vm.WorkflowDataSnapshotRevision);
        Assert.Equal(2, vm.WorkflowDataSnapshotBuildCount);
    }

    private static object BuildWorkflowDataSnapshot(FreeformHelperViewModel vm)
    {
        var method = typeof(FreeformHelperViewModel).GetMethod(
            "BuildWorkflowDataSnapshot",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return method!.Invoke(vm, null)!;
    }

    private static void InvalidateWorkflowDataSnapshot(FreeformHelperViewModel vm)
    {
        var method = typeof(FreeformHelperViewModel).GetMethod(
            "InvalidateWorkflowDataSnapshot",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(vm, null);
    }
}
