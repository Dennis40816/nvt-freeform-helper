namespace FreeformHelper.Infrastructure.Dxf;

/// <summary>
/// Represents options that control how DXF files are imported.
/// </summary>
public sealed class DxfImportOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether only closed polylines should be imported from the DXF file.
    /// If <c>true</c>, open polylines will be ignored during import.
    /// Default is <c>true</c>.
    /// </summary>
    public bool OnlyClosedPolylines { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether closed polylines from the DXF BLOCKS section
    /// should also be imported as pads.
    /// Default is <c>true</c>.
    /// </summary>
    public bool IncludeBlockPolylines { get; set; } = true;
}
