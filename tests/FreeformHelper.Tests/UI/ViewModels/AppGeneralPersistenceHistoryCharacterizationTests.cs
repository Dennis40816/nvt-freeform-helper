// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
[Trait("Category", "Characterization")]
public sealed class AppGeneralPersistenceHistoryCharacterizationTests
{
    [AvaloniaFact]
    public async Task FailedAppSaveProjectLoadKeepsLastSuccessfulVisualSnapshot()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new ProjectLoadCharacterizationScope();
        var path = files.PathFor("visual-history.json");
        var project = new ProjectFile { UiSnapshot = new ProjectUiSnapshot { View = new UiViewSnapshot { GlobalFontSizePercent = 96 } } };
        await new JsonProjectStore().SaveAsync(path, project, TestContext.Current.CancellationToken);
        var attempts = new List<AppGeneralSettingsDocument>();
        var store = new AppGeneralSettingsStore(files.PathFor("app.json"), static _ => null, document =>
        {
            attempts.Add(document);
            if (attempts.Count > 1)
            {
                throw new IOException("The second app settings write failed.");
            }
        });
        var vm = new FreeformHelperViewModel(store);
        var persistence = vm.AppGeneralPersistenceForTests;
        try
        {
            ApplyFontScale(vm, 112m);
            Assert.Equal(112d, Assert.Single(attempts).View.GlobalFontSizePercent);
            ApplyFontScale(vm, 136m);
            Assert.Equal(2, attempts.Count);
            Assert.Equal(136d, attempts[1].View.GlobalFontSizePercent);
            Assert.Equal(136m, vm.GlobalFontSizePercent);
            persistence.PersistTimer?.Stop();

            Assert.True((await vm.LoadProjectFromPathAsync(path)).IsLoaded);

            Assert.Equal(112m, vm.GlobalFontSizePercent);
            Assert.Same(persistence, vm.AppGeneralPersistenceForTests);
            Assert.Equal(2, attempts.Count);
            Assert.True(persistence.DeferAfterProjectLoad);
            Assert.False(persistence.HasDeferredChanges);
        }
        finally
        {
            persistence.PersistTimer?.Stop();
        }
    }

    [AvaloniaFact]
    public async Task SuccessfulProjectSaveFlushesChangesButKeepsSubsequentEditsDeferred()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new ProjectLoadCharacterizationScope();
        var path = files.PathFor("deferred.json");
        await new JsonProjectStore().SaveAsync(path, new ProjectFile(), TestContext.Current.CancellationToken);
        var vm = files.CreateViewModel();
        var persistence = vm.AppGeneralPersistenceForTests;
        Assert.True((await vm.LoadProjectFromPathAsync(path)).IsLoaded);
        vm.GlobalFontSizePercent = 128m;
        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(path);

        Assert.True(await vm.SaveProjectAsync());

        Assert.Same(persistence, vm.AppGeneralPersistenceForTests);
        Assert.True(persistence.DeferAfterProjectLoad);
        Assert.False(persistence.HasDeferredChanges);
        Assert.Equal(128d, Assert.IsType<AppGeneralSettingsDocument>(files.AppStore.TryLoad()).View.GlobalFontSizePercent);
        vm.GlobalFontSizePercent = 136m;
        vm.PersistAppGeneralSettingsNow();
        Assert.True(persistence.HasDeferredChanges);
        Assert.False(persistence.PersistTimer?.IsEnabled ?? false);
        Assert.Equal(128d, Assert.IsType<AppGeneralSettingsDocument>(files.AppStore.TryLoad()).View.GlobalFontSizePercent);
    }

    private static void ApplyFontScale(FreeformHelperViewModel vm, decimal value)
    {
        var draft = vm.CreateSettingsWindowViewModel();
        draft.GlobalFontSizePercent = value;
        draft.ApplyAppVisualPreferencesOnProjectLoad = true;
        draft.SaveCommand.Execute(null);
    }
}
