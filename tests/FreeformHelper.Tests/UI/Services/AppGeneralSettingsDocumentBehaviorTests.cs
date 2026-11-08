// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class AppGeneralSettingsDocumentBehaviorTests : IDisposable
{
    private readonly DirectoryInfo _directory = TestFiles.CreateTempDirectory();

    [Fact]
    public void TryLoad_JsonNull_ReturnsNoDocumentAndKeepsTheFile()
    {
        var store = CreateStore("null");

        var document = store.TryLoad();

        Assert.Null(document);
        Assert.Equal("null", TestFiles.ReadContent(store.SettingsPath));
        Assert.Equal(store.SettingsPath, Assert.Single(_directory.EnumerateFiles()).FullName);
    }

    [Fact]
    public void TryLoad_LegacyNullSections_DefaultsMissingPreferencesAndKeepsOtherValues()
    {
        (string Snapshot, bool ShowCad, bool IncludeBlocks, string LogLevel)[] cases =
        [
            ("null", true, true, string.Empty),
            ("{\"View\":null,\"Import\":{\"IncludeBlockPolylines\":false,\"LogLevel\":\"Warn\"}}", true, false, "Warn"),
            ("{\"View\":{\"ShowCad\":false},\"Import\":null}", false, true, string.Empty),
        ];

        foreach (var item in cases)
        {
            var json = "{\"SchemaVersion\":1,\"SavedAtUtc\":\"2026-11-08T14:15:16Z\",\"UiSnapshot\":" + item.Snapshot + "}";
            var store = CreateStore(json);

            var document = store.TryLoad();

            Assert.NotNull(document);
            Assert.NotNull(document.View);
            Assert.NotNull(document.Import);
            Assert.NotNull(document.Behavior);
            Assert.Equal(item.ShowCad, document.View.ShowCad);
            Assert.Equal(item.IncludeBlocks, document.Import.IncludeBlockPolylines);
            Assert.Equal(item.LogLevel, document.Import.LogLevel);
            Assert.True(document.Behavior.ApplyVisualPreferencesOnProjectLoad);
            Assert.Equal(new DateTimeOffset(2026, 11, 8, 14, 15, 16, TimeSpan.Zero), document.SavedAtUtc);
        }
    }

    [Fact]
    public void TryLoad_CurrentNullOrOmittedSections_DefaultsOnlyMissingPreferences()
    {
        (string Json, bool ShowCad, bool IncludeBlocks, bool ApplyVisuals)[] cases =
        [
            ("{\"View\":null,\"Import\":{\"IncludeBlockPolylines\":false},\"Behavior\":null}", true, false, true),
            ("{\"Import\":null,\"Behavior\":{\"ApplyVisualPreferencesOnProjectLoad\":false}}", true, true, false),
            ("{\"Import\":{\"IncludeBlockPolylines\":false}}", true, false, true),
            ("{\"Behavior\":{\"ApplyVisualPreferencesOnProjectLoad\":false}}", true, true, false),
            ("{\"View\":{\"ShowCad\":false},\"Import\":null,\"Behavior\":null}", false, true, true),
        ];

        foreach (var item in cases)
        {
            var store = CreateStore(item.Json);

            var document = store.TryLoad();

            Assert.NotNull(document);
            Assert.NotNull(document.View);
            Assert.NotNull(document.Import);
            Assert.NotNull(document.Behavior);
            Assert.Equal(item.ShowCad, document.View.ShowCad);
            Assert.Equal(item.IncludeBlocks, document.Import.IncludeBlockPolylines);
            Assert.Equal(item.ApplyVisuals, document.Behavior.ApplyVisualPreferencesOnProjectLoad);
        }
    }

    [Fact]
    public void Constructor_TrimmedOverrideTemplate_ExpandsTokensAndSeparatesStores()
    {
        using var culture = TestCulture.UseEnUs();
        var original = TestEnvironment.Get(AppGeneralSettingsStore.SettingsPathOverrideEnvironmentVariable);
        var template = Path.Combine(_directory.FullName, "{PiD}", "{GuId}", "app-general-settings.json");
        AppGeneralSettingsStore first;
        AppGeneralSettingsStore second;
        try
        {
            TestEnvironment.Set(AppGeneralSettingsStore.SettingsPathOverrideEnvironmentVariable, "  " + template + "  ");
            first = new AppGeneralSettingsStore(" \t");
            second = new AppGeneralSettingsStore();
        }
        finally
        {
            TestEnvironment.Set(AppGeneralSettingsStore.SettingsPathOverrideEnvironmentVariable, original);
        }

        var expectedParent = Path.Combine(_directory.FullName, Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        foreach (var store in new[] { first, second })
        {
            var directory = Path.GetDirectoryName(store.SettingsPath)!;
            Assert.Equal(expectedParent, Path.GetDirectoryName(directory));
            Assert.True(Guid.TryParseExact(Path.GetFileName(directory), "N", out _));
            Assert.Equal("app-general-settings.json", Path.GetFileName(store.SettingsPath));
        }

        Assert.NotEqual(first.SettingsPath, second.SettingsPath);
        var firstDocument = new AppGeneralSettingsDocument();
        firstDocument.View.ShowCad = false;

        first.Save(firstDocument);
        second.Save(new AppGeneralSettingsDocument());

        var firstLoaded = first.TryLoad();
        var secondLoaded = second.TryLoad();
        Assert.NotNull(firstLoaded);
        Assert.NotNull(secondLoaded);
        Assert.False(firstLoaded.View.ShowCad);
        Assert.True(secondLoaded.View.ShowCad);
    }

    public void Dispose() => _directory.Delete(recursive: true);

    private AppGeneralSettingsStore CreateStore(string json)
    {
        var path = Path.Combine(_directory.FullName, "app-general-settings.json");
        TestFiles.WriteText(path, json);
        return new AppGeneralSettingsStore(path);
    }
}
