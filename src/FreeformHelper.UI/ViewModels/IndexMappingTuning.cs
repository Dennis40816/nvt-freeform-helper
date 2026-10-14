namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Holds the index mapping values that the settings edit as plain numbers: the three scoring
/// weights, the two suggestion thresholds, the legacy padding cell count and the candidate number.
/// The view model replaces the whole value when one of them changes, and forwards one property
/// per member so the bindings keep their names.
/// </summary>
/// <param name="WeightIou">The IoU weight used for DXF/Regular mapping scoring.</param>
/// <param name="WeightCentroidDistance">The centroid distance weight used for mapping scoring.</param>
/// <param name="WeightAreaRatio">The area ratio weight used for mapping scoring.</param>
/// <param name="LowConfidenceThreshold">The low-confidence score threshold for mapping suggestions.</param>
/// <param name="AmbiguousMargin">The ambiguity margin threshold (best minus second best).</param>
/// <param name="CandidatePaddingCells">The legacy number of candidate padding cells, kept for project compatibility.</param>
/// <param name="CandidateNumber">The number of retained candidates per CAD pad.</param>
public sealed record IndexMappingTuning(
    decimal WeightIou,
    decimal WeightCentroidDistance,
    decimal WeightAreaRatio,
    decimal LowConfidenceThreshold,
    decimal AmbiguousMargin,
    decimal CandidatePaddingCells,
    decimal CandidateNumber)
{
    /// <summary>
    /// Gets the value before any project settings were loaded.
    /// </summary>
    public static IndexMappingTuning Initial { get; } = new(0m, 0m, 0m, 0m, 0m, 0m, 9m);
}
