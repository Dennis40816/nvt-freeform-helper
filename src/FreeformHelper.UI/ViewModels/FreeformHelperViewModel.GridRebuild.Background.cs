using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private GridRebuildBuildRequest CreateGridRebuildBuildRequestSnapshot()
    {
        var fullCadSnapshot = _cad;
        var boundsCadSnapshot = GridAlignmentMode == GridAlignmentMode.FromCadBounds
            ? BuildCadPadSetForBounds()
            : fullCadSnapshot;

        return new GridRebuildBuildRequest
        {
            ProjectFile = CreateGridRebuildProjectSnapshot(),
            Settings = CloneGridSettings(_projectFile.Settings.Grid),
            RegularSourceMode = RegularSourceMode,
            GridAlignmentMode = GridAlignmentMode,
            Cad = fullCadSnapshot,
            RegularSourceLayerName = SelectedRegularSourceLayerOption?.Name,
            HiddenCadPadIds = BuildEffectiveHiddenCadPadIds(),
            BuildCadPadSetForBounds = () => boundsCadSnapshot,
        };
    }

    private ProjectFile CreateGridRebuildProjectSnapshot()
    {
        return new ProjectFile
        {
            FreeformOverrides = new Dictionary<int, FreeformType>(_projectFile.FreeformOverrides),
        };
    }

    private static GridSettings CloneGridSettings(GridSettings source)
    {
        return new GridSettings
        {
            XChannels = source.XChannels,
            YChannels = source.YChannels,
            WidthScope = source.WidthScope,
            HeightScope = source.HeightScope,
            CascadeNum = source.CascadeNum,
            PerIcXChannels = new List<int>(source.PerIcXChannels),
            PerIcYChannels = new List<int>(source.PerIcYChannels),
            AlignmentMode = source.AlignmentMode,
            ScanOrder = source.ScanOrder,
            AfeOverlap = source.AfeOverlap,
            RegularSourceMode = source.RegularSourceMode,
            RegularSourceLayerName = source.RegularSourceLayerName,
            ColumnWidths = new List<double>(source.ColumnWidths),
            ColumnOverrides = new List<bool>(source.ColumnOverrides),
            RowHeights = new List<double>(source.RowHeights),
            RowOverrides = new List<bool>(source.RowOverrides),
            RowWidthOverrides = source.RowWidthOverrides.Select(static row => new List<double>(row)).ToList(),
            RowWidthOverrideFlags = source.RowWidthOverrideFlags.Select(static row => new List<bool>(row)).ToList(),
            ColumnHeightOverrides = source.ColumnHeightOverrides.Select(static column => new List<double>(column)).ToList(),
            ColumnHeightOverrideFlags = source.ColumnHeightOverrideFlags.Select(static column => new List<bool>(column)).ToList(),
            BoundsPaddingRatio = source.BoundsPaddingRatio,
            MaxChannelsX = source.MaxChannelsX,
            MaxChannelsY = source.MaxChannelsY,
            ActiveAreaWidth = source.ActiveAreaWidth,
            ActiveAreaHeight = source.ActiveAreaHeight,
            PanelBiasX = source.PanelBiasX,
            PanelBiasY = source.PanelBiasY,
            BoundLayerName = source.BoundLayerName,
            RecalcBoundsOnLayerFilter = source.RecalcBoundsOnLayerFilter,
        };
    }
}
