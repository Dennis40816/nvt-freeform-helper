using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using SkiaSharp;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DxfLayerImageExportServiceTests
{
    [Fact]
    public void ExportLayerImage_WritesExpectedResolution()
    {
        _ = new DxfLayerImageExportService();
        var layerPads = new[]
        {
            CreateSquareCad(1, "L_TOP", 0, 0, 10),
            CreateSquareCad(2, "L_TOP", 20, 10, 15),
        };

        var path = TestFiles.NewTempFilePath("dxf-layer-", ".png");
        ExportLayerImage(
            layerPads,
            320,
            240,
            lineWidthPixels: 1m,
            paddingXPixels: 4,
            paddingYPixels: 4,
            DxfLayerImageFormat.Png,
            useDarkTheme: false,
            path);

        Assert.True(File.Exists(path));
        using (var bitmap = SKBitmap.Decode(path))
        {
            Assert.NotNull(bitmap);
            Assert.Equal(328, bitmap.Width);
            Assert.Equal(248, bitmap.Height);
            Assert.Equal(SKColors.White, bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2));
        }
    }

    [Fact]
    public void ExportLayerImage_EmptyPads_Throws()
    {
        var service = new DxfLayerImageExportService();
        var path = TestFiles.NewTempFilePath("dxf-layer-", ".png");

        Assert.Throws<InvalidOperationException>(() =>
            ExportLayerImage(
                Array.Empty<CadPad>(),
                320,
                240,
                lineWidthPixels: 1m,
                paddingXPixels: 4,
                paddingYPixels: 4,
                DxfLayerImageFormat.Png,
                useDarkTheme: false,
                path));
    }

    [Fact]
    public void ExportLayerImage_LeavesOuterPadding()
    {
        _ = new DxfLayerImageExportService();
        var layerPads = new[]
        {
            CreateSquareCad(1, "L_EDGE", 0, 0, 100),
        };

        var path = TestFiles.NewTempFilePath("dxf-layer-", ".png");
        ExportLayerImage(
            layerPads,
            300,
            300,
            lineWidthPixels: 1m,
            paddingXPixels: 40,
            paddingYPixels: 30,
            DxfLayerImageFormat.Png,
            useDarkTheme: false,
            path);

        using (var bitmap = SKBitmap.Decode(path))
        {
            Assert.NotNull(bitmap);
            Assert.Equal(SKColors.White, bitmap.GetPixel(0, 0));
            Assert.Equal(SKColors.White, bitmap.GetPixel(bitmap.Width - 1, bitmap.Height - 1));
            Assert.NotEqual(SKColors.White, bitmap.GetPixel(41, 31));
        }
    }

    [Fact]
    public void ExportLayerImage_ThickerLineProducesMoreInkPixels()
    {
        _ = new DxfLayerImageExportService();
        var layerPads = new[]
        {
            CreateSquareCad(1, "L_EDGE", 0, 0, 100),
        };

        var thinPath = TestFiles.NewTempFilePath("dxf-layer-thin-", ".png");
        var thickPath = TestFiles.NewTempFilePath("dxf-layer-thick-", ".png");
        ExportLayerImage(
            layerPads,
            320,
            320,
            lineWidthPixels: 1m,
            paddingXPixels: 12,
            paddingYPixels: 12,
            DxfLayerImageFormat.Png,
            useDarkTheme: false,
            thinPath);

        ExportLayerImage(
            layerPads,
            320,
            320,
            lineWidthPixels: 8m,
            paddingXPixels: 12,
            paddingYPixels: 12,
            DxfLayerImageFormat.Png,
            useDarkTheme: false,
            thickPath);

        using (var thinBitmap = SKBitmap.Decode(thinPath))
        using (var thickBitmap = SKBitmap.Decode(thickPath))
        {
            Assert.NotNull(thinBitmap);
            Assert.NotNull(thickBitmap);

            var thinInk = CountNonWhitePixels(thinBitmap);
            var thickInk = CountNonWhitePixels(thickBitmap);
            Assert.True(thickInk > thinInk, $"Expected thick line pixels > thin line pixels, got {thickInk} <= {thinInk}.");
        }
    }

    [Fact]
    public void ExportLayerImage_ZeroPaddingStillKeepsWideStrokeInsideCanvas()
    {
        _ = new DxfLayerImageExportService();
        var layerPads = new[]
        {
            CreateSquareCad(1, "L_EDGE", 0, 0, 100),
        };

        var path = TestFiles.NewTempFilePath("dxf-layer-safe-inset-", ".png");
        ExportLayerImage(
            layerPads,
            320,
            320,
            lineWidthPixels: 14m,
            paddingXPixels: 0,
            paddingYPixels: 0,
            DxfLayerImageFormat.Png,
            useDarkTheme: false,
            path);

        using (var bitmap = SKBitmap.Decode(path))
        {
            Assert.NotNull(bitmap);
            Assert.Equal(SKColors.White, bitmap.GetPixel(0, 0));
            Assert.Equal(SKColors.White, bitmap.GetPixel(bitmap.Width - 1, 0));
            Assert.Equal(SKColors.White, bitmap.GetPixel(0, bitmap.Height - 1));
            Assert.Equal(SKColors.White, bitmap.GetPixel(bitmap.Width - 1, bitmap.Height - 1));
        }
    }

    // Renders one layer and writes it the way the export window does: LayerImageRenderer, then EncodeImage.
    private static void ExportLayerImage(
        CadPad[] pads,
        int widthPixels,
        int heightPixels,
        decimal lineWidthPixels,
        int paddingXPixels,
        int paddingYPixels,
        DxfLayerImageFormat format,
        bool useDarkTheme,
        string outputPath)
    {
        var layer = new LayerImageLayer(pads.Length == 0 ? string.Empty : pads[0].Layer, pads);
        var request = new LayerImageRenderRequest(
            [layer], widthPixels, heightPixels, paddingXPixels, paddingYPixels, lineWidthPixels, useDarkTheme);
        using var bitmap = LayerImageRenderer.Render(request);
        using var stream = File.Create(outputPath);
        DxfLayerImageExportService.EncodeImage(bitmap, stream, format);
    }

    private static CadPad CreateSquareCad(int id, string layer, double x, double y, double size)
    {
        var polygon = new Polygon2(new[]
        {
            new Point2(x, y),
            new Point2(x + size, y),
            new Point2(x + size, y + size),
            new Point2(x, y + size),
        });

        return new CadPad(id, $"CAD{id}", layer, polygon);
    }

    private static int CountNonWhitePixels(SKBitmap bitmap)
    {
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y) != SKColors.White)
                {
                    count++;
                }
            }
        }

        return count;
    }
}
