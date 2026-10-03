using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

/// <summary>
/// Builds CAD-to-regular evidence through the current overlap matcher.
/// </summary>
public sealed class PadMatcher
{
    // Guard against numeric noise from boundary touch / near-zero clipping artifacts.
    // Effective overlap must exceed both:
    // 1) absolute area floor, and
    // 2) relative floor by regular-pad area (0.001%).
    private const double OverlapAreaFloorAbsolute = 1e-6;
    private const double OverlapAreaFloorRegularRatio = 0.00001;

    /// <summary>
    /// Builds the current CAD-to-regular overlap evidence.
    /// </summary>
    /// <param name="cad">The set of CAD pads.</param>
    /// <param name="grid">The regular grid to match.</param>
    /// <param name="settings">
    /// Compatibility parameter retained until the overlap-evidence API is formalized; currently ignored.
    /// </param>
    public static PadMatchResult Match(CadPadSet cad, RegularGrid grid, MatchingSettings settings, Action<double>? reportProgress = null)
    {
        ReportProgress(reportProgress, 0.0);
        _ = settings;
        return MatchOverlap(cad, grid, reportProgress);
    }

    /// <summary>
    /// Builds overlap relations between CAD pads and regular pads.
    /// One CAD can match multiple regular pads, and one regular can match multiple CAD pads.
    /// For compatibility, each regular pad still stores its best single CAD match in
    /// <see cref="RegularPad.MatchedCadPadId"/> and <see cref="RegularPad.MatchScore"/>.
    /// </summary>
    private static PadMatchResult MatchOverlap(
        CadPadSet cad,
        RegularGrid grid,
        Action<double>? reportProgress = null)
    {
        var cadPads = cad.Pads;
        var cadToRegular = new Dictionary<int, List<PadMatchLink>>(cadPads.Count);
        var regularToCad = new Dictionary<int, List<PadMatchLink>>(Math.Max(1, grid.Pads.Count / 2));
        var candidateCellsPerCad = new int[cadPads.Count];
        long candidateCellVisits = 0;
        long boundsIntersections = 0;
        long polygonIntersections = 0;
        var cadCount = Math.Max(1, cadPads.Count);
        var cadProgressInterval = Math.Max(1, cadCount / 100);

        for (var cadIndex = 0; cadIndex < cadPads.Count; cadIndex++)
        {
            var cadPad = cadPads[cadIndex];
            var links = new List<PadMatchLink>();
            var (rowMin, rowMax, colMin, colMax) = RegularGridCandidateQuery.GetCandidateRange(grid, cadPad.Bounds);
            var candidateCountForCad = 0;

            for (var row = rowMin; row <= rowMax; row++)
            {
                for (var col = colMin; col <= colMax; col++)
                {
                    candidateCountForCad++;
                    candidateCellVisits++;
                    var regularPad = grid.GetPad(row, col);
                    if (!cadPad.Bounds.Intersects(regularPad.Bounds))
                    {
                        continue;
                    }

                    boundsIntersections++;

                    var overlapArea = Polygon2.IntersectionAreaWithRect(cadPad.Polygon, regularPad.Bounds);
                    var overlapFloor = Math.Max(
                        OverlapAreaFloorAbsolute,
                        regularPad.Area * OverlapAreaFloorRegularRatio);
                    if (overlapArea <= overlapFloor)
                    {
                        continue;
                    }

                    var regularCoverage = overlapArea / Math.Max(regularPad.Area, 1e-12);
                    var cadCoverage = overlapArea / Math.Max(cadPad.Area, 1e-12);
                    var link = new PadMatchLink(
                        CadPadId: cadPad.Id,
                        RegularPadId: regularPad.RegularPadId,
                        OverlapArea: overlapArea,
                        RegularCoverage: regularCoverage,
                        CadCoverage: cadCoverage);

                    polygonIntersections++;
                    links.Add(link);
                    if (!regularToCad.TryGetValue(regularPad.RegularPadId, out var regularLinks))
                    {
                        regularLinks = new List<PadMatchLink>();
                        regularToCad[regularPad.RegularPadId] = regularLinks;
                    }

                    regularLinks.Add(link);
                }
            }

            if (links.Count > 0)
            {
                links.Sort(static (a, b) => CompareLinksDescending(a, b));
                cadToRegular[cadPad.Id] = links;
            }

            candidateCellsPerCad[cadIndex] = candidateCountForCad;

            MaybeReportProgress(
                reportProgress,
                0.85 * (cadIndex + 1) / cadCount,
                cadIndex + 1,
                cadCount,
                cadProgressInterval);
        }

        foreach (var list in regularToCad.Values)
        {
            list.Sort(static (a, b) => CompareLinksDescending(a, b));
        }

        var regularCount = Math.Max(1, grid.Pads.Count);
        var regularProgressInterval = Math.Max(1, regularCount / 100);
        for (var i = 0; i < grid.Pads.Count; i++)
        {
            var regularPad = grid.Pads[i];
            if (regularToCad.TryGetValue(regularPad.RegularPadId, out var links) && links.Count > 0)
            {
                var best = links[0];
                regularPad.MatchedCadPadId = best.CadPadId;
                regularPad.MatchScore = best.RegularCoverage;
            }
            else
            {
                regularPad.MatchedCadPadId = null;
                regularPad.MatchScore = 0.0;
            }

            MaybeReportProgress(
                reportProgress,
                0.85 + (0.15 * (i + 1) / regularCount),
                i + 1,
                regularCount,
                regularProgressInterval);
        }

        ReportProgress(reportProgress, 1.0);
        var telemetry = BuildTelemetry(
            cadPads.Count,
            candidateCellVisits,
            boundsIntersections,
            polygonIntersections,
            candidateCellsPerCad);

        return new PadMatchResult(
            cadToRegular.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<PadMatchLink>)pair.Value.AsReadOnly()),
            regularToCad.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<PadMatchLink>)pair.Value.AsReadOnly()),
            telemetry);
    }

    private static void ReportProgress(Action<double>? reportProgress, double value)
    {
        if (reportProgress is null)
        {
            return;
        }

        reportProgress(Math.Clamp(value, 0.0, 1.0));
    }

    private static void MaybeReportProgress(
        Action<double>? reportProgress,
        double value,
        int processed,
        int total,
        int interval)
    {
        if (reportProgress is null || total <= 0)
        {
            return;
        }

        if (processed >= total || processed % Math.Max(1, interval) == 0)
        {
            ReportProgress(reportProgress, value);
        }
    }

    private static int CompareLinksDescending(PadMatchLink left, PadMatchLink right)
    {
        var byRegularCoverage = right.RegularCoverage.CompareTo(left.RegularCoverage);
        if (byRegularCoverage != 0)
        {
            return byRegularCoverage;
        }

        var byOverlapArea = right.OverlapArea.CompareTo(left.OverlapArea);
        if (byOverlapArea != 0)
        {
            return byOverlapArea;
        }

        return right.CadCoverage.CompareTo(left.CadCoverage);
    }

    private static PadMatchTelemetry BuildTelemetry(
        int cadPadCount,
        long candidateCellVisits,
        long boundsIntersections,
        long polygonIntersections,
        IReadOnlyList<int> candidateCellsPerCad)
    {
        if (cadPadCount <= 0)
        {
            return PadMatchTelemetry.Empty;
        }

        var average = candidateCellVisits / (double)Math.Max(1, cadPadCount);
        var p95 = ComputeP95(candidateCellsPerCad);
        return new PadMatchTelemetry(
            CadPadCount: cadPadCount,
            CandidateCellVisits: candidateCellVisits,
            BoundsIntersections: boundsIntersections,
            PolygonIntersections: polygonIntersections,
            CandidateCellsPerCadAverage: average,
            CandidateCellsPerCadP95: p95);
    }

    private static int ComputeP95(IReadOnlyList<int> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var sorted = values.OrderBy(static value => value).ToArray();
        var index = (int)Math.Ceiling(0.95 * sorted.Length) - 1;
        index = Math.Clamp(index, 0, sorted.Length - 1);
        return sorted[index];
    }
}
