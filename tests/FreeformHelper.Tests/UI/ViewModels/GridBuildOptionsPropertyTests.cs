// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.ViewModelPropertyTestHarness;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins the nine grid build options of the main view model: the start values, the change notifications,
/// the unsaved flag, the undo entry and a project file that stays identical, except for its save time,
/// after a save, load and save.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class GridBuildOptionsPropertyTests
{
    private sealed record Setting(string Name, object NewValue, bool HasUndo, bool MarksUnsaved);

    private static readonly string[] _recalculated = [nameof(FreeformHelperViewModel.UseLocalSizing), nameof(FreeformHelperViewModel.IsWidthRowLocal), nameof(FreeformHelperViewModel.IsHeightColumnLocal)];

    private static readonly Setting[] _settings =
    [
        new(nameof(FreeformHelperViewModel.SelectedScanOrder), FreeformHelper.Application.Settings.ScanOrder.RightToLeft_TopToBottom, true, true),
        new(nameof(FreeformHelperViewModel.GridAlignmentMode), FreeformHelper.Application.Settings.GridAlignmentMode.FromPanelAa, true, true),
        new(nameof(FreeformHelperViewModel.RegularSourceMode), FreeformHelper.Application.Settings.RegularSourceMode.FromDxfLayer, false, true),
        new(nameof(FreeformHelperViewModel.UseLocalSizing), false, false, true),
        new(nameof(FreeformHelperViewModel.IsWidthRowLocal), false, false, true),
        new(nameof(FreeformHelperViewModel.IsHeightColumnLocal), false, false, true),
        new(nameof(FreeformHelperViewModel.AreaBucketTolerance), 0.5m, true, true),
        new(nameof(FreeformHelperViewModel.MaxAreaBuckets), 10m, true, true),
        new(nameof(FreeformHelperViewModel.CoordinatePreferredAaOutlineLayerName), "AA", false, true),
    ];

    public static TheoryData<int> SettingIndexes => new(Enumerable.Range(0, _settings.Length));

    [Fact]
    public void NewViewModel_HasTheLoadedDefaults()
    {
        var vm = new FreeformHelperViewModel();

        Assert.Equal(FreeformHelper.Application.Settings.ScanOrder.LeftToRight_TopToBottom, vm.SelectedScanOrder);
        Assert.Equal(FreeformHelper.Application.Settings.GridAlignmentMode.FromCadBounds, vm.GridAlignmentMode);
        Assert.Equal(FreeformHelper.Application.Settings.RegularSourceMode.GeneratedGrid, vm.RegularSourceMode);
        Assert.True(vm.UseLocalSizing);
        Assert.True(vm.IsWidthRowLocal);
        Assert.True(vm.IsHeightColumnLocal);
        Assert.Equal(0.001m, vm.AreaBucketTolerance);
        Assert.Equal(32m, vm.MaxAreaBuckets);
        Assert.Equal("", vm.CoordinatePreferredAaOutlineLayerName);
    }

    [Theory]
    [MemberData(nameof(SettingIndexes))]
    public async Task Set_NewValue_RaisesChangingBeforeChangedOnceAndKeepsTheOtherValues(int index)
    {
        var setting = _settings[index];
        var vm = new FreeformHelperViewModel();

        await AssertChangeNotificationsAsync(vm, setting.Name, setting.NewValue, _settings.Select(item => item.Name), _recalculated);
    }

    [Theory]
    [MemberData(nameof(SettingIndexes))]
    public void Set_SameValue_RaisesNothing(int index)
    {
        var setting = _settings[index];
        AssertSameValueRaisesNothing(vm => Write(vm, setting.Name, Read(vm, setting.Name)));
    }

    [Theory]
    [MemberData(nameof(SettingIndexes))]
    public async Task Set_NewValue_SetsTheUnsavedFlagAsBefore(int index)
    {
        var setting = _settings[index];
        var vm = await SetValueOnCleanProjectAsync(setting.Name, setting.NewValue);
        Assert.Equal(setting.MarksUnsaved, vm.HasUnsavedChanges);
    }

    [Theory]
    [MemberData(nameof(SettingIndexes))]
    public async Task Set_NewValue_AddsAnUndoEntryOnlyWhereTheSettingHasUndo(int index)
    {
        var setting = _settings[index];
        await AssertUndoAsync(setting.Name, setting.NewValue, setting.HasUndo);
    }

    [Theory]
    [InlineData(nameof(FreeformHelperViewModel.UseLocalSizing), false, false, false)]
    [InlineData(nameof(FreeformHelperViewModel.IsWidthRowLocal), false, false, true)]
    [InlineData(nameof(FreeformHelperViewModel.IsHeightColumnLocal), false, true, false)]
    public void Set_LocalSizingFlag_WritesTheOtherFlagsThroughTheirCallbacks(string name, bool useLocalSizing, bool widthRowLocal, bool heightColumnLocal)
    {
        var vm = new FreeformHelperViewModel();

        Write(vm, name, false);

        Assert.Equal(useLocalSizing, vm.UseLocalSizing);
        Assert.Equal(widthRowLocal, vm.IsWidthRowLocal);
        Assert.Equal(heightColumnLocal, vm.IsHeightColumnLocal);
    }

    [Fact]
    public void Set_LayerNameWithSpaces_StoresTheTrimmedNameWithoutUndoEntry()
    {
        var vm = new FreeformHelperViewModel();

        vm.CoordinatePreferredAaOutlineLayerName = "  AA ";

        Assert.Equal("AA", vm.CoordinatePreferredAaOutlineLayerName);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public async Task SaveLoadSave_ChangedValues_WritesTheSameProjectFileExceptTheSaveTime()
    {
        await AssertProjectRoundTripAsync(
            saved => WriteValues(saved, _settings.Select(setting => (setting.Name, RoundTripValue(setting)))),
            vm => AssertValues(vm, _settings.Where(setting => !_recalculated.Contains(setting.Name))
                .Select(setting => (setting.Name, RoundTripValue(setting)))));
    }

    private static object RoundTripValue(Setting setting) => setting.NewValue;
}
