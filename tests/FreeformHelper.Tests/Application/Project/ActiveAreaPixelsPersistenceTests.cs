using System.Text.Json.Nodes;
using FreeformHelper.Application.Settings;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class ActiveAreaPixelsPersistenceTests
{
    [Fact]
    public void ProjectSettings_DefaultsToNoPixelCounts()
    {
        var settings = new ProjectSettings();

        Assert.Null(settings.ActiveAreaPixels.X);
        Assert.Null(settings.ActiveAreaPixels.Y);
        settings.ValidateOrThrow();
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsThePixelCounts()
    {
        var path = TestFiles.NewTempFilePath("cad_project_", ".json");
        var store = new JsonProjectStore();
        var file = new ProjectFile
        {
            Settings = new ProjectSettings
            {
                Grid = new GridSettings { ActiveAreaWidth = 915.52, ActiveAreaHeight = 119.59 },
                ActiveAreaPixels = new ActiveAreaPixelsSettings { X = 6480, Y = 848 },
            },
        };

        await store.SaveAsync(path, file, CancellationToken.None);
        var loaded = JsonProjectStore.Load(path);

        Assert.Equal(6480, loaded.Settings.ActiveAreaPixels.X);
        Assert.Equal(848, loaded.Settings.ActiveAreaPixels.Y);
        Assert.Equal(915.52, loaded.Settings.Grid.ActiveAreaWidth, 6);
    }

    [Fact]
    public async Task Load_ReadsAnOlderProjectWithoutPixelCountsAsNotSet()
    {
        var path = TestFiles.NewTempFilePath("cad_project_", ".json");
        var store = new JsonProjectStore();
        var file = new ProjectFile
        {
            Settings = new ProjectSettings
            {
                ActiveAreaPixels = new ActiveAreaPixelsSettings { X = 6480, Y = 848 },
            },
        };
        await store.SaveAsync(path, file, CancellationToken.None);
        var root = JsonNode.Parse(TestFiles.ReadContent(path))!.AsObject();
        var settingsNode = root["settings"]!.AsObject();
        Assert.True(settingsNode.Remove("activeAreaPixels"));
        TestFiles.WriteText(path, root.ToJsonString());

        var loaded = JsonProjectStore.Load(path);

        Assert.Null(loaded.Settings.ActiveAreaPixels.X);
        Assert.Null(loaded.Settings.ActiveAreaPixels.Y);
    }

    [Fact]
    public async Task Load_ReadsANullPixelCountSectionAsNotSet()
    {
        var path = TestFiles.NewTempFilePath("cad_project_", ".json");
        var store = new JsonProjectStore();
        await store.SaveAsync(path, new ProjectFile(), CancellationToken.None);
        var root = JsonNode.Parse(TestFiles.ReadContent(path))!.AsObject();
        root["settings"]!.AsObject()["activeAreaPixels"] = null;
        TestFiles.WriteText(path, root.ToJsonString());

        var loaded = JsonProjectStore.Load(path);

        Assert.NotNull(loaded.Settings.ActiveAreaPixels);
        Assert.Null(loaded.Settings.ActiveAreaPixels.X);
        Assert.Null(loaded.Settings.ActiveAreaPixels.Y);
    }
}
