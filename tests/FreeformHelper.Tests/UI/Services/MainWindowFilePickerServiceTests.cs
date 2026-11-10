// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Tests.TestInfrastructure;
using Xunit;
using FilePickerService = FreeformHelper.UI.Services.MainWindowFilePickerService;

namespace FreeformHelper.Tests;

public sealed class MainWindowFilePickerServiceTests
{
    [Fact]
    public async Task OpenDxfForwardsRequestAndReturnsNullOnCancelAsync()
    {
        var provider = RecordingFilePickerProvider.Create(out var recorder);
        var service = new FilePickerService(provider);

        var path = await service.OpenDxfAsync();

        Assert.Null(path);
        FilePickerRequestAssertions.Open(recorder, "Open DXF", ("DXF", "*.dxf"), ("All files", "*"));
    }

    [Fact]
    public async Task OpenRegularVisibilityMaskForwardsRequestAndReturnsNullOnCancelAsync()
    {
        var provider = RecordingFilePickerProvider.Create(out var recorder);
        var service = new FilePickerService(provider);

        var path = await service.OpenRegularVisibilityMaskAsync();

        Assert.Null(path);
        FilePickerRequestAssertions.Open(recorder, "Import Regular Visibility Mask (SeeRegular.csv)",
            ("CSV", "*.csv"), ("All files", "*"));
    }

    [Fact]
    public async Task OpenProjectForwardsRequestAndReturnsNullOnCancelAsync()
    {
        var provider = RecordingFilePickerProvider.Create(out var recorder);
        var service = new FilePickerService(provider);

        var path = await service.OpenProjectAsync();

        Assert.Null(path);
        FilePickerRequestAssertions.Open(recorder, "Open Project",
            ("FreeformHelper Project", "*.json"), ("All files", "*"));
    }

    [Fact]
    public async Task SaveProjectForwardsRequestAndReturnsNullOnCancelAsync()
    {
        var provider = RecordingFilePickerProvider.Create(out var recorder);
        var service = new FilePickerService(provider);

        var path = await service.SaveProjectAsync();

        Assert.Null(path);
        FilePickerRequestAssertions.Save(recorder, "Save Project", "json", "freeform_helper_project.json",
            ("FreeformHelper Project", "*.json"), ("All files", "*"));
    }

    [Theory]
    [InlineData("notch_table", ".C", "c", "C source (*.c)", "*.c", "CSV (*.csv)", "*.csv")]
    [InlineData("other_table", "csv", "csv", "CSV (*.csv)", "*.csv", "C source (*.c)", "*.c")]
    public async Task SaveNotchForwardsArgumentsAndReturnsNullOnCancelAsync(
        string baseName, string ext, string expectedExt, string firstName, string firstPattern,
        string secondName, string secondPattern)
    {
        var provider = RecordingFilePickerProvider.Create(out var recorder);
        var service = new FilePickerService(provider);

        var path = await service.SaveNotchAsync(baseName, ext);

        Assert.Null(path);
        FilePickerRequestAssertions.Save(recorder, "Save Notch Table", expectedExt, $"{baseName}.{expectedExt}",
            (firstName, firstPattern), (secondName, secondPattern), ("All files", "*"));
    }

    [Theory]
    [InlineData("layer_image", ".PNG", "png", "Save Image", "PNG image")]
    [InlineData("layer_outline", "dxf", "dxf", "Save DXF", "DXF file")]
    public async Task SaveExportForwardsArgumentsAndReturnsNullOnCancelAsync(
        string baseName, string ext, string expectedExt, string title, string typeName)
    {
        var provider = RecordingFilePickerProvider.Create(out var recorder);
        var service = new FilePickerService(provider);

        var path = await service.SaveExportAsync(baseName, ext);

        Assert.Null(path);
        FilePickerRequestAssertions.Save(recorder, title, expectedExt, $"{baseName}.{expectedExt}",
            (typeName, $"*.{expectedExt}"), ("All files", "*"));
    }

    [Theory]
    [InlineData(nameof(FilePickerService.OpenDxfAsync))]
    [InlineData(nameof(FilePickerService.OpenRegularVisibilityMaskAsync))]
    [InlineData(nameof(FilePickerService.OpenProjectAsync))]
    [InlineData(nameof(FilePickerService.SaveProjectAsync))]
    [InlineData(nameof(FilePickerService.SaveNotchAsync))]
    [InlineData(nameof(FilePickerService.SaveExportAsync))]
    public async Task ProviderExceptionPropagatesUnchangedAsync(string operation)
    {
        var provider = RecordingFilePickerProvider.Create(out var recorder);
        var failure = new IOException("picker failed");
        recorder.Failure = failure;
        var service = new FilePickerService(provider);
        Func<Task<string?>> pickAsync = operation switch
        {
            nameof(FilePickerService.OpenDxfAsync) => service.OpenDxfAsync,
            nameof(FilePickerService.OpenRegularVisibilityMaskAsync) => service.OpenRegularVisibilityMaskAsync,
            nameof(FilePickerService.OpenProjectAsync) => service.OpenProjectAsync,
            nameof(FilePickerService.SaveProjectAsync) => service.SaveProjectAsync,
            nameof(FilePickerService.SaveNotchAsync) => () => service.SaveNotchAsync("notch_table", "c"),
            nameof(FilePickerService.SaveExportAsync) => () => service.SaveExportAsync("layer_image", "png"),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

        var actual = await Assert.ThrowsAsync<IOException>(pickAsync);

        Assert.Same(failure, actual);
        Assert.Equal(1, recorder.OpenRequests.Count + recorder.SaveRequests.Count);
    }
}
