using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.CadImport;
using FreeformHelper.UI.Icons;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed partial class FreeformHelperViewModelTests
{


    [Fact]
    public async Task ApplyCadLoadOutcomeAsync_ClearsStaleCombinedMetadataBeforeApplyingHiddenRows()
    {
        var vm = new FreeformHelperViewModel();
        var source = new[]
        {
            CreateRectCad(1, 0, 0, 10, 10),
            CreateRectCad(2, 8, 0, 18, 10),
        };

        FreeformHelperViewModelPrivateAccess.Cad(vm) = new CadPadSet(source);
        FreeformHelperViewModelPrivateAccess.ResetDxfEditBaseline(vm, new CadPadSet(source));
        await SelectCadPadsAsync(vm, _pairCadSelection);
        vm.CombineSelectedCadPadsCommand.Execute(null);
        Assert.True(vm.HasCombinedCadPads);

        var nextCad = new CadPadSet(new[]
        {
            CreateRectCad(1, 100, 0, 110, 10),
        });
        var applyTask = Assert.IsAssignableFrom<Task>(FreeformHelperViewModelPrivateAccess.ApplyCadLoadOutcomeAsync(
                vm,
                new CadLoadOutcome(
                    nextCad,
                    CadLoadSource.Path,
                    @"C:\temp\next.dxf",
                    new CadPadExactDuplicateSanitizationResult(
                        nextCad,
                        0,
                        Array.Empty<int>(),
                        new Dictionary<int, int>())),
                null));
        await applyTask;
        var persistedState = FreeformHelperViewModel.CapturePersistedDxfEditProjectState(new ProjectFile
        {
            HiddenCadPadIds = new HashSet<int> { 1 },
        });
        vm.ApplyCapturedDxfEditProjectState(persistedState);

        var entries = Assert.IsType<List<DxfEditChangeListEntry>>(FreeformHelperViewModelPrivateAccess.BuildDxfEditChangeEntries(vm));
        var hiddenEntry = Assert.Single(entries, entry => entry.Kind == DxfEditChangeKind.Hidden);
        Assert.Equal("Hidden", hiddenEntry.StatusText);
        Assert.Equal("Restore", hiddenEntry.ActionLabel);
    }


    [Fact]
    public async Task ApplyCadLoadOutcomeAsync_PreservesRawDuplicatePads_ButAutoHidesThemFromWorkingSet()
    {
        var vm = new FreeformHelperViewModel();
        var duplicateCadIds = new[] { 1 };
        var expectedVisibleCadIds = new[] { 0, 2 };

        var rawCad = new CadPadSet(new[]
        {
            CreateRectCad(0, 0, 0, 10, 10),
            CreateRectCad(1, 0, 0, 10, 10),
            CreateCad(2, "OTHER", 0, 0, 10, 10),
        });

        var applyTask = Assert.IsAssignableFrom<Task>(FreeformHelperViewModelPrivateAccess.ApplyCadLoadOutcomeAsync(
                vm,
                new CadLoadOutcome(
                    rawCad,
                    CadLoadSource.Path,
                    @"C:\temp\duplicate.dxf",
                    new CadPadExactDuplicateSanitizationResult(
                        rawCad,
                        1,
                        duplicateCadIds,
                        new Dictionary<int, int> { [1] = 0 })),
                null));
        await applyTask;

        var currentCad = GetCurrentCadPadSet(vm);
        Assert.Equal(3, currentCad.Pads.Count);
        Assert.Equal(0, vm.DeletedCadPadCount);
        Assert.Equal(expectedVisibleCadIds, vm.CadPads.Select(static pad => pad.Id).OrderBy(static id => id).ToArray());

        var activeCad = Assert.IsType<CadPadSet>(FreeformHelperViewModelPrivateAccess.BuildActiveCadPadSet(vm));
        Assert.Equal(expectedVisibleCadIds, activeCad.Pads.Select(static pad => pad.Id).OrderBy(static id => id).ToArray());
    }


    [Fact]
    public async Task ResetAllDxfEditsCommand_PreservesAutoHiddenDuplicatePads()
    {
        var vm = new FreeformHelperViewModel();
        var duplicateCadIds = new[] { 1 };
        var expectedVisibleCadIds = new[] { 0, 2 };
        var selectedCadIds = new[] { 2 };

        var rawCad = new CadPadSet(new[]
        {
            CreateRectCad(0, 0, 0, 10, 10),
            CreateRectCad(1, 0, 0, 10, 10),
            CreateCad(2, "OTHER", 20, 0, 30, 10),
        });

        var applyTask = Assert.IsAssignableFrom<Task>(FreeformHelperViewModelPrivateAccess.ApplyCadLoadOutcomeAsync(
                vm,
                new CadLoadOutcome(
                    rawCad,
                    CadLoadSource.Path,
                    @"C:\temp\duplicate.dxf",
                    new CadPadExactDuplicateSanitizationResult(
                        rawCad,
                        1,
                        duplicateCadIds,
                        new Dictionary<int, int> { [1] = 0 })),
                null));
        await applyTask;

        await SelectCadPadsAsync(vm, selectedCadIds);
        vm.DeleteSelectedCadPadsCommand.Execute(null);
        Assert.Equal(1, vm.DeletedCadPadCount);

        vm.ResetAllDxfEditsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);

        Assert.Equal(0, vm.DeletedCadPadCount);
        Assert.Equal(expectedVisibleCadIds, vm.CadPads.Select(static pad => pad.Id).OrderBy(static id => id).ToArray());

        var currentCad = GetCurrentCadPadSet(vm);
        Assert.Equal(3, currentCad.Pads.Count);
    }


    [Fact]
    public async Task DxfEditChangeListApply_OnDuplicateRow_TogglesDuplicateVisibility()
    {
        var vm = new FreeformHelperViewModel();

        var rawCad = new CadPadSet(new[]
        {
            CreateRectCad(0, 0, 0, 10, 10),
            CreateRectCad(1, 0, 0, 10, 10),
            CreateCad(2, "OTHER", 20, 0, 30, 10),
        });

        var applyTask = Assert.IsAssignableFrom<Task>(FreeformHelperViewModelPrivateAccess.ApplyCadLoadOutcomeAsync(
                vm,
                new CadLoadOutcome(
                    rawCad,
                    CadLoadSource.Path,
                    @"C:\temp\duplicate.dxf",
                    new CadPadExactDuplicateSanitizationResult(
                        rawCad,
                        1,
                        [1],
                        new Dictionary<int, int> { [1] = 0 })),
                null));
        await applyTask;

        var entries = Assert.IsType<List<DxfEditChangeListEntry>>(FreeformHelperViewModelPrivateAccess.BuildDxfEditChangeEntries(vm));
        var duplicateEntry = Assert.Single(entries, entry => entry.Kind == DxfEditChangeKind.Duplicate);
        Assert.Equal(0, duplicateEntry.FocusCadPadId);
        Assert.Equal("Auto-hidden duplicate", duplicateEntry.StatusText);
        Assert.Equal("Restore", duplicateEntry.ActionLabel);

        FreeformHelperViewModelPrivateAccess.ApplyDxfEditChangeEntry(vm, duplicateEntry);

        Assert.Equal([0, 1, 2], vm.CadPads.Select(static pad => pad.Id).OrderBy(static id => id).ToArray());

        entries = Assert.IsType<List<DxfEditChangeListEntry>>(FreeformHelperViewModelPrivateAccess.BuildDxfEditChangeEntries(vm));
        duplicateEntry = Assert.Single(entries, entry => entry.Kind == DxfEditChangeKind.Duplicate);
        Assert.Equal(1, duplicateEntry.FocusCadPadId);
        Assert.Equal("Restored duplicate", duplicateEntry.StatusText);
        Assert.Equal("Hide again", duplicateEntry.ActionLabel);
    }


    [Fact]
    public async Task RestoreHiddenCadPad_PreservesRemainingUndoBatches()
    {
        var vm = new FreeformHelperViewModel();
        var source = new[]
        {
            CreateRectCad(1, 0, 0, 10, 10),
            CreateRectCad(2, 12, 0, 22, 10),
        };

        FreeformHelperViewModelPrivateAccess.Cad(vm) = new CadPadSet(source);

        await SelectCadPadsAsync(vm, [1]);
        vm.DeleteSelectedCadPadsCommand.Execute(null);
        await SelectCadPadsAsync(vm, [2]);
        vm.DeleteSelectedCadPadsCommand.Execute(null);
        Assert.Equal(2, vm.DeletedCadPadCount);

        FreeformHelperViewModelPrivateAccess.RestoreHiddenCadPad(vm, 2);
        Assert.Equal(1, vm.DeletedCadPadCount);

        vm.RestoreLastDeletedCadPadsCommand.Execute(null);

        Assert.Equal(0, vm.DeletedCadPadCount);
    }


    [Fact]
    public async Task DxfEditChangeList_ListsEveryChangeKindWithStableRowContent()
    {
        var path = CreateTempDxfFile(
            ("signal", 0, 0, 10, 10),
            ("signal", 0, 0, 10, 10),
            ("signal", 20, 0, 30, 10),
            ("signal", 40, 0, 50, 10),
            ("signal", 60, 0, 70, 10),
            ("signal", 80, 0, 90, 10),
            ("signal", 100, 0, 110, 10));
        try
        {
            var vm = new FreeformHelperViewModel { PickOpenDxfPathAsync = () => Task.FromResult<string?>(path) };
            await vm.OpenDxfCommand.ExecuteAsync(null);
            var ids = vm.CadPads.Select(static pad => pad.Id).OrderBy(static id => id).ToArray();
            vm.DxfEditTargetLayerName = "moved";
            await SelectCadPadsAsync(vm, [ids[1]]);
            vm.MoveSelectedCadPadsToLayerCommand.Execute(null);
            vm.DxfEditRotationDegrees = 90m;
            await SelectCadPadsAsync(vm, [ids[2]]);
            vm.RotateSelectedCadPadsCommand.Execute(null);
            await SelectCadPadsAsync(vm, [ids[3], ids[4]]);
            vm.CombineSelectedCadPadsCommand.Execute(null);
            await SelectCadPadsAsync(vm, [ids[5]]);
            vm.DeleteSelectedCadPadsCommand.Execute(null);

            DxfEditChangeListViewModel? list = null;
            vm.OpenDxfEditChangeListAsync = viewModel =>
            {
                list = viewModel;
                return Task.CompletedTask;
            };
            await vm.OpenHiddenDxfEditChangeListCommand.ExecuteAsync(null);
            var changeList = Assert.IsType<DxfEditChangeListViewModel>(list);
            var lines = DescribeChangeListRows(changeList);

            var clearGroupHint = "Clear this combine group and restore its source pads.";
            Assert.Equal(
                [
                    $"Hidden|4|4|PAD_5|Hidden by combine|Layer signal|Group 1|Combine group 1 hides this source pad. Source pads: 2.|{IconGlyphs.Remove}|Clear group|{clearGroupHint}|False|True",
                    $"Hidden|5|5|PAD_6|Hidden by combine|Layer signal|Group 1|Combine group 1 hides this source pad. Source pads: 2.|{IconGlyphs.Remove}|Clear group|{clearGroupHint}|False|True",
                    $"Hidden|6|6|PAD_7|Hidden|Layer signal|Manual hide|Hidden manually from the current DXF edit session.|{IconGlyphs.Undo}|Restore|Restore this hidden CAD pad.|False|True",
                    $"Duplicate|1|0|PAD_2|Auto-hidden duplicate|Layer signal · Canonical CAD 0|Canonical CAD 0|Exact same-layer duplicate of CAD 0 (PAD_1).|{IconGlyphs.Undo}|Restore|Restore this duplicate CAD pad into the active working set.|True|True",
                    $"Combined|1500000000|1500000000|COMB_1500000000|Synthetic|Layer signal|Group 1 · 2 src|Combine group 1 output. Source pads: 2.|{IconGlyphs.Remove}|Clear group|{clearGroupHint}|True|True",
                    $"Combined|1500000001|1500000001|COMB_1500000001|Synthetic|Layer signal|Group 1 · 2 src|Combine group 1 output. Source pads: 2.|{IconGlyphs.Remove}|Clear group|{clearGroupHint}|True|True",
                    $"Moved|2|2|PAD_3|Moved|Layer moved|signal -> moved|Original layer: signal -> Current layer: moved.|{IconGlyphs.Undo}|Restore layer|Move this CAD pad back to layer 'signal'.|True|True",
                    $"Rotated|3|3|PAD_4|Rotated|Layer signal|Geometry differs from DXF baseline|Polygon vertices were rotated relative to the imported DXF baseline. Restore resets this pad geometry only.|{IconGlyphs.Undo}|Restore geometry|Restore this CAD pad geometry back to DXF baseline.|True|True",
                ],
                lines);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static List<string> DescribeChangeListRows(DxfEditChangeListViewModel changeList)
    {
        var lines = new List<string>();
        foreach (var selectKind in new[]
        {
            changeList.SetHiddenKindCommand,
            changeList.SetDuplicateKindCommand,
            changeList.SetCombinedKindCommand,
            changeList.SetMovedKindCommand,
            changeList.SetRotatedKindCommand,
        })
        {
            selectKind.Execute(null);
            lines.AddRange(changeList.Rows.Select(static entry => string.Join(
                "|",
                entry.Kind,
                entry.CadPadId,
                entry.FocusCadPadId,
                entry.CadName,
                entry.StatusText,
                entry.CurrentLayerText,
                entry.ListDetailText,
                entry.DetailText,
                entry.ActionGlyph,
                entry.ActionLabel,
                entry.ActionHint,
                entry.CanFocus,
                entry.CanApply)));
        }

        return lines;
    }

    private static readonly int[] _singleCadSelection = [1];
    private static readonly int[] _pairCadSelection = [1, 2];
    private static readonly int[] _selectedCad23 = [2, 3];
    private static readonly double[] _shiftGridXEdges = [0d, 10d, 20d, 30d];
    private static readonly double[] _shiftGridYEdges = [0d, 10d];
    private static readonly (int CadPadId, int RegularPadId)[] _shiftVisibleCadMatches =
    [
        (1, 100),
        (2, 101),
        (3, 102),
    ];

    private static CadPad CreateCad(int id, string layer, double minX, double minY, double maxX, double maxY)
    {
        var polygon = new Polygon2(new[]
        {
            new Point2(minX, minY),
            new Point2(maxX, minY),
            new Point2(maxX, maxY),
            new Point2(minX, maxY),
        });

        return new CadPad(id, $"CAD{id}", layer, polygon);
    }

    private static void InitializeDxfEditCadState(FreeformHelperViewModel vm, IReadOnlyList<CadPad> source)
    {
        var cad = new CadPadSet(source);

        FreeformHelperViewModelPrivateAccess.Cad(vm) = cad;
        FreeformHelperViewModelPrivateAccess.ResetDxfEditBaseline(vm, cad);
        FreeformHelperViewModelPrivateAccess.UpdateLayerToggles(vm, cad);
    }

    private static CadPadSet GetCurrentCadPadSet(FreeformHelperViewModel vm)
    {
        return Assert.IsType<CadPadSet>(FreeformHelperViewModelPrivateAccess.Cad(vm));
    }

    private static int[] GetCombinedCadPadIds(FreeformHelperViewModel vm)
    {
        var cad = GetCurrentCadPadSet(vm);
        return cad.Pads
            .Where(static pad => pad.Name.StartsWith("COMB_", StringComparison.Ordinal))
            .Select(static pad => pad.Id)
            .ToArray();
    }

    private static void InitializeVisibleCadIndexing(FreeformHelperViewModel vm, IReadOnlyList<CadPad> source, RegularGrid grid)
    {
        var cad = new CadPadSet(source);

        FreeformHelperViewModelPrivateAccess.Cad(vm) = cad;
        FreeformHelperViewModelPrivateAccess.Grid(vm) = grid;
        vm.CadPads = new System.Collections.ObjectModel.ObservableCollection<CadPad>(source);

        var projectFile = Assert.IsType<ProjectFile>(FreeformHelperViewModelPrivateAccess.ProjectFile(vm));
        projectFile.Settings.Grid.XChannels = grid.Cols;
        projectFile.Settings.Grid.YChannels = grid.Rows;
        projectFile.Settings.Grid.ActiveAreaWidth = grid.XEdges[^1] - grid.XEdges[0];
        projectFile.Settings.Grid.ActiveAreaHeight = grid.YEdges[^1] - grid.YEdges[0];
    }

    private static RegularPad CreateRegularPad(int regularPadId, int row, int col, double minX, double minY, double maxX, double maxY, int diffIndex)
    {
        var pad = CreateRectRegular(row, col, regularPadId, minX, minY, maxX, maxY);
        pad.IcIndex = 0;
        pad.DiffIndex = diffIndex;
        return pad;
    }
}
