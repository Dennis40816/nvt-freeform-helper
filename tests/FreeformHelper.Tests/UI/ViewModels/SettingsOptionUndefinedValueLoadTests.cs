// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Settings;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins what a project load does with a saved option value that no option of the list matches.
/// The grid alignment and the regular source mode fall back to their default option. The scan order
/// keeps the stored value. These results come from the load code, not from the option properties,
/// and they must stay the same when the option properties are derived from their values.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class SettingsOptionUndefinedValueLoadTests
{
    private const int UndefinedValue = 99;

    [AvaloniaFact]
    public async Task LoadProject_UndefinedGridAlignment_FallsBackToCadBounds()
    {
        using var files = new LoadingModeTestScope();
        await files.SaveAsync(saved => saved.GridAlignmentMode = (GridAlignmentMode)UndefinedValue);
        var vm = files.CreateViewModel();

        var result = await vm.LoadProjectFromPathAsync(files.PathFor("project.json"));

        Assert.True(result.IsLoaded);
        Assert.Equal(GridAlignmentMode.FromCadBounds, vm.GridAlignmentMode);
        Assert.Equal(GridAlignmentMode.FromCadBounds, vm.SelectedGridAlignmentOption.Value);
    }

    [AvaloniaFact]
    public async Task LoadProject_UndefinedRegularSourceMode_FallsBackToGeneratedGrid()
    {
        using var files = new LoadingModeTestScope();
        await files.SaveAsync(saved => saved.RegularSourceMode = (RegularSourceMode)UndefinedValue);
        var vm = files.CreateViewModel();

        var result = await vm.LoadProjectFromPathAsync(files.PathFor("project.json"));

        Assert.True(result.IsLoaded);
        Assert.Equal(RegularSourceMode.GeneratedGrid, vm.RegularSourceMode);
        Assert.Equal(RegularSourceMode.GeneratedGrid, vm.SelectedRegularSourceModeOption.Value);
    }

    [AvaloniaFact]
    public async Task LoadProject_UndefinedScanOrder_KeepsTheStoredValue()
    {
        using var files = new LoadingModeTestScope();
        await files.SaveAsync(saved => saved.SelectedScanOrder = (ScanOrder)UndefinedValue);
        var vm = files.CreateViewModel();

        var result = await vm.LoadProjectFromPathAsync(files.PathFor("project.json"));

        Assert.True(result.IsLoaded);
        Assert.Equal((ScanOrder)UndefinedValue, vm.SelectedScanOrder);
    }
}
