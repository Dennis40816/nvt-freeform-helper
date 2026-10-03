namespace FreeformHelper.Application.Settings;

/// <summary>
/// Defines how regular pads are sourced when building the workspace grid.
/// </summary>
public enum RegularSourceMode
{
    /// <summary>
    /// Generate a regular grid from panel/cad bounds based on channel settings.
    /// </summary>
    GeneratedGrid = 0,

    /// <summary>
    /// Use polygons from a selected DXF layer as regular pads.
    /// </summary>
    FromDxfLayer = 1,
}
