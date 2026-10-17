using System.Reflection;
using Avalonia.Headless.XUnit;
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

    [AvaloniaFact]
    public async Task FilterCadPadsByLayer_RaisesHasStep4DuplicateDiffGroupsAndKeepsItInStepWithTheGroups()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 15, 10), ("signal", 5, 0, 20, 10), ("signal", 0, 0, 14, 10));
        try
        {
            var vm = await CreateNotchExportCacheViewModelAsync(path);
            var raised = new List<string?>();
            vm.PropertyChanged += (_, args) => raised.Add(args.PropertyName);

            vm.FilterCadPadsByLayer();

            Assert.Contains(nameof(FreeformHelperViewModel.HasStep4DuplicateDiffGroups), raised);
            Assert.Equal(vm.Step4DuplicateDiffGroups.Count > 0, vm.HasStep4DuplicateDiffGroups);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
