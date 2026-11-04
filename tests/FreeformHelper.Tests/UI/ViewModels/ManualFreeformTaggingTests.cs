using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class ManualFreeformTaggingTests
{
    private static readonly double[] _threePadGridXEdges = [0.0, 10.0, 20.0, 30.0];
    private static readonly double[] _threePadGridYEdges = [0.0, 10.0];
    private static readonly int[] _selectedRegular0And2 = [0, 2];

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_grid")]
    private static extern ref RegularGrid? GridField(FreeformHelperViewModel viewModel);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_projectFile")]
    private static extern ref ProjectFile ProjectFileOf(FreeformHelperViewModel viewModel);

    private static RegularPad CreateRectRegular(int row, int col, int index, double minX, double minY, double maxX, double maxY)
    {
        var polygon = new Polygon2(new[]
        {
            new Point2(minX, minY),
            new Point2(maxX, minY),
            new Point2(maxX, maxY),
            new Point2(minX, maxY),
        });

        return new RegularPad(row, col, index, polygon);
    }

    private static FreeformHelperViewModel CreateViewModelWithThreeRegularPads(out IReadOnlyList<RegularPad> pads)
    {
        var padList = new List<RegularPad>
        {
            CreateRectRegular(row: 0, col: 0, index: 0, minX: 0, minY: 0, maxX: 10, maxY: 10),
            CreateRectRegular(row: 0, col: 1, index: 1, minX: 10, minY: 0, maxX: 20, maxY: 10),
            CreateRectRegular(row: 0, col: 2, index: 2, minX: 20, minY: 0, maxX: 30, maxY: 10),
        };
        var vm = new FreeformHelperViewModel
        {
            RegularPads = new ObservableCollection<RegularPad>(padList),
        };
        GridField(vm) = new RegularGrid(rows: 1, cols: 3, xEdges: _threePadGridXEdges, yEdges: _threePadGridYEdges, pads: padList);
        pads = padList;
        return vm;
    }

    [Fact]
    public void SetFreeformCommands_WithoutAGrid_ReportThatTheGridMustBeBuiltFirst()
    {
        var vm = new FreeformHelperViewModel();

        vm.SetFreeformXCommand.Execute(null);

        Assert.Equal("Freeform tagging: build grid first.", vm.StatusText);
        Assert.False(vm.HasUnsavedChanges);
    }

    [Fact]
    public void SetFreeformCommands_WithoutSelectedRegularPads_AskForASelectionAndChangeNothing()
    {
        var vm = CreateViewModelWithThreeRegularPads(out var pads);

        vm.SetFreeformXYCommand.Execute(null);

        Assert.Equal("Freeform tagging: select regular pads first.", vm.StatusText);
        Assert.All(pads, static pad => Assert.Equal(FreeformType.None, pad.Freeform));
        Assert.False(vm.HasUnsavedChanges);
    }

    [Theory]
    [InlineData("X", FreeformType.XWay)]
    [InlineData("Y", FreeformType.YWay)]
    [InlineData("XY", FreeformType.XYWay)]
    public void SetFreeformCommands_TagOnlyTheSelectedPads_AndRecordTheOverrides(string command, FreeformType expected)
    {
        var vm = CreateViewModelWithThreeRegularPads(out var pads);
        vm.ApplyCanvasSelection(Array.Empty<int>(), _selectedRegular0And2);

        switch (command)
        {
            case "X":
                vm.SetFreeformXCommand.Execute(null);
                break;
            case "Y":
                vm.SetFreeformYCommand.Execute(null);
                break;
            default:
                vm.SetFreeformXYCommand.Execute(null);
                break;
        }

        Assert.Equal(expected, pads[0].Freeform);
        Assert.Equal(FreeformType.None, pads[1].Freeform);
        Assert.Equal(expected, pads[2].Freeform);
        Assert.Equal($"Freeform set to {expected} for 2 regular pads.", vm.StatusText);
        Assert.True(vm.HasUnsavedChanges);
        var overrides = ProjectFileOf(vm).FreeformOverrides;
        Assert.Equal(2, overrides.Count);
        Assert.Equal(expected, overrides[0]);
        Assert.Equal(expected, overrides[2]);
        Assert.False(overrides.ContainsKey(1));
    }

    [Fact]
    public void SetFreeformNoneCommand_ClearsEarlierTagsAndRemovesTheirOverrides()
    {
        var vm = CreateViewModelWithThreeRegularPads(out var pads);
        vm.ApplyCanvasSelection(Array.Empty<int>(), _selectedRegular0And2);
        vm.SetFreeformXCommand.Execute(null);
        var overrides = ProjectFileOf(vm).FreeformOverrides;
        Assert.Equal(2, overrides.Count);

        vm.ApplyCanvasSelection(Array.Empty<int>(), [2]);
        vm.SetFreeformNoneCommand.Execute(null);

        Assert.Equal(FreeformType.XWay, pads[0].Freeform);
        Assert.Equal(FreeformType.None, pads[2].Freeform);
        Assert.Equal("Freeform set to None for 1 regular pads.", vm.StatusText);
        Assert.Single(overrides);
        Assert.Equal(FreeformType.XWay, overrides[0]);
    }

    [Fact]
    public async Task SetFreeformCommands_AreNotRunnableWhileProjectEditingIsDisabled()
    {
        var vm = CreateViewModelWithThreeRegularPads(out var pads);
        vm.ApplyCanvasSelection(Array.Empty<int>(), _selectedRegular0And2);
        var editingEnabledDuringLoad = true;
        var setXRunnableDuringLoad = true;
        var setNoneRunnableDuringLoad = true;
        vm.PickLoadProjectPathAsync = () =>
        {
            editingEnabledDuringLoad = vm.IsProjectEditingEnabled;
            setXRunnableDuringLoad = vm.SetFreeformXCommand.CanExecute(null);
            setNoneRunnableDuringLoad = vm.SetFreeformNoneCommand.CanExecute(null);
            return Task.FromResult<string?>(null);
        };

        await vm.LoadProjectCommand.ExecuteAsync(null);

        Assert.False(editingEnabledDuringLoad);
        Assert.False(setXRunnableDuringLoad);
        Assert.False(setNoneRunnableDuringLoad);
        Assert.All(pads, static pad => Assert.Equal(FreeformType.None, pad.Freeform));
    }

    [Fact]
    public void RegularPadInfo_FreeformButtons_ForwardTheTypeAndThePadIdsAndThenClosePadInfo()
    {
        var pads = new List<RegularPad>
        {
            CreateRectRegular(row: 0, col: 0, index: 5, minX: 0, minY: 0, maxX: 10, maxY: 10),
            CreateRectRegular(row: 0, col: 1, index: 6, minX: 10, minY: 0, maxX: 20, maxY: 10),
        };
        var applied = new List<(int[] Ids, FreeformType Type)>();
        var closed = 0;
        var info = new RegularPadInfoViewModel(
            pads: pads,
            toDisplayRow: static row => row,
            getDxfIndex: null,
            getMatchedCadPadIds: null,
            getMatchedCadLinks: null,
            applyPadSize: null,
            resetPadSize: null,
            setFreeform: (ids, type) => applied.Add((ids.ToArray(), type)),
            getCrossIcOwnerShares: null,
            getNotchRowsByRegularPad: null,
            highlightCadOwnerPads: null,
            focusRegularMatches: null,
            highlightRegularMatches: null,
            closePadInfo: () => closed++);

        info.SetFreeformXCommand.Execute(null);
        info.SetFreeformYCommand.Execute(null);
        info.SetFreeformXYCommand.Execute(null);
        info.SetFreeformNoneCommand.Execute(null);

        Assert.Equal(
            [FreeformType.XWay, FreeformType.YWay, FreeformType.XYWay, FreeformType.None],
            applied.Select(static entry => entry.Type));
        Assert.All(applied, static entry => Assert.Equal([5, 6], entry.Ids));
        Assert.Equal(4, closed);
    }

    [Fact]
    public void RegularPadInfo_FreeformButtons_WithoutCallbacks_DoNothingAndDoNotThrow()
    {
        var pad = CreateRectRegular(row: 0, col: 0, index: 5, minX: 0, minY: 0, maxX: 10, maxY: 10);
        var info = new RegularPadInfoViewModel(
            pads: new List<RegularPad> { pad },
            toDisplayRow: static row => row,
            getDxfIndex: null,
            getMatchedCadPadIds: null,
            getMatchedCadLinks: null,
            applyPadSize: null,
            resetPadSize: null,
            setFreeform: null,
            getCrossIcOwnerShares: null,
            getNotchRowsByRegularPad: null,
            highlightCadOwnerPads: null,
            focusRegularMatches: null,
            highlightRegularMatches: null,
            closePadInfo: null);

        var exception = Record.Exception(() => info.SetFreeformXYCommand.Execute(null));

        Assert.Null(exception);
        Assert.Equal(FreeformType.None, pad.Freeform);
    }
}
