// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Collections.Concurrent;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfEditCountProjectionTests
{
    [Fact]
    public async Task DeleteSelectedCadPadsCommand_HiddenPad_RaisesChangedProjectionsInOrder()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 10, 20));
        try
        {
            var vm = new FreeformHelperViewModel { PickOpenDxfPathAsync = () => Task.FromResult<string?>(path) };
            await vm.OpenDxfCommand.ExecuteAsync(null);
            await SelectCadPadsAsync(vm, [vm.CadPads[0].Id]);
            var changes = ObserveDxfEditChanges(vm);

            vm.DeleteSelectedCadPadsCommand.Execute(null);

            AssertDxfEditCounts(vm, 1, 0, 0, 0, 0, true);
            AssertProjectionChanges(changes, nameof(vm.DeletedCadPadCount), nameof(vm.HasDeletedCadPads), nameof(vm.HasAnyCadEdits));
            AssertSyncPrefix(changes, nameof(vm.DeletedCadPadCount), nameof(vm.HasDeletedCadPads),
                nameof(vm.HiddenDxfEditBadgeTooltip), nameof(vm.HasAnyCadEdits));
            await WaitForGridRebuildAsync(vm);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task DeleteSelectedCadPadsCommand_AdditionalHiddenPad_RaisesOnlyChangedCount()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 10, 20), ("signal", 30, 0, 40, 20));
        try
        {
            var vm = new FreeformHelperViewModel { PickOpenDxfPathAsync = () => Task.FromResult<string?>(path) };
            await vm.OpenDxfCommand.ExecuteAsync(null);
            await SelectCadPadsAsync(vm, [vm.CadPads[0].Id]);
            vm.DeleteSelectedCadPadsCommand.Execute(null);
            await WaitForGridRebuildAsync(vm);
            await SelectCadPadsAsync(vm, [vm.CadPads[0].Id]);
            var changes = ObserveDxfEditChanges(vm);

            vm.DeleteSelectedCadPadsCommand.Execute(null);

            AssertDxfEditCounts(vm, 2, 0, 0, 0, 0, true);
            AssertProjectionChanges(changes, nameof(vm.DeletedCadPadCount));
            await WaitForGridRebuildAsync(vm);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RestoreLastDeletedCadPadsCommand_HiddenPad_RaisesChangedProjectionsInOrder()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 10, 20));
        try
        {
            var vm = new FreeformHelperViewModel { PickOpenDxfPathAsync = () => Task.FromResult<string?>(path) };
            await vm.OpenDxfCommand.ExecuteAsync(null);
            await SelectCadPadsAsync(vm, [vm.CadPads[0].Id]);
            vm.DeleteSelectedCadPadsCommand.Execute(null);
            await WaitForGridRebuildAsync(vm);
            var changes = ObserveDxfEditChanges(vm);

            vm.RestoreLastDeletedCadPadsCommand.Execute(null);

            AssertDxfEditCounts(vm, 0, 0, 0, 0, 0, false);
            AssertProjectionChanges(changes, nameof(vm.DeletedCadPadCount), nameof(vm.HasDeletedCadPads), nameof(vm.HasAnyCadEdits));
            await WaitForGridRebuildAsync(vm);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RestoreAllHiddenCadPadsCommand_NoHiddenPads_RaisesNoProjectionChanges()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 10, 20));
        try
        {
            var vm = new FreeformHelperViewModel { PickOpenDxfPathAsync = () => Task.FromResult<string?>(path) };
            await vm.OpenDxfCommand.ExecuteAsync(null);
            await WaitForGridRebuildAsync(vm);
            var changes = ObserveDxfEditChanges(vm);

            vm.RestoreAllHiddenCadPadsCommand.Execute(null);

            AssertDxfEditCounts(vm, 0, 0, 0, 0, 0, false);
            AssertProjectionChanges(changes);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task CombineSelectedCadPadsCommand_OverlappingPads_RaisesChangedProjectionsInOrder()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 10, 20), ("signal", 8, 0, 18, 20));
        try
        {
            var vm = new FreeformHelperViewModel { PickOpenDxfPathAsync = () => Task.FromResult<string?>(path) };
            await vm.OpenDxfCommand.ExecuteAsync(null);
            await SelectCadPadsAsync(vm, [vm.CadPads[0].Id, vm.CadPads[1].Id]);
            var changes = ObserveDxfEditChanges(vm);

            vm.CombineSelectedCadPadsCommand.Execute(null);

            AssertDxfEditCounts(vm, 2, 0, 1, 0, 0, true);
            AssertProjectionChanges(changes, nameof(vm.CombinedCadPadCount), nameof(vm.HasCombinedCadPads),
                nameof(vm.HasAnyCadEdits), nameof(vm.DeletedCadPadCount), nameof(vm.HasDeletedCadPads));
            AssertSyncPrefix(changes, nameof(vm.MovedDxfEditBadgeTooltip), nameof(vm.CombinedCadPadCount),
                nameof(vm.HasCombinedCadPads), nameof(vm.CombinedDxfEditBadgeTooltip), nameof(vm.HasAnyCadEdits),
                nameof(vm.DeletedCadPadCount), nameof(vm.HasDeletedCadPads), nameof(vm.HiddenDxfEditBadgeTooltip));
            await WaitForGridRebuildAsync(vm);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ClearCombinedCadPadsCommand_CombinedGroup_RaisesChangedProjectionsInOrder()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 10, 20), ("signal", 8, 0, 18, 20));
        try
        {
            var vm = new FreeformHelperViewModel { PickOpenDxfPathAsync = () => Task.FromResult<string?>(path) };
            await vm.OpenDxfCommand.ExecuteAsync(null);
            await SelectCadPadsAsync(vm, [vm.CadPads[0].Id, vm.CadPads[1].Id]);
            vm.CombineSelectedCadPadsCommand.Execute(null);
            await WaitForGridRebuildAsync(vm);
            await SelectCadPadsAsync(vm, [Assert.Single(vm.CadPads).Id]);
            var changes = ObserveDxfEditChanges(vm);

            vm.ClearCombinedCadPadsCommand.Execute(null);

            AssertDxfEditCounts(vm, 0, 0, 0, 0, 0, false);
            AssertProjectionChanges(changes, nameof(vm.CombinedCadPadCount), nameof(vm.HasCombinedCadPads),
                nameof(vm.DeletedCadPadCount), nameof(vm.HasDeletedCadPads), nameof(vm.HasAnyCadEdits));
            AssertSyncPrefix(changes, nameof(vm.MovedDxfEditBadgeTooltip), nameof(vm.RotatedDxfEditBadgeTooltip),
                nameof(vm.CombinedCadPadCount), nameof(vm.HasCombinedCadPads), nameof(vm.CombinedDxfEditBadgeTooltip),
                nameof(vm.DeletedCadPadCount), nameof(vm.HasDeletedCadPads), nameof(vm.HiddenDxfEditBadgeTooltip), nameof(vm.HasAnyCadEdits));
            await WaitForGridRebuildAsync(vm);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task OpenDxfCommand_DuplicatePads_RaisesDuplicateProjectionsWithoutAggregate()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 10, 20), ("signal", 0, 0, 10, 20));
        try
        {
            var vm = new FreeformHelperViewModel { PickOpenDxfPathAsync = () => Task.FromResult<string?>(path) };
            await WaitForGridRebuildAsync(vm);
            var changes = ObserveDxfEditChanges(vm);

            await vm.OpenDxfCommand.ExecuteAsync(null);

            AssertDxfEditCounts(vm, 0, 1, 0, 0, 0, false);
            AssertProjectionChanges(changes, nameof(vm.DuplicateCadPadCount), nameof(vm.HasDuplicateCadPads));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task OpenDxfCommand_SameDuplicatePads_RaisesNoProjectionChanges()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 10, 20), ("signal", 0, 0, 10, 20));
        try
        {
            var vm = new FreeformHelperViewModel
            {
                PickOpenDxfPathAsync = () => Task.FromResult<string?>(path),
            };
            await vm.OpenDxfCommand.ExecuteAsync(null);
            await WaitForGridRebuildAsync(vm);
            var changes = ObserveDxfEditChanges(vm);

            await vm.OpenDxfCommand.ExecuteAsync(null);

            AssertDxfEditCounts(vm, 0, 1, 0, 0, 0, false);
            AssertProjectionChanges(changes);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task OpenDxfCommand_DuplicatesRemoved_RaisesChangedDuplicateProjections()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 10, 20), ("signal", 0, 0, 10, 20));
        var replacement = CreateTempDxfFile(("signal", 0, 0, 10, 20));
        try
        {
            var selectedPath = path;
            var vm = new FreeformHelperViewModel
            {
                PickOpenDxfPathAsync = () => Task.FromResult<string?>(selectedPath),
            };
            await vm.OpenDxfCommand.ExecuteAsync(null);
            await WaitForGridRebuildAsync(vm);
            selectedPath = replacement;
            var changes = ObserveDxfEditChanges(vm);

            await vm.OpenDxfCommand.ExecuteAsync(null);

            AssertDxfEditCounts(vm, 0, 0, 0, 0, 0, false);
            AssertProjectionChanges(changes, nameof(vm.DuplicateCadPadCount), nameof(vm.HasDuplicateCadPads));
        }
        finally
        {
            File.Delete(path);
            File.Delete(replacement);
        }
    }

    [Fact]
    public async Task CreateLayerAndMoveSelectedCadPadsCommand_NewLayer_RaisesChangedProjectionsInOrder()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 10, 20));
        try
        {
            var vm = new FreeformHelperViewModel { PickOpenDxfPathAsync = () => Task.FromResult<string?>(path) };
            await vm.OpenDxfCommand.ExecuteAsync(null);
            vm.DxfEditNewLayerName = "moved";
            await SelectCadPadsAsync(vm, [vm.CadPads[0].Id]);
            var changes = ObserveDxfEditChanges(vm);

            vm.CreateLayerAndMoveSelectedCadPadsCommand.Execute(null);

            AssertDxfEditCounts(vm, 0, 0, 0, 1, 0, true);
            AssertProjectionChanges(changes, nameof(vm.RelayeredCadPadCount), nameof(vm.HasRelayeredCadPads), nameof(vm.HasAnyCadEdits));
            AssertSyncPrefix(changes, nameof(vm.RelayeredCadPadCount), nameof(vm.HasRelayeredCadPads),
                nameof(vm.MovedDxfEditBadgeTooltip), nameof(vm.HasAnyCadEdits));
            await WaitForGridRebuildAsync(vm);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task MoveSelectedCadPadsToLayerCommand_CurrentLayer_RaisesNoProjectionChanges()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 10, 20));
        try
        {
            var vm = new FreeformHelperViewModel { PickOpenDxfPathAsync = () => Task.FromResult<string?>(path) };
            await vm.OpenDxfCommand.ExecuteAsync(null);
            vm.DxfEditTargetLayerName = "signal";
            await SelectCadPadsAsync(vm, [vm.CadPads[0].Id]);
            var changes = ObserveDxfEditChanges(vm);

            vm.MoveSelectedCadPadsToLayerCommand.Execute(null);

            AssertDxfEditCounts(vm, 0, 0, 0, 0, 0, false);
            AssertProjectionChanges(changes);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RotateSelectedCadPadsCommand_ChangedGeometry_RaisesChangedProjectionsInOrder()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 10, 20));
        try
        {
            var vm = new FreeformHelperViewModel { PickOpenDxfPathAsync = () => Task.FromResult<string?>(path) };
            await vm.OpenDxfCommand.ExecuteAsync(null);
            await SelectCadPadsAsync(vm, [vm.CadPads[0].Id]);
            var changes = ObserveDxfEditChanges(vm);

            vm.RotateSelectedCadPadsCommand.Execute(null);

            AssertDxfEditCounts(vm, 0, 0, 0, 0, 1, true);
            AssertProjectionChanges(changes, nameof(vm.RotatedCadPadCount), nameof(vm.HasRotatedCadPads), nameof(vm.HasAnyCadEdits));
            AssertSyncPrefix(changes, nameof(vm.RotatedCadPadCount), nameof(vm.HasRotatedCadPads),
                nameof(vm.RotatedDxfEditBadgeTooltip), nameof(vm.HasAnyCadEdits));
            await WaitForGridRebuildAsync(vm);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RotateSelectedCadPadsCommand_ZeroAngle_RaisesNoProjectionChanges()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 10, 20));
        try
        {
            var vm = new FreeformHelperViewModel { PickOpenDxfPathAsync = () => Task.FromResult<string?>(path) };
            await vm.OpenDxfCommand.ExecuteAsync(null);
            vm.DxfEditRotationDegrees = 0m;
            await SelectCadPadsAsync(vm, [vm.CadPads[0].Id]);
            var changes = ObserveDxfEditChanges(vm);

            vm.RotateSelectedCadPadsCommand.Execute(null);

            AssertDxfEditCounts(vm, 0, 0, 0, 0, 0, false);
            AssertProjectionChanges(changes);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ResetAllDxfEditsCommand_MixedEdits_RaisesChangedProjectionsInSyncOrder()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 10, 20), ("signal", 30, 0, 40, 20), ("signal", 38, 0, 48, 20));
        try
        {
            var vm = new FreeformHelperViewModel { PickOpenDxfPathAsync = () => Task.FromResult<string?>(path) };
            await vm.OpenDxfCommand.ExecuteAsync(null);
            var firstId = vm.CadPads[0].Id;
            await SelectCadPadsAsync(vm, [firstId]);
            vm.DxfEditNewLayerName = "moved";
            vm.CreateLayerAndMoveSelectedCadPadsCommand.Execute(null);
            await SelectCadPadsAsync(vm, [firstId]);
            vm.RotateSelectedCadPadsCommand.Execute(null);
            await SelectCadPadsAsync(vm, [firstId]);
            vm.DeleteSelectedCadPadsCommand.Execute(null);
            await WaitForGridRebuildAsync(vm);
            await SelectCadPadsAsync(vm, vm.CadPads.Select(static pad => pad.Id).ToArray());
            vm.CombineSelectedCadPadsCommand.Execute(null);
            await WaitForGridRebuildAsync(vm);
            AssertDxfEditCounts(vm, 3, 0, 1, 1, 1, true);
            var changes = ObserveDxfEditChanges(vm);

            vm.ResetAllDxfEditsCommand.Execute(null);

            AssertDxfEditCounts(vm, 0, 0, 0, 0, 0, false);
            AssertProjectionChanges(changes, nameof(vm.RelayeredCadPadCount), nameof(vm.HasRelayeredCadPads),
                nameof(vm.RotatedCadPadCount), nameof(vm.HasRotatedCadPads), nameof(vm.DeletedCadPadCount),
                nameof(vm.HasDeletedCadPads), nameof(vm.CombinedCadPadCount), nameof(vm.HasCombinedCadPads), nameof(vm.HasAnyCadEdits));
            await WaitForGridRebuildAsync(vm);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ResetAllDxfEditsCommand_NoEdits_RaisesNoProjectionChanges()
    {
        var vm = new FreeformHelperViewModel();
        await WaitForGridRebuildAsync(vm);
        var changes = ObserveDxfEditChanges(vm);

        vm.ResetAllDxfEditsCommand.Execute(null);

        AssertDxfEditCounts(vm, 0, 0, 0, 0, 0, false);
        AssertProjectionChanges(changes);
    }

    [Fact]
    public async Task LoadProjectCommand_SavedDxfEdits_RaisesChangedProjectionsInSyncOrder()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 10, 20), ("signal", 30, 0, 40, 20), ("signal", 38, 0, 48, 20));
        var projectPath = Path.ChangeExtension(path, ".json");
        try
        {
            var vm = new FreeformHelperViewModel
            {
                PickOpenDxfPathAsync = () => Task.FromResult<string?>(path),
                PickSaveProjectPathAsync = () => Task.FromResult<string?>(projectPath),
                ConfirmEmbedDxfAsync = () => Task.FromResult(false),
            };
            await vm.OpenDxfCommand.ExecuteAsync(null);
            var firstId = vm.CadPads[0].Id;
            await SelectCadPadsAsync(vm, [firstId]);
            vm.DxfEditNewLayerName = "moved";
            vm.CreateLayerAndMoveSelectedCadPadsCommand.Execute(null);
            await SelectCadPadsAsync(vm, [firstId]);
            vm.RotateSelectedCadPadsCommand.Execute(null);
            await SelectCadPadsAsync(vm, [firstId]);
            vm.DeleteSelectedCadPadsCommand.Execute(null);
            await WaitForGridRebuildAsync(vm);
            await SelectCadPadsAsync(vm, vm.CadPads.Select(static pad => pad.Id).ToArray());
            vm.CombineSelectedCadPadsCommand.Execute(null);
            await WaitForGridRebuildAsync(vm);
            Assert.True(await vm.SaveProjectAsync());
            var loaded = new FreeformHelperViewModel { PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath) };
            await WaitForGridRebuildAsync(loaded);
            var changes = ObserveDxfEditChanges(loaded);

            await loaded.LoadProjectCommand.ExecuteAsync(null);

            AssertDxfEditCounts(loaded, 3, 0, 1, 1, 1, true);
            AssertProjectionChanges(changes, nameof(vm.DeletedCadPadCount), nameof(vm.HasDeletedCadPads), nameof(vm.HasAnyCadEdits),
                nameof(vm.CombinedCadPadCount), nameof(vm.HasCombinedCadPads), nameof(vm.RelayeredCadPadCount),
                nameof(vm.HasRelayeredCadPads), nameof(vm.RotatedCadPadCount), nameof(vm.HasRotatedCadPads));
            await WaitForGridRebuildAsync(loaded);
        }
        finally
        {
            File.Delete(path);
            File.Delete(projectPath);
        }
    }

    [Fact]
    public async Task CombineSelectedCadPadsCommand_AdditionalGroup_RaisesCountsWithoutUnchangedFlags()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 10, 20), ("signal", 8, 0, 18, 20),
            ("signal", 30, 0, 40, 20), ("signal", 38, 0, 48, 20));
        try
        {
            var vm = new FreeformHelperViewModel { PickOpenDxfPathAsync = () => Task.FromResult<string?>(path) };
            await vm.OpenDxfCommand.ExecuteAsync(null);
            await SelectCadPadsAsync(vm, [vm.CadPads[0].Id, vm.CadPads[1].Id]);
            vm.CombineSelectedCadPadsCommand.Execute(null);
            await WaitForGridRebuildAsync(vm);
            await SelectCadPadsAsync(vm, vm.CadPads.Where(static pad =>
                !pad.Name.StartsWith("COMB_", StringComparison.Ordinal)).Select(static pad => pad.Id).ToArray());
            var changes = ObserveDxfEditChanges(vm);

            vm.CombineSelectedCadPadsCommand.Execute(null);

            AssertDxfEditCounts(vm, 4, 0, 2, 0, 0, true);
            AssertProjectionChanges(changes, nameof(vm.CombinedCadPadCount), nameof(vm.DeletedCadPadCount));
            await WaitForGridRebuildAsync(vm);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task CreateLayerAndMoveSelectedCadPadsCommand_AlreadyMovedPad_RaisesNoUnchangedProjections()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 10, 20));
        try
        {
            var vm = new FreeformHelperViewModel { PickOpenDxfPathAsync = () => Task.FromResult<string?>(path) };
            await vm.OpenDxfCommand.ExecuteAsync(null);
            var cadId = vm.CadPads[0].Id;
            vm.DxfEditNewLayerName = "moved";
            await SelectCadPadsAsync(vm, [cadId]);
            vm.CreateLayerAndMoveSelectedCadPadsCommand.Execute(null);
            await SelectCadPadsAsync(vm, [cadId]);
            vm.DxfEditNewLayerName = "again";
            var changes = ObserveDxfEditChanges(vm);

            vm.CreateLayerAndMoveSelectedCadPadsCommand.Execute(null);

            AssertDxfEditCounts(vm, 0, 0, 0, 1, 0, true);
            AssertProjectionChanges(changes);
            await WaitForGridRebuildAsync(vm);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RotateSelectedCadPadsCommand_AlreadyRotatedPad_RaisesNoUnchangedProjections()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 10, 20));
        try
        {
            var vm = new FreeformHelperViewModel { PickOpenDxfPathAsync = () => Task.FromResult<string?>(path) };
            await vm.OpenDxfCommand.ExecuteAsync(null);
            var cadId = vm.CadPads[0].Id;
            await SelectCadPadsAsync(vm, [cadId]);
            vm.RotateSelectedCadPadsCommand.Execute(null);
            await SelectCadPadsAsync(vm, [cadId]);
            vm.DxfEditRotationDegrees = 45m;
            var changes = ObserveDxfEditChanges(vm);

            vm.RotateSelectedCadPadsCommand.Execute(null);

            AssertDxfEditCounts(vm, 0, 0, 0, 0, 1, true);
            AssertProjectionChanges(changes);
            await WaitForGridRebuildAsync(vm);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ResetAllDxfEditsCommand_OnlyDuplicates_PreservesDuplicateProjectionsWithoutNotifications()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 10, 20), ("signal", 0, 0, 10, 20));
        try
        {
            var vm = new FreeformHelperViewModel { PickOpenDxfPathAsync = () => Task.FromResult<string?>(path) };
            await vm.OpenDxfCommand.ExecuteAsync(null);
            await WaitForGridRebuildAsync(vm);
            var changes = ObserveDxfEditChanges(vm);

            vm.ResetAllDxfEditsCommand.Execute(null);

            AssertDxfEditCounts(vm, 0, 1, 0, 0, 0, false);
            AssertProjectionChanges(changes);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadProjectCommand_WithoutDxf_ClearsMeasuredProjectionsInOrder()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 10, 20));
        var projectPath = Path.ChangeExtension(path, ".json");
        try
        {
            await new FreeformHelper.Infrastructure.Project.JsonProjectStore().SaveAsync(projectPath,
                new FreeformHelper.Infrastructure.Project.ProjectFile(), CancellationToken.None);
            var vm = new FreeformHelperViewModel
            {
                PickOpenDxfPathAsync = () => Task.FromResult<string?>(path),
                PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath),
            };
            await vm.OpenDxfCommand.ExecuteAsync(null);
            var cadId = vm.CadPads[0].Id;
            vm.DxfEditNewLayerName = "moved";
            await SelectCadPadsAsync(vm, [cadId]);
            vm.CreateLayerAndMoveSelectedCadPadsCommand.Execute(null);
            await SelectCadPadsAsync(vm, [cadId]);
            vm.RotateSelectedCadPadsCommand.Execute(null);
            await WaitForGridRebuildAsync(vm);
            var changes = ObserveDxfEditChanges(vm);

            await vm.LoadProjectCommand.ExecuteAsync(null);

            AssertDxfEditCounts(vm, 0, 0, 0, 0, 0, false);
            AssertProjectionChanges(changes, nameof(vm.RelayeredCadPadCount), nameof(vm.HasRelayeredCadPads),
                nameof(vm.RotatedCadPadCount), nameof(vm.HasRotatedCadPads), nameof(vm.HasAnyCadEdits));
            await WaitForGridRebuildAsync(vm);
        }
        finally
        {
            File.Delete(path);
            File.Delete(projectPath);
        }
    }

    [Fact]
    public async Task ClearCombinedCadPadsCommand_NoCombinedGroup_RaisesNoProjectionChanges()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 10, 20));
        try
        {
            var vm = new FreeformHelperViewModel { PickOpenDxfPathAsync = () => Task.FromResult<string?>(path) };
            await vm.OpenDxfCommand.ExecuteAsync(null);
            await SelectCadPadsAsync(vm, [vm.CadPads[0].Id]);
            var changes = ObserveDxfEditChanges(vm);

            vm.ClearCombinedCadPadsCommand.Execute(null);

            AssertDxfEditCounts(vm, 0, 0, 0, 0, 0, false);
            AssertProjectionChanges(changes);
            await WaitForGridRebuildAsync(vm);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadProjectCommand_WithoutDxfAndNoEdits_RaisesNoProjectionChanges()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 10, 20));
        var projectPath = Path.ChangeExtension(path, ".json");
        try
        {
            await new FreeformHelper.Infrastructure.Project.JsonProjectStore().SaveAsync(projectPath,
                new FreeformHelper.Infrastructure.Project.ProjectFile(), CancellationToken.None);
            var vm = new FreeformHelperViewModel
            {
                PickOpenDxfPathAsync = () => Task.FromResult<string?>(path),
                PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath),
            };
            await vm.OpenDxfCommand.ExecuteAsync(null);
            await WaitForGridRebuildAsync(vm);
            var changes = ObserveDxfEditChanges(vm);

            await vm.LoadProjectCommand.ExecuteAsync(null);

            AssertDxfEditCounts(vm, 0, 0, 0, 0, 0, false);
            AssertProjectionChanges(changes);
            await WaitForGridRebuildAsync(vm);
        }
        finally
        {
            File.Delete(path);
            File.Delete(projectPath);
        }
    }

    private static ConcurrentQueue<string?> ObserveDxfEditChanges(FreeformHelperViewModel vm)
    {
        var changes = new ConcurrentQueue<string?>();
        vm.PropertyChanged += (_, change) => changes.Enqueue(change.PropertyName);
        return changes;
    }

    private static void AssertDxfEditCounts(FreeformHelperViewModel vm, int deleted, int duplicate, int combined, int relayered, int rotated, bool hasAny)
    {
        Assert.Equal(deleted, vm.DeletedCadPadCount);
        Assert.Equal(duplicate, vm.DuplicateCadPadCount);
        Assert.Equal(combined, vm.CombinedCadPadCount);
        Assert.Equal(relayered, vm.RelayeredCadPadCount);
        Assert.Equal(rotated, vm.RotatedCadPadCount);
        Assert.Equal(deleted > 0, vm.HasDeletedCadPads);
        Assert.Equal(duplicate > 0, vm.HasDuplicateCadPads);
        Assert.Equal(combined > 0, vm.HasCombinedCadPads);
        Assert.Equal(relayered > 0, vm.HasRelayeredCadPads);
        Assert.Equal(rotated > 0, vm.HasRotatedCadPads);
        Assert.Equal(hasAny, vm.HasAnyCadEdits);
    }

    private static void AssertProjectionChanges(ConcurrentQueue<string?> changes, params string[] expected)
    {
        Assert.Equal(expected, changes.ToArray().Where(IsDxfEditProjection));
    }

    private static void AssertSyncPrefix(ConcurrentQueue<string?> changes, params string[] expected)
    {
        Assert.Equal(expected, changes.ToArray().Where(name => IsDxfEditProjection(name) ||
            expected.Contains(name, StringComparer.Ordinal)).Take(expected.Length));
    }

    private static bool IsDxfEditProjection(string? name) => name is
        nameof(FreeformHelperViewModel.DeletedCadPadCount) or nameof(FreeformHelperViewModel.DuplicateCadPadCount) or
        nameof(FreeformHelperViewModel.CombinedCadPadCount) or nameof(FreeformHelperViewModel.RelayeredCadPadCount) or
        nameof(FreeformHelperViewModel.RotatedCadPadCount) or nameof(FreeformHelperViewModel.HasDeletedCadPads) or
        nameof(FreeformHelperViewModel.HasDuplicateCadPads) or nameof(FreeformHelperViewModel.HasCombinedCadPads) or
        nameof(FreeformHelperViewModel.HasRelayeredCadPads) or nameof(FreeformHelperViewModel.HasRotatedCadPads) or
        nameof(FreeformHelperViewModel.HasAnyCadEdits);
}
