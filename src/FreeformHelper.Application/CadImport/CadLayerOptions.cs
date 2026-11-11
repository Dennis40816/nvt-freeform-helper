namespace FreeformHelper.Application.CadImport;

/// <summary>
/// Represents an option for selecting a DXF layer to use as grid bounds.
/// </summary>
/// <param name="Name">The layer name (null means auto/visible layers).</param>
/// <param name="Display">The human-readable string for display in the UI.</param>
public sealed record BoundLayerOption(string? Name, string Display);

/// <summary>
/// Represents an option for selecting a DXF layer as regular source.
/// </summary>
/// <param name="Name">The layer name (null means no layer selected).</param>
/// <param name="Display">The human-readable string for display in the UI.</param>
public sealed record RegularSourceLayerOption(string? Name, string Display);

/// <summary>
/// Represents a coarse DXF layer category used for batch layer toggles.
/// </summary>
public enum DxfLayerCategory
{
    Polyline,
    Block,
    Text,
}

/// <summary>
/// Represents one DXF layer category state summary in current layer toggles.
/// </summary>
/// <param name="Category">Category enum.</param>
/// <param name="Display">Display label.</param>
/// <param name="Description">Short category note.</param>
/// <param name="TotalCount">Total layer count in this category.</param>
/// <param name="SelectedCount">Selected layer count in this category.</param>
public readonly record struct DxfLayerCategoryState(
    DxfLayerCategory Category,
    string Display,
    string Description,
    int TotalCount,
    int SelectedCount);

