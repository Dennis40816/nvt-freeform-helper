using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class FreeformHelperViewModelTests
{
    [Fact]
    public void BuildWorkflowDataSnapshot_ReusesCacheUntilInvalidated()
    {
        var vm = new FreeformHelperViewModel();

        var first = FreeformHelperViewModelPrivateAccess.BuildWorkflowDataSnapshot(vm);
        var second = FreeformHelperViewModelPrivateAccess.BuildWorkflowDataSnapshot(vm);

        Assert.Same(first, second);
        Assert.Equal(1, vm.WorkflowDataSnapshotBuildCount);

        vm.InvalidateWorkflowDataSnapshot();
        var third = FreeformHelperViewModelPrivateAccess.BuildWorkflowDataSnapshot(vm);

        Assert.NotSame(first, third);
        Assert.Equal(2, vm.WorkflowDataSnapshotBuildCount);
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

    [Fact]
    public void Step4Index_ShiftDeltaChange_RaisesTheDerivedStep4ShiftMembersOnTheViewModel()
    {
        var vm = new FreeformHelperViewModel();
        var raised = new List<string?>();
        vm.PropertyChanged += (_, args) => raised.Add(args.PropertyName);

        vm.Step4Index.ShiftDelta = 3m;

        Assert.Contains(nameof(FreeformHelperViewModel.CanOffsetSelectedCadOutputFwDiffIndices), raised);
        Assert.Contains(nameof(FreeformHelperViewModel.Step4DiffShiftSelectionSummary), raised);
        Assert.Same(vm.Step4Index, vm.Step4Index);
    }
}
