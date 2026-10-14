// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Collections.Concurrent;
using Avalonia.Headless.XUnit;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins how four setting enums and their dropdown option values stay in step (issue #64, state
/// inventory item 8: scan order, grid alignment, regular source mode, CAD output FW diff auto
/// mode). Each pair is two properties of the view model that copy each other through property
/// callbacks. A change of either side moves the other side, raises the change notification of
/// both and, for three of the pairs, marks the project as changed. The regular source mode pair
/// is not in the list of settings that mark the project as changed, so its tests do not assert
/// the unsaved flag: the flag after such a change depends on side effects of the grid rebuild
/// and on the UI thread context of the test. A project load sets both sides from the file. These
/// tests describe the current behavior. A change that derives the option from the enum must keep
/// every value here, or say so in its pull request.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class SettingsOptionMirrorCharacterizationTests
{
    [Fact]
    public void ScanOrder_SetEnum_MovesTheOptionAndRaisesBoth()
    {
        var vm = new FreeformHelperViewModel();
        Assert.False(vm.HasUnsavedChanges);
        var target = vm.ScanOrderOptions.First(option => option.Value != vm.SelectedScanOrder).Value;

        var raised = ObserveRaised(vm, () => vm.SelectedScanOrder = target);

        Assert.Equal(target, vm.SelectedScanOrderOption.Value);
        Assert.Equal(1, raised.Count(name => name == nameof(FreeformHelperViewModel.SelectedScanOrder)));
        Assert.Equal(1, raised.Count(name => name == nameof(FreeformHelperViewModel.SelectedScanOrderOption)));
        Assert.True(vm.HasUnsavedChanges);
    }

    [Fact]
    public void ScanOrder_SetOption_MovesTheEnumAndRaisesBoth()
    {
        var vm = new FreeformHelperViewModel();
        Assert.False(vm.HasUnsavedChanges);
        var target = vm.ScanOrderOptions.First(option => option.Value != vm.SelectedScanOrder);

        var raised = ObserveRaised(vm, () => vm.SelectedScanOrderOption = target);

        Assert.Equal(target.Value, vm.SelectedScanOrder);
        Assert.Equal(1, raised.Count(name => name == nameof(FreeformHelperViewModel.SelectedScanOrder)));
        Assert.Equal(1, raised.Count(name => name == nameof(FreeformHelperViewModel.SelectedScanOrderOption)));
        Assert.True(vm.HasUnsavedChanges);
    }

    [Fact]
    public void GridAlignment_SetEnum_MovesTheOptionAndRaisesBoth()
    {
        var vm = new FreeformHelperViewModel();
        Assert.False(vm.HasUnsavedChanges);
        var target = vm.GridAlignmentOptions.First(option => option.Value != vm.GridAlignmentMode).Value;

        var raised = ObserveRaised(vm, () => vm.GridAlignmentMode = target);

        Assert.Equal(target, vm.SelectedGridAlignmentOption.Value);
        Assert.Equal(1, raised.Count(name => name == nameof(FreeformHelperViewModel.GridAlignmentMode)));
        Assert.Equal(1, raised.Count(name => name == nameof(FreeformHelperViewModel.SelectedGridAlignmentOption)));
        Assert.True(vm.HasUnsavedChanges);
    }

    [Fact]
    public void GridAlignment_SetOption_MovesTheEnumAndRaisesBoth()
    {
        var vm = new FreeformHelperViewModel();
        Assert.False(vm.HasUnsavedChanges);
        var target = vm.GridAlignmentOptions.First(option => option.Value != vm.GridAlignmentMode);

        var raised = ObserveRaised(vm, () => vm.SelectedGridAlignmentOption = target);

        Assert.Equal(target.Value, vm.GridAlignmentMode);
        Assert.Equal(1, raised.Count(name => name == nameof(FreeformHelperViewModel.GridAlignmentMode)));
        Assert.Equal(1, raised.Count(name => name == nameof(FreeformHelperViewModel.SelectedGridAlignmentOption)));
        Assert.True(vm.HasUnsavedChanges);
    }

    [Fact]
    public void RegularSourceMode_SetEnum_MovesTheOptionAndRaisesBoth()
    {
        var vm = new FreeformHelperViewModel();
        Assert.False(vm.HasUnsavedChanges);
        var target = vm.RegularSourceModeOptions.First(option => option.Value != vm.RegularSourceMode).Value;

        var raised = ObserveRaised(vm, () => vm.RegularSourceMode = target);

        Assert.Equal(target, vm.SelectedRegularSourceModeOption.Value);
        Assert.Equal(1, raised.Count(name => name == nameof(FreeformHelperViewModel.RegularSourceMode)));
        Assert.Equal(1, raised.Count(name => name == nameof(FreeformHelperViewModel.SelectedRegularSourceModeOption)));
    }

    [Fact]
    public void RegularSourceMode_SetOption_MovesTheEnumAndRaisesBoth()
    {
        var vm = new FreeformHelperViewModel();
        Assert.False(vm.HasUnsavedChanges);
        var target = vm.RegularSourceModeOptions.First(option => option.Value != vm.RegularSourceMode);

        var raised = ObserveRaised(vm, () => vm.SelectedRegularSourceModeOption = target);

        Assert.Equal(target.Value, vm.RegularSourceMode);
        Assert.Equal(1, raised.Count(name => name == nameof(FreeformHelperViewModel.RegularSourceMode)));
        Assert.Equal(1, raised.Count(name => name == nameof(FreeformHelperViewModel.SelectedRegularSourceModeOption)));
    }

    [Fact]
    public void CadOutputFwDiffAutoMode_SetEnum_MovesTheOptionAndRaisesBoth()
    {
        var vm = new FreeformHelperViewModel();
        Assert.False(vm.HasUnsavedChanges);
        var target = vm.CadOutputFwDiffAutoModeOptions.First(option => option.Value != vm.CadOutputFwDiffAutoMode).Value;

        var raised = ObserveRaised(vm, () => vm.CadOutputFwDiffAutoMode = target);

        Assert.Equal(target, vm.SelectedCadOutputFwDiffAutoModeOption.Value);
        Assert.Equal(1, raised.Count(name => name == nameof(FreeformHelperViewModel.CadOutputFwDiffAutoMode)));
        Assert.Equal(1, raised.Count(name => name == nameof(FreeformHelperViewModel.SelectedCadOutputFwDiffAutoModeOption)));
        Assert.True(vm.HasUnsavedChanges);
    }

    [Fact]
    public void CadOutputFwDiffAutoMode_SetOption_MovesTheEnumAndRaisesBoth()
    {
        var vm = new FreeformHelperViewModel();
        Assert.False(vm.HasUnsavedChanges);
        var target = vm.CadOutputFwDiffAutoModeOptions.First(option => option.Value != vm.CadOutputFwDiffAutoMode);

        var raised = ObserveRaised(vm, () => vm.SelectedCadOutputFwDiffAutoModeOption = target);

        Assert.Equal(target.Value, vm.CadOutputFwDiffAutoMode);
        Assert.Equal(1, raised.Count(name => name == nameof(FreeformHelperViewModel.CadOutputFwDiffAutoMode)));
        Assert.Equal(1, raised.Count(name => name == nameof(FreeformHelperViewModel.SelectedCadOutputFwDiffAutoModeOption)));
        Assert.True(vm.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public async Task LoadProject_SetsEnumAndOptionOfAllFourPairsFromTheFile()
    {
        using var files = new LoadingModeTestScope();
        var defaults = new FreeformHelperViewModel();
        var scanOrder = defaults.ScanOrderOptions.First(option => option.Value != defaults.SelectedScanOrder).Value;
        var alignment = defaults.GridAlignmentOptions.First(option => option.Value != defaults.GridAlignmentMode).Value;
        var sourceMode = defaults.RegularSourceModeOptions.First(option => option.Value != defaults.RegularSourceMode).Value;
        var autoMode = defaults.CadOutputFwDiffAutoModeOptions.First(option => option.Value != defaults.CadOutputFwDiffAutoMode).Value;
        await files.SaveAsync(saved =>
        {
            saved.SelectedScanOrder = scanOrder;
            saved.GridAlignmentMode = alignment;
            saved.RegularSourceMode = sourceMode;
            saved.CadOutputFwDiffAutoMode = autoMode;
        });
        var vm = files.CreateViewModel();

        var result = await vm.LoadProjectFromPathAsync(files.PathFor("project.json"));

        Assert.True(result.IsLoaded);
        Assert.Equal(scanOrder, vm.SelectedScanOrder);
        Assert.Equal(scanOrder, vm.SelectedScanOrderOption.Value);
        Assert.Equal(alignment, vm.GridAlignmentMode);
        Assert.Equal(alignment, vm.SelectedGridAlignmentOption.Value);
        Assert.Equal(sourceMode, vm.RegularSourceMode);
        Assert.Equal(sourceMode, vm.SelectedRegularSourceModeOption.Value);
        Assert.Equal(autoMode, vm.CadOutputFwDiffAutoMode);
        Assert.Equal(autoMode, vm.SelectedCadOutputFwDiffAutoModeOption.Value);
        Assert.False(vm.HasUnsavedChanges);
    }

    private static string?[] ObserveRaised(FreeformHelperViewModel vm, Action change)
    {
        // A grid rebuild started by the change can continue on a thread-pool thread and raise
        // notifications at the same time, so the collection must be safe for concurrent adds.
        var raised = new ConcurrentQueue<string?>();
        vm.PropertyChanged += (_, args) => raised.Enqueue(args.PropertyName);
        change();
        return raised.ToArray();
    }
}
