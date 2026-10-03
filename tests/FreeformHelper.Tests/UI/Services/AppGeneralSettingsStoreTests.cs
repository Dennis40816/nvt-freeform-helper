using System.Text.Json;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class AppGeneralSettingsStoreTests
{
    [Fact]
    public void DefaultCtor_UsesEnvironmentOverridePath()
    {
        var original = Environment.GetEnvironmentVariable(AppGeneralSettingsStore.SettingsPathOverrideEnvironmentVariable);
        var expectedPath = Path.Combine(Path.GetTempPath(), $"app-general-env-{Guid.NewGuid():N}.json");

        try
        {
            Environment.SetEnvironmentVariable(
                AppGeneralSettingsStore.SettingsPathOverrideEnvironmentVariable,
                expectedPath);
            var store = new AppGeneralSettingsStore();
            Assert.Equal(expectedPath, store.SettingsPath);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                AppGeneralSettingsStore.SettingsPathOverrideEnvironmentVariable,
                original);
            if (File.Exists(expectedPath))
            {
                File.Delete(expectedPath);
            }
        }
    }

    [Fact]
    public void SaveThenLoad_WhitelistShape_RoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), $"app-general-{Guid.NewGuid():N}.json");

        try
        {
            var store = new AppGeneralSettingsStore(path);
            store.Save(new AppGeneralSettingsDocument
            {
                View = new UiViewSnapshot
                {
                    ShowCad = false,
                    ShowRegular = true,
                    ColorCadByArea = true,
                    ShowNotchCanvasPreview = false,
                    ShowNotchToRegularLabels = false,
                    GlobalFontSizePercent = 126
                },
                Import = new UiImportSnapshot
                {
                    OnlyClosedPolylines = true,
                    IncludeBlockPolylines = false,
                    LogLevel = "Warn"
                },
                Behavior = new AppGeneralBehaviorSettings
                {
                    ApplyVisualPreferencesOnProjectLoad = false
                }
            });

            var loaded = store.TryLoad();
            Assert.NotNull(loaded);
            Assert.Equal(AppGeneralSettingsDocument.CurrentSchemaVersion, loaded!.SchemaVersion);
            Assert.False(loaded.View.ShowCad);
            Assert.True(loaded.View.ShowRegular);
            Assert.True(loaded.View.ColorCadByArea);
            Assert.False(loaded.View.ShowNotchCanvasPreview);
            Assert.False(loaded.View.ShowNotchToRegularLabels);
            Assert.Equal(126, loaded.View.GlobalFontSizePercent, 6);
            Assert.True(loaded.Import.OnlyClosedPolylines);
            Assert.False(loaded.Import.IncludeBlockPolylines);
            Assert.Equal("Warn", loaded.Import.LogLevel);
            Assert.False(loaded.Behavior.ApplyVisualPreferencesOnProjectLoad);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void TryLoad_LegacyDocument_MapsToWhitelistAndDefaultsBehavior()
    {
        var path = Path.Combine(Path.GetTempPath(), $"app-general-legacy-{Guid.NewGuid():N}.json");

        try
        {
            var legacy = new
            {
                SchemaVersion = 1,
                SavedAtUtc = DateTimeOffset.UtcNow,
                Settings = new
                {
                    Matching = new
                    {
                        MatchThreshold = 0.61
                    }
                },
                UiSnapshot = new
                {
                    View = new
                    {
                        ShowCad = false,
                        ShowRegular = true,
                        ColorCadByArea = true
                    },
                    Import = new
                    {
                        OnlyClosedPolylines = true,
                        IncludeBlockPolylines = false,
                        LogLevel = "Info"
                    }
                }
            };
            File.WriteAllText(path, JsonSerializer.Serialize(legacy));

            var store = new AppGeneralSettingsStore(path);
            var loaded = store.TryLoad();
            Assert.NotNull(loaded);
            Assert.False(loaded!.View.ShowCad);
            Assert.True(loaded.View.ShowRegular);
            Assert.True(loaded.View.ColorCadByArea);
            Assert.True(loaded.View.ShowNotchToRegularLabels);
            Assert.True(loaded.Import.OnlyClosedPolylines);
            Assert.False(loaded.Import.IncludeBlockPolylines);
            Assert.Equal("Info", loaded.Import.LogLevel);
            Assert.True(loaded.Behavior.ApplyVisualPreferencesOnProjectLoad);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
