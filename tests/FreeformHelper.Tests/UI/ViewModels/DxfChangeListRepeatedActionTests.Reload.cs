// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;
using static FreeformHelper.Tests.SuppressedWorkspaceBehaviorTests;

namespace FreeformHelper.Tests;

public sealed partial class DxfChangeListRepeatedActionTests
{
    [AvaloniaFact]
    public async Task RestoreLayer_AfterImportingDxfWithoutThatPad_PreservesReplacementAndSelectionAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var fixture = new WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        var oldId = vm.CadPads.Max(static pad => pad.Id);
        await SelectCadPadsAsync(vm, [oldId]);
        vm.DxfEditTargetLayerName = "edited";
        vm.MoveSelectedCadPadsToLayerCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        var changes = await OpenChangesAsync(vm, DxfEditChangeKind.Moved);
        Assert.Equal(oldId, Assert.Single(changes.Rows).CadPadId);
        var path = CreateTempDxfFile(("replacement", 100, 0, 110, 10));
        try
        {
            vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(path);
            await vm.OpenDxfCommand.ExecuteAsync(null);
            await WaitForGridRebuildAsync(vm);
            var replacement = Assert.Single(vm.CadPads);
            Assert.NotEqual(oldId, replacement.Id);
            await SelectCadPadsAsync(vm, [replacement.Id]);
            vm.HasUnsavedChanges = false;
            var status = vm.StatusText;
            var revision = vm.SimulationWorkspaceSourceRevision;

            changes.ApplySelectedEntryCommand.Execute(null);
            await WaitForGridRebuildAsync(vm);

            Assert.Empty(changes.Rows);
            Assert.Equal(replacement.Id, Assert.Single(vm.CadPads).Id);
            Assert.Equal("replacement", vm.CadPads[0].Layer);
            Assert.Equal(replacement.Polygon.Vertices.ToArray(), vm.CadPads[0].Polygon.Vertices.ToArray());
            Assert.Equal(replacement.Id, Assert.Single(vm.SelectedCadPadIds));
            Assert.False(vm.HasUnsavedChanges);
            Assert.Equal(status, vm.StatusText);
            Assert.Equal(revision, vm.SimulationWorkspaceSourceRevision);
            Assert.Equal(0, vm.RelayeredCadPadCount);
        }
        finally
        {
            new FileInfo(path).Delete();
        }
    }

    private static async Task<DxfEditChangeListViewModel> OpenChangesAsync(FreeformHelperViewModel vm, DxfEditChangeKind kind)
    {
        DxfEditChangeListViewModel? opened = null;
        vm.OpenDxfEditChangeListAsync = changes =>
        {
            opened = changes;
            return Task.CompletedTask;
        };
        var command = kind switch
        {
            DxfEditChangeKind.Hidden => vm.OpenHiddenDxfEditChangeListCommand,
            DxfEditChangeKind.Duplicate => vm.OpenDuplicateDxfEditChangeListCommand,
            DxfEditChangeKind.Combined => vm.OpenCombinedDxfEditChangeListCommand,
            DxfEditChangeKind.Moved => vm.OpenMovedDxfEditChangeListCommand,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        await command.ExecuteAsync(null);
        var result = Assert.IsType<DxfEditChangeListViewModel>(opened);
        Assert.Equal(kind, result.SelectedKind);
        return result;
    }
}
