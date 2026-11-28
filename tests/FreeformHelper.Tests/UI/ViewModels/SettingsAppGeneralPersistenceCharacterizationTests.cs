// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
[Trait("Category", "Characterization")]
public sealed class SettingsAppGeneralPersistenceCharacterizationTests
{
    [AvaloniaFact]
    public async Task AppGeneralSettings_DebouncedEdits_PersistLatestValuesOnce()
    {
        using var culture = TestCulture.UseEnUs();
        var writes = new List<AppGeneralSettingsDocument>();
        var writeCompleted = new TaskCompletionSource<AppGeneralSettingsDocument>(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new AppGeneralSettingsStore(
            "app-general-settings-unused.json",
            readTextOverride: static _ => null,
            saveOverride: document =>
            {
                writes.Add(document);
                writeCompleted.TrySetResult(document);
            });
        var vm = new FreeformHelperViewModel(store);
        try
        {
            await HeadlessUiPump.RunningDispatcher.InvokeAsync(() =>
            {
                vm.GlobalFontSizePercent = 112m;
                vm.ShowCad = false;
                vm.ImportOnlyClosedPolylines = false;
                vm.ApplyAppVisualPreferencesOnProjectLoad = false;
                vm.GlobalFontSizePercent = 128m;
                vm.ShowCad = true;
                vm.ImportOnlyClosedPolylines = true;
                vm.ApplyAppVisualPreferencesOnProjectLoad = true;
                vm.ShowCad = false;
                vm.ImportOnlyClosedPolylines = false;
                vm.ApplyAppVisualPreferencesOnProjectLoad = false;
                Assert.Empty(writes);
            });

            // Pump the real dispatcher timer until the store signals a write; the pump has a hang watchdog.
            await HeadlessUiPump.WaitUntilAsync(() => writeCompleted.Task.IsCompleted, "App general settings were not written.");
            var saved = await writeCompleted.Task.WaitAsync(TestContext.Current.CancellationToken);
            await HeadlessUiPump.FlushQueueAsync();

            Assert.Same(saved, Assert.Single(writes));
            Assert.Equal(128d, saved.View.GlobalFontSizePercent);
            Assert.False(saved.View.ShowCad);
            Assert.False(saved.Import.OnlyClosedPolylines);
            Assert.False(saved.Behavior.ApplyVisualPreferencesOnProjectLoad);
        }
        finally
        {
            vm.AppGeneralPersistenceForTests.PersistTimer?.Stop();
        }
    }

    [AvaloniaFact]
    public void SettingsWindow_AppGeneralWriteFails_NextApplyRetriesLatestValues()
    {
        using var culture = TestCulture.UseEnUs();
        var attempts = new List<AppGeneralSettingsDocument>();
        AppGeneralSettingsDocument? successfulWrite = null;
        var store = new AppGeneralSettingsStore(
            "app-general-settings-unused.json",
            readTextOverride: static _ => null,
            saveOverride: document =>
            {
                attempts.Add(document);
                if (attempts.Count == 1)
                {
                    throw new IOException("The first app general settings write failed.");
                }

                successfulWrite = document;
            });
        var vm = new FreeformHelperViewModel(store);
        try
        {
            var closeRequests = 0;
            var first = vm.CreateSettingsWindowViewModel();
            first.GlobalFontSizePercent = 112m;
            first.ImportOnlyClosedPolylines = false;
            first.ApplyAppVisualPreferencesOnProjectLoad = false;
            first.RequestClose += () => closeRequests++;

            first.SaveCommand.Execute(null);

            Assert.Equal(1, closeRequests);
            var failedWrite = Assert.Single(attempts);
            Assert.Equal(112d, failedWrite.View.GlobalFontSizePercent);
            Assert.False(failedWrite.Import.OnlyClosedPolylines);
            Assert.False(failedWrite.Behavior.ApplyVisualPreferencesOnProjectLoad);
            Assert.Null(successfulWrite);

            var second = vm.CreateSettingsWindowViewModel();
            second.GlobalFontSizePercent = 136m;
            second.ImportOnlyClosedPolylines = true;
            second.ApplyAppVisualPreferencesOnProjectLoad = true;
            second.RequestClose += () => closeRequests++;

            second.SaveCommand.Execute(null);

            Assert.Equal(2, closeRequests);
            Assert.Equal(2, attempts.Count);
            var saved = Assert.IsType<AppGeneralSettingsDocument>(successfulWrite);
            Assert.Same(saved, attempts[1]);
            Assert.Equal(136d, saved.View.GlobalFontSizePercent);
            Assert.True(saved.Import.OnlyClosedPolylines);
            Assert.True(saved.Behavior.ApplyVisualPreferencesOnProjectLoad);
        }
        finally
        {
            vm.AppGeneralPersistenceForTests.PersistTimer?.Stop();
        }
    }
}
