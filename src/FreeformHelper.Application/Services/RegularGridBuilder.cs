using System.Diagnostics.CodeAnalysis;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

/// <summary>
/// Constructs a <see cref="RegularGrid"/> based on a variety of settings,
/// allowing for uniform or non-uniform cell sizes.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1822:Mark members as static",
    Justification = "Instance API is intentionally preserved for service call-site stability.")]
public sealed class RegularGridBuilder
{
    /// <summary>
    /// Builds a regular grid that fits the bounding box of a given CAD pad set.
    /// </summary>
    /// <param name="cad">The CAD pad set to derive the grid bounds from.</param>
    /// <param name="settings">The settings controlling the grid's dimensions and properties.</param>
    /// <returns>A new <see cref="RegularGrid"/>.</returns>
    public RegularGrid BuildFromCadBounds(CadPadSet cad, GridSettings settings)
    {
        settings.ValidateOrThrow();

        // Start with the CAD bounds and add optional padding.
        var bounds = cad.Bounds;
        var padX = Math.Max(bounds.Width * settings.BoundsPaddingRatio, 0);
        var padY = Math.Max(bounds.Height * settings.BoundsPaddingRatio, 0);
        bounds = bounds.Inflate(padX, padY);

        return BuildGridInternal(
            rows: settings.YChannels,
            cols: settings.XChannels,
            startX: bounds.MinX,
            startY: bounds.MinY,
            totalWidth: bounds.Width,
            totalHeight: bounds.Height,
            settings: settings);
    }

    /// <summary>
    /// Builds a regular grid based entirely on explicit size and position settings, without CAD data.
    /// </summary>
    /// <param name="settings">The settings defining the grid's dimensions and active area.</param>
    /// <returns>A new <see cref="RegularGrid"/>.</returns>
    public RegularGrid BuildFromSettings(GridSettings settings)
    {
        return BuildFromSettings(settings, settings.AlignmentMode);
    }

    /// <summary>
    /// Builds a regular grid based entirely on explicit size and position settings, without CAD data.
    /// </summary>
    /// <param name="settings">The settings defining the grid's dimensions and active area.</param>
    /// <param name="alignmentMode">The alignment mode to apply for this build request.</param>
    /// <returns>A new <see cref="RegularGrid"/>.</returns>
    public RegularGrid BuildFromSettings(GridSettings settings, GridAlignmentMode alignmentMode)
    {
        settings.ValidateOrThrow();

        if (settings.ActiveAreaWidth <= 0 || settings.ActiveAreaHeight <= 0)
        {
            throw new InvalidOperationException("Active area size must be > 0 to build grid without CAD.");
        }

        // Determine the starting corner of the grid based on alignment settings.
        var startX = alignmentMode == GridAlignmentMode.FromPanelAa ? settings.PanelBiasX : 0.0;
        var startY = alignmentMode == GridAlignmentMode.FromPanelAa ? settings.PanelBiasY : 0.0;

        return BuildGridInternal(
            rows: settings.YChannels,
            cols: settings.XChannels,
            startX: startX,
            startY: startY,
            totalWidth: settings.ActiveAreaWidth,
            totalHeight: settings.ActiveAreaHeight,
            settings: settings);
    }

    /// <summary>
    /// The core logic for constructing the grid, pads, and their geometry.
    /// </summary>
    private static RegularGrid BuildGridInternal(
        int rows,
        int cols,
        double startX,
        double startY,
        double totalWidth,
        double totalHeight,
        GridSettings settings)
    {
        var perRowXEdges = new List<IReadOnlyList<double>>(rows);
        var perColYEdges = new List<IReadOnlyList<double>>(cols);

        // Determine if there's a global (shared) sizing for all columns or rows.
        var globalX = settings.WidthScope == WidthAdjustmentScope.ColumnGlobal
            ? BuildEdges(startX, NormalizeOrUniform(settings.ColumnWidths, cols, totalWidth))
            : null;
        var globalY = settings.HeightScope == HeightAdjustmentScope.RowGlobal
            ? BuildEdges(startY, NormalizeOrUniform(settings.RowHeights, rows, totalHeight))
            : null;

        // Calculate the X-coordinates of the vertical edges for each row.
        for (var r = 0; r < rows; r++)
        {
            // If globalX is set, use it. Otherwise, calculate widths for this specific row.
            var edges = globalX ?? BuildEdges(startX, NormalizeOrUniform(GetRowWidths(settings, r, cols), cols, totalWidth));
            perRowXEdges.Add(edges);
        }

        // Calculate the Y-coordinates of the horizontal edges for each column.
        for (var c = 0; c < cols; c++)
        {
            // If globalY is set, use it. Otherwise, calculate heights for this specific column.
            var edges = globalY ?? BuildEdges(startY, NormalizeOrUniform(GetColumnHeights(settings, c, rows), rows, totalHeight));
            perColYEdges.Add(edges);
        }

        // Create the individual regular pads using the calculated edge positions.
        var pads = new List<RegularPad>(rows * cols);
        for (var r = 0; r < rows; r++)
        {
            var xEdges = perRowXEdges[r];
            for (var c = 0; c < cols; c++)
            {
                var yEdges = perColYEdges[c];
                var x0 = xEdges[c];
                var x1 = xEdges[c + 1];
                var y0 = yEdges[r];
                var y1 = yEdges[r + 1];

                var poly = new Polygon2(new[]
                {
                    new Point2(x0, y0), new Point2(x1, y0),
                    new Point2(x1, y1), new Point2(x0, y1),
                });

                pads.Add(new RegularPad(r, c, r * cols + c, poly));
            }
        }

        var firstRowEdges = perRowXEdges.Count > 0 ? perRowXEdges[0].ToList() : new List<double>();
        var firstColEdges = perColYEdges.Count > 0 ? perColYEdges[0].ToList() : new List<double>();
        return new RegularGrid(rows, cols, firstRowEdges, firstColEdges, pads);
    }

    /// <summary>
    /// Takes a list of desired sizes (widths or heights), fills in any missing values, and scales them to fit the total dimension.
    /// </summary>
    private static List<double> NormalizeOrUniform(List<double> values, int count, double total)
    {
        if (count <= 0) return new List<double>();

        var uniform = total / count;
        // If no specific sizes are provided, create a uniform distribution.
        if (values.Count != count)
        {
            return Enumerable.Repeat(uniform, count).ToList();
        }

        // Allow partial manual sizing:
        // - A value <= 0 is considered "unset".
        // - Unset values are filled with the average of the set (positive) values.
        var positives = values.Where(v => v > 0).ToList();
        if (positives.Count == 0)
        {
            return Enumerable.Repeat(uniform, count).ToList();
        }

        var fill = positives.Average();
        var filled = values.Select(v => v > 0 ? v : fill).ToList();

        // Scale the final list of sizes so their sum equals the total dimension.
        var sum = filled.Sum();
        if (sum <= 0)
        {
            return Enumerable.Repeat(uniform, count).ToList();
        }

        var scale = total / sum;
        return filled.Select(v => v * scale).ToList();
    }

    /// <summary>
    /// Gets the effective column widths for a specific row, applying any row-specific overrides.
    /// </summary>
    private static List<double> GetRowWidths(GridSettings settings, int row, int cols)
    {
        var baseWidths = Resize(settings.ColumnWidths, cols, 0.0);
        if (settings.RowWidthOverrides.Count > row)
        {
            var overrides = Resize(settings.RowWidthOverrides[row], cols, 0.0);
            var flags = settings.RowWidthOverrideFlags.Count > row
                ? Resize(settings.RowWidthOverrideFlags[row], cols, false)
                : Enumerable.Repeat(false, cols).ToList();

            for (var c = 0; c < cols; c++)
            {
                // An override is applied if its flag is set or if it's a positive value.
                if ((flags[c] || overrides[c] > 0) && overrides[c] > 0)
                {
                    baseWidths[c] = overrides[c];
                }
            }
        }

        return baseWidths;
    }

    /// <summary>
    /// Gets the effective row heights for a specific column, applying any column-specific overrides.
    /// </summary>
    private static List<double> GetColumnHeights(GridSettings settings, int col, int rows)
    {
        var baseHeights = Resize(settings.RowHeights, rows, 0.0);
        if (settings.ColumnHeightOverrides.Count > col)
        {
            var overrides = Resize(settings.ColumnHeightOverrides[col], rows, 0.0);
            var flags = settings.ColumnHeightOverrideFlags.Count > col
                ? Resize(settings.ColumnHeightOverrideFlags[col], rows, false)
                : Enumerable.Repeat(false, rows).ToList();

            for (var r = 0; r < rows; r++)
            {
                // An override is applied if its flag is set or if it's a positive value.
                if ((flags[r] || overrides[r] > 0) && overrides[r] > 0)
                {
                    baseHeights[r] = overrides[r];
                }
            }
        }

        return baseHeights;
    }

    /// <summary>
    /// Creates a list of edge coordinates from a starting point and a list of segment sizes.
    /// </summary>
    private static List<double> BuildEdges(double start, List<double> sizes)
    {
        var edges = new List<double>(sizes.Count + 1) { start };
        var current = start;
        foreach (var s in sizes)
        {
            current += s;
            edges.Add(current);
        }
        return edges;
    }

    /// <summary>
    /// Resizes a list to a specified count, filling new elements with a default value.
    /// </summary>
    private static List<T> Resize<T>(List<T> list, int count, T fill)
    {
        var resized = new List<T>(count);
        for (var i = 0; i < count; i++)
        {
            resized.Add(i < list.Count ? list[i] : fill);
        }
        return resized;
    }
}
