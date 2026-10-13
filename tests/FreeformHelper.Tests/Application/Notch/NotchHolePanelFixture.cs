using Clipper2Lib;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;

namespace FreeformHelper.Tests;

/// <summary>
/// Synthetic panel with holes for notch compensation tests. The panel is a square of
/// <see cref="GridSize"/> by <see cref="GridSize"/> pad cells of <see cref="Pitch"/>. Each pad cell
/// holds one CAD pad (the cell shrunk by <see cref="PadInset"/>) minus the holes. The geometry
/// answers (<see cref="HolePanel.RemainingRatioByCellIndex"/>) are computed with Sutherland-Hodgman
/// clipping and the shoelace formula, not with the Clipper library that builds the CAD pads.
/// Two regular grids are possible. The aligned grid has the same cells as the pad cells, so every
/// CAD pad lies inside one regular pad. The staggered grid has the same cell count and pitch but
/// starts at <see cref="StaggerForwardOrigin"/> (or <see cref="StaggerBackwardOrigin"/> in the
/// half-turned panel). Every CAD pad then lies across two regular pads in each direction and the
/// compensation has foreign cells to project into. CAD beyond the grid border is not covered by it.
/// </summary>
internal static class NotchHolePanelFixture
{
    public const int GridSize = 12;
    public const double Pitch = 5d;
    public const double PadInset = 0.25d;
    public const double PanelSize = GridSize * Pitch;
    public const int NullValue = 65535;

    /// <summary>Area of one CAD pad before any hole is cut.</summary>
    public const double PadArea = (Pitch - (2d * PadInset)) * (Pitch - (2d * PadInset));

    /// <summary>Grid start of the staggered layout. Each pad holds 2.75 of itself in its own regular pad and sticks 1.75 into the next one.</summary>
    public const double StaggerForwardOrigin = -2d;

    /// <summary>Grid start of the half-turned staggered layout.</summary>
    public const double StaggerBackwardOrigin = 2d;

    /// <summary>Divisible by 8, so a circle has vertices on its axes and diagonals and the hole counts do not depend on the segment count.</summary>
    private const int CircleSegments = 256;
    private const double ClipperPrecisionScale = 1_000_000d;
    private const double RemovedAreaTolerance = 1e-9;

    internal sealed record HolePanel(
        CadPadSet Cad,
        RegularGrid Grid,
        IReadOnlyList<double> RemainingRatioByCellIndex,
        IReadOnlySet<int> FullyRemovedCellIndexes,
        IReadOnlySet<int> PartiallyRemovedCellIndexes);

    public static Polygon2 Circle(double centerX, double centerY, double radius)
    {
        var vertices = Enumerable.Range(0, CircleSegments)
            .Select(index =>
            {
                var angle = 2d * Math.PI * index / CircleSegments;
                return new Point2(centerX + (radius * Math.Cos(angle)), centerY + (radius * Math.Sin(angle)));
            });
        return new Polygon2(vertices);
    }

    public static Polygon2 Rect(double minX, double minY, double maxX, double maxY) =>
        TestGeometryFactory.CreateRect(minX, minY, maxX, maxY);

    public static Polygon2 Transform(Polygon2 polygon, Func<Point2, Point2> map) =>
        new(polygon.Vertices.Select(map));

    public static Point2 RotateHalfTurn(Point2 point) => new(PanelSize - point.X, PanelSize - point.Y);

    public static int RotateHalfTurnIndex(int index) => (GridSize * GridSize) - 1 - index;

    /// <summary>
    /// Index one column to the left in the row-major grid. A pad in column 0 has no such neighbor and
    /// gets a negative index that no real pad has.
    /// </summary>
    public static int OneColumnLeftIndex(int index) => index % GridSize == 0 ? -1 - index : index - 1;

    /// <summary>
    /// Builds the CAD pads and the regular grid. Holes must not overlap each other, because the
    /// remaining ratio adds up the removed area of every hole. <paramref name="gridOrigin"/> 0 gives the aligned grid.
    /// <see cref="StaggerForwardOrigin"/> and <see cref="StaggerBackwardOrigin"/> give a staggered grid.
    /// </summary>
    public static HolePanel Build(IReadOnlyList<Polygon2> holes, bool reverseCadOrder = false, double gridOrigin = 0d)
    {
        var ratios = new double[GridSize * GridSize];
        var removed = new HashSet<int>();
        var partial = new HashSet<int>();
        var pads = new List<CadPad>();
        var nextCadId = 1;

        for (var row = 0; row < GridSize; row++)
        {
            for (var col = 0; col < GridSize; col++)
            {
                var index = (row * GridSize) + col;
                var minX = (col * Pitch) + PadInset;
                var minY = (row * Pitch) + PadInset;
                var maxX = ((col + 1) * Pitch) - PadInset;
                var maxY = ((row + 1) * Pitch) - PadInset;
                var removedArea = holes.Sum(hole => IntersectionArea(hole, minX, minY, maxX, maxY));
                ratios[index] = 1d - (removedArea / PadArea);
                if (ratios[index] <= RemovedAreaTolerance)
                {
                    ratios[index] = 0d;
                    removed.Add(index);
                }
                else if (ratios[index] < 1d - RemovedAreaTolerance)
                {
                    partial.Add(index);
                }

                foreach (var piece in BuildCadPieces(minX, minY, maxX, maxY, holes))
                {
                    pads.Add(new CadPad(nextCadId, $"PAD{nextCadId}", "SENSOR", piece));
                    nextCadId++;
                }
            }
        }

        if (reverseCadOrder)
        {
            pads.Reverse();
            pads = pads.Select((pad, order) => new CadPad(order + 1, $"PAD{order + 1}", pad.Layer, pad.Polygon)).ToList();
        }

        return new HolePanel(new CadPadSet(pads), BuildGrid(gridOrigin), ratios, removed, partial);
    }

    private static RegularGrid BuildGrid(double gridOrigin)
    {
        var edges = Enumerable.Range(0, GridSize + 1).Select(index => gridOrigin + (index * Pitch)).ToArray();
        var pads = new List<RegularPad>(GridSize * GridSize);
        for (var row = 0; row < GridSize; row++)
        {
            for (var col = 0; col < GridSize; col++)
            {
                var index = (row * GridSize) + col;
                pads.Add(TestGeometryFactory.CreateRegularPad(row, col, index, edges[col], edges[row], edges[col + 1], edges[row + 1], index));
            }
        }

        return new RegularGrid(GridSize, GridSize, edges, edges, pads);
    }

    public static ProjectSettings CreateSettings(bool capsEnabled = false)
    {
        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                NullValue = NullValue,
                ComputationMode = NotchComputationMode.CadAllocation,
                EnableBoundaryVirtualAreaCap = capsEnabled,
                EnableTargetCoverageGuard = capsEnabled,
            },
        };
        settings.Notch.EnabledVersions.Clear();
        settings.Notch.EnabledVersions.Add(NotchAlgorithmVersion.V22);
        return settings;
    }

    public static NotchTable Generate(HolePanel panel, bool capsEnabled = false) =>
        new NotchTableGenerator().Generate(panel.Cad, panel.Grid, CreateSettings(capsEnabled));

    /// <summary>
    /// One text line per anchor diff: combine percent and the target legs. Continuation rows of
    /// one anchor are merged. Indexes go through <paramref name="mapIndex"/> so that rotated or
    /// shifted panels can be compared with the original one.
    /// </summary>
    public static IReadOnlyDictionary<int, string> DescribeByAnchor(NotchTable table, Func<int, int> mapIndex)
    {
        var result = new SortedDictionary<int, string>();
        foreach (var group in table.Rows.Where(static row => row.V22Node is not null).GroupBy(static row => row.DiffIndex))
        {
            var nodes = group.Select(static row => row.V22Node!).ToList();
            var combine = nodes.First(static node => (node.Flags & 1) == 0).CombinePercent;
            var legs = nodes
                .SelectMany(static node => new[]
                {
                    (node.TargetDiffIndex1, node.TargetRatioPercent1),
                    (node.TargetDiffIndex2, node.TargetRatioPercent2),
                })
                .Where(static leg => leg.Item1 != NullValue && leg.Item2 != 0)
                .Select(leg => (Index: mapIndex(leg.Item1), Ratio: leg.Item2))
                .OrderBy(static leg => leg.Index)
                .Select(static leg => $"{leg.Index}:{leg.Ratio}");
            result[mapIndex(group.Key)] = $"C{combine}|{string.Join(",", legs)}";
        }

        return result;
    }

    private static List<Polygon2> BuildCadPieces(double minX, double minY, double maxX, double maxY, IReadOnlyList<Polygon2> holes)
    {
        var subject = new PathsD { Clipper.MakePath([minX, minY, maxX, minY, maxX, maxY, minX, maxY]) };
        var clip = new PathsD(holes.Select(hole => Clipper.MakePath(
            hole.Vertices.SelectMany(static vertex => new[] { vertex.X, vertex.Y }).ToArray())));
        var difference = Clipper.Difference(subject, clip, FillRule.NonZero, 6);
        return difference
            .Where(static path => path.Count >= 3 && Clipper.Area(path) > 1d / ClipperPrecisionScale)
            .Select(static path => new Polygon2(path.Select(static point => new Point2(point.x, point.y))))
            .ToList();
    }

    /// <summary>Area of (convex hole polygon) clipped by an axis-aligned rectangle.</summary>
    private static double IntersectionArea(Polygon2 hole, double minX, double minY, double maxX, double maxY)
    {
        var clipped = hole.Vertices.ToList();
        clipped = ClipHalfPlane(clipped, static (point, limit) => point.X - limit, minX, keepGreater: true);
        clipped = ClipHalfPlane(clipped, static (point, limit) => point.X - limit, maxX, keepGreater: false);
        clipped = ClipHalfPlane(clipped, static (point, limit) => point.Y - limit, minY, keepGreater: true);
        clipped = ClipHalfPlane(clipped, static (point, limit) => point.Y - limit, maxY, keepGreater: false);
        if (clipped.Count < 3)
        {
            return 0d;
        }

        var sum = 0d;
        for (var index = 0; index < clipped.Count; index++)
        {
            var current = clipped[index];
            var next = clipped[(index + 1) % clipped.Count];
            sum += (current.X * next.Y) - (next.X * current.Y);
        }

        return Math.Abs(sum) / 2d;
    }

    private static List<Point2> ClipHalfPlane(
        List<Point2> input,
        Func<Point2, double, double> signedDistance,
        double limit,
        bool keepGreater)
    {
        var output = new List<Point2>();
        for (var index = 0; index < input.Count; index++)
        {
            var current = input[index];
            var previous = input[(index + input.Count - 1) % input.Count];
            var currentDistance = signedDistance(current, limit) * (keepGreater ? 1d : -1d);
            var previousDistance = signedDistance(previous, limit) * (keepGreater ? 1d : -1d);
            var currentInside = currentDistance >= 0d;
            var previousInside = previousDistance >= 0d;
            if (currentInside != previousInside)
            {
                var t = previousDistance / (previousDistance - currentDistance);
                output.Add(new Point2(
                    previous.X + (t * (current.X - previous.X)),
                    previous.Y + (t * (current.Y - previous.Y))));
            }

            if (currentInside)
            {
                output.Add(current);
            }
        }

        return output;
    }
}
