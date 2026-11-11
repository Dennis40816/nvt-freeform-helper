using System.Globalization;
using FreeformHelper.Application.CadImport;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using NLog;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Decides grid rebuild source/flow and returns a normalized build outcome.
/// </summary>
public sealed class GridRebuildOrchestrator
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public static GridRebuildBuildResult Build(GridRebuildBuildRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.RegularSourceMode == RegularSourceMode.FromDxfLayer)
        {
            return BuildFromDxfLayer(request);
        }

        return BuildFromGeneratedGrid(request);
    }

    public static string BuildDxfRegularSourceMismatchHint(int padCount, int x, int y, int cascade) =>
        CadRegularSourceRules.BuildDxfRegularSourceMismatchHint(padCount, x, y, cascade);

    private static GridRebuildBuildResult BuildFromDxfLayer(GridRebuildBuildRequest request)
    {
        if (request.Cad is null)
        {
            return GridRebuildBuildResult.Failed(
                "DXF not loaded; unable to use DXF layer regular source.",
                "Load DXF first.\nThen select a regular source layer.");
        }

        if (string.IsNullOrWhiteSpace(request.RegularSourceLayerName))
        {
            return GridRebuildBuildResult.Failed(
                "Select a regular source layer.",
                "Select a DXF layer for regular source.");
        }

        var layerPads = request.Cad.Pads
            .Where(p => !request.HiddenCadPadIds.Contains(p.Id))
            .Where(p => string.Equals(p.Layer, request.RegularSourceLayerName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (layerPads.Count == 0)
        {
            return GridRebuildBuildResult.Failed(
                "Selected regular source layer has no visible pads.",
                "Selected regular layer has no pads.");
        }

        var expectedPadCount = request.Settings.XChannels * request.Settings.YChannels;
        if (layerPads.Count != expectedPadCount)
        {
            var mismatchHint = BuildDxfRegularSourceMismatchHint(
                layerPads.Count,
                request.Settings.XChannels,
                request.Settings.YChannels,
                request.Settings.CascadeNum);
            _logger.Warn(CultureInfo.InvariantCulture, "DXF regular source mismatch. {0}", mismatchHint);
            return GridRebuildBuildResult.Failed(
                $"DXF regular source mismatch. {mismatchHint}",
                $"Regular source count mismatch.\n{mismatchHint}",
                mismatchHint);
        }

        var cadLayerSet = new CadPadSet(layerPads);
        try
        {
            var grid = GridBuildService.BuildFromDxfLayer(cadLayerSet, request.Settings);
            PadOverrideService.ApplyFreeformOverrides(request.ProjectFile, grid);
            return GridRebuildBuildResult.Succeeded(grid, "Ready.");
        }
        catch (InvalidOperationException ex)
        {
            _logger.Warn(CultureInfo.InvariantCulture, "DXF regular source build skipped: {0}", ex.Message);
            var mismatchHint = BuildDxfRegularSourceMismatchHint(
                layerPads.Count,
                request.Settings.XChannels,
                request.Settings.YChannels,
                request.Settings.CascadeNum);
            return GridRebuildBuildResult.Failed(
                ex.Message,
                $"Regular source mismatch.\n{mismatchHint}",
                mismatchHint);
        }
    }

    private static GridRebuildBuildResult BuildFromGeneratedGrid(GridRebuildBuildRequest request)
    {
        var cadForBuild = request.GridAlignmentMode == GridAlignmentMode.FromCadBounds
            ? request.BuildCadPadSetForBounds()
            : request.Cad;

        if (request.GridAlignmentMode == GridAlignmentMode.FromCadBounds &&
            cadForBuild is not null &&
            cadForBuild.Pads.Count == 0)
        {
            _logger.Info(CultureInfo.InvariantCulture, "Grid build skipped: no CAD output pads for bounds.");
            return GridRebuildBuildResult.Failed(
                "No visible DXF pads (all layers off).",
                "No visible DXF pads.\nTurn on a layer.");
        }

        var effectiveAlignmentMode = request.GridAlignmentMode;
        var postBuildStatus = "Ready.";
        if (request.GridAlignmentMode == GridAlignmentMode.FromCadBounds && cadForBuild is null)
        {
            effectiveAlignmentMode = GridAlignmentMode.FromPanelAa;
            postBuildStatus = "Ready (DXF not loaded; using panel AA alignment).";
            _logger.Info(CultureInfo.InvariantCulture, "Grid alignment fallback: DXF bounds requested but no DXF loaded.");
        }

        var gridResult = GridBuildService.Build(cadForBuild, request.Settings, effectiveAlignmentMode);
        PadOverrideService.ApplyFreeformOverrides(request.ProjectFile, gridResult);
        return GridRebuildBuildResult.Succeeded(gridResult, postBuildStatus);
    }


}

public sealed class GridRebuildBuildRequest
{
    public required ProjectFile ProjectFile { get; init; }
    public required GridSettings Settings { get; init; }
    public required RegularSourceMode RegularSourceMode { get; init; }
    public required GridAlignmentMode GridAlignmentMode { get; init; }
    public required CadPadSet? Cad { get; init; }
    public required string? RegularSourceLayerName { get; init; }
    public required IReadOnlySet<int> HiddenCadPadIds { get; init; }
    public required Func<CadPadSet?> BuildCadPadSetForBounds { get; init; }
}

public sealed class GridRebuildBuildResult
{
    private GridRebuildBuildResult(bool success, RegularGrid? grid, string statusText, string? canvasHint, string? dxfRegularSourceHint)
    {
        Success = success;
        Grid = grid;
        StatusText = statusText;
        CanvasHint = canvasHint;
        DxfRegularSourceHint = dxfRegularSourceHint;
    }

    public bool Success { get; }
    public RegularGrid? Grid { get; }
    public string StatusText { get; }
    public string? CanvasHint { get; }
    public string? DxfRegularSourceHint { get; }

    public static GridRebuildBuildResult Succeeded(RegularGrid grid, string statusText)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentException.ThrowIfNullOrWhiteSpace(statusText);
        return new GridRebuildBuildResult(true, grid, statusText, null, null);
    }

    public static GridRebuildBuildResult Failed(string statusText, string canvasHint, string? dxfRegularSourceHint = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statusText);
        ArgumentException.ThrowIfNullOrWhiteSpace(canvasHint);
        return new GridRebuildBuildResult(false, null, statusText, canvasHint, dxfRegularSourceHint);
    }
}

