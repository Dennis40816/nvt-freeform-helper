// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class MainWindowFilePickerCharacterizationTests
{
    [AvaloniaFact]
    public async Task OpenDxfForwardsRequestAndReturnsNullOnCancelAsync()
    {
        var provider = RecordingFilePickerProvider.Create(out var recorder);
        using var host = new MainWindowFilePickerTestHost(provider);
        var pickAsync = host.ViewModel.PickOpenDxfPathAsync;
        Assert.NotNull(pickAsync);

        var path = await pickAsync();

        Assert.Null(path);
        Assert.Same(provider, host.Window.StorageProvider);
        FilePickerRequestAssertions.Open(recorder, "Open DXF", ("DXF", "*.dxf"), ("All files", "*"));
    }

    [AvaloniaFact]
    public async Task OpenRegularVisibilityMaskForwardsRequestAndReturnsNullOnCancelAsync()
    {
        var provider = RecordingFilePickerProvider.Create(out var recorder);
        using var host = new MainWindowFilePickerTestHost(provider);
        var pickAsync = host.ViewModel.PickOpenRegularVisibilityMaskPathAsync;
        Assert.NotNull(pickAsync);

        var path = await pickAsync();

        Assert.Null(path);
        FilePickerRequestAssertions.Open(recorder, "Import Regular Visibility Mask (SeeRegular.csv)",
            ("CSV", "*.csv"), ("All files", "*"));
    }

    [AvaloniaFact]
    public async Task OpenProjectForwardsRequestAndReturnsNullOnCancelAsync()
    {
        var provider = RecordingFilePickerProvider.Create(out var recorder);
        using var host = new MainWindowFilePickerTestHost(provider);
        var pickAsync = host.ViewModel.PickLoadProjectPathAsync;
        Assert.NotNull(pickAsync);

        var path = await pickAsync();

        Assert.Null(path);
        FilePickerRequestAssertions.Open(recorder, "Open Project",
            ("FreeformHelper Project", "*.json"), ("All files", "*"));
    }

    [AvaloniaFact]
    public async Task SaveProjectForwardsRequestAndReturnsNullOnCancelAsync()
    {
        var provider = RecordingFilePickerProvider.Create(out var recorder);
        using var host = new MainWindowFilePickerTestHost(provider);
        var pickAsync = host.ViewModel.PickSaveProjectPathAsync;
        Assert.NotNull(pickAsync);

        var path = await pickAsync();

        Assert.Null(path);
        FilePickerRequestAssertions.Save(recorder, "Save Project", "json", "freeform_helper_project.json",
            ("FreeformHelper Project", "*.json"), ("All files", "*"));
    }

    [AvaloniaTheory]
    [InlineData("notch_table", ".C", "c", "C source (*.c)", "*.c", "CSV (*.csv)", "*.csv")]
    [InlineData("other_table", "csv", "csv", "CSV (*.csv)", "*.csv", "C source (*.c)", "*.c")]
    public async Task SaveNotchForwardsArgumentsAndReturnsNullOnCancelAsync(
        string baseName, string ext, string expectedExt, string firstName, string firstPattern,
        string secondName, string secondPattern)
    {
        var provider = RecordingFilePickerProvider.Create(out var recorder);
        using var host = new MainWindowFilePickerTestHost(provider);
        var pickAsync = host.ViewModel.PickSaveNotchPathAsync;
        Assert.NotNull(pickAsync);

        var path = await pickAsync(baseName, ext);

        Assert.Null(path);
        FilePickerRequestAssertions.Save(recorder, "Save Notch Table", expectedExt, $"{baseName}.{expectedExt}",
            (firstName, firstPattern), (secondName, secondPattern), ("All files", "*"));
    }

    [AvaloniaTheory]
    [InlineData("layer_image", ".PNG", "png", "Save Image", "PNG image")]
    [InlineData("layer_outline", "dxf", "dxf", "Save DXF", "DXF file")]
    public async Task SaveExportForwardsArgumentsAndReturnsNullOnCancelAsync(
        string baseName, string ext, string expectedExt, string title, string typeName)
    {
        var provider = RecordingFilePickerProvider.Create(out var recorder);
        using var host = new MainWindowFilePickerTestHost(provider);
        var pickAsync = host.ViewModel.PickSaveExportPathAsync;
        Assert.NotNull(pickAsync);

        var path = await pickAsync(baseName, ext);

        Assert.Null(path);
        FilePickerRequestAssertions.Save(recorder, title, expectedExt, $"{baseName}.{expectedExt}",
            (typeName, $"*.{expectedExt}"), ("All files", "*"));
    }

    [AvaloniaFact]
    public async Task ProviderExceptionPropagatesUnchangedAsync()
    {
        var provider = RecordingFilePickerProvider.Create(out var recorder);
        var failure = new IOException("picker failed");
        recorder.Failure = failure;
        using var host = new MainWindowFilePickerTestHost(provider);
        var pickAsync = host.ViewModel.PickOpenDxfPathAsync;
        Assert.NotNull(pickAsync);

        var actual = await Assert.ThrowsAsync<IOException>(pickAsync);

        Assert.Same(failure, actual);
        FilePickerRequestAssertions.Open(recorder, "Open DXF", ("DXF", "*.dxf"), ("All files", "*"));
    }

    [AvaloniaFact]
    public async Task DetachClearsPickerDelegatesAndReattachRestoresThemAsync()
    {
        var provider = RecordingFilePickerProvider.Create(out var recorder);
        using var host = new MainWindowFilePickerTestHost(provider);
        var vm = host.ViewModel;

        host.Window.Content = null;

        Assert.Null(vm.PickOpenDxfPathAsync);
        Assert.Null(vm.PickOpenRegularVisibilityMaskPathAsync);
        Assert.Null(vm.PickLoadProjectPathAsync);
        Assert.Null(vm.PickSaveProjectPathAsync);
        Assert.Null(vm.PickSaveNotchPathAsync);
        Assert.Null(vm.PickSaveExportPathAsync);
        Assert.Null(vm.ConfirmEmbedDxfAsync);
        Assert.Null(vm.ProjectDialogs.ConfirmSaveBeforeLoadAsync);

        host.Window.Content = host.View;

        Assert.NotNull(vm.PickOpenDxfPathAsync);
        Assert.NotNull(vm.PickOpenRegularVisibilityMaskPathAsync);
        Assert.NotNull(vm.PickLoadProjectPathAsync);
        var pickAsync = vm.PickSaveProjectPathAsync;
        Assert.NotNull(pickAsync);
        Assert.NotNull(vm.PickSaveNotchPathAsync);
        Assert.NotNull(vm.PickSaveExportPathAsync);
        Assert.NotNull(vm.ConfirmEmbedDxfAsync);
        Assert.NotNull(vm.ProjectDialogs.ConfirmSaveBeforeLoadAsync);
        Assert.Null(await pickAsync());
        FilePickerRequestAssertions.Save(recorder, "Save Project", "json", "freeform_helper_project.json",
            ("FreeformHelper Project", "*.json"), ("All files", "*"));
    }
}
