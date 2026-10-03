namespace FreeformHelper.Application.Settings;

/// <summary>
/// Defines settings related to how data is visually presented in the user interface.
/// </summary>
public sealed class ViewSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether loading a project should automatically
    /// replay Step 2 freeform auto-detect after Step 1 match is restored.
    /// Default is <c>true</c>.
    /// </summary>
    public bool AutoReplayStep2AfterProjectLoad { get; set; } = true;

    /// <summary>
    /// Gets or sets the opacity (0-1) for CAD fill rendering.
    /// </summary>
    public double CadFillOpacity { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the opacity (0-1) for CAD line rendering.
    /// </summary>
    public double CadLineOpacity { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the opacity (0-1) for regular pad fill rendering.
    /// </summary>
    public double RegularFillOpacity { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the opacity (0-1) for regular pad line rendering.
    /// </summary>
    public double RegularLineOpacity { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the relative tolerance used for grouping pad areas into "buckets" for colorization.
    /// Pads whose areas are within this tolerance of each other will be considered part of the same bucket
    /// and thus share the same display color.
    /// Default is 0.001 (0.1% relative tolerance).
    /// </summary>
    public double AreaBucketTolerance { get; set; } = 0.001; // relative tolerance

    /// <summary>
    /// Gets or sets the maximum number of distinct area buckets that will be assigned unique colors.
    /// If there are more distinct area buckets than this maximum, the colors will cycle
    /// or be reused for subsequent buckets.
    /// Default is 32.
    /// </summary>
    public int MaxAreaBuckets { get; set; } = 32;
}
