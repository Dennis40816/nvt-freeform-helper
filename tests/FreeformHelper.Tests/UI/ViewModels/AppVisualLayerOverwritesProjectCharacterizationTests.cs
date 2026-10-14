// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins what the app visual preference layer does to project values after a project load (issue
/// #64, inventory finding SETTINGS-D05). With the layer enabled, the whole app view snapshot is
/// applied again after the project is loaded, so values that are not appearance, such as the
/// coordinate image size and the AA outline layer name, take the app value and not the value of
/// the project. This test describes the current result, not the wanted one. A fix that limits the
/// layer to an appearance allowlist changes the expected values here and must say so in its pull
/// request.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class AppVisualLayerOverwritesProjectCharacterizationTests
{
    private const decimal ProjectWidth = 300m;
    private const decimal ProjectHeight = 200m;
    private const decimal AppWidth = 640m;
    private const decimal AppHeight = 480m;
    private const string ProjectLayerName = "AA_PROJECT";
    private const string AppLayerName = "AA_APP";

    [Fact]
    public async Task LoadProject_WithAppVisualLayerEnabled_ReplacesProjectCoordinateValuesWithAppValues()
    {
        var directory = TestFiles.CreateTempDirectory();
        try
        {
            var appStore = new AppGeneralSettingsStore(Path.Combine(directory.FullName, "app-settings.json"));
            appStore.Save(new AppGeneralSettingsDocument
            {
                View = new UiViewSnapshot
                {
                    ShowCad = false,
                    CoordinatePixelWidth = (double)AppWidth,
                    CoordinatePixelHeight = (double)AppHeight,
                    CoordinatePreferredAaOutlineLayerName = AppLayerName,
                },
                Behavior = new AppGeneralBehaviorSettings { ApplyVisualPreferencesOnProjectLoad = true },
            });
            var projectPath = Path.Combine(directory.FullName, "project.json");
            var saver = new FreeformHelperViewModel(appStore)
            {
                CoordinatePixelWidth = ProjectWidth,
                CoordinatePixelHeight = ProjectHeight,
                CoordinatePreferredAaOutlineLayerName = ProjectLayerName,
                PickSaveProjectPathAsync = () => Task.FromResult<string?>(projectPath),
                ConfirmEmbedDxfAsync = () => Task.FromResult(false),
            };
            Assert.True(await saver.SaveProjectAsync());

            var loader = new FreeformHelperViewModel(appStore)
            {
                PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath),
            };
            await loader.LoadProjectCommand.ExecuteAsync(null);

            // The app layer is applied after the project, so the project values are gone.
            Assert.Equal(AppWidth, loader.CoordinatePixelWidth);
            Assert.Equal(AppHeight, loader.CoordinatePixelHeight);
            Assert.Equal(AppLayerName, loader.CoordinatePreferredAaOutlineLayerName);
            Assert.False(loader.ShowCad);

            // Control: the saved file holds the project values, so the layer is what replaced them.
            var control = new FreeformHelperViewModel(appStore)
            {
                ApplyAppVisualPreferencesOnProjectLoad = false,
                PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath),
            };
            await control.LoadProjectCommand.ExecuteAsync(null);
            Assert.Equal(ProjectWidth, control.CoordinatePixelWidth);
            Assert.Equal(ProjectHeight, control.CoordinatePixelHeight);
            Assert.Equal(ProjectLayerName, control.CoordinatePreferredAaOutlineLayerName);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
