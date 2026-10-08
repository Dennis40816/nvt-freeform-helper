using Avalonia;
using Avalonia.Media;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Nvt.Core.Avalonia.Theme;

namespace FreeformHelper.UI.Controls;

/// <summary>
/// This partial class of <see cref="PadCanvas"/> is responsible for managing various
/// caches to optimize rendering performance, especially for geometric data and color brushes.
/// </summary>
public sealed partial class PadCanvas
{
    // --- Geometry Caching ---

    /// <summary>
    /// Ensures that the CAD pad geometries are cached.
    /// This cache stores the Avalonia <see cref="Geometry"/> objects built from <see cref="CadPad"/> polygons,
    /// preventing redundant conversions during rendering.
    /// </summary>
    private void EnsureCadGeometryCache()
    {
        // If the cache is already built, no need to rebuild.
        if (_cadGeometryCache is not null)
        {
            return;
        }

        // If there are no CAD pads, clear the cache and return.
        if (CadPads is not { Count: > 0 })
        {
            _cadGeometryCache = null;
            return;
        }

        // Build the geometry cache: map CadPadId to its corresponding Avalonia Geometry.
        var map = new Dictionary<int, Geometry>(CadPads.Count);
        foreach (var pad in CadPads)
        {
            map[pad.Id] = BuildWorldGeometry(pad.Polygon);
        }
        _cadGeometryCache = map;
    }

    // --- Brush Caching ---

    /// <summary>
    /// Retrieves a <see cref="ISolidColorBrush"/> for a given color from the cache, or creates and caches it if not found.
    /// </summary>
    /// <param name="c">The color for which to get the brush.</param>
    /// <returns>An <see cref="ISolidColorBrush"/> instance.</returns>
    private ISolidColorBrush GetBrush(Color c)
    {
        // Use an integer representation of the color as the cache key.
        var key = ((uint)c.A << 24) | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
        if (_brushCache.TryGetValue(key, out var b))
        {
            return b;
        }

        var brush = new SolidColorBrush(c);
        _brushCache[key] = brush;
        return brush;
    }

    private Color GetResourceColor(string key, Color fallback = default)
    {
        return UiResourceResolver.GetColor(this, key, fallback);
    }

    private IBrush GetResourceBrush(string key, IBrush fallback)
    {
        return UiResourceResolver.GetBrush(this, key, fallback, GetBrush);
    }

    private double GetResourceDouble(string key, double fallback = 0.0)
    {
        return UiResourceResolver.GetDouble(this, key, fallback);
    }

    private byte GetResourceAlpha(string key, double fallback = 0.0)
    {
        var alpha = GetResourceDouble(key, fallback);
        return (byte)Math.Clamp((int)Math.Round(alpha), 0, 255);
    }

    private static byte ApplyOpacity(byte alpha, double opacity)
    {
        return (byte)Math.Clamp((int)Math.Round(alpha * opacity), 0, 255);
    }

    private static byte ScaleAlpha(byte alpha, double scale)
    {
        return (byte)Math.Clamp((int)Math.Round(alpha * Math.Clamp(scale, 0.0, 1.0)), 0, 255);
    }

    // --- Area-Based Color Caching ---

    /// <summary>
    /// Ensures that the cache for area-based CAD pad colors is built.
    /// This cache assigns colors to CAD pads based on their area, grouping pads with similar areas into "buckets."
    /// </summary>
    private void EnsureAreaColorCache()
    {
        // If colorization by area is not enabled, or cache is already built, or no CAD pads, then return.
        if (!ColorCadByArea || _cadAreaColorCache is not null || CadPads is null)
        {
            return;
        }

        // Clamp the area bucket tolerance to a reasonable range (0% to 50% relative difference).
        var tol = Math.Clamp(AreaBucketTolerance, 0.0, 0.5);

        // Sort CAD pads by their area to facilitate bucketing.
        var areas = CadPads.Select(p => (p.Id, a: p.Area)).OrderBy(t => t.a).ToList();

        var buckets = new List<List<int>>(); // Stores lists of CadPad IDs, where each list is a bucket.
        var current = new List<int>(); // Current bucket being filled.
        var currentRef = 0.0; // Reference area for the current bucket.

        // Iterate through sorted pads to group them into area buckets.
        foreach (var (id, a) in areas)
        {
            if (current.Count == 0)
            {
                // Start a new bucket with the first pad.
                currentRef = a;
                current.Add(id);
                continue;
            }

            // Calculate the relative difference between the current pad's area and the bucket's reference area.
            var rel = Math.Abs(a - currentRef) / Math.Max(currentRef, 1e-12);
            if (rel <= tol)
            {
                // If within tolerance, add to the current bucket.
                current.Add(id);
            }
            else
            {
                // Otherwise, finalize the current bucket and start a new one.
                buckets.Add(current);
                current = new List<int> { id };
                currentRef = a;
            }
        }
        // Add the last bucket if it contains any pads.
        if (current.Count > 0) buckets.Add(current);

        // Determine the number of unique colors needed, up to MaxAreaBuckets.
        var maxBuckets = (int)Math.Clamp(MaxAreaBuckets, 1, 1024);
        var colors = BuildPalette(Math.Max(1, Math.Min(buckets.Count, maxBuckets)));

        _cadAreaColorCache = new Dictionary<int, Color>(); // Maps CadPadId to its assigned color.
        _cadAreaBrushCache = new Dictionary<int, ISolidColorBrush>(); // Maps CadPadId to its assigned brush (semi-transparent).

        var areaFillAlpha = GetResourceAlpha("CanvasCadAreaFillAlpha", 0.0);

        // Assign colors and brushes to pads based on their buckets.
        for (var i = 0; i < buckets.Count; i++)
        {
            var c = colors[i % colors.Count]; // Cycle through the palette if there are more buckets than colors.
            foreach (var id in buckets[i])
            {
                _cadAreaColorCache[id] = c;
                _cadAreaBrushCache[id] = GetBrush(Color.FromArgb(areaFillAlpha, c.R, c.G, c.B)); // Create a semi-transparent brush.
            }
        }
    }

    /// <summary>
    /// Generates a palette of distinct colors by varying hue evenly.
    /// </summary>
    /// <param name="n">The number of colors to generate.</param>
    /// <returns>A list of <see cref="Color"/> objects.</returns>
    private List<Color> BuildPalette(int n)
    {
        var palette = new List<Color>(n);
        foreach (var key in CadAreaPaletteKeys)
        {
            if (UiResourceResolver.TryGetColor(this, key, out var color))
            {
                palette.Add(color);
            }
        }

        if (palette.Count == 0)
        {
            return BuildFallbackPalette(n);
        }

        if (palette.Count >= n)
        {
            return TakeSpacedPalette(palette, n);
        }

        return palette;
    }

    private static List<Color> BuildFallbackPalette(int n)
    {
        var colors = new List<Color>(n);
        for (var i = 0; i < n; i++)
        {
            // Evenly distribute hues across the color wheel (0-360 degrees).
            var h = (i * 360.0 / Math.Max(1, n)) % 360.0;
            // Convert HSV to RGB with fixed saturation (0.65) and value (0.95).
            colors.Add(HsvToColor(h, 0.65, 0.95));
        }
        return colors;
    }

    private static List<Color> TakeSpacedPalette(List<Color> palette, int n)
    {
        if (n <= 0)
        {
            return new List<Color>();
        }

        if (n == 1)
        {
            return new List<Color> { palette[0] };
        }

        var lastIndex = palette.Count - 1;
        var step = lastIndex / (double)(n - 1);
        var result = new List<Color>(n);
        for (var i = 0; i < n; i++)
        {
            var index = (int)Math.Round(i * step);
            result.Add(palette[Math.Clamp(index, 0, lastIndex)]);
        }

        return result;
    }

    private static readonly string[] CadAreaPaletteKeys =
    {
        "ColorCadAreaBucket1",
        "ColorCadAreaBucket2",
        "ColorCadAreaBucket3",
        "ColorCadAreaBucket4",
        "ColorCadAreaBucket5",
        "ColorCadAreaBucket6",
        "ColorCadAreaBucket7",
        "ColorCadAreaBucket8",
        "ColorCadAreaBucket9",
        "ColorCadAreaBucket10",
        "ColorCadAreaBucket11",
        "ColorCadAreaBucket12"
    };

    /// <summary>
    /// Converts an HSV color value to an RGB <see cref="Color"/> object.
    /// </summary>
    /// <param name="hDeg">Hue component in degrees (0-360).</param>
    /// <param name="s">Saturation component (0-1).</param>
    /// <param name="v">Value (brightness) component (0-1).</param>
    /// <returns>The corresponding <see cref="Color"/> in RGB format.</returns>
    private static Color HsvToColor(double hDeg, double s, double v)
    {
        // Normalize hue to be within 0-360 degrees.
        hDeg = (hDeg % 360.0 + 360.0) % 360.0;
        var c = v * s; // Chroma
        var x = c * (1 - Math.Abs(((hDeg / 60.0) % 2) - 1));
        var m = v - c; // Match

        double r1, g1, b1; // RGB components before adding 'm'

        // Determine the RGB primary components based on the hue sector.
        if (hDeg < 60) (r1, g1, b1) = (c, x, 0);
        else if (hDeg < 120) (r1, g1, b1) = (x, c, 0);
        else if (hDeg < 180) (r1, g1, b1) = (0, c, x);
        else if (hDeg < 240) (r1, g1, b1) = (0, x, c);
        else if (hDeg < 300) (r1, g1, b1) = (x, 0, c);
        else (r1, g1, b1) = (c, 0, x);

        // Add 'm' to each component and scale to 0-255 range.
        var r = (byte)Math.Clamp((r1 + m) * 255.0, 0, 255);
        var g = (byte)Math.Clamp((g1 + m) * 255.0, 0, 255);
        var b = (byte)Math.Clamp((b1 + m) * 255.0, 0, 255);
        return Color.FromRgb(r, g, b);
    }

    /// <summary>
    /// Converts a <see cref="Polygon2"/> from domain geometry to an Avalonia <see cref="Geometry"/> object.
    /// This is used for rendering purposes.
    /// </summary>
    /// <param name="poly">The <see cref="Polygon2"/> to convert.</param>
    /// <returns>An Avalonia <see cref="Geometry"/> representing the polygon.</returns>
    private static StreamGeometry BuildWorldGeometry(Polygon2 poly)
    {
        var g = new StreamGeometry();
        using var ctx = g.Open(); // Open a context to define the geometry.

        var vs = poly.Vertices;
        if (vs.Length == 0)
        {
            return g; // Return empty geometry if no vertices.
        }

        // Begin a new figure (path) starting at the first vertex.
        ctx.BeginFigure(new Point(vs[0].X, vs[0].Y), true); // 'true' means the figure is filled (closed).

        // Add lines to connect all subsequent vertices.
        for (var i = 1; i < vs.Length; i++)
        {
            ctx.LineTo(new Point(vs[i].X, vs[i].Y));
        }

        // Close the figure (connects the last vertex back to the first).
        ctx.EndFigure(true);
        return g;
    }
}
