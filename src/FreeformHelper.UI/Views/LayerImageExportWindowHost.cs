// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using FreeformHelper.UI.Logging;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Nvt.Core.Threading;

namespace FreeformHelper.UI.Views;

/// <summary>Opens the layer image export windows and supplies the file writer to the export flow.</summary>
internal static class LayerImageExportWindowHost
{
    internal static async Task<LayerImageExportRequest?> ShowAsync(Window owner, LayerImageExportContext context)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(context);
        context.WriteLayerImagesAsync = WriteAsync;
        var previewContext = context with { RenderPngAsync = LayerImagePreviewService.RenderPngAsync };
        using var viewModel = new LayerImageExportViewModel(previewContext);
        var window = new LayerImageExportWindow { DataContext = viewModel };
        var events = new UiEventRunner(EmergencyLogSink.Report, EmergencyLogSink.Report);
        void Close(bool confirmed) => window.Close(confirmed);
        void OpenSettings() => events.Run("LayerImageExportSettings", _ =>
            ShowSettingsAsync(window, viewModel, previewContext));
        viewModel.RequestClose += Close;
        viewModel.RequestExportSettings += OpenSettings;
        try
        {
            bool confirmed = await window.ShowDialog<bool>(owner);
            return confirmed ? viewModel.CompletedRequest : null;
        }
        finally
        {
            viewModel.RequestClose -= Close;
            viewModel.RequestExportSettings -= OpenSettings;
        }
    }

    internal static Task<IReadOnlyList<string>> WriteAsync(LayerImageWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var format = new DxfLayerImageExportRequest { Format = request.Format };
        var encoding = new LayerImageFileEncoding(format.FileExtension,
            (bitmap, stream) => DxfLayerImageExportService.EncodeImage(bitmap, stream, request.Format));
        return Task.Run(() => LayerImageFileWriter.Write(request.Jobs, request.PadsOfLayer, request.Size,
            request.LineWidthPixels, request.UseDarkTheme, encoding, request.ChosenPath));
    }

    private static async Task ShowSettingsAsync(
        Window owner, LayerImageExportViewModel viewModel, LayerImageExportContext context)
    {
        var settings = new LayerImageExportSettingsViewModel(viewModel, context.PickSavePathAsync);
        var dialog = new LayerImageExportSettingsDialog { DataContext = settings };
        void Close(bool saved) => dialog.Close(saved);
        settings.RequestClose += Close;
        try
        {
            bool saved = await dialog.ShowDialog<bool>(owner);
            if (saved && settings.Request is { } request)
            {
                viewModel.CompleteExport(request);
            }
        }
        finally
        {
            settings.RequestClose -= Close;
        }
    }
}
