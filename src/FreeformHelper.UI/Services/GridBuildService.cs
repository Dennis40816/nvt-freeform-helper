using System.Diagnostics.CodeAnalysis;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Provides a service layer for building <see cref="RegularGrid"/> objects.
/// This service orchestrates the process of grid construction and diff-index mapping.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1822:Mark members as static",
    Justification = "Instance service API is retained for workflow composition stability.")]
public sealed class GridBuildService
{
    // Private instances of the underlying application services for grid building and diff-index mapping.
    private readonly RegularGridBuilder _gridBuilder = new();
    private readonly DxfRegularLayerGridBuilder _dxfRegularLayerGridBuilder = new();
    private readonly DiffIndexMapper _diffIndexMapper = new();

    /// <summary>
    /// Builds a <see cref="RegularGrid"/> based on the provided CAD pads (optional),
    /// grid settings, and the specified alignment mode.
    /// </summary>
    /// <param name="cad">The <see cref="CadPadSet"/> to use for determining bounds if <paramref name="alignmentMode"/> is <see cref="GridAlignmentMode.FromCadBounds"/>.</param>
    /// <param name="settings">The <see cref="GridSettings"/> containing parameters for grid construction.</param>
    /// <param name="alignmentMode">The <see cref="GridAlignmentMode"/> to dictate how the grid's dimensions are established.</param>
    /// <returns>A newly constructed <see cref="RegularGrid"/>.</returns>
    public RegularGrid Build(CadPadSet? cad, GridSettings settings, GridAlignmentMode alignmentMode)
    {
        RegularGrid grid;

        // Decide whether to build the grid from CAD bounds or from explicit settings.
        if (alignmentMode == GridAlignmentMode.FromCadBounds && cad is not null)
        {
            grid = _gridBuilder.BuildFromCadBounds(cad, settings);
        }
        else
        {
            grid = _gridBuilder.BuildFromSettings(settings, alignmentMode);
        }

        // Apply diff-index mapping to the newly built grid.
        DiffIndexMapper.ApplyMapping(grid, settings);
        return grid;
    }

    /// <summary>
    /// Builds a <see cref="RegularGrid"/> by using polygons from a selected DXF layer as regular pads.
    /// </summary>
    /// <param name="layerPads">CAD pads from the selected regular source layer.</param>
    /// <param name="settings">The <see cref="GridSettings"/> containing channel and scan settings.</param>
    /// <returns>A newly constructed <see cref="RegularGrid"/>.</returns>
    public RegularGrid BuildFromDxfLayer(CadPadSet layerPads, GridSettings settings)
    {
        var grid = DxfRegularLayerGridBuilder.BuildFromLayer(layerPads, settings);
        DiffIndexMapper.ApplyMapping(grid, settings);
        return grid;
    }
}
