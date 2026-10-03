using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class FreeformHelperViewModelTests
{
    [Fact]
    public async Task SettingsLayerPrecedence_ProjectOverridesAppGeneralAndAppGeneralOverridesDefault()
    {
        var appSettingsPath = Path.Combine(Path.GetTempPath(), $"freeform-app-general-{Guid.NewGuid():N}.json");
        var projectPath = Path.Combine(Path.GetTempPath(), $"freeform-project-{Guid.NewGuid():N}.json");

        try
        {
            var appStore = new AppGeneralSettingsStore(appSettingsPath);
            appStore.Save(new AppGeneralSettingsDocument
            {
                View = new UiViewSnapshot
                {
                    ShowCad = false,
                    ShowRegular = true,
                    ColorCadByArea = true,
                    ShowNotchCanvasPreview = false,
                    ShowNotchToRegularLabels = false
                },
                Behavior = new AppGeneralBehaviorSettings
                {
                    ApplyVisualPreferencesOnProjectLoad = false
                }
            });

            var vmFromApp = new FreeformHelperViewModel(appStore);
            Assert.False(vmFromApp.ShowCad);
            Assert.True(vmFromApp.ShowRegular);
            Assert.True(vmFromApp.ColorCadByArea);
            Assert.False(vmFromApp.ShowNotchCanvasPreview);
            Assert.False(vmFromApp.ShowNotchToRegularLabels);
            Assert.False(vmFromApp.ApplyAppVisualPreferencesOnProjectLoad);

            vmFromApp.MatchThreshold = 0.27m;
            vmFromApp.ShowCad = true;
            vmFromApp.ColorCadByArea = false;
            vmFromApp.ShowNotchCanvasPreview = true;
            vmFromApp.ShowNotchToRegularLabels = true;
            vmFromApp.PickSaveProjectPathAsync = () => Task.FromResult<string?>(projectPath);
            vmFromApp.ConfirmEmbedDxfAsync = () => Task.FromResult(false);
            Assert.True(await vmFromApp.SaveProjectAsync());

            var vmLoad = new FreeformHelperViewModel(appStore);
            Assert.False(vmLoad.ShowCad);
            Assert.True(vmLoad.ColorCadByArea);

            vmLoad.PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath);
            await vmLoad.LoadProjectCommand.ExecuteAsync(null);

            Assert.Equal(0.27m, vmLoad.MatchThreshold);
            Assert.True(vmLoad.ShowCad);
            Assert.False(vmLoad.ColorCadByArea);
            Assert.True(vmLoad.ShowNotchCanvasPreview);
            Assert.True(vmLoad.ShowNotchToRegularLabels);
        }
        finally
        {
            if (File.Exists(projectPath))
            {
                File.Delete(projectPath);
            }

            if (File.Exists(appSettingsPath))
            {
                File.Delete(appSettingsPath);
            }
        }
    }

    [Fact]
    public async Task LoadProject_WhenApplyAppVisualPreferencesEnabled_ReappliesAppVisualLayer()
    {
        var appSettingsPath = Path.Combine(Path.GetTempPath(), $"freeform-app-general-{Guid.NewGuid():N}.json");
        var projectPath = Path.Combine(Path.GetTempPath(), $"freeform-project-{Guid.NewGuid():N}.json");

        try
        {
            var appStore = new AppGeneralSettingsStore(appSettingsPath);
            appStore.Save(new AppGeneralSettingsDocument
            {
                View = new UiViewSnapshot
                {
                    ShowCad = false,
                    ColorCadByArea = true,
                    ShowNotchCanvasPreview = false,
                    ShowNotchToRegularLabels = false
                },
                Behavior = new AppGeneralBehaviorSettings
                {
                    ApplyVisualPreferencesOnProjectLoad = true
                }
            });

            var vmSaveProject = new FreeformHelperViewModel(appStore)
            {
                MatchThreshold = 0.33m,
                ShowCad = true,
                ColorCadByArea = false,
                ShowNotchCanvasPreview = true,
                ShowNotchToRegularLabels = true,
                PickSaveProjectPathAsync = () => Task.FromResult<string?>(projectPath),
                ConfirmEmbedDxfAsync = () => Task.FromResult(false)
            };
            Assert.True(await vmSaveProject.SaveProjectAsync());

            var vmLoad = new FreeformHelperViewModel(appStore);
            vmLoad.PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath);
            await vmLoad.LoadProjectCommand.ExecuteAsync(null);

            Assert.Equal(0.33m, vmLoad.MatchThreshold);
            Assert.False(vmLoad.ShowCad);
            Assert.True(vmLoad.ColorCadByArea);
            Assert.False(vmLoad.ShowNotchCanvasPreview);
            Assert.False(vmLoad.ShowNotchToRegularLabels);
            Assert.True(vmLoad.ApplyAppVisualPreferencesOnProjectLoad);
        }
        finally
        {
            if (File.Exists(projectPath))
            {
                File.Delete(projectPath);
            }

            if (File.Exists(appSettingsPath))
            {
                File.Delete(appSettingsPath);
            }
        }
    }

    [Fact]
    public async Task AppGeneralSettings_AfterLoadProject_DefersUntilNextProjectSave()
    {
        var appSettingsPath = Path.Combine(Path.GetTempPath(), $"freeform-app-general-{Guid.NewGuid():N}.json");
        var projectPath = Path.Combine(Path.GetTempPath(), $"freeform-project-{Guid.NewGuid():N}.json");

        try
        {
            var appStore = new AppGeneralSettingsStore(appSettingsPath);
            appStore.Save(new AppGeneralSettingsDocument
            {
                View = new UiViewSnapshot
                {
                    GlobalFontSizePercent = 120
                }
            });

            var vm = new FreeformHelperViewModel(appStore)
            {
                PickSaveProjectPathAsync = () => Task.FromResult<string?>(projectPath),
                ConfirmEmbedDxfAsync = () => Task.FromResult(false)
            };
            Assert.True(await vm.SaveProjectAsync());

            vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath);
            await vm.LoadProjectCommand.ExecuteAsync(null);

            var baselineDoc = appStore.TryLoad();
            Assert.NotNull(baselineDoc);
            var baselineFont = baselineDoc!.View.GlobalFontSizePercent;

            vm.GlobalFontSizePercent = baselineFont >= 130
                ? 118m
                : (decimal)baselineFont + 8m;
            await Task.Delay(700);

            var deferredDoc = appStore.TryLoad();
            Assert.NotNull(deferredDoc);
            Assert.Equal(baselineFont, deferredDoc!.View.GlobalFontSizePercent, 6);

            Assert.True(await vm.SaveProjectAsync());

            var flushedDoc = appStore.TryLoad();
            Assert.NotNull(flushedDoc);
            Assert.Equal((double)vm.GlobalFontSizePercent, flushedDoc!.View.GlobalFontSizePercent, 6);
        }
        finally
        {
            if (File.Exists(projectPath))
            {
                File.Delete(projectPath);
            }

            if (File.Exists(appSettingsPath))
            {
                File.Delete(appSettingsPath);
            }
        }
    }
}
