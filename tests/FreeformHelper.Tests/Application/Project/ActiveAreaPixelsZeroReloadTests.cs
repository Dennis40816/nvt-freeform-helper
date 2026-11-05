using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Headless.XUnit;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ActiveAreaPixelsZeroReloadTests
{
    [AvaloniaFact]
    public async Task SettingsZero_SavedToFileAndReloaded_ReadsAsNotSetWithoutAZeroInTheJson()
    {
        using var files = new LoadingModeTestScope();
        await files.SaveAsync(project =>
        {
            project.ActiveAreaPixelsX = 6480m;
            project.ActiveAreaPixelsY = 848m;
        });
        var path = files.PathFor("project.json");
        var vm = files.CreateViewModel();
        Assert.True((await vm.LoadProjectFromPathAsync(path)).IsLoaded);
        var settings = vm.CreateSettingsWindowViewModel();
        Assert.Equal(6480m, settings.ActiveAreaPixels.PixelsX);

        settings.ActiveAreaPixels.PixelsX = 0m;
        settings.SaveCommand.Execute(null);
        Assert.Equal(0m, vm.ActiveAreaPixelsX);
        Assert.True(await vm.SaveProjectAsync());

        var loaded = JsonProjectStore.Load(path);
        Assert.Null(loaded.Settings.ActiveAreaPixels.X);
        Assert.Equal(848, loaded.Settings.ActiveAreaPixels.Y);
        var section = JsonNode.Parse(TestFiles.ReadContent(path))!["settings"]!["activeAreaPixels"]!.AsObject();
        Assert.Null(section["x"]);
        Assert.DoesNotContain(
            section,
            property => property.Value is { } value && value.GetValueKind() == JsonValueKind.Number && value.GetValue<int>() == 0);
        Assert.Equal(848, section["y"]!.GetValue<int>());
    }
}
