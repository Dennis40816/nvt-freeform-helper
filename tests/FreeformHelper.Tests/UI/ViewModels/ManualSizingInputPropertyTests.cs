// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Avalonia.Headless.XUnit;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins the nine manual sizing inputs of the main view model: the start values, the change notifications,
/// the unsaved flag, the undo entry and a project file that stays identical, except for its save time,
/// after a save, load and save.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class ManualSizingInputPropertyTests
{
    private sealed record Setting(string Name, object NewValue, bool HasUndo, bool MarksUnsaved);

    private static readonly string[] _recalculated = [nameof(FreeformHelperViewModel.ManualRangeStatus)];

    private static readonly Setting[] _settings =
    [
        new(nameof(FreeformHelperViewModel.PendingColumnWidth), 5m, true, false),
        new(nameof(FreeformHelperViewModel.PendingRowHeight), 6m, true, false),
        new(nameof(FreeformHelperViewModel.IsPendingColumnWidthMixed), true, false, false),
        new(nameof(FreeformHelperViewModel.IsPendingRowHeightMixed), true, false, false),
        new(nameof(FreeformHelperViewModel.ManualRowsRange), "1-3", true, false),
        new(nameof(FreeformHelperViewModel.ManualColsRange), "A-C", true, false),
        new(nameof(FreeformHelperViewModel.ManualRangeStatus), "changed", false, false),
        new(nameof(FreeformHelperViewModel.PendingColumnWidthDisplay), "5", false, false),
        new(nameof(FreeformHelperViewModel.PendingRowHeightDisplay), "6", false, false),
    ];

    public static TheoryData<int> SettingIndexes => new(Enumerable.Range(0, _settings.Length));

    [Fact]
    public void NewViewModel_HasTheLoadedDefaults()
    {
        var vm = new FreeformHelperViewModel();

        Assert.Equal(0m, vm.PendingColumnWidth);
        Assert.Equal(0m, vm.PendingRowHeight);
        Assert.False(vm.IsPendingColumnWidthMixed);
        Assert.False(vm.IsPendingRowHeightMixed);
        Assert.Equal("", vm.ManualRowsRange);
        Assert.Equal("", vm.ManualColsRange);
        Assert.Equal("No manual range.", vm.ManualRangeStatus);
        Assert.Equal("0", vm.PendingColumnWidthDisplay);
        Assert.Equal("0", vm.PendingRowHeightDisplay);
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
    [InlineData(nameof(FreeformHelperViewModel.ManualRowsRange), "1-3")]
    [InlineData(nameof(FreeformHelperViewModel.ManualColsRange), "A-C")]
    public void Set_ManualRange_RewritesTheRangeStatusWhileNoGridIsBuilt(string name, string value)
    {
        var vm = new FreeformHelperViewModel();
        Assert.Equal("No manual range.", vm.ManualRangeStatus);

        Write(vm, name, value);

        Assert.Equal("Grid not built.", vm.ManualRangeStatus);
    }

    [Fact]
    public void Set_PendingColumnWidth_ClearsTheMixedFlagOfTheWidth()
    {
        var vm = new FreeformHelperViewModel();
        vm.IsPendingColumnWidthMixed = true;

        vm.PendingColumnWidth = 5m;

        Assert.False(vm.IsPendingColumnWidthMixed);
    }

    [Fact]
    public void Set_PendingRowHeight_ClearsTheMixedFlagOfTheHeight()
    {
        var vm = new FreeformHelperViewModel();
        vm.IsPendingRowHeightMixed = true;

        vm.PendingRowHeight = 6m;

        Assert.False(vm.IsPendingRowHeightMixed);
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

    // The project file does not store the manual sizing inputs, so the round trip keeps their start values.
    private static object RoundTripValue(Setting setting) =>
        setting.Name switch
        {
            nameof(FreeformHelperViewModel.PendingColumnWidth) => 0m,
            nameof(FreeformHelperViewModel.PendingRowHeight) => 0m,
            nameof(FreeformHelperViewModel.IsPendingColumnWidthMixed) => false,
            nameof(FreeformHelperViewModel.IsPendingRowHeightMixed) => false,
            nameof(FreeformHelperViewModel.ManualRowsRange) => "",
            nameof(FreeformHelperViewModel.ManualColsRange) => "",
            nameof(FreeformHelperViewModel.PendingColumnWidthDisplay) => "0",
            nameof(FreeformHelperViewModel.PendingRowHeightDisplay) => "0",
            _ => setting.NewValue,
        };

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
