// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.Tests;

internal static class LayerImageTestSupport
{
    public static CadPad CreateSquare(int id, string layer, double x, double y, double size)
    {
        var polygon = new Polygon2(new[]
        {
            new Point2(x, y),
            new Point2(x + size, y),
            new Point2(x + size, y + size),
            new Point2(x, y + size),
        });
        return new CadPad(id, string.Create(CultureInfo.InvariantCulture, $"CAD{id}"), layer, polygon);
    }

    public static LayerImageFileEncoding EncodingFor(DxfLayerImageFormat format)
    {
        var request = new DxfLayerImageExportRequest(string.Empty, 2, 2, 1m, 0, 0, format, false);
        return new LayerImageFileEncoding(
            request.FileExtension,
            (bitmap, stream) => DxfLayerImageExportService.EncodeImage(bitmap, stream, format));
    }

    public static void DeleteTempFile(string path) => DeleteBestEffort(() => File.Delete(path));

    public static void DeleteTempDirectory(string path) => DeleteBestEffort(() => Directory.Delete(path, recursive: true));

    private static void DeleteBestEffort(Action delete)
    {
        try
        {
            delete();
        }
        catch (IOException)
        {
            // Temp cleanup is best effort; a locked file is removed with the temp folder later.
        }
    }
}
