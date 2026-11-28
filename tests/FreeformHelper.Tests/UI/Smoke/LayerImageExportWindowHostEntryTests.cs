// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.CadExport;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class LayerImageExportWindowHostEntryTests
{
    private const string SavePath = "layer-host-export.png";

    [AvaloniaFact]
    public async Task ShowAsync_ConfirmReturnsCompletedRequestAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var owner = new Window();
        try
        {
            owner.Show();
            var resultTask = LayerImageExportWindowHost.ShowAsync(owner, CreateContext());
            await HeadlessUiPump.FlushQueueAsync();
            var window = GetExportWindow(owner);
            var viewModel = Assert.IsType<LayerImageExportViewModel>(window.DataContext);
            var request = viewModel.BuildRequest(viewModel.FileSettings) with { SavePath = SavePath };
            Assert.False(resultTask.IsCompleted);

            viewModel.CompleteExport(request);

            var result = await resultTask.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            Assert.NotNull(result);
            Assert.Same(request, result);
            Assert.Same(viewModel.CompletedRequest, result);
            Assert.False(window.IsVisible);
        }
        finally
        {
            CloseWindows(owner);
            await HeadlessUiPump.FlushQueueAsync();
        }
    }

    [AvaloniaFact]
    public async Task ShowAsync_CancelReturnsNullAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var owner = new Window();
        try
        {
            owner.Show();
            var resultTask = LayerImageExportWindowHost.ShowAsync(owner, CreateContext());
            await HeadlessUiPump.FlushQueueAsync();
            var window = GetExportWindow(owner);
            var viewModel = Assert.IsType<LayerImageExportViewModel>(window.DataContext);
            Assert.False(resultTask.IsCompleted);

            viewModel.CancelCommand.Execute(null);

            var result = await resultTask.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            Assert.Null(result);
            Assert.Null(viewModel.CompletedRequest);
            Assert.False(window.IsVisible);
        }
        finally
        {
            CloseWindows(owner);
            await HeadlessUiPump.FlushQueueAsync();
        }
    }

    [AvaloniaFact]
    public async Task ShowAsync_SavingSettingsCompletesExportWithSavedRequestAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var owner = new Window();
        try
        {
            owner.Show();
            var resultTask = LayerImageExportWindowHost.ShowAsync(owner, CreateContext());
            await HeadlessUiPump.FlushQueueAsync();
            var window = GetExportWindow(owner);
            var viewModel = Assert.IsType<LayerImageExportViewModel>(window.DataContext);
            viewModel.ToggleAllLayersCommand.Execute(null);
            viewModel.VisibleRows[1].IsChecked = true;
            viewModel.FileSettings.OneFilePerLayer = true;
            var dialog = await OpenSettingsAsync(window, viewModel);
            var settings = Assert.IsType<LayerImageExportSettingsViewModel>(dialog.DataContext);
            Assert.False(resultTask.IsCompleted);

            await settings.SaveCommand.ExecuteAsync(null)
                .WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            await HeadlessUiPump.FlushQueueAsync();

            var result = await resultTask.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            Assert.NotNull(result);
            Assert.Same(settings.Request, result);
            Assert.Same(viewModel.CompletedRequest, result);
            Assert.Equal(SavePath, result.SavePath);
            Assert.Equal("nc.pads", Assert.Single(result.Layers));
            Assert.True(result.OneFilePerLayer);
            Assert.False(dialog.IsVisible);
            Assert.False(window.IsVisible);
        }
        finally
        {
            CloseWindows(owner);
            await HeadlessUiPump.FlushQueueAsync();
        }
    }

    [AvaloniaFact]
    public async Task ShowAsync_CancellingSettingsKeepsExportOpenAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var owner = new Window();
        try
        {
            owner.Show();
            var resultTask = LayerImageExportWindowHost.ShowAsync(owner, CreateContext());
            await HeadlessUiPump.FlushQueueAsync();
            var window = GetExportWindow(owner);
            var viewModel = Assert.IsType<LayerImageExportViewModel>(window.DataContext);
            var dialog = await OpenSettingsAsync(window, viewModel);
            var settings = Assert.IsType<LayerImageExportSettingsViewModel>(dialog.DataContext);

            settings.BackCommand.Execute(null);
            await HeadlessUiPump.FlushQueueAsync();

            Assert.False(dialog.IsVisible);
            Assert.Empty(window.OwnedWindows);
            Assert.True(window.IsVisible);
            Assert.True(window.IsEnabled);
            Assert.Same(window, GetExportWindow(owner));
            Assert.False(resultTask.IsCompleted);
            Assert.Null(settings.Request);
            Assert.Null(viewModel.CompletedRequest);

            viewModel.CancelCommand.Execute(null);
            Assert.Null(await resultTask.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
        }
        finally
        {
            CloseWindows(owner);
            await HeadlessUiPump.FlushQueueAsync();
        }
    }

    private static LayerImageExportContext CreateContext() => LayerImageExportTestData.CreateContext(32, 24) with
    {
        PickSavePathAsync = static (_, _) => Task.FromResult<string?>(SavePath),
        WriteLayerImagesAsync = static _ => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>()),
    };

    private static LayerImageExportWindow GetExportWindow(Window owner) =>
        Assert.Single(owner.OwnedWindows.OfType<LayerImageExportWindow>());

    private static async Task<LayerImageExportSettingsDialog> OpenSettingsAsync(
        LayerImageExportWindow window, LayerImageExportViewModel viewModel)
    {
        viewModel.ConfirmCommand.Execute(null);
        await HeadlessUiPump.FlushQueueAsync();
        var dialog = Assert.Single(window.OwnedWindows.OfType<LayerImageExportSettingsDialog>());
        Assert.True(dialog.IsVisible);
        return dialog;
    }

    private static void CloseWindows(Window window)
    {
        foreach (var ownedWindow in window.OwnedWindows.ToArray())
        {
            CloseWindows(ownedWindow);
        }

        window.Close();
    }
}
