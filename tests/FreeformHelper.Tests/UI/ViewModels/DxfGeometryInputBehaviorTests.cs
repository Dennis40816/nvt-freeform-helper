// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfGeometryInputBehaviorTests
{
    [AvaloniaFact]
    public async Task RotateCommand_WithoutDxf_RequestsImportWithoutCreatingEditsAsync()
    {
        var vm = CreateViewModel();
        await WaitForGridRebuildAsync(vm);
        vm.HasUnsavedChanges = false;

        vm.RotateSelectedCadPadsCommand.Execute(null);

        Assert.Equal("DXF rotate: import DXF first.", vm.StatusText);
        Assert.False(vm.CanRotateSelectedCadPads);
        Assert.Empty(vm.CadPads);
        Assert.False(vm.HasAnyCadEdits);
        Assert.False(vm.HasUnsavedChanges);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(360)]
    [InlineData(-720)]
    public async Task RotateCommand_FullTurns_KeepGeometrySelectionAndUndoUnchangedAsync(int degrees)
    {
        using var culture = TestCulture.UseEnUs();
        var vm = await OpenWorkspaceAsync(("signal", 0, 0, 10, 20));
        var original = Assert.Single(vm.CadPads);
        await SelectCadPadsAsync(vm, [original.Id]);
        vm.DxfEditRotationDegrees = degrees;
        vm.HasUnsavedChanges = false;
        Assert.False(vm.CanRotateSelectedCadPads);

        vm.RotateSelectedCadPadsCommand.Execute(null);

        Assert.Equal("DXF rotate: input a non-zero angle first.", vm.StatusText);
        Assert.Equal(original.Polygon.Vertices.ToArray(), Assert.Single(vm.CadPads).Polygon.Vertices.ToArray());
        Assert.Equal(original.Id, Assert.Single(vm.SelectedCadPadIds));
        Assert.False(vm.HasAnyCadEdits);
        Assert.False(vm.HasUnsavedChanges);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaTheory]
    [InlineData(false, "", false, "DXF rotate: select baseline CAD pads first.")]
    [InlineData(true, "   ", false, "DXF rotate: select rotation layer first.")]
    [InlineData(true, "missing", false, "DXF rotate: selected layer has no visible baseline CAD pads.")]
    [InlineData(true, "signal", true, "DXF rotate: selected layer has no visible baseline CAD pads.")]
    public async Task RotateCommand_UnresolvedScope_ReportsReasonWithoutChangingEditsAsync(
        bool targetLayer, string layerName, bool hidePad, string expectedStatus)
    {
        using var culture = TestCulture.UseEnUs();
        var vm = await OpenWorkspaceAsync(("signal", 0, 0, 10, 20));
        if (hidePad)
        {
            await SelectCadPadsAsync(vm, [Assert.Single(vm.CadPads).Id]);
            vm.DeleteSelectedCadPadsCommand.Execute(null);
            await WaitForGridRebuildAsync(vm);
        }
        if (targetLayer)
        {
            vm.SelectedDxfEditRotationScopeOption = vm.DxfEditRotationScopeOptions.Single(
                static option => option.Value == FreeformHelperViewModel.DxfEditRotationScope.TargetLayer);
        }
        vm.DxfEditRotationLayerName = layerName;
        vm.DxfEditRotationDegrees = 45m;
        vm.HasUnsavedChanges = false;
        var before = vm.CadPads.ToArray();
        var couldUndo = vm.CanUndo;
        Assert.False(vm.CanRotateSelectedCadPads);

        vm.RotateSelectedCadPadsCommand.Execute(null);

        Assert.Equal(expectedStatus, vm.StatusText);
        Assert.Equal(before, vm.CadPads.ToArray());
        Assert.Empty(vm.SelectedCadPadIds);
        Assert.Equal(hidePad ? 1 : 0, vm.DeletedCadPadCount);
        Assert.Equal(0, vm.RotatedCadPadCount);
        Assert.False(vm.HasUnsavedChanges);
        Assert.Equal(couldUndo, vm.CanUndo);
    }

    [AvaloniaFact]
    public async Task LoadProject_GeometryOverrides_KeepValidChangesIgnoreIncompleteShapesAndRestoreBaselineAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var dxfPath = CreateTempDxfFile(
            ("signal", 0, 0, 10, 20), ("signal", 30, 0, 40, 20),
            ("signal", 60, 0, 70, 20), ("signal", 90, 0, 100, 20));
        var vm = CreateViewModel();
        vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(dxfPath);
        await vm.OpenDxfCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(vm);
        var originals = vm.CadPads.OrderBy(static pad => pad.Bounds.MinX).ToArray();
        Assert.Equal(4, originals.Length);
        var triangle = originals[0].Polygon.Vertices.Take(3).ToList();
        var shifted = originals[1].Polygon.Vertices.Select(static point => new Point2(point.X, point.Y + 2)).ToList();
        var project = new ProjectFile
        {
            LastDxfPath = dxfPath,
            DxfCadGeometryOverrides = new()
            {
                [originals[0].Id] = new ProjectCadGeometrySnapshot { Vertices = triangle },
                [originals[1].Id] = new ProjectCadGeometrySnapshot { Vertices = shifted },
                [originals[2].Id] = new ProjectCadGeometrySnapshot { Vertices = originals[2].Polygon.Vertices.Take(2).ToList() },
            },
        };
        var projectPath = TestFiles.NewTempFilePath("cov33-geometry-", ".json");
        await new JsonProjectStore().SaveAsync(projectPath, project, TestContext.Current.CancellationToken);

        Assert.True((await vm.LoadProjectFromPathAsync(projectPath)).IsLoaded);
        await WaitForGridRebuildAsync(vm);

        AssertVertices(vm, originals[0].Id, triangle);
        AssertVertices(vm, originals[1].Id, shifted);
        AssertVertices(vm, originals[2].Id, originals[2].Polygon.Vertices);
        AssertVertices(vm, originals[3].Id, originals[3].Polygon.Vertices);
        Assert.Equal(2, vm.RotatedCadPadCount);
        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(projectPath);
        vm.ConfirmEmbedDxfAsync = () => Task.FromResult(false);
        Assert.True(await vm.SaveProjectAsync());
        var persisted = JsonProjectStore.Load(projectPath).DxfCadGeometryOverrides;
        Assert.Equal(new[] { originals[0].Id, originals[1].Id }.Order(), persisted.Keys.Order());
        Assert.Equal(triangle, persisted[originals[0].Id].Vertices);
        Assert.Equal(shifted, persisted[originals[1].Id].Vertices);
        DxfEditChangeListViewModel? opened = null;
        vm.OpenDxfEditChangeListAsync = changes =>
        {
            opened = changes;
            return Task.CompletedTask;
        };
        await vm.OpenRotatedDxfEditChangeListCommand.ExecuteAsync(null);
        var changes = Assert.IsType<DxfEditChangeListViewModel>(opened);
        Assert.Equal(2, changes.Rows.Count);
        changes.SelectedEntry = Assert.Single(changes.Rows, row => row.CadPadId == originals[0].Id);

        changes.ApplySelectedEntryCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);

        AssertVertices(vm, originals[0].Id, originals[0].Polygon.Vertices);
        AssertVertices(vm, originals[1].Id, shifted);
        Assert.Equal(1, vm.RotatedCadPadCount);
        Assert.Equal(originals[1].Id, Assert.Single(changes.Rows).CadPadId);
    }

    private static FreeformHelperViewModel CreateViewModel() =>
        new(new AppGeneralSettingsStore(TestFiles.NewTempFilePath("cov33-geometry-app-", ".json")))
        {
            RecalcBoundsOnLayerFilter = false,
            ApplyAppVisualPreferencesOnProjectLoad = false,
        };

    private static async Task<FreeformHelperViewModel> OpenWorkspaceAsync(
        params (string Layer, double MinX, double MinY, double MaxX, double MaxY)[] pads)
    {
        var vm = CreateViewModel();
        var path = CreateTempDxfFile(pads);
        vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(path);
        await vm.OpenDxfCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(vm);
        return vm;
    }

    private static void AssertVertices(FreeformHelperViewModel vm, int cadPadId, IEnumerable<Point2> expected) =>
        Assert.Equal(expected.ToArray(), Assert.Single(vm.CadPads, pad => pad.Id == cadPadId).Polygon.Vertices.ToArray());
}
