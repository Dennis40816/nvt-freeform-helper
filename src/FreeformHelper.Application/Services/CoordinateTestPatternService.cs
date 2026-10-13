namespace FreeformHelper.Application.Services;

/// <summary>
/// The preset test patterns the coordinate planner can draw on the machine area.
/// </summary>
public enum CoordinateTestPatternKind
{
    /// <summary>Five horizontal and five vertical guide lines.</summary>
    GuideLines5x5,

    /// <summary>The two diagonals of the area, as custom paths.</summary>
    Diagonals,

    /// <summary>
    /// Thirteen points: a 5 by 5 node lattice, checkerboard positions only. Rows 1, 3 and 5 hold
    /// three points (left edge, middle, right edge). Rows 2 and 4 hold two points between them.
    /// </summary>
    Points13,
}

/// <summary>The rectangle (machine coordinates) that a test pattern is drawn in.</summary>
public sealed record CoordinateTestPatternArea(double MinX, double MinY, double MaxX, double MaxY)
{
    public double Width => MaxX - MinX;

    public double Height => MaxY - MinY;
}

/// <summary>
/// What a test pattern adds to a <see cref="CoordinatePlannerRequest"/>. Guide counts apply to the
/// planner's guide settings. Points and paths are machine coordinates.
/// </summary>
public sealed record CoordinateTestPatternResult(
    int HorizontalGuideCount,
    int VerticalGuideCount,
    IReadOnlyList<CoordinateCustomPointRequest> CustomPoints,
    IReadOnlyList<CoordinateCustomPathRequest> CustomPaths);

/// <summary>
/// Builds the preset test patterns as plain planner inputs. Pure function, no UI state.
/// </summary>
public static class CoordinateTestPatternService
{
    public const int GuideLineCount = 5;
    public const int DefaultDiagonalStepCount = 10;
    public const int LatticeNodeCount = 5;
    public const int Points13Count = 13;

    private static readonly CoordinateCustomPointRequest[] _noPoints = [];
    private static readonly CoordinateCustomPathRequest[] _noPaths = [];

    /// <summary>Builds one pattern inside <paramref name="area"/>, shrunk by <paramref name="inset"/> on every side.</summary>
    public static CoordinateTestPatternResult Build(
        CoordinateTestPatternKind kind,
        CoordinateTestPatternArea area,
        double inset = 0d,
        int diagonalStepCount = DefaultDiagonalStepCount)
    {
        ArgumentNullException.ThrowIfNull(area);
        if (inset < 0d || double.IsNaN(inset) || double.IsInfinity(inset))
        {
            throw new ArgumentOutOfRangeException(nameof(inset), inset, "Inset must be a finite value of zero or more.");
        }

        if (diagonalStepCount < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(diagonalStepCount), diagonalStepCount, "A diagonal needs at least two steps.");
        }

        var inner = new CoordinateTestPatternArea(area.MinX + inset, area.MinY + inset, area.MaxX - inset, area.MaxY - inset);
        if (!(inner.Width > 0d) || !(inner.Height > 0d))
        {
            throw new ArgumentException("The area is empty after the inset.", nameof(area));
        }

        return kind switch
        {
            CoordinateTestPatternKind.GuideLines5x5 => new CoordinateTestPatternResult(GuideLineCount, GuideLineCount, _noPoints, _noPaths),
            CoordinateTestPatternKind.Diagonals => BuildDiagonals(inner, diagonalStepCount),
            CoordinateTestPatternKind.Points13 => BuildPoints13(inner),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown test pattern."),
        };
    }

    private static CoordinateTestPatternResult BuildDiagonals(CoordinateTestPatternArea area, int stepCount)
    {
        CoordinateCustomPathRequest[] paths =
        [
            new("diagonal-rising", "Diagonal /", area.MinX, area.MinY, area.MaxX, area.MaxY, stepCount),
            new("diagonal-falling", "Diagonal \\", area.MinX, area.MaxY, area.MaxX, area.MinY, stepCount),
        ];
        return new CoordinateTestPatternResult(0, 0, _noPoints, paths);
    }

    private static CoordinateTestPatternResult BuildPoints13(CoordinateTestPatternArea area)
    {
        var points = new List<CoordinateCustomPointRequest>(Points13Count);
        var last = LatticeNodeCount - 1;
        for (var row = 0; row < LatticeNodeCount; row++)
        {
            for (var col = 0; col < LatticeNodeCount; col++)
            {
                if ((row + col) % 2 != 0)
                {
                    continue;
                }

                var x = area.MinX + (area.Width * col / last);
                var y = area.MinY + (area.Height * row / last);
                points.Add(new CoordinateCustomPointRequest($"p13-r{row + 1}c{col + 1}", $"T{points.Count + 1}", x, y));
            }
        }

        return new CoordinateTestPatternResult(0, 0, points, _noPaths);
    }
}
