namespace FreeformHelper.Application.Settings;

/// <summary>
/// Settings for deriving CAD Output FW Diff ↔ Regular (IC/FW) mapping.
/// </summary>
public sealed class IndexMappingSettings
{
    /// <summary>
    /// Gets or sets how CAD Output FW Diff is assigned for CAD pads.
    /// </summary>
    public CadOutputFwDiffAutoMode CadOutputFwDiffAutoMode { get; set; } = CadOutputFwDiffAutoMode.BestMatchDirect;

    /// <summary>
    /// Gets or sets the start value for CAD Output FW Diff numbering.
    /// </summary>
    public int CadOutputFwDiffStartIndex { get; set; }

    /// <summary>
    /// Gets or sets the weight applied to IoU (intersection-over-union).
    /// Higher values prioritize geometric overlap.
    /// </summary>
    public double WeightIou { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the weight applied to centroid distance.
    /// Higher values prioritize closer centroids.
    /// </summary>
    public double WeightCentroidDistance { get; set; } = 0.25;

    /// <summary>
    /// Gets or sets the weight applied to area similarity.
    /// Higher values prioritize similar areas.
    /// </summary>
    public double WeightAreaRatio { get; set; } = 0.15;

    /// <summary>
    /// Gets or sets the score threshold below which a mapping is considered low confidence.
    /// </summary>
    public double LowConfidenceThreshold { get; set; } = 0.55;

    /// <summary>
    /// Gets or sets the margin threshold (best - secondBest) below which a mapping is considered ambiguous.
    /// </summary>
    public double AmbiguousMargin { get; set; } = 0.05;

    /// <summary>
    /// Gets or sets how many cells to expand the CAD bounds-derived candidate range.
    /// Legacy field kept for backward compatibility with existing project files.
    /// </summary>
    public int CandidatePaddingCells { get; set; } = 1;

    /// <summary>
    /// Gets or sets the number of CAD->Regular candidates retained per CAD pad for matching.
    /// When 0, derives from <see cref="CandidatePaddingCells"/> for backward compatibility.
    /// </summary>
    public int CandidateNumber { get; set; }

    /// <summary>
    /// Gets or sets how many candidate mappings are retained per CAD pad for diagnostics.
    /// </summary>
    public int DiagnosticTopK { get; set; } = 3;

    /// <summary>
    /// Gets or sets the maximum number of sample issues returned in reports.
    /// </summary>
    public int MaxSampleIssues { get; set; } = 50;

    /// <summary>
    /// Gets or sets the maximum number of issues included in the manual review list.
    /// </summary>
    public int MaxReportIssues { get; set; } = 2000;

    public int GetEffectiveCandidateNumber()
    {
        if (CandidateNumber > 0)
        {
            return CandidateNumber;
        }

        return CandidateNumberFromPadding(CandidatePaddingCells);
    }

    public static int CandidateNumberFromPadding(int padding)
    {
        var clampedPadding = Math.Clamp(padding, 0, 10);
        var span = 2 * clampedPadding + 1;
        return Math.Max(1, span * span);
    }

    public static int PaddingFromCandidateNumber(int candidateNumber)
    {
        var clampedCandidateNumber = Math.Max(1, candidateNumber);
        var padding = (int)Math.Ceiling((Math.Sqrt(clampedCandidateNumber) - 1.0) / 2.0);
        return Math.Clamp(padding, 0, 10);
    }

    public void ValidateOrThrow()
    {
        if (!Enum.IsDefined(CadOutputFwDiffAutoMode)) throw new InvalidOperationException("CadOutputFwDiffAutoMode is invalid.");
        if (CadOutputFwDiffStartIndex < 0 || CadOutputFwDiffStartIndex > 1_000_000) throw new InvalidOperationException("CadOutputFwDiffStartIndex must be in [0,1000000].");
        if (WeightIou < 0) throw new InvalidOperationException("WeightIou must be non-negative.");
        if (WeightCentroidDistance < 0) throw new InvalidOperationException("WeightCentroidDistance must be non-negative.");
        if (WeightAreaRatio < 0) throw new InvalidOperationException("WeightAreaRatio must be non-negative.");
        if (LowConfidenceThreshold < 0 || LowConfidenceThreshold > 1) throw new InvalidOperationException("LowConfidenceThreshold must be in [0,1].");
        if (AmbiguousMargin < 0 || AmbiguousMargin > 1) throw new InvalidOperationException("AmbiguousMargin must be in [0,1].");
        if (CandidatePaddingCells < 0 || CandidatePaddingCells > 10) throw new InvalidOperationException("CandidatePaddingCells must be in [0,10].");
        if (CandidateNumber < 0 || CandidateNumber > 400) throw new InvalidOperationException("CandidateNumber must be in [0,400].");
        if (DiagnosticTopK <= 0 || DiagnosticTopK > 10) throw new InvalidOperationException("DiagnosticTopK must be in (0,10].");
        if (MaxSampleIssues <= 0 || MaxSampleIssues > 1000) throw new InvalidOperationException("MaxSampleIssues must be in (0,1000].");
        if (MaxReportIssues <= 0 || MaxReportIssues > 20000) throw new InvalidOperationException("MaxReportIssues must be in (0,20000].");
    }
}
