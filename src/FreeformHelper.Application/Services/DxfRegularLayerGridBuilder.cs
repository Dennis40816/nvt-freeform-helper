using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

/// <summary>
/// Builds a <see cref="RegularGrid"/> by taking polygons from a specific DXF layer.
/// </summary>
public sealed class DxfRegularLayerGridBuilder
{
    /// <summary>
    /// Builds a regular grid from layer pads.
    /// </summary>
    /// <param name="layerPads">CAD pads from the selected layer.</param>
    /// <param name="settings">Current grid settings.</param>
    /// <returns>A regular grid whose pads come from the selected DXF layer.</returns>
    public static RegularGrid BuildFromLayer(CadPadSet layerPads, GridSettings settings)
    {
        settings.ValidateOrThrow();

        var rows = settings.YChannels;
        var cols = settings.XChannels;
        var expected = rows * cols;

        if (layerPads.Pads.Count != expected)
        {
            throw new InvalidOperationException(
                $"DXF regular layer pads count mismatch. Expected {expected} ({rows}x{cols}), got {layerPads.Pads.Count}.");
        }

        var pads = new List<RegularPad>(expected);
        var rowMajor = BuildRowMajor(layerPads.Pads, rows, cols);
        for (var index = 0; index < rowMajor.Count; index++)
        {
            var row = index / cols;
            var col = index % cols;
            pads.Add(new RegularPad(row, col, index, rowMajor[index].Polygon));
        }

        var xEdges = BuildColumnEdges(rowMajor, rows, cols);
        var yEdges = BuildRowEdges(rowMajor, rows, cols);
        return new RegularGrid(rows, cols, xEdges, yEdges, pads);
    }

    /// <summary>
    /// Produces a stable row-major order compatible with <see cref="RegularGridBuilder"/>:
    /// row 0 = bottom row, each row sorted by X ascending.
    /// </summary>
    private static List<CadPad> BuildRowMajor(IReadOnlyList<CadPad> sourcePads, int rows, int cols)
    {
        var orderedTopToBottom = sourcePads
            .OrderByDescending(p => p.Centroid.Y)
            .ThenBy(p => p.Centroid.X)
            .ToList();

        var rowMajor = new List<CadPad>(rows * cols);
        for (var row = 0; row < rows; row++)
        {
            var topRow = (rows - 1) - row;
            var start = topRow * cols;
            var rowSlice = orderedTopToBottom
                .Skip(start)
                .Take(cols)
                .OrderBy(p => p.Centroid.X)
                .ThenByDescending(p => p.Centroid.Y)
                .ThenBy(p => p.Id)
                .ToList();

            if (rowSlice.Count != cols)
            {
                throw new InvalidOperationException(
                    $"DXF regular layer row grouping mismatch at row {row}. Expected {cols}, got {rowSlice.Count}.");
            }

            rowMajor.AddRange(rowSlice);
        }

        return rowMajor;
    }

    private static List<double> BuildColumnEdges(List<CadPad> rowMajor, int rows, int cols)
    {
        var minByCol = Enumerable.Repeat(double.PositiveInfinity, cols).ToArray();
        var maxByCol = Enumerable.Repeat(double.NegativeInfinity, cols).ToArray();
        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < cols; col++)
            {
                var pad = rowMajor[(row * cols) + col];
                var bounds = pad.Bounds;
                if (bounds.MinX < minByCol[col])
                {
                    minByCol[col] = bounds.MinX;
                }

                if (bounds.MaxX > maxByCol[col])
                {
                    maxByCol[col] = bounds.MaxX;
                }
            }
        }

        var edges = new List<double>(cols + 1)
        {
            minByCol[0],
        };

        for (var col = 1; col < cols; col++)
        {
            var mid = (maxByCol[col - 1] + minByCol[col]) * 0.5;
            edges.Add(mid);
        }

        edges.Add(maxByCol[cols - 1]);
        EnsureStrictlyIncreasing(edges);
        return edges;
    }

    private static List<double> BuildRowEdges(List<CadPad> rowMajor, int rows, int cols)
    {
        var minByRow = Enumerable.Repeat(double.PositiveInfinity, rows).ToArray();
        var maxByRow = Enumerable.Repeat(double.NegativeInfinity, rows).ToArray();
        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < cols; col++)
            {
                var pad = rowMajor[(row * cols) + col];
                var bounds = pad.Bounds;
                if (bounds.MinY < minByRow[row])
                {
                    minByRow[row] = bounds.MinY;
                }

                if (bounds.MaxY > maxByRow[row])
                {
                    maxByRow[row] = bounds.MaxY;
                }
            }
        }

        var edges = new List<double>(rows + 1)
        {
            minByRow[0],
        };

        for (var row = 1; row < rows; row++)
        {
            var mid = (maxByRow[row - 1] + minByRow[row]) * 0.5;
            edges.Add(mid);
        }

        edges.Add(maxByRow[rows - 1]);
        EnsureStrictlyIncreasing(edges);
        return edges;
    }

    private static void EnsureStrictlyIncreasing(List<double> edges)
    {
        const double epsilon = 1e-6;
        for (var i = 1; i < edges.Count; i++)
        {
            if (edges[i] <= edges[i - 1])
            {
                edges[i] = edges[i - 1] + epsilon;
            }
        }
    }
}
