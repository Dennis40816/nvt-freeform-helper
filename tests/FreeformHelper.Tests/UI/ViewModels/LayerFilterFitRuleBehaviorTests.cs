// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Settings;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class LayerFilterFitRuleBehaviorTests
{
    [AvaloniaFact]
    public async Task LayerFilter_WithBoundsRecalculation_RebuildsWithoutRequestingFitOrResettingFocusZoomAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var project = new SyntheticWorkflowProjectScope();
        var vm = await project.LoadAsync();
        vm.RecalcBoundsOnLayerFilter = true;
        vm.GridAlignmentMode = GridAlignmentMode.FromCadBounds;
        vm.GridPaddingPercent = 0;
        await vm.WaitForGridRebuildIdleAsync();
        var originalGrid = vm.RegularPads;
        var originalMinX = vm.RegularPads.Min(static pad => pad.Bounds.MinX);
        var signal = vm.CadPads.Single(static pad => pad.Layer == "signal-a");
        var host = new SelectionLayerCanvasProbe();
        vm.CanvasHost = host;
        vm.QuickFocusQueryText = $"cad {signal.Id}";
        vm.QuickFocusCommand.Execute(null);
        Assert.Equal(1, host.FocusWithMinZoomCount);
        Assert.Equal(2.0, host.LastMinZoom);

        vm.ApplyLayerSelection(vm.LayerToggles.Single(static toggle => toggle.Name == "frame"), soloSelect: false);
        await vm.WaitForGridRebuildIdleAsync();

        Assert.Equal(["signal-a", "signal-b"], vm.CadPads.Select(static pad => pad.Layer).Order(StringComparer.Ordinal));
        Assert.NotSame(originalGrid, vm.RegularPads);
        Assert.Equal(9, vm.RegularPads.Count);
        Assert.NotEqual(originalMinX, vm.RegularPads.Min(static pad => pad.Bounds.MinX));
        Assert.Equal(0, host.FitToContentCount);
        Assert.Equal(0, host.ResetViewCount);
        Assert.Equal(0, host.FocusSelectionCount);
        Assert.Equal(1, host.FocusWithMinZoomCount);
        Assert.Equal(2.0, host.LastMinZoom);
    }
}
