namespace FreeformHelper.Application.Settings;

/// <summary>
/// Specifies how the regular grid should be aligned or positioned.
/// </summary>
public enum GridAlignmentMode
{
    /// <summary>
    /// The grid's origin (MinX, MinY) is determined by the panel's active area bias settings.
    /// </summary>
    FromPanelAa = 0,
    /// <summary>
    /// The grid's origin (MinX, MinY) is derived from the bounding box of the imported CAD pads.
    /// </summary>
    FromCadBounds = 1,
}
