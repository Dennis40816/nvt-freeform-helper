// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Avalonia.Headless.XUnit;
using FreeformHelper.UI.ViewModels;
using Xunit;

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
        var before = ReadAll(vm);
        var raised = new ConcurrentQueue<string>();
        vm.PropertyChanging += (_, args) => raised.Enqueue("changing:" + args.PropertyName);
        vm.PropertyChanged += (_, args) => raised.Enqueue("changed:" + args.PropertyName);

        Write(vm, setting.Name, setting.NewValue);
        await vm.WaitForGridRebuildIdleAsync();

        var sequence = raised.ToList();
        if (_recalculated.Contains(setting.Name))
        {
            // The grid recalculates these settings, so the setting can change more than once.
            Assert.Contains("changing:" + setting.Name, sequence);
            Assert.Contains("changed:" + setting.Name, sequence);
        }
        else
        {
            Assert.Equal(1, sequence.Count(entry => entry == "changing:" + setting.Name));
            Assert.Equal(1, sequence.Count(entry => entry == "changed:" + setting.Name));
        }

        Assert.True(sequence.IndexOf("changing:" + setting.Name) < sequence.IndexOf("changed:" + setting.Name));
        var after = ReadAll(vm);
        foreach (var other in _settings.Select(item => item.Name).Where(other => other != setting.Name && !_recalculated.Contains(other)))
        {
            Assert.Equal(before[other], after[other]);
        }

        // The listed settings are recalculated by the grid after a change, so the new value does not stay.
        if (!_recalculated.Contains(setting.Name))
        {
            Assert.Equal(setting.NewValue, after[setting.Name]);
        }
    }

    [Theory]
    [MemberData(nameof(SettingIndexes))]
    public void Set_SameValue_RaisesNothing(int index)
    {
        var setting = _settings[index];
        var vm = new FreeformHelperViewModel();
        var raised = new ConcurrentQueue<string?>();
        vm.PropertyChanging += (_, args) => raised.Enqueue(args.PropertyName);
        vm.PropertyChanged += (_, args) => raised.Enqueue(args.PropertyName);

        Write(vm, setting.Name, Read(vm, setting.Name));

        Assert.Empty(raised);
    }

    [Theory]
    [MemberData(nameof(SettingIndexes))]
    public async Task Set_NewValue_SetsTheUnsavedFlagAsBefore(int index)
    {
        var setting = _settings[index];
        var vm = new FreeformHelperViewModel();
        Assert.False(vm.HasUnsavedChanges);

        Write(vm, setting.Name, setting.NewValue);
        await vm.WaitForGridRebuildIdleAsync();

        Assert.Equal(setting.MarksUnsaved, vm.HasUnsavedChanges);
    }

    [Theory]
    [MemberData(nameof(SettingIndexes))]
    public async Task Set_NewValue_AddsAnUndoEntryOnlyWhereTheSettingHasUndo(int index)
    {
        var setting = _settings[index];
        var vm = new FreeformHelperViewModel();
        var start = Read(vm, setting.Name);
        Assert.False(vm.CanUndo);

        Write(vm, setting.Name, setting.NewValue);
        await vm.WaitForGridRebuildIdleAsync();

        Assert.Equal(setting.HasUndo, vm.CanUndo);
        if (!setting.HasUndo)
        {
            return;
        }

        vm.Undo();

        Assert.Equal(start, Read(vm, setting.Name));
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
        using var files = new LoadingModeTestScope();
        await files.SaveAsync(saved =>
        {
            foreach (var setting in _settings)
            {
                Write(saved, setting.Name, RoundTripValue(setting));
            }
        });
        var first = await ReadWithoutSavedAtAsync(files.PathFor("project.json"));
        var vm = files.CreateViewModel();
        var secondPath = files.PathFor("project-second.json");
        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(secondPath);

        var result = await vm.LoadProjectFromPathAsync(files.PathFor("project.json"));
        Assert.True(result.IsLoaded);
        var mismatches = _settings
            .Where(setting => !_recalculated.Contains(setting.Name) && !Equals(RoundTripValue(setting), Read(vm, setting.Name)))
            .Select(setting => setting.Name + "=" + Read(vm, setting.Name))
            .ToList();
        Assert.Equal("", string.Join(",", mismatches));
        Assert.False(vm.HasUnsavedChanges);
        Assert.True(await vm.SaveProjectAsync());

        var second = await ReadWithoutSavedAtAsync(secondPath);
        Assert.Equal(first, second);
    }

    private static object RoundTripValue(Setting setting) => setting.NewValue;

    // The save time is the only member that differs between two saves of the same state.
    private static async Task<string> ReadWithoutSavedAtAsync(string projectFilePath)
    {
        await using var stream = File.OpenRead(projectFilePath);
        var root = (await JsonNode.ParseAsync(stream))!.AsObject();
        var key = root.Select(member => member.Key).Single(name => string.Equals(name, "SavedAt", StringComparison.OrdinalIgnoreCase));
        root.Remove(key);
        return root.ToJsonString();
    }

    private static Dictionary<string, object> ReadAll(FreeformHelperViewModel vm) =>
        _settings.ToDictionary(setting => setting.Name, setting => Read(vm, setting.Name));

    private static object Read(FreeformHelperViewModel vm, string name) =>
        typeof(FreeformHelperViewModel).GetProperty(name)!.GetValue(vm)!;

    private static void Write(FreeformHelperViewModel vm, string name, object value) =>
        typeof(FreeformHelperViewModel).GetProperty(name)!.SetValue(vm, value);
}
