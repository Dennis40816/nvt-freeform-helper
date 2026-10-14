// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Avalonia.Headless.XUnit;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins the sixteen notch algorithm settings of the main view model: the start values, the
/// change notifications, the unsaved flag, the undo entry, the clamps, the threshold link and a
/// project file that stays identical, except for its save time, after a save, load and save.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class NotchAlgorithmSettingsPropertyTests
{
    private sealed record Setting(string Name, object NewValue, bool HasUndo);

    private static readonly Setting[] _settings =
    [
        new(nameof(FreeformHelperViewModel.EnableV21), false, true),
        new(nameof(FreeformHelperViewModel.EnableV22), false, true),
        new(nameof(FreeformHelperViewModel.LenScale), 3m, true),
        new(nameof(FreeformHelperViewModel.NullValue), 7m, true),
        new(nameof(FreeformHelperViewModel.NotchThresholdQ7), 5m, true),
        new(nameof(FreeformHelperViewModel.NotchThresholdPercent), 5m, true),
        new(nameof(FreeformHelperViewModel.LinkNotchThresholds), false, true),
        new(nameof(FreeformHelperViewModel.EnableToRegular), false, true),
        new(nameof(FreeformHelperViewModel.EnableToFull), false, true),
        new(nameof(FreeformHelperViewModel.ToFullStrictOverlapPercent), 2.5m, true),
        new(nameof(FreeformHelperViewModel.EnableToFullRuleEngine), false, false),
        new(nameof(FreeformHelperViewModel.EnableToFullRuleTrace), true, false),
        new(nameof(FreeformHelperViewModel.EnableBoundaryVirtualAreaCap), false, false),
        new(nameof(FreeformHelperViewModel.BoundaryVirtualAreaCapPercent), 50m, false),
        new(nameof(FreeformHelperViewModel.EnableTargetCoverageGuard), false, false),
        new(nameof(FreeformHelperViewModel.TargetCoverageCapPercent), 80m, false),
    ];

    private static readonly (string Name, decimal Input, decimal Clamped)[] _clamps =
    [
        (nameof(FreeformHelperViewModel.ToFullStrictOverlapPercent), 150m, 100m),
        (nameof(FreeformHelperViewModel.NotchThresholdQ7), 200m, 128m),
        (nameof(FreeformHelperViewModel.NotchThresholdPercent), 150m, 100m),
        (nameof(FreeformHelperViewModel.BoundaryVirtualAreaCapPercent), 2000m, 1000m),
        (nameof(FreeformHelperViewModel.TargetCoverageCapPercent), 300m, 255m),
    ];

    public static TheoryData<int> SettingIndexes => new(Enumerable.Range(0, _settings.Length));

    public static TheoryData<int> ClampIndexes => new(Enumerable.Range(0, _clamps.Length));

    [Fact]
    public void NewViewModel_HasTheLoadedDefaults()
    {
        var vm = new FreeformHelperViewModel();

        Assert.True(vm.EnableV21);
        Assert.True(vm.EnableV22);
        Assert.Equal(10m, vm.LenScale);
        Assert.Equal(65535m, vm.NullValue);
        Assert.Equal(0m, vm.NotchThresholdQ7);
        Assert.Equal(0m, vm.NotchThresholdPercent);
        Assert.True(vm.LinkNotchThresholds);
        Assert.True(vm.EnableToRegular);
        Assert.True(vm.EnableToFull);
        Assert.Equal(0.1m, vm.ToFullStrictOverlapPercent);
        Assert.True(vm.EnableToFullRuleEngine);
        Assert.False(vm.EnableToFullRuleTrace);
        Assert.True(vm.EnableBoundaryVirtualAreaCap);
        Assert.Equal(100m, vm.BoundaryVirtualAreaCapPercent);
        Assert.True(vm.EnableTargetCoverageGuard);
        Assert.Equal(120m, vm.TargetCoverageCapPercent);
    }

    [Theory]
    [MemberData(nameof(SettingIndexes))]
    public void Set_NewValue_RaisesChangingBeforeChangedOnceAndKeepsTheOtherValues(int index)
    {
        var setting = _settings[index];
        var vm = new FreeformHelperViewModel();
        if (setting.Name != nameof(FreeformHelperViewModel.LinkNotchThresholds))
        {
            // The two thresholds follow each other while linked. Unlinked, each one is independent.
            vm.LinkNotchThresholds = false;
        }

        var before = ReadAll(vm);
        var raised = new ConcurrentQueue<string>();
        vm.PropertyChanging += (_, args) => raised.Enqueue("changing:" + args.PropertyName);
        vm.PropertyChanged += (_, args) => raised.Enqueue("changed:" + args.PropertyName);

        Write(vm, setting.Name, setting.NewValue);

        var sequence = raised.ToList();
        Assert.Equal(1, sequence.Count(entry => entry == "changing:" + setting.Name));
        Assert.Equal(1, sequence.Count(entry => entry == "changed:" + setting.Name));
        Assert.True(sequence.IndexOf("changing:" + setting.Name) < sequence.IndexOf("changed:" + setting.Name));
        var after = ReadAll(vm);
        foreach (var other in _settings.Select(item => item.Name).Where(other => other != setting.Name))
        {
            Assert.Equal(before[other], after[other]);
        }

        Assert.Equal(setting.NewValue, after[setting.Name]);
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
    public void Set_NewValue_MarksTheProjectAsChanged(int index)
    {
        var setting = _settings[index];
        var vm = new FreeformHelperViewModel();
        Assert.False(vm.HasUnsavedChanges);

        Write(vm, setting.Name, setting.NewValue);

        Assert.True(vm.HasUnsavedChanges);
    }

    [Theory]
    [MemberData(nameof(SettingIndexes))]
    public void Set_NewValue_AddsAnUndoEntryOnlyWhereTheSettingHasUndo(int index)
    {
        var setting = _settings[index];
        var vm = new FreeformHelperViewModel();
        var start = Read(vm, setting.Name);
        Assert.False(vm.CanUndo);

        Write(vm, setting.Name, setting.NewValue);

        Assert.Equal(setting.HasUndo, vm.CanUndo);
        if (!setting.HasUndo)
        {
            return;
        }

        vm.Undo();

        Assert.Equal(start, Read(vm, setting.Name));
    }

    [Theory]
    [MemberData(nameof(ClampIndexes))]
    public void Set_ValueAboveTheLimit_StoresTheLimit(int index)
    {
        var (name, input, clamped) = _clamps[index];
        var vm = new FreeformHelperViewModel();

        Write(vm, name, input);

        Assert.Equal(clamped, Read(vm, name));
    }

    [Fact]
    public void Set_ThresholdQ7WhileLinked_UpdatesThePercentThreshold()
    {
        var vm = new FreeformHelperViewModel();
        Assert.True(vm.LinkNotchThresholds);

        vm.NotchThresholdQ7 = 64m;

        Assert.Equal(50m, vm.NotchThresholdPercent);
    }

    [Fact]
    public void Set_ThresholdQ7WhileNotLinked_KeepsThePercentThreshold()
    {
        var vm = new FreeformHelperViewModel();
        vm.LinkNotchThresholds = false;

        vm.NotchThresholdQ7 = 64m;

        Assert.Equal(0m, vm.NotchThresholdPercent);
    }

    [AvaloniaFact]
    public async Task SaveLoadSave_ChangedValues_WritesTheSameProjectFileExceptTheSaveTime()
    {
        using var files = new LoadingModeTestScope();
        await files.SaveAsync(saved =>
        {
            saved.LinkNotchThresholds = false;
            foreach (var setting in _settings.Where(item => item.Name != nameof(FreeformHelperViewModel.LinkNotchThresholds)))
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
            .Where(setting => !Equals(RoundTripValue(setting), Read(vm, setting.Name)))
            .Select(setting => setting.Name + "=" + Read(vm, setting.Name))
            .ToList();
        Assert.Equal("", string.Join(",", mismatches));

        Assert.False(vm.HasUnsavedChanges);
        Assert.True(await vm.SaveProjectAsync());

        var second = await ReadWithoutSavedAtAsync(secondPath);
        Assert.Equal(first, second);
    }

    // A loaded project takes the version 2.2 output path and the two compensation switches from its
    // compensation model and output version, so the round trip leaves these three on.
    private static object RoundTripValue(Setting setting) =>
        setting.Name is nameof(FreeformHelperViewModel.EnableV22)
            or nameof(FreeformHelperViewModel.EnableToRegular)
            or nameof(FreeformHelperViewModel.EnableToFull)
            ? true
            : setting.NewValue;

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
