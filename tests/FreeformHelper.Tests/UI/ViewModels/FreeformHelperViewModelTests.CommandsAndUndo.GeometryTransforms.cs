using System.Reflection;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class FreeformHelperViewModelTests
{


    [Fact]
    public async Task RotateSelectedCadPadsCommand_RotatesSelectedPadAndCanUndo()
    {
        var vm = new FreeformHelperViewModel();
        var source = new[]
        {
            CreateCad(1, "signal", 0, 0, 10, 20),
        };
        InitializeDxfEditCadState(vm, source);
        vm.DxfEditRotationDegrees = 90m;

        await SelectCadPadsAsync(vm, [1]);
        vm.RotateSelectedCadPadsCommand.Execute(null);

        Assert.True(vm.HasRotatedCadPads);
        Assert.Equal(1, vm.RotatedCadPadCount);

        var rotatedPad = Assert.Single(GetCurrentCadPadSet(vm).Pads);
        Assert.Equal(source[0].Centroid.X, rotatedPad.Centroid.X, 6);
        Assert.Equal(source[0].Centroid.Y, rotatedPad.Centroid.Y, 6);
        Assert.Equal(-5, rotatedPad.Bounds.MinX, 6);
        Assert.Equal(5, rotatedPad.Bounds.MinY, 6);
        Assert.Equal(15, rotatedPad.Bounds.MaxX, 6);
        Assert.Equal(15, rotatedPad.Bounds.MaxY, 6);

        vm.UndoCommand.Execute(null);

        Assert.False(vm.HasRotatedCadPads);
        Assert.Equal(0, vm.RotatedCadPadCount);

        var restoredPad = Assert.Single(GetCurrentCadPadSet(vm).Pads);
        Assert.Equal(source[0].Bounds.MinX, restoredPad.Bounds.MinX, 6);
        Assert.Equal(source[0].Bounds.MinY, restoredPad.Bounds.MinY, 6);
        Assert.Equal(source[0].Bounds.MaxX, restoredPad.Bounds.MaxX, 6);
        Assert.Equal(source[0].Bounds.MaxY, restoredPad.Bounds.MaxY, 6);
    }


    [Fact]
    public async Task RotateSelectedCadPadsCommand_RotatesSelectedPadsAsGroupAroundSelectionCenter()
    {
        var vm = new FreeformHelperViewModel();
        var source = new[]
        {
            CreateCad(1, "signal", 0, 0, 4, 2),
            CreateCad(2, "signal", 8, 0, 12, 2),
        };
        InitializeDxfEditCadState(vm, source);
        vm.DxfEditRotationDegrees = 90m;

        await SelectCadPadsAsync(vm, [1, 2]);
        vm.RotateSelectedCadPadsCommand.Execute(null);

        Assert.Equal(2, vm.RotatedCadPadCount);
        var rotatedPads = GetCurrentCadPadSet(vm).Pads.OrderBy(static pad => pad.Id).ToArray();
        Assert.Equal(6, rotatedPads[0].Centroid.X, 6);
        Assert.Equal(-3, rotatedPads[0].Centroid.Y, 6);
        Assert.Equal(6, rotatedPads[1].Centroid.X, 6);
        Assert.Equal(5, rotatedPads[1].Centroid.Y, 6);
    }


    [Fact]
    public void RotateSelectedCadPadsCommand_WhenScopeIsTargetLayer_RotatesVisibleBaselinePadsInLayer()
    {
        var vm = new FreeformHelperViewModel();
        var source = new[]
        {
            CreateCad(1, "signal", 0, 0, 4, 2),
            CreateCad(2, "signal", 8, 0, 12, 2),
            CreateCad(3, "aux", 20, 0, 24, 2),
        };
        InitializeDxfEditCadState(vm, source);
        vm.SelectedDxfEditRotationScopeOption = vm.DxfEditRotationScopeOptions.Single(static option => option.Value == FreeformHelperViewModel.DxfEditRotationScope.TargetLayer);
        vm.DxfEditRotationLayerName = "signal";
        vm.DxfEditRotationDegrees = 90m;

        vm.RotateSelectedCadPadsCommand.Execute(null);

        Assert.Equal(2, vm.RotatedCadPadCount);
        var rotatedPads = GetCurrentCadPadSet(vm).Pads.OrderBy(static pad => pad.Id).ToArray();
        Assert.Equal(6, rotatedPads[0].Centroid.X, 6);
        Assert.Equal(-3, rotatedPads[0].Centroid.Y, 6);
        Assert.Equal(6, rotatedPads[1].Centroid.X, 6);
        Assert.Equal(5, rotatedPads[1].Centroid.Y, 6);
        Assert.Equal(source[2].Centroid.X, rotatedPads[2].Centroid.X, 6);
        Assert.Equal(source[2].Centroid.Y, rotatedPads[2].Centroid.Y, 6);
    }


    [Fact]
    public async Task OffsetSelectedCadOutputFwDiffIndicesCommand_ShiftsSelectedVisibleCadDiffsAndCanUndo()
    {
        var vm = new FreeformHelperViewModel();
        var source = new[]
        {
            CreateCad(1, "signal", 0, 0, 10, 10),
            CreateCad(2, "signal", 10, 0, 20, 10),
            CreateCad(3, "signal", 20, 0, 30, 10),
        };
        var grid = new RegularGrid(
            rows: 1,
            cols: 3,
            xEdges: ShiftGridXEdges,
            yEdges: ShiftGridYEdges,
            pads: new[]
            {
                CreateRegularPad(100, row: 0, col: 0, minX: 0, minY: 0, maxX: 10, maxY: 10, diffIndex: 10),
                CreateRegularPad(101, row: 0, col: 1, minX: 10, minY: 0, maxX: 20, maxY: 10, diffIndex: 11),
                CreateRegularPad(102, row: 0, col: 2, minX: 20, minY: 0, maxX: 30, maxY: 10, diffIndex: 12),
            });

        InitializeVisibleCadIndexing(vm, source, grid);
        SetPadMatchResults(vm, ShiftVisibleCadMatches);

        var updateVisibleCadIndexingMethod = typeof(FreeformHelperViewModel).GetMethod("UpdateCadOutputFwDiffIndexing", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(updateVisibleCadIndexingMethod);
        updateVisibleCadIndexingMethod!.Invoke(vm, new object?[] { source });

        await SelectCadPadsAsync(vm, SelectedCad23);
        vm.CadOutputFwDiffIndexShiftDelta = 1m;

        vm.OffsetSelectedCadOutputFwDiffIndicesCommand.Execute(null);

        var projectFile = GetPrivateField<ProjectFile>(vm, "_projectFile");
        Assert.Equal(12, projectFile.CadOutputFwDiffIndexOverrides[2]);
        Assert.Equal(13, projectFile.CadOutputFwDiffIndexOverrides[3]);
        Assert.Equal("Shifted 2 CAD pad(s) by 1. Range 11-12 -> 12-13.", vm.StatusText);

        var cadOutputFwDiffByCad = GetPrivateField<Dictionary<int, int>>(vm, "_cadOutputFwDiffIndexByCadId");
        Assert.Equal(10, cadOutputFwDiffByCad[1]);
        Assert.Equal(12, cadOutputFwDiffByCad[2]);
        Assert.Equal(13, cadOutputFwDiffByCad[3]);

        vm.UndoCommand.Execute(null);

        Assert.Empty(projectFile.CadOutputFwDiffIndexOverrides);
        cadOutputFwDiffByCad = GetPrivateField<Dictionary<int, int>>(vm, "_cadOutputFwDiffIndexByCadId");
        Assert.Equal(10, cadOutputFwDiffByCad[1]);
        Assert.Equal(11, cadOutputFwDiffByCad[2]);
        Assert.Equal(12, cadOutputFwDiffByCad[3]);
    }


    [Fact]
    public async Task ResetAllDxfEditsCommand_RestoresBaselineAcrossHiddenCombinedLayerMovesAndRotation()
    {
        var vm = new FreeformHelperViewModel();
        var source = new[]
        {
            CreateCad(1, "signal", 0, 0, 10, 10),
            CreateCad(2, "signal", 12, 0, 22, 10),
            CreateCad(3, "signal", 20, 0, 30, 10),
        };
        InitializeDxfEditCadState(vm, source);

        vm.DxfEditTargetLayerName = "moved";
        await SelectCadPadsAsync(vm, [1]);
        vm.MoveSelectedCadPadsToLayerCommand.Execute(null);
        await SelectCadPadsAsync(vm, [1]);
        vm.DeleteSelectedCadPadsCommand.Execute(null);
        vm.DxfEditRotationDegrees = 90m;
        await SelectCadPadsAsync(vm, [2]);
        vm.RotateSelectedCadPadsCommand.Execute(null);
        await SelectCadPadsAsync(vm, [2, 3]);
        vm.CombineSelectedCadPadsCommand.Execute(null);

        Assert.True(vm.HasAnyCadEdits);
        Assert.True(vm.HasDeletedCadPads);
        Assert.True(vm.HasCombinedCadPads);
        Assert.True(vm.HasRelayeredCadPads);
        Assert.True(vm.HasRotatedCadPads);
        Assert.Equal(1, vm.CombinedCadPadCount);

        vm.ResetAllDxfEditsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);

        Assert.False(vm.HasAnyCadEdits);
        Assert.False(vm.HasDeletedCadPads);
        Assert.False(vm.HasCombinedCadPads);
        Assert.False(vm.HasRelayeredCadPads);
        Assert.False(vm.HasRotatedCadPads);
        Assert.Equal(0, vm.DeletedCadPadCount);
        Assert.Equal(0, vm.CombinedCadPadCount);
        Assert.Equal(0, vm.RelayeredCadPadCount);
        Assert.Equal(0, vm.RotatedCadPadCount);

        var currentCad = GetCurrentCadPadSet(vm);
        Assert.Equal(source.Length, currentCad.Pads.Count);
        Assert.DoesNotContain(currentCad.Pads, pad => pad.Name.StartsWith("COMB_", StringComparison.Ordinal));
        foreach (var expected in source)
        {
            var restored = Assert.Single(currentCad.Pads, pad => pad.Id == expected.Id);
            Assert.Equal(expected.Layer, restored.Layer);
            Assert.Equal(expected.Area, restored.Area, 6);
        }
    }


    [Fact]
    public async Task DxfEditChangeListApply_OnCombinedRow_ClearsWholeCombineGroup()
    {
        var vm = new FreeformHelperViewModel();
        var source = new[]
        {
            CreateRectCad(1, 0, 0, 10, 10),
            CreateRectCad(2, 30, 0, 40, 10),
        };

        var cadField = typeof(FreeformHelperViewModel).GetField("_cad", BindingFlags.Instance | BindingFlags.NonPublic);
        var resetBaselineMethod = typeof(FreeformHelperViewModel).GetMethod("ResetDxfEditBaseline", BindingFlags.Instance | BindingFlags.NonPublic);
        var buildEntriesMethod = typeof(FreeformHelperViewModel).GetMethod("BuildDxfEditChangeEntries", BindingFlags.Instance | BindingFlags.NonPublic);
        var applyEntryMethod = typeof(FreeformHelperViewModel).GetMethod("ApplyDxfEditChangeEntry", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(cadField);
        Assert.NotNull(resetBaselineMethod);
        Assert.NotNull(buildEntriesMethod);
        Assert.NotNull(applyEntryMethod);

        cadField!.SetValue(vm, new CadPadSet(source));
        resetBaselineMethod!.Invoke(vm, new object?[] { new CadPadSet(source) });
        await SelectCadPadsAsync(vm, PairCadSelection);
        vm.CombineSelectedCadPadsCommand.Execute(null);

        Assert.Equal(2, vm.CombinedCadPadCount);

        var entries = (List<DxfEditChangeListEntry>)buildEntriesMethod!.Invoke(vm, null)!;
        var combinedEntry = Assert.Single(entries.Where(entry => entry.Kind == DxfEditChangeKind.Combined).Take(1));
        applyEntryMethod!.Invoke(vm, new object?[] { combinedEntry });

        Assert.Equal(0, vm.CombinedCadPadCount);
        Assert.Equal(0, vm.DeletedCadPadCount);
    }


    [Fact]
    public async Task DxfEditChangeListApply_OnRotatedRow_RestoresGeometry()
    {
        var vm = new FreeformHelperViewModel();
        var source = new[]
        {
            CreateCad(1, "signal", 0, 0, 10, 20),
        };
        InitializeDxfEditCadState(vm, source);
        vm.DxfEditRotationDegrees = 90m;

        var buildEntriesMethod = typeof(FreeformHelperViewModel).GetMethod("BuildDxfEditChangeEntries", BindingFlags.Instance | BindingFlags.NonPublic);
        var applyEntryMethod = typeof(FreeformHelperViewModel).GetMethod("ApplyDxfEditChangeEntry", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(buildEntriesMethod);
        Assert.NotNull(applyEntryMethod);

        await SelectCadPadsAsync(vm, [1]);
        vm.RotateSelectedCadPadsCommand.Execute(null);
        Assert.Equal(1, vm.RotatedCadPadCount);

        var entries = Assert.IsType<List<DxfEditChangeListEntry>>(buildEntriesMethod!.Invoke(vm, null));
        var rotatedEntry = Assert.Single(entries, entry => entry.Kind == DxfEditChangeKind.Rotated);
        applyEntryMethod!.Invoke(vm, [rotatedEntry]);

        Assert.Equal(0, vm.RotatedCadPadCount);
        var restoredPad = Assert.Single(GetCurrentCadPadSet(vm).Pads);
        Assert.Equal(source[0].Bounds.MinX, restoredPad.Bounds.MinX, 6);
        Assert.Equal(source[0].Bounds.MinY, restoredPad.Bounds.MinY, 6);
        Assert.Equal(source[0].Bounds.MaxX, restoredPad.Bounds.MaxX, 6);
        Assert.Equal(source[0].Bounds.MaxY, restoredPad.Bounds.MaxY, 6);
    }
}
