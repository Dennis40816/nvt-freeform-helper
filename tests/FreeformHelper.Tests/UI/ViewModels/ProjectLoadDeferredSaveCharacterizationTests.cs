// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>Current behaviour: a cancelled or failed Save Project does not flush the app settings deferred by Load Project.</summary>
[Collection("HeadlessUiSerial")]
public sealed class ProjectLoadDeferredSaveCharacterizationTests
{
    [AvaloniaFact]
    public async Task LoadFromPath_CurrentBehavior_DefersAppWritesThroughCancelledAndFailedSavesUntilSuccessfulSaveAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new ProjectLoadCharacterizationScope();
        var appStore = files.AppStore;
        appStore.Save(new AppGeneralSettingsDocument
        {
            View = new UiViewSnapshot { GlobalFontSizePercent = 120 },
            Behavior = new AppGeneralBehaviorSettings { ApplyVisualPreferencesOnProjectLoad = false },
        });
        var path = files.PathFor("saved.json");
        await new JsonProjectStore().SaveAsync(path, new ProjectFile(), TestContext.Current.CancellationToken);
        var vm = files.CreateViewModel();
        Assert.True((await vm.LoadProjectFromPathAsync(path)).IsLoaded);
        vm.GlobalFontSizePercent = 128m;
        AssertStillDeferred(vm, appStore);

        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(null);
        Assert.False(await vm.SaveProjectAsync());
        AssertStillDeferred(vm, appStore);

        var blocker = files.PathFor("blocker");
        await TestFiles.WriteTextAsync(blocker, "not a directory", TestContext.Current.CancellationToken);
        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(Path.Combine(blocker, "project.json"));
        Assert.False(await vm.SaveProjectAsync());
        Assert.StartsWith("Save project failed:", vm.StatusText, StringComparison.Ordinal);
        AssertStillDeferred(vm, appStore);

        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(path);
        Assert.True(await vm.SaveProjectAsync());
        var document = Assert.IsType<AppGeneralSettingsDocument>(appStore.TryLoad());
        Assert.Equal(128.0, document.View.GlobalFontSizePercent);
        Assert.False(vm.HasUnsavedChanges);
    }

    private static void AssertStillDeferred(FreeformHelperViewModel vm, AppGeneralSettingsStore appStore)
    {
        var document = Assert.IsType<AppGeneralSettingsDocument>(appStore.TryLoad());
        Assert.Equal(120.0, document.View.GlobalFontSizePercent);
        Assert.True(vm.HasUnsavedChanges);
        var persistence = vm.AppGeneralPersistenceForTests;
        Assert.True(persistence.DeferAfterProjectLoad);
        Assert.True(persistence.HasDeferredChanges);
        Assert.False(persistence.PersistTimer?.IsEnabled ?? false);
    }
}
