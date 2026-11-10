// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.Application.Services;

/// <summary>A gray image, one byte per pixel, row by row from the top. 0 is black and 255 is white.</summary>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
/// <param name="Pixels">The pixel bytes. The length is <c>Width * Height</c>.</param>
public sealed record CornerCircleImage(int Width, int Height, byte[] Pixels);

/// <summary>
/// Draws the four corner circles of a <see cref="CornerCircleLayoutResult"/> into an image that has exactly the
/// pixel size of the active area. One image pixel is one active-area pixel.
/// </summary>
public static class CornerCircleImageRenderer
{
    private const int MaxSide = 32768;
    private const long MaxPixels = 1L << 28;
    private const byte White = 255;

    /// <summary>Returns true when <see cref="Render"/> accepts an image of this size.</summary>
    public static bool IsSupportedSize(int width, int height)
    {
        return width >= 1 && height >= 1 && width <= MaxSide && height <= MaxSide && (long)width * height <= MaxPixels;
    }

    /// <summary>
    /// Draws white circles on a black background without anti-aliasing, so the image has only two colors.
    /// </summary>
    /// <remarks>
    /// Coordinates are continuous. The origin is the top-left corner of the top-left pixel. Pixel (i, j) covers
    /// [i, i+1) x [j, j+1) and its center is (i+0.5, j+0.5). A pixel is white when its center is no farther from a
    /// circle center than the radius. A radius of 0 therefore draws nothing. With <paramref name="drawCenterRectangle"/>
    /// the rectangle through the four centers is drawn one pixel wide. Each edge covers the pixel row or column that
    /// contains the edge coordinate, clamped to the image.
    /// </remarks>
    /// <exception cref="ArgumentException">The layout status is not <see cref="CornerCircleLayoutStatus.Ok"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The size is below 1 or above the limit.</exception>
    public static CornerCircleImage Render(int width, int height, CornerCircleLayoutResult layout, bool drawCenterRectangle = false)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (layout.Status != CornerCircleLayoutStatus.Ok)
        {
            throw new ArgumentException("The layout must have the status Ok.", nameof(layout));
        }

        if (!IsSupportedSize(width, height))
        {
            throw new ArgumentOutOfRangeException(nameof(width), "The image size is outside the supported range.");
        }

        var pixels = new byte[width * height];
        foreach (var center in layout.Centers)
        {
            FillCircle(pixels, width, height, center.X, center.Y, layout.Radius);
        }

        if (drawCenterRectangle)
        {
            DrawRectangle(pixels, width, height, layout.CenterRectangle);
        }

        return new CornerCircleImage(width, height, pixels);
    }

    private static void FillCircle(byte[] pixels, int width, int height, double centerX, double centerY, double radius)
    {
        var minX = Math.Max(0, (int)Math.Floor(centerX - radius));
        var maxX = Math.Min(width - 1, (int)Math.Ceiling(centerX + radius));
        var minY = Math.Max(0, (int)Math.Floor(centerY - radius));
        var maxY = Math.Min(height - 1, (int)Math.Ceiling(centerY + radius));
        var limit = (radius * radius) + 1e-9;
        for (var y = minY; y <= maxY; y++)
        {
            var dy = y + 0.5 - centerY;
            for (var x = minX; x <= maxX; x++)
            {
                var dx = x + 0.5 - centerX;
                if ((dx * dx) + (dy * dy) <= limit)
                {
                    pixels[(y * width) + x] = White;
                }
            }
        }
    }

    private static void DrawRectangle(byte[] pixels, int width, int height, FreeformHelper.Domain.Geometry.Rect2 rectangle)
    {
        var left = Math.Clamp((int)Math.Floor(rectangle.MinX), 0, width - 1);
        var right = Math.Clamp((int)Math.Floor(rectangle.MaxX), 0, width - 1);
        var top = Math.Clamp((int)Math.Floor(rectangle.MinY), 0, height - 1);
        var bottom = Math.Clamp((int)Math.Floor(rectangle.MaxY), 0, height - 1);
        for (var x = left; x <= right; x++)
        {
            pixels[(top * width) + x] = White;
            pixels[(bottom * width) + x] = White;
        }

        for (var y = top; y <= bottom; y++)
        {
            pixels[(y * width) + left] = White;
            pixels[(y * width) + right] = White;
        }
    }
}
