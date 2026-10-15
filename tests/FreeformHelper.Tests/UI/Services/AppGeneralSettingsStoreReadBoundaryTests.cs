// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins what <see cref="AppGeneralSettingsStore.TryLoad"/> does with the text it reads, without touching the disk:
/// a file that cannot be used gives no document, and a failure that means the process is no longer sound is not hidden.
/// </summary>
public sealed class AppGeneralSettingsStoreReadBoundaryTests
{
    private const string UnusedPath = "app-general-settings-unused.json";

    [Fact]
    public void TryLoad_WhenTheFileIsMissing_ReturnsNull()
    {
        var store = CreateStore(static _ => null);

        Assert.Null(store.TryLoad());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \r\n  ")]
    public void TryLoad_WhenTheFileIsBlank_ReturnsNull(string text)
    {
        var store = CreateStore(_ => text);

        Assert.Null(store.TryLoad());
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("[]")]
    [InlineData("42")]
    public void TryLoad_WhenTheFileIsNotAUsableDocument_ReturnsNull(string text)
    {
        var store = CreateStore(_ => text);

        Assert.Null(store.TryLoad());
    }

    [Fact]
    public void TryLoad_WhenTheFileHasTheCurrentShape_ReturnsTheDocumentWithAllParts()
    {
        var store = CreateStore(static _ => "{\"View\":{},\"Import\":{}}");

        var document = store.TryLoad();

        Assert.NotNull(document);
        Assert.NotNull(document.View);
        Assert.NotNull(document.Import);
        Assert.NotNull(document.Behavior);
    }

    [Fact]
    public void TryLoad_WhenTheFileHasTheLegacyShape_ReturnsTheDocumentWithDefaultBehavior()
    {
        var store = CreateStore(static _ => "{\"UiSnapshot\":{\"View\":{},\"Import\":{}}}");

        var document = store.TryLoad();

        Assert.NotNull(document);
        Assert.NotNull(document.View);
        Assert.NotNull(document.Import);
        Assert.True(document.Behavior.ApplyVisualPreferencesOnProjectLoad);
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    [InlineData(typeof(InvalidOperationException))]
    public void TryLoad_WhenTheReadFails_ReturnsNull(Type failureType)
    {
        var failure = (Exception)Activator.CreateInstance(failureType)!;
        var store = CreateStore(_ => throw failure);

        Assert.Null(store.TryLoad());
    }

    private static AppGeneralSettingsStore CreateStore(Func<string, string?> readText) =>
        new(UnusedPath, readText, saveOverride: null);
}
