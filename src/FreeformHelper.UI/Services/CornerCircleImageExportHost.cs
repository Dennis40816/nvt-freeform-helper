// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Asks the user for a file name and saves a corner circle image. The file has exactly the pixel size of the image.
/// The view decides which message to show for each result.
/// </summary>
internal static class CornerCircleImageExportHost
{
    public static async Task<TextExportOutcome> SaveAsync(TopLevel? topLevel, CornerCircleImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var storage = topLevel?.StorageProvider;
        if (storage is null)
        {
            return TextExportOutcome.Unavailable;
        }

        var file = await storage.SaveFilePickerAsync(
            CoordinatePlannerFilePickerOptions.SaveCornerCircleImage(image.Width, image.Height));
        if (file is null)
        {
            return TextExportOutcome.Cancelled;
        }

        await CornerCircleImageEncoder.SaveToFileAsync(image, file.Path.LocalPath);
        return TextExportOutcome.Saved;
    }
}
