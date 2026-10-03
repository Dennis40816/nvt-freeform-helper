using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

/// <summary>
/// Specifies the kind of overlap found between two CAD pads.
/// </summary>
public enum DxfOverlapKind
{
    /// <summary>
    /// Two pads have identical geometry and are on the same layer.
    /// </summary>
    DuplicateSameLayer,
    /// <summary>
    /// Two pads have identical geometry but are on different layers.
    /// </summary>
    DuplicateDifferentLayer,
    /// <summary>
    /// Two pads have intersecting but not identical geometries.
    /// </summary>
    Overlap
}

/// <summary>
/// Represents a single detected overlap issue between two CAD pads.
/// </summary>
public sealed class DxfOverlapIssue
{
    public DxfOverlapIssue(DxfOverlapKind kind, CadPad a, CadPad b)
    {
        Kind = kind;
        PadAId = a.Id;
        PadBId = b.Id;
        LayerA = a.Layer;
        LayerB = b.Layer;
    }

    public DxfOverlapKind Kind { get; }
    public int PadAId { get; }
    public int PadBId { get; }
    public string LayerA { get; }
    public string LayerB { get; }

    /// <summary>
    /// Builds a human-readable message describing the issue.
    /// </summary>
    public string BuildMessage()
    {
        var layerInfo = LayerA == LayerB ? $"layer {LayerA}" : $"layers {LayerA}, {LayerB}";
        return $"{Kind}: pads {PadAId} & {PadBId} ({layerInfo})";
    }
}

/// <summary>
/// Contains the results of a DXF overlap analysis.
/// </summary>
public sealed class DxfOverlapReport
{
    public DxfOverlapReport(
        int padCount,
        int duplicateSameLayerPairs,
        int duplicateCrossLayerPairs,
        int overlapPairs,
        IReadOnlyList<DxfOverlapIssue> samples)
    {
        PadCount = padCount;
        DuplicateSameLayerPairs = duplicateSameLayerPairs;
        DuplicateCrossLayerPairs = duplicateCrossLayerPairs;
        OverlapPairs = overlapPairs;
        SampleIssues = samples;
    }

    public int PadCount { get; }
    public int DuplicateSameLayerPairs { get; }
    public int DuplicateCrossLayerPairs { get; }
    public int OverlapPairs { get; }
    /// <summary>
    /// A list of sample issues found during analysis.
    /// </summary>
    public IReadOnlyList<DxfOverlapIssue> SampleIssues { get; }

    public bool HasIssues => DuplicateSameLayerPairs + DuplicateCrossLayerPairs + OverlapPairs > 0;

    public string Summary =>
        $"duplicates same-layer={DuplicateSameLayerPairs}, cross-layer={DuplicateCrossLayerPairs}, overlaps={OverlapPairs}";
}

/// <summary>
/// Analyzes a set of CAD pads to find duplicates and overlaps.
/// </summary>
public sealed partial class DxfOverlapAnalyzer
{
    private const double Epsilon = 1e-9;
    private const double OverlapAreaEpsilon = 1e-12;
    private const double MinCellSize = 1e-6;
    private const int SignatureDecimals = 6;
    private const int DefaultMaxSamples = 12;
    private const int SpatialAccelerationMinPadCount = 64;
    private const int MaxSpatialCellsPerPad = 4096;

    /// <summary>
    /// Analyzes the provided CAD pad set for overlap issues.
    /// </summary>
    /// <param name="cad">The set of CAD pads to analyze.</param>
    /// <param name="maxSamples">The maximum number of issue samples to include in the report.</param>
    /// <param name="reportProgress">
    /// Optional callback for reporting analysis progress in range [0, 1].
    /// </param>
    /// <returns>A <see cref="DxfOverlapReport"/> summarizing the findings.</returns>
    public static DxfOverlapReport Analyze(
        CadPadSet cad,
        int maxSamples = DefaultMaxSamples,
        Action<double>? reportProgress = null)
    {
        ReportProgress(reportProgress, 0.0);
        if (cad.Pads.Count == 0)
        {
            ReportProgress(reportProgress, 1.0);
            return new DxfOverlapReport(0, 0, 0, 0, Array.Empty<DxfOverlapIssue>());
        }

        // --- Step 1: Find duplicates using a geometric signature ---
        // A signature is a canonical string representation of a polygon's vertices.
        // Polygons with the same signature are considered geometrically identical.
        var signatureMap = new Dictionary<string, List<CadPad>>(StringComparer.Ordinal);
        for (var index = 0; index < cad.Pads.Count; index++)
        {
            var pad = cad.Pads[index];
            var signature = BuildSignature(pad.Polygon);
            if (!signatureMap.TryGetValue(signature, out var list))
            {
                list = new List<CadPad>();
                signatureMap[signature] = list;
            }
            list.Add(pad);
            // Step 1 weight: 15%
            MaybeReportProgress(
                reportProgress,
                0.15 * (index + 1) / cad.Pads.Count,
                index + 1,
                cad.Pads.Count,
                interval: 64);
        }

        var samples = new List<DxfOverlapIssue>(maxSamples);
        var duplicatePairs = new HashSet<long>();
        var duplicateSameLayer = 0;
        var duplicateCrossLayer = 0;
        var totalDuplicateComparisons = 0L;
        foreach (var group in signatureMap.Values)
        {
            totalDuplicateComparisons += PairCount(group.Count);
        }
        var processedDuplicateComparisons = 0L;

        foreach (var group in signatureMap.Values)
        {
            if (group.Count < 2) continue; // Not a duplicate

            // Process all pairs within the group of identical pads
            for (var i = 0; i < group.Count; i++)
            {
                for (var j = i + 1; j < group.Count; j++)
                {
                    var a = group[i];
                    var b = group[j];
                    processedDuplicateComparisons++;
                    var key = BuildPairKey(a.Id, b.Id);
                    duplicatePairs.Add(key); // Mark this pair as a known duplicate

                    if (string.Equals(a.Layer, b.Layer, StringComparison.OrdinalIgnoreCase))
                    {
                        duplicateSameLayer++;
                        AddSample(samples, maxSamples, new DxfOverlapIssue(DxfOverlapKind.DuplicateSameLayer, a, b));
                    }
                    else
                    {
                        duplicateCrossLayer++;
                        AddSample(samples, maxSamples, new DxfOverlapIssue(DxfOverlapKind.DuplicateDifferentLayer, a, b));
                    }

                    // Step 2 weight: 25% (15% -> 40%)
                    MaybeReportProgress(
                        reportProgress,
                        0.15 + (0.25 * processedDuplicateComparisons / totalDuplicateComparisons),
                        processedDuplicateComparisons,
                        totalDuplicateComparisons,
                        interval: 1024);
                }
            }
        }
        if (totalDuplicateComparisons == 0)
        {
            ReportProgress(reportProgress, 0.40);
        }

        // --- Step 2: Find overlaps for non-duplicate pairs ---
        var overlapPairs = 0;
        var pads = cad.Pads;
        var candidatePairs = BuildOverlapCandidates(pads);
        var totalPairs = candidatePairs.Count;
        var processedPairs = 0L;
        foreach (var pair in candidatePairs)
        {
            var a = pads[pair.LeftIndex];
            var b = pads[pair.RightIndex];
            processedPairs++;
            // Skip if this pair was already identified as a duplicate
            if (duplicatePairs.Contains(BuildPairKey(a.Id, b.Id)))
            {
                MaybeReportProgress(
                    reportProgress,
                    0.40 + (0.60 * processedPairs / totalPairs),
                    processedPairs,
                    totalPairs,
                    interval: 2048);
                continue;
            }

            // Broad phase: check if bounding boxes intersect
            if (!a.Bounds.Intersects(b.Bounds))
            {
                MaybeReportProgress(
                    reportProgress,
                    0.40 + (0.60 * processedPairs / totalPairs),
                    processedPairs,
                    totalPairs,
                    interval: 2048);
                continue;
            }

            // Narrow phase: perform more expensive polygon intersection test
            if (!PolygonsOverlap(a.Polygon, b.Polygon))
            {
                MaybeReportProgress(
                    reportProgress,
                    0.40 + (0.60 * processedPairs / totalPairs),
                    processedPairs,
                    totalPairs,
                    interval: 2048);
                continue;
            }

            overlapPairs++;
            AddSample(samples, maxSamples, new DxfOverlapIssue(DxfOverlapKind.Overlap, a, b));
            MaybeReportProgress(
                reportProgress,
                0.40 + (0.60 * processedPairs / totalPairs),
                processedPairs,
                totalPairs,
                interval: 2048);
        }

        ReportProgress(reportProgress, 1.0);
        return new DxfOverlapReport(pads.Count, duplicateSameLayer, duplicateCrossLayer, overlapPairs, samples);
    }

    private static void AddSample(List<DxfOverlapIssue> samples, int maxSamples, DxfOverlapIssue issue)
    {
        if (samples.Count < maxSamples)
        {
            samples.Add(issue);
        }
    }

    /// <summary>
    /// Builds a unique, order-independent key for a pair of pad IDs.
    /// </summary>
    private static long BuildPairKey(int a, int b)
    {
        var min = Math.Min(a, b);
        var max = Math.Max(a, b);
        return ((long)min << 32) | (uint)max;
    }

    private static long PairCount(int count)
    {
        return count < 2 ? 0L : ((long)count * (count - 1)) / 2;
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
        long processed,
        long total,
        int interval)
    {
        if (reportProgress is null || total <= 0)
        {
            return;
        }

        if (processed >= total || processed % interval == 0)
        {
            ReportProgress(reportProgress, value);
        }
    }
}
