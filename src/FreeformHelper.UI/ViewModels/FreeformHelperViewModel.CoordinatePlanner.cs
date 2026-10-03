using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    public event EventHandler? WorkspaceDerivedSourceChanged
    {
        add => SimulationWorkspaceSourceChanged += value;
        remove => SimulationWorkspaceSourceChanged -= value;
    }

    public int WorkspaceDerivedSourceRevision => SimulationWorkspaceSourceRevision;

    public Task<CoordinatePlannerWorkspaceSession?> CreateCoordinatePlannerWorkspaceSessionAsync(bool silent = false)
    {
        CadPadSet? activeCad;
        RegularGrid? grid;
        if (silent)
        {
            grid = _grid;
            activeCad = BuildActiveCadPadSet();
            if (grid is null || activeCad is null || activeCad.Pads.Count == 0)
            {
                return Task.FromResult<CoordinatePlannerWorkspaceSession?>(null);
            }
        }
        else if (!TryGetOperationCadAndGrid(
                     BuildActiveCadPadSet,
                     "Coordinate: import DXF and build grid first.",
                     out activeCad!,
                     out grid!))
        {
            return Task.FromResult<CoordinatePlannerWorkspaceSession?>(null);
        }

        var canvasCadPads = activeCad.Pads
            .OrderBy(static pad => pad.Id)
            .ToArray();
        var allCadPadsForLayerOptions = (_cad?.Pads ?? activeCad.Pads)
            .Where(pad => !_autoHiddenDuplicateCadPadIds.Contains(pad.Id))
            .ToArray();
        var layerNames = allCadPadsForLayerOptions
            .Select(static pad => pad.Layer)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        var preferredActiveAreaOutlineLayerName = ResolveCoordinatePreferredAaOutlineLayerName(layerNames);
        var coordinatePixelWidth = Math.Max(
            1,
            (int)Math.Round(
                CoordinatePixelWidth > 0m ? CoordinatePixelWidth : XChannels,
                MidpointRounding.AwayFromZero));
        var coordinatePixelHeight = Math.Max(
            1,
            (int)Math.Round(
                CoordinatePixelHeight > 0m ? CoordinatePixelHeight : YChannels,
                MidpointRounding.AwayFromZero));

        var session = new CoordinatePlannerWorkspaceSession(
            grid,
            canvasCadPads,
            layerNames,
            WorkspaceDerivedSourceRevision,
            Math.Max(0.0, (double)ActiveAreaWidth),
            Math.Max(0.0, (double)ActiveAreaHeight),
            coordinatePixelWidth,
            coordinatePixelHeight,
            preferredActiveAreaOutlineLayerName,
            ShowRegular,
            HighlightUnmatched,
            HighlightFreeform);
        return Task.FromResult<CoordinatePlannerWorkspaceSession?>(session);
    }

    internal void ApplyCoordinatePlannerPreferences(
        int pixelWidth,
        int pixelHeight,
        string? preferredActiveAreaOutlineLayerName)
    {
        var normalizedPixelWidth = Math.Max(1, pixelWidth);
        var normalizedPixelHeight = Math.Max(1, pixelHeight);
        var normalizedLayerName = preferredActiveAreaOutlineLayerName?.Trim() ?? string.Empty;

        if (CoordinatePixelWidth != normalizedPixelWidth)
        {
            CoordinatePixelWidth = normalizedPixelWidth;
        }

        if (CoordinatePixelHeight != normalizedPixelHeight)
        {
            CoordinatePixelHeight = normalizedPixelHeight;
        }

        if (!string.Equals(
                CoordinatePreferredAaOutlineLayerName,
                normalizedLayerName,
                StringComparison.Ordinal))
        {
            CoordinatePreferredAaOutlineLayerName = normalizedLayerName;
        }
    }

    private string? ResolveCoordinatePreferredAaOutlineLayerName(IReadOnlyCollection<string> layerNames)
    {
        return CoordinatePlannerActiveAreaResolver.ResolvePreferredLayerName(
            layerNames,
            CoordinatePreferredAaOutlineLayerName);
    }
}
