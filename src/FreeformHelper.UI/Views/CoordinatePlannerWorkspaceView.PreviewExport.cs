// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views;

public sealed partial class CoordinatePlannerWorkspaceView
{
    private PixelSize GetPreviewPixelSize()
    {
        var bounds = _overlayHost.FindPreviewHost().Bounds;
        return new PixelSize(Math.Max(2, (int)Math.Ceiling(bounds.Width)), Math.Max(2, (int)Math.Ceiling(bounds.Height)));
    }

    private void PreviewImageMenu_Opened(object? sender, EventArgs e)
    {
        var size = GetPreviewPixelSize();
        PreviewImageDescription.Header = new CoordinatePreviewImageInfo(size.Width, size.Height).SummaryText;
    }

    private async Task<bool> ExportPreviewImageAsync()
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null)
        {
            return false;
        }

        var file = await storage.SaveFilePickerAsync(CoordinatePlannerFilePickerOptions.SavePreviewImage());
        if (file is null)
        {
            return false;
        }

        var previewHost = _overlayHost.FindPreviewHost();
        if (previewHost.Bounds.Width <= 1 || previewHost.Bounds.Height <= 1)
        {
            return false;
        }

        await Dispatcher.InvokeAsync(_overlayHost.Render, DispatcherPriority.Render);
        var pixelSize = GetPreviewPixelSize();
        using var bitmap = new RenderTargetBitmap(pixelSize);
        bitmap.Render(previewHost);
        await PreviewImageWriter.SaveToFileAsync(bitmap, file.Path.LocalPath);
        return true;
    }

}
