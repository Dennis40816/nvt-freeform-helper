// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfGeometryReloadBehaviorTests
{
    [AvaloniaFact]
    public async Task Import_DxfWithoutUsablePads_KeepsPreviousCadAndReportsFailureAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new DxfEditingBehaviorTestScope();
        var vm = files.CreateViewModel();
        await files.OpenAsync(vm, ("signal", 0, 0, 10, 20));
        var previousId = Assert.Single(vm.CadPads).Id;
        await SelectCadPadsAsync(vm, [previousId]);
        vm.HasUnsavedChanges = false;

        await files.OpenAsync(vm);

        Assert.Contains("has no usable pads", vm.StatusText, StringComparison.Ordinal);
        Assert.Equal(previousId, Assert.Single(vm.CadPads).Id);
        Assert.Equal(previousId, Assert.Single(vm.SelectedCadPadIds));
        Assert.False(vm.HasAnyCadEdits);
        Assert.False(vm.HasUnsavedChanges);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RestoreGeometry_FromOldDetailsAfterReload_PreservesReplacementAndSelectionAsync(bool hasReplacementCad)
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new DxfEditingBehaviorTestScope();
        var vm = files.CreateViewModel();
        await WaitForGridRebuildAsync(vm);
        Assert.True(await vm.SaveProjectAsync(), vm.StatusText);
        await files.OpenAsync(vm, ("signal", 0, 0, 10, 20), ("signal", 30, 0, 40, 20));
        var oldId = vm.CadPads.Max(static pad => pad.Id);
        await SelectCadPadsAsync(vm, [oldId]);
        vm.DxfEditRotationDegrees = 90m;
        vm.RotateSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        DxfEditChangeListViewModel? opened = null;
        vm.OpenDxfEditChangeListAsync = changes =>
        {
            opened = changes;
            return Task.CompletedTask;
        };
        await vm.OpenRotatedDxfEditChangeListCommand.ExecuteAsync(null);
        var changes = Assert.IsType<DxfEditChangeListViewModel>(opened);
        Assert.Equal(oldId, Assert.Single(changes.Rows).CadPadId);
        if (hasReplacementCad)
        {
            await files.OpenAsync(vm, ("replacement", 100, 0, 110, 20));
            Assert.NotEqual(oldId, Assert.Single(vm.CadPads).Id);
        }
        else
        {
            await vm.LoadProjectCommand.ExecuteAsync(null);
            await WaitForGridRebuildAsync(vm);
            Assert.Empty(vm.CadPads);
        }
        var replacements = vm.CadPads.ToArray();
        var regularId = vm.RegularPads[0].RegularPadId;
        vm.ApplyCanvasSelection([], [regularId]);
        vm.HasUnsavedChanges = false;
        var status = vm.StatusText;
        var revision = vm.SimulationWorkspaceSourceRevision;
        var couldUndo = vm.CanUndo;

        changes.ApplySelectedEntryCommand.Execute(null);

        Assert.Empty(changes.Rows);
        Assert.Equal(replacements, vm.CadPads.ToArray());
        Assert.Equal(regularId, Assert.Single(vm.SelectedRegularPadIndices));
        Assert.Empty(vm.SelectedCadPadIds);
        Assert.Equal(status, vm.StatusText);
        Assert.Equal(revision, vm.SimulationWorkspaceSourceRevision);
        Assert.False(vm.HasAnyCadEdits);
        Assert.False(vm.HasUnsavedChanges);
        Assert.Equal(couldUndo, vm.CanUndo);
    }
}
