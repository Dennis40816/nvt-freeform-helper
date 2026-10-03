namespace FreeformHelper.Application.Settings;

/// <summary>
/// A container class that aggregates all configuration settings for a Freeform Helper project.
/// This allows for easy serialization, deserialization, and validation of project-wide parameters.
/// </summary>
public sealed class ProjectSettings
{
    /// <summary>
    /// Gets or sets the settings related to the generation and configuration of the regular grid.
    /// </summary>
    public GridSettings Grid { get; set; } = new();

    /// <summary>
    /// Gets or sets the settings that control how regular grid pads are matched to CAD pads.
    /// </summary>
    public MatchingSettings Matching { get; set; } = new();

    /// <summary>
    /// Gets or sets the settings related to the visual representation and display of data in the UI.
    /// </summary>
    public ViewSettings View { get; set; } = new();

    /// <summary>
    /// Gets or sets the settings related to the generation of notch tables.
    /// </summary>
    public NotchSettings Notch { get; set; } = new();

    /// <summary>
    /// Gets or sets settings for DXF idx ↔ Regular idx mapping analysis.
    /// </summary>
    public IndexMappingSettings IndexMapping { get; set; } = new();

    /// <summary>
    /// Validates all aggregated settings and throws an <see cref="InvalidOperationException"/>
    /// if any of the settings or their sub-settings contain invalid values.
    /// This method performs a cascading validation call to each contained settings object.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if any of the project settings are found to be in an invalid state.
    /// </exception>
    public void ValidateOrThrow()
    {
        Grid.ValidateOrThrow();
        if (Matching.MatchThreshold < 0 || Matching.MatchThreshold > 1) throw new InvalidOperationException("MatchThreshold must be in [0,1].");
        if (Matching.FreeformAxisThreshold < 0 || Matching.FreeformAxisThreshold > 1) throw new InvalidOperationException("FreeformAxisThreshold must be in [0,1].");
        if (View.CadLineOpacity < 0 || View.CadLineOpacity > 1) throw new InvalidOperationException("CadLineOpacity must be in [0,1].");
        if (View.RegularLineOpacity < 0 || View.RegularLineOpacity > 1) throw new InvalidOperationException("RegularLineOpacity must be in [0,1].");
        if (View.AreaBucketTolerance < 0) throw new InvalidOperationException("AreaBucketTolerance must be non-negative.");
        if (!Enum.IsDefined(Notch.ComputationMode)) throw new InvalidOperationException("Notch ComputationMode is invalid.");
        if (!Enum.IsDefined(Notch.CompensationModel)) throw new InvalidOperationException("Notch CompensationModel is invalid.");
        if (Notch.LenScale <= 0) throw new InvalidOperationException("LenScale must be positive.");
        NotchSettings.ValidateNullValueOrThrow(Notch.NullValue);
        if (Notch.ThresholdQ7 < 0 || Notch.ThresholdQ7 > 128) throw new InvalidOperationException("Notch ThresholdQ7 must be in [0,128].");
        if (Notch.ThresholdPercentV22 < 0 || Notch.ThresholdPercentV22 > 100) throw new InvalidOperationException("Notch ThresholdPercentV22 must be in [0,100].");
        if (Notch.MultiOwnerStrictOverlapPercent < 0 || Notch.MultiOwnerStrictOverlapPercent > 100)
        {
            throw new InvalidOperationException("Notch MultiOwnerStrictOverlapPercent must be in [0,100].");
        }
        if (!Enum.IsDefined(Notch.ExportProfile))
        {
            throw new InvalidOperationException("Notch ExportProfile is invalid.");
        }
        IndexMapping.ValidateOrThrow();
    }
}
