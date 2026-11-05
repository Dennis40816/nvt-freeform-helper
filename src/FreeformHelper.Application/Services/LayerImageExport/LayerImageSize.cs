// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.Application.Services;

/// <summary>Owns the immutable drawing dimensions, padding and original sizing defaults.</summary>
public sealed record LayerImageSize
{
    /// <summary>The longer drawing side when project pixels are unavailable.</summary>
    public const int AutoLongSidePixels = 4096;

    /// <summary>The smallest drawing dimension.</summary>
    public const int MinPixels = 2;

    /// <summary>The largest drawing dimension.</summary>
    public const int MaxPixels = 100_000;

    /// <summary>The largest padding on each side of an axis.</summary>
    public const int MaxPadding = 100_000;

    private readonly (int PixelsX, int PixelsY, LayerImageSizeSource Source) _defaults;

    private LayerImageSize(
        (int PixelsX, int PixelsY, LayerImageSizeSource Source) defaults,
        int pixelsX,
        int pixelsY,
        int paddingX,
        int paddingY,
        bool keepRatio,
        LayerImageSizeSource source)
    {
        _defaults = defaults;
        PixelsX = pixelsX;
        PixelsY = pixelsY;
        PaddingX = paddingX;
        PaddingY = paddingY;
        KeepRatio = keepRatio;
        Source = source;
    }

    /// <summary>Gets the drawing width, excluding padding.</summary>
    public int PixelsX { get; }

    /// <summary>Gets the drawing height, excluding padding.</summary>
    public int PixelsY { get; }

    /// <summary>Gets the padding on each horizontal side.</summary>
    public int PaddingX { get; }

    /// <summary>Gets the padding on each vertical side.</summary>
    public int PaddingY { get; }

    /// <summary>Gets whether pixel edits preserve the original pixel pair's ratio, subject to the limits.</summary>
    public bool KeepRatio { get; }

    /// <summary>Gets the origin of the current dimensions.</summary>
    public LayerImageSizeSource Source { get; }

    /// <summary>Gets the full output width, including both padding sides.</summary>
    public int OutputWidth => (int)(PixelsX + (2L * PaddingX));

    /// <summary>Gets the full output height, including both padding sides.</summary>
    public int OutputHeight => (int)(PixelsY + (2L * PaddingY));

    private double DefaultRatio => (double)_defaults.PixelsY / _defaults.PixelsX;

    /// <summary>Creates unpadded defaults from a complete valid project pair, otherwise from the drawing bounds.</summary>
    /// <remarks>Invalid bounds produce a square. Pixel rounding uses midpoint-to-even rounding.</remarks>
    public static LayerImageSize CreateDefault(
        double boundsWidth,
        double boundsHeight,
        int? projectPixelsX,
        int? projectPixelsY)
    {
        int x = AutoLongSidePixels;
        int y = AutoLongSidePixels;
        var source = LayerImageSizeSource.Auto;
        if (projectPixelsX is >= MinPixels and <= MaxPixels && projectPixelsY is >= MinPixels and <= MaxPixels)
        {
            x = projectPixelsX.Value;
            y = projectPixelsY.Value;
            source = LayerImageSizeSource.Project;
        }
        else if (double.IsFinite(boundsWidth) && boundsWidth > 0 && double.IsFinite(boundsHeight) && boundsHeight > 0)
        {
            if (boundsWidth >= boundsHeight)
            {
                y = RoundPixels((boundsHeight / boundsWidth) * AutoLongSidePixels);
            }
            else
            {
                x = RoundPixels((boundsWidth / boundsHeight) * AutoLongSidePixels);
            }
        }

        return new LayerImageSize((x, y, source), x, y, 0, 0, false, source);
    }

    /// <summary>Clamps and edits X, deriving Y from the default ratio when enabled.</summary>
    public LayerImageSize WithPixelsX(int pixelsX)
    {
        int x = Math.Clamp(pixelsX, MinPixels, MaxPixels);
        int y = KeepRatio ? RoundPixels(x * DefaultRatio) : PixelsY;
        return new LayerImageSize(_defaults, x, y, PaddingX, PaddingY, KeepRatio, LayerImageSizeSource.Custom);
    }

    /// <summary>Clamps and edits Y, deriving X from the default ratio when enabled.</summary>
    public LayerImageSize WithPixelsY(int pixelsY)
    {
        int y = Math.Clamp(pixelsY, MinPixels, MaxPixels);
        int x = KeepRatio ? RoundPixels(y / DefaultRatio) : PixelsX;
        return new LayerImageSize(_defaults, x, y, PaddingX, PaddingY, KeepRatio, LayerImageSizeSource.Custom);
    }

    /// <summary>Clamps and edits the horizontal padding.</summary>
    public LayerImageSize WithPaddingX(int padding) => new(
        _defaults, PixelsX, PixelsY, Math.Clamp(padding, 0, MaxPadding), PaddingY, KeepRatio, LayerImageSizeSource.Custom);

    /// <summary>Clamps and edits the vertical padding.</summary>
    public LayerImageSize WithPaddingY(int padding) => new(
        _defaults, PixelsX, PixelsY, PaddingX, Math.Clamp(padding, 0, MaxPadding), KeepRatio, LayerImageSizeSource.Custom);

    /// <summary>Changes ratio locking without changing the dimensions or their source.</summary>
    public LayerImageSize WithKeepRatio(bool keepRatio) => new(
        _defaults, PixelsX, PixelsY, PaddingX, PaddingY, keepRatio, Source);

    /// <summary>Restores the original pixels and source, clears padding, and preserves the current ratio switch.</summary>
    public LayerImageSize Reset() => new(
        _defaults, _defaults.PixelsX, _defaults.PixelsY, 0, 0, KeepRatio, _defaults.Source);

    private static int RoundPixels(double pixels) => (int)Math.Clamp(Math.Round(pixels), MinPixels, MaxPixels);
}
