namespace FreeformHelper.Application.Settings;

/// <summary>
/// Defines persisted matching and freeform-detection settings.
/// </summary>
public sealed class MatchingSettings
{
    /// <summary>
    /// Gets or sets the retired match-mode value retained for project compatibility.
    /// The current overlap matcher does not branch on this value.
    /// </summary>
    public MatchMode Mode { get; set; } = MatchMode.LegacyOverlap;

    /// <summary>
    /// Gets or sets the display threshold used to highlight low-coverage regular pads
    /// as unmatched. It does not change geometry links or match scores.
    /// Default is 0.35 (35%).
    /// </summary>
    public double MatchThreshold { get; set; } = 0.35;

    /// <summary>
    /// Gets or sets the retired centroid-fallback value retained for project compatibility.
    /// The current overlap matcher does not read this value.
    /// </summary>
    public bool EnableCentroidFallback { get; set; } = true;

    /// <summary>
    /// Gets or sets the retired nearest-K value retained for project compatibility.
    /// The current overlap matcher does not read this value.
    /// </summary>
    public int NearestK { get; set; } = 5;

    /// <summary>
    /// Gets or sets the minimum directional spread ratio used by freeform auto-detection.
    /// The detector computes X/Y spread from CAD→Regular overlap coverage and marks a pad
    /// as freeform only when the dominant direction is greater than this threshold.
    /// Default is 0.1.
    /// </summary>
    public double FreeformAxisThreshold { get; set; } = 0.1;

    /// <summary>
    /// Gets or sets a value indicating whether freeform auto-detection can classify XYWay.
    /// Default is <c>false</c> to keep X/Y/None behavior.
    /// </summary>
    public bool EnableAutoDetectXy { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether boundary one-side tiny spill should be
    /// preserved as freeform when the global TH would classify it as None.
    /// This helps edge cells while still suppressing two-side jitter.
    /// Default is <c>false</c>.
    /// </summary>
    public bool EnableFreeformEdgeSpecialization { get; set; }
}
