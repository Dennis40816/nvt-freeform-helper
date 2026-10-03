using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Tests.TestInfrastructure;

internal static class TestGeometryFactory
{
    public static Polygon2 CreateRect(double minX, double minY, double maxX, double maxY)
    {
        return new Polygon2(new[]
        {
            new Point2(minX, minY),
            new Point2(maxX, minY),
            new Point2(maxX, maxY),
            new Point2(minX, maxY),
        });
    }

    public static RegularPad CreateRegularPad(
        int row,
        int col,
        int regularPadId,
        double minX,
        double minY,
        double maxX,
        double maxY,
        int? diffIndex = null,
        int icIndex = 0)
    {
        var pad = new RegularPad(
            row,
            col,
            regularPadId,
            CreateRect(minX, minY, maxX, maxY))
        {
            IcIndex = icIndex,
        };

        if (diffIndex.HasValue)
        {
            pad.DiffIndex = diffIndex.Value;
        }

        return pad;
    }

    public static RegularGrid CreateRegularGrid(
        int rows,
        int cols,
        double cellWidth = 1d,
        double cellHeight = 1d,
        Func<int, int, int>? diffIndexSelector = null,
        Func<int, int, int>? icIndexSelector = null,
        Func<int, int, int>? regularPadIdSelector = null)
    {
        var xEdges = Enumerable.Range(0, cols + 1)
            .Select(index => index * cellWidth)
            .ToArray();
        var yEdges = Enumerable.Range(0, rows + 1)
            .Select(index => index * cellHeight)
            .ToArray();
        var pads = new List<RegularPad>(rows * cols);

        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < cols; col++)
            {
                var regularPadId = regularPadIdSelector?.Invoke(row, col) ?? row * cols + col;
                pads.Add(CreateRegularPad(
                    row,
                    col,
                    regularPadId,
                    col * cellWidth,
                    row * cellHeight,
                    (col + 1) * cellWidth,
                    (row + 1) * cellHeight,
                    diffIndexSelector?.Invoke(row, col),
                    icIndexSelector?.Invoke(row, col) ?? 0));
            }
        }

        return new RegularGrid(rows, cols, xEdges, yEdges, pads);
    }

    public static RegularGrid CreateLinearRegularGrid(
        IReadOnlyList<int> diffIndices,
        Func<int, int>? icIndexSelector = null,
        double cellWidth = 1d,
        double cellHeight = 1d)
    {
        return CreateRegularGrid(
            rows: 1,
            cols: diffIndices.Count,
            cellWidth: cellWidth,
            cellHeight: cellHeight,
            diffIndexSelector: (_, col) => diffIndices[col],
            icIndexSelector: (_, col) => icIndexSelector?.Invoke(col) ?? 0);
    }

    public static CadPad CreateCadPad(
        int id,
        string layer,
        double minX,
        double minY,
        double maxX,
        double maxY,
        string? name = null)
    {
        return new CadPad(
            id,
            name ?? $"CAD{id}",
            layer,
            CreateRect(minX, minY, maxX, maxY));
    }
}
