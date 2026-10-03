using System.Reflection;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class FreeformHelperViewModelTests
{
    [Fact]
    public async Task RebuildGrid_WhenDxfBoundsWithoutDxf_FallsBackToPanelAaAlignment()
    {
        var vm = new FreeformHelperViewModel
        {
            ActiveAreaWidth = 120m,
            ActiveAreaHeight = 80m,
            PanelBiasX = 12.5m,
            PanelBiasY = -3.75m,
            GridAlignmentMode = GridAlignmentMode.FromCadBounds,
        };

        await vm.RebuildGridCommand.ExecuteAsync(null);

        Assert.Equal("Ready (DXF not loaded; using panel AA alignment).", vm.StatusText);
        Assert.NotEmpty(vm.RegularPads);
        Assert.Equal(12.5, vm.RegularPads.Min(p => p.Bounds.MinX), 3);
        Assert.Equal(-3.75, vm.RegularPads.Min(p => p.Bounds.MinY), 3);
    }


    [Fact]
    public void PitchSizeSummary_ListsDistinctGridWidthsAndHeights()
    {
        var vm = new FreeformHelperViewModel();
        var gridField = typeof(FreeformHelperViewModel).GetField("_grid", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(gridField);

        var pads = new List<RegularPad>
        {
            CreateRectRegular(0, 0, 0, 0, 0, 10, 4),
            CreateRectRegular(0, 1, 1, 10, 0, 30, 4),
            CreateRectRegular(1, 0, 2, 0, 4, 10, 10),
            CreateRectRegular(1, 1, 3, 10, 4, 30, 10),
        };
        var pitchXEdges = new[] { 0.0, 10.0, 30.0 };
        var pitchYEdges = new[] { 0.0, 4.0, 10.0 };
        var grid = new RegularGrid(
            rows: 2,
            cols: 2,
            xEdges: pitchXEdges,
            yEdges: pitchYEdges,
            pads: pads);

        gridField!.SetValue(vm, grid);

        Assert.Equal("Pitch X: X1=10 mm, X2=20 mm", vm.PitchSizeXSummary);
        Assert.Equal("Pitch Y: Y1=4 mm, Y2=6 mm", vm.PitchSizeYSummary);
    }


    [Fact]
    public async Task RebuildGrid_RaisesPitchSizeSummaryPropertyChanged()
    {
        var vm = new FreeformHelperViewModel
        {
            ActiveAreaWidth = 120m,
            ActiveAreaHeight = 80m,
            XChannels = 4m,
            YChannels = 2m,
        };
        var changed = new System.Collections.Concurrent.ConcurrentQueue<string>();
        vm.PropertyChanged += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.PropertyName))
            {
                changed.Enqueue(e.PropertyName!);
            }
        };

        changed.Clear();
        await vm.RebuildGridCommand.ExecuteAsync(null);

        Assert.Contains(nameof(FreeformHelperViewModel.PitchSizeXSummary), changed);
        Assert.Contains(nameof(FreeformHelperViewModel.PitchSizeYSummary), changed);
        Assert.StartsWith("Pitch X: X1=", vm.PitchSizeXSummary, StringComparison.Ordinal);
        Assert.StartsWith("Pitch Y: Y1=", vm.PitchSizeYSummary, StringComparison.Ordinal);
    }
}
