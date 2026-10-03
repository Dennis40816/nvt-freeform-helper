using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

/// <summary>
/// Detects and tags "freeform" pads. A freeform pad occurs when a single CAD pad
/// is large enough to be matched with multiple adjacent regular grid pads.
/// </summary>
public sealed class FreeformDetector
{
    private const double CoverageNoiseFloor = 1e-6;

    /// <summary>
    /// Automatically identifies and tags freeform pads based on matching results.
    /// </summary>
    /// <param name="cad">The set of CAD pads (not directly used, but conceptually relevant).</param>
    /// <param name="grid">The regular grid containing pads with match information.</param>
    /// <param name="matchSettings">The settings used for matching.</param>
    /// <param name="matchResult">Latest overlap mapping result.</param>
    public static void AutoTagFreeforms(CadPadSet cad, RegularGrid grid, MatchingSettings matchSettings, PadMatchResult? matchResult = null)
    {
        ApplyAssignments(grid, DetectAssignments(cad, grid, matchSettings, matchResult));
    }

    public static IReadOnlyDictionary<int, FreeformType> DetectAssignments(
        CadPadSet cad,
        RegularGrid grid,
        MatchingSettings matchSettings,
        PadMatchResult? matchResult = null)
    {
        _ = cad;
        matchSettings ??= new MatchingSettings();
        var threshold = Math.Clamp(matchSettings.FreeformAxisThreshold, 0.0, 1.0);
        var enableXy = matchSettings.EnableAutoDetectXy;
        var enableEdgeSpecialization = matchSettings.EnableFreeformEdgeSpecialization;

        if (matchResult is not null && matchResult.CadToRegular.Count > 0)
        {
            return DetectAssignmentsFromOverlapLinks(grid, matchResult, threshold, enableXy, enableEdgeSpecialization);
        }

        return DetectAssignmentsFromLegacyMatches(grid, threshold, enableXy);
    }

    public static void ApplyAssignments(RegularGrid grid, IReadOnlyDictionary<int, FreeformType> assignments)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(assignments);

        foreach (var pad in grid.Pads)
        {
            pad.Freeform = assignments.TryGetValue(pad.RegularPadId, out var type)
                ? type
                : FreeformType.None;
        }
    }

    private static Dictionary<int, FreeformType> DetectAssignmentsFromOverlapLinks(
        RegularGrid grid,
        PadMatchResult matchResult,
        double threshold,
        bool enableXy,
        bool enableEdgeSpecialization)
    {
        var regularById = grid.Pads.ToDictionary(pad => pad.RegularPadId);
        var activeRegularPadIds = matchResult.RegularToCad.Keys.ToHashSet();
        var occupiedGridCells = RegularBoundaryClassifier.BuildOccupiedGridCells(grid, activeRegularPadIds);
        var bestAssignments = new Dictionary<int, (double weight, FreeformType type)>();

        foreach (var pair in matchResult.CadToRegular)
        {
            var links = pair.Value
                .Where(link => regularById.ContainsKey(link.RegularPadId) && link.CadCoverage > CoverageNoiseFloor)
                .ToList();
            if (links.Count < 2)
            {
                continue;
            }

            var rowCoverage = new Dictionary<int, double>();
            var colCoverage = new Dictionary<int, double>();
            var totalCoverage = 0.0;

            foreach (var link in links)
            {
                var regularPad = regularById[link.RegularPadId];

                var coverage = Math.Max(0.0, link.CadCoverage);
                if (coverage <= CoverageNoiseFloor)
                {
                    continue;
                }

                totalCoverage += coverage;
                rowCoverage.TryGetValue(regularPad.Row, out var rowSum);
                rowCoverage[regularPad.Row] = rowSum + coverage;
                colCoverage.TryGetValue(regularPad.Col, out var colSum);
                colCoverage[regularPad.Col] = colSum + coverage;
            }

            if (totalCoverage <= CoverageNoiseFloor)
            {
                continue;
            }

            var rowDominance = rowCoverage.Count == 0 ? 1.0 : rowCoverage.Values.Max() / totalCoverage;
            var colDominance = colCoverage.Count == 0 ? 1.0 : colCoverage.Values.Max() / totalCoverage;
            var xSpread = 1.0 - colDominance;
            var ySpread = 1.0 - rowDominance;

            var freeformType = ResolveTypeFromSpread(xSpread, ySpread, threshold, enableXy);
            var isBoundarySpecialization = false;
            if (freeformType == FreeformType.None && enableEdgeSpecialization)
            {
                freeformType = TryResolveBoundarySpecializationType(
                    links,
                    regularById,
                    threshold,
                    enableXy,
                    grid.Rows,
                    grid.Cols,
                    occupiedGridCells);
                isBoundarySpecialization = freeformType != FreeformType.None;
            }

            if (freeformType == FreeformType.None)
            {
                continue;
            }

            foreach (var link in links)
            {
                var weight = Math.Max(0.0, link.CadCoverage);
                if (weight <= CoverageNoiseFloor || !regularById.ContainsKey(link.RegularPadId))
                {
                    continue;
                }

                if (!isBoundarySpecialization && !ShouldAssignFreeformToRegular(link, threshold))
                {
                    continue;
                }

                if (!bestAssignments.TryGetValue(link.RegularPadId, out var current) || weight > current.weight)
                {
                    bestAssignments[link.RegularPadId] = (weight, freeformType);
                }
            }
        }

        return bestAssignments.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.type);
    }

    private static FreeformType ResolveTypeFromSpread(double xSpread, double ySpread, double threshold, bool enableXy)
    {
        var dominantSpread = Math.Max(xSpread, ySpread);
        if (dominantSpread < threshold)
        {
            return FreeformType.None;
        }

        if (enableXy && xSpread >= threshold && ySpread >= threshold)
        {
            return FreeformType.XYWay;
        }

        return xSpread >= ySpread ? FreeformType.XWay : FreeformType.YWay;
    }

    private static FreeformType TryResolveBoundarySpecializationType(
        IReadOnlyList<PadMatchLink> links,
        Dictionary<int, RegularPad> regularById,
        double threshold,
        bool enableXy,
        int rows,
        int cols,
        IReadOnlySet<(int Row, int Col)> occupiedGridCells)
    {
        var covered = links
            .Where(link => regularById.ContainsKey(link.RegularPadId) && link.CadCoverage > CoverageNoiseFloor)
            .Select(link => (link, pad: regularById[link.RegularPadId]))
            .OrderByDescending(item => item.link.CadCoverage)
            .ToList();
        if (covered.Count < 2)
        {
            return FreeformType.None;
        }

        var anchor = covered[0];
        var weakBoundary = covered
            .Skip(1)
            .Where(item => item.link.CadCoverage <= threshold)
            .Where(item => RegularBoundaryClassifier.IsBoundaryPad(item.pad, rows, cols, occupiedGridCells))
            .ToList();
        if (weakBoundary.Count == 0)
        {
            return FreeformType.None;
        }

        var hasLeft = false;
        var hasRight = false;
        var hasUp = false;
        var hasDown = false;

        foreach (var item in weakBoundary)
        {
            if (item.pad.Col < anchor.pad.Col) hasLeft = true;
            if (item.pad.Col > anchor.pad.Col) hasRight = true;
            if (item.pad.Row < anchor.pad.Row) hasUp = true;
            if (item.pad.Row > anchor.pad.Row) hasDown = true;
        }

        // Ignore two-side micro shifts (left+right or up+down).
        if ((hasLeft && hasRight) || (hasUp && hasDown))
        {
            return FreeformType.None;
        }

        var hasHorizontal = hasLeft || hasRight;
        var hasVertical = hasUp || hasDown;
        if (hasHorizontal && hasVertical)
        {
            return enableXy ? FreeformType.XYWay : FreeformType.None;
        }

        if (hasHorizontal)
        {
            return FreeformType.XWay;
        }

        if (hasVertical)
        {
            return FreeformType.YWay;
        }

        return FreeformType.None;
    }

    private static bool ShouldAssignFreeformToRegular(PadMatchLink link, double threshold)
    {
        var effectiveCoverage = Math.Max(
            Math.Max(0.0, link.CadCoverage),
            Math.Max(0.0, link.RegularCoverage));
        return effectiveCoverage >= threshold;
    }

    private static Dictionary<int, FreeformType> DetectAssignmentsFromLegacyMatches(RegularGrid grid, double threshold, bool enableXy)
    {
        var assignments = new Dictionary<int, FreeformType>();
        var groups = grid.Pads
            .Where(pad => pad.MatchedCadPadId is not null && pad.MatchScore > 0)
            .GroupBy(pad => pad.MatchedCadPadId!.Value)
            .ToList();

        foreach (var group in groups)
        {
            var pads = group.ToList();
            if (pads.Count < 2)
            {
                continue;
            }

            var minR = pads.Min(p => p.Row);
            var maxR = pads.Max(p => p.Row);
            var minC = pads.Min(p => p.Col);
            var maxC = pads.Max(p => p.Col);

            var spanRows = maxR - minR + 1;
            var spanCols = maxC - minC + 1;
            var xSpread = spanCols <= 1 ? 0.0 : (spanCols - 1.0) / spanCols;
            var ySpread = spanRows <= 1 ? 0.0 : (spanRows - 1.0) / spanRows;
            var dominantSpread = Math.Max(xSpread, ySpread);

            if (dominantSpread < threshold)
            {
                continue;
            }

            var type = enableXy && xSpread >= threshold && ySpread >= threshold
                ? FreeformType.XYWay
                : (xSpread >= ySpread ? FreeformType.XWay : FreeformType.YWay);

            foreach (var pad in pads)
            {
                assignments[pad.RegularPadId] = type;
            }
        }

        return assignments;
    }
}
