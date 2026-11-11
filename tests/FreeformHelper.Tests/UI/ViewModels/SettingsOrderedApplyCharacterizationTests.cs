// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
[Trait("Category", "Characterization")]
public sealed class SettingsOrderedApplyCharacterizationTests
{
    [AvaloniaFact]
    public async Task Save_MixedLayoutApply_RefreshesCascadeThenMarksDirtyPersistsAndPublishesGridAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var trace = new List<string>();
        var writes = new List<AppGeneralSettingsDocument>();
        var store = new AppGeneralSettingsStore("unused.json", readTextOverride: static _ => null,
            saveOverride: document =>
            {
                trace.Add("persist");
                writes.Add(document);
            });
        using var scope = new SettingsDraftTestScope(new FreeformHelperViewModel(store));
        var vm = scope.Owner;
        var path = FreeformHelperViewModelTests.CreateTempDxfFile(("signal", 0, 0, 10, 20));
        try
        {
            vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(path);
            await vm.OpenDxfCommand.ExecuteAsync(null);
            await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
            var draft = vm.CreateSettingsWindowViewModel();
            Assert.Single(draft.CascadeIcSettings).XChannels += 1m;
            draft.GridPaddingPercent = 10m;
            draft.GlobalFontSizePercent = 112m;
            vm.AppGeneralPersistenceForTests.PersistTimer?.Stop();
            vm.HasUnsavedChanges = false;
            trace.Clear();
            writes.Clear();
            vm.PropertyChanged += (sender, change) =>
            {
                Assert.Same(vm, sender);
                if (change.PropertyName == nameof(vm.CascadeIcSettings)) trace.Add("cascade");
                if (change.PropertyName == nameof(vm.HasUnsavedChanges) && vm.HasUnsavedChanges) trace.Add("dirty");
                if (change.PropertyName == nameof(vm.RegularPads)) trace.Add("grid");
            };

            draft.SaveCommand.Execute(null);
            await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);

            Assert.Equal<string>(["cascade", "dirty", "persist", "grid"], trace);
            Assert.Equal(112d, Assert.Single(writes).View.GlobalFontSizePercent);
            Assert.Equal(draft.XChannels, vm.XChannels);
            Assert.True(vm.HasUnsavedChanges);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [AvaloniaFact]
    public void Save_AppPreferenceOnly_PersistsWithoutDirtyingTheProject()
    {
        var writes = new List<AppGeneralSettingsDocument>();
        var store = new AppGeneralSettingsStore("unused.json", readTextOverride: static _ => null,
            saveOverride: writes.Add);
        using var scope = new SettingsDraftTestScope(new FreeformHelperViewModel(store));
        var vm = scope.Owner;
        var draft = vm.CreateSettingsWindowViewModel();
        draft.ApplyAppVisualPreferencesOnProjectLoad = !vm.ApplyAppVisualPreferencesOnProjectLoad;

        draft.SaveCommand.Execute(null);

        Assert.Equal(draft.ApplyAppVisualPreferencesOnProjectLoad,
            Assert.Single(writes).Behavior.ApplyVisualPreferencesOnProjectLoad);
        Assert.False(vm.HasUnsavedChanges);
        Assert.False(vm.CanUndo);
    }

    [Fact]
    public void PercentAtQ7Midpoint_RoundsAwayFromZeroBeforePublishingPercent()
    {
        using var scope = new SettingsDraftTestScope();
        var vm = scope.Owner;
        vm.LinkNotchThresholds = true;
        var trace = new List<(string Property, decimal Q7, decimal Percent)>();
        vm.PropertyChanged += (sender, change) =>
        {
            Assert.Same(vm, sender);
            if (change.PropertyName is nameof(vm.NotchThresholdQ7) or nameof(vm.NotchThresholdPercent))
            {
                trace.Add((change.PropertyName!, vm.NotchThresholdQ7, vm.NotchThresholdPercent));
            }
        };

        vm.NotchThresholdPercent = 50.390625m;

        Assert.Equal(new[]
        {
            (nameof(vm.NotchThresholdQ7), 65m, 50.390625m),
            (nameof(vm.NotchThresholdPercent), 65m, 50.390625m),
        }, trace);
    }
}
