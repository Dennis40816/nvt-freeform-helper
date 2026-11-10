// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>Characterizes initial fit effects through the real view model and subsequent grid builds.</summary>
[Collection("HeadlessUiSerial")]
public sealed class EnsureInitialFitCharacterizationTests
{
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task EnsureInitialFitWithoutHostDoesNotQueueFitAsync(bool hasCadPads, bool hasRegularPads)
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new LoadingModeTestScope();
        var vm = files.CreateViewModel();
        AddPads(vm, hasCadPads, hasRegularPads);
        Assert.Null(vm.CanvasHost);

        vm.EnsureInitialFit();

        var host = new SelectionLayerCanvasProbe();
        vm.CanvasHost = host;
        EnsureRegularPad(vm);
        await RebuildWithoutFitAsync(vm);

        Assert.Equal(0, host.FitToContentCount);
    }

    [AvaloniaTheory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task EnsureInitialFitWithPadsFitsImmediatelyAndPreservesPendingRequestAsync(bool hasCadPads, bool hasRegularPads)
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new LoadingModeTestScope();
        var vm = files.CreateViewModel();
        var host = new SelectionLayerCanvasProbe();
        vm.CanvasHost = host;
        vm.EnsureInitialFit();
        AddPads(vm, hasCadPads, hasRegularPads);

        vm.EnsureInitialFit();

        Assert.Equal(1, host.FitToContentCount);
        EnsureRegularPad(vm);
        await RebuildWithoutFitAsync(vm);
        Assert.Equal(2, host.FitToContentCount);
        await RebuildWithoutFitAsync(vm);
        Assert.Equal(2, host.FitToContentCount);
    }

    [AvaloniaFact]
    public async Task EnsureInitialFitWithEmptyProjectDefersFitToNextGridBuildAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new LoadingModeTestScope();
        var vm = files.CreateViewModel();
        var host = new SelectionLayerCanvasProbe();
        vm.CanvasHost = host;
        Assert.Empty(vm.CadPads);
        Assert.Empty(vm.RegularPads);

        vm.EnsureInitialFit();

        Assert.Equal(0, host.FitToContentCount);
        EnsureRegularPad(vm);
        await RebuildWithoutFitAsync(vm);
        Assert.Equal(1, host.FitToContentCount);
        await RebuildWithoutFitAsync(vm);
        Assert.Equal(1, host.FitToContentCount);
    }

    private static void AddPads(FreeformHelperViewModel viewModel, bool hasCadPads, bool hasRegularPads)
    {
        if (hasCadPads)
        {
            viewModel.CadPads.Add(TestGeometryFactory.CreateCadPad(1, "signal", 0, 0, 1, 1));
        }

        if (hasRegularPads)
        {
            EnsureRegularPad(viewModel);
        }
    }

    private static void EnsureRegularPad(FreeformHelperViewModel viewModel)
    {
        // Existing regular content prevents the independent first-grid auto-fit from masking the pending request.
        if (viewModel.RegularPads.Count == 0)
        {
            viewModel.RegularPads.Add(TestGeometryFactory.CreateRegularPad(0, 0, 1, 0, 0, 1, 1));
        }
    }

    private static async Task RebuildWithoutFitAsync(FreeformHelperViewModel viewModel)
    {
        var originalPads = viewModel.RegularPads;
        viewModel.SelectedScanOrder = viewModel.ScanOrderOptions.First(option => option.Value != viewModel.SelectedScanOrder).Value;
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(viewModel);
        Assert.NotSame(originalPads, viewModel.RegularPads);
        Assert.NotEmpty(viewModel.RegularPads);
    }
}
