namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// This partial class of <see cref="FreeformHelperViewModel"/> contains nested types
/// that serve as data models or option structures specifically used within the UI layer.
/// </summary>
public sealed partial class FreeformHelperViewModel
{
    /// <summary>
    /// Represents supported notch export file types.
    /// </summary>
    public enum NotchExportFileType
    {
        Csv,
        Cv21,
        Cv22,
    }

    /// <summary>
    /// Represents a coarse DXF layer category used for batch layer toggles.
    /// </summary>
    public enum DxfLayerCategory
    {
        Polyline,
        Block,
        Text,
    }
}
