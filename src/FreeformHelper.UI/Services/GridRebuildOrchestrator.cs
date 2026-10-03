using System.Globalization;
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
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly GridBuildService _gridBuildService;
    private readonly PadOverrideService _padOverrideService;

    public GridRebuildOrchestrator(GridBuildService gridBuildService, PadOverrideService padOverrideService)
    {
        _gridBuildService = gridBuildService ?? throw new ArgumentNullException(nameof(gridBuildService));
        _padOverrideService = padOverrideService ?? throw new ArgumentNullException(nameof(padOverrideService));
    }

    public GridRebuildBuildResult Build(GridRebuildBuildRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.RegularSourceMode == RegularSourceMode.FromDxfLayer)
        {
            return BuildFromDxfLayer(request);
        }

        return BuildFromGeneratedGrid(request);
    }

    public static string BuildDxfRegularSourceMismatchHint(int padCount, int x, int y, int cascade)
    {
        var expected = x * y;
        var hints = new List<string>
        {
            $"Regular layer pads={padCount}, current X*Y={x}*{y}={expected}.",
        };

        int? keepYSuggestedX = null;
        if (y > 0 && padCount % y == 0)
        {
            keepYSuggestedX = padCount / y;
            hints.Add($"Keep Y={y} -> set X={keepYSuggestedX.Value}.");
        }

        int? keepXSuggestedY = null;
        if (x > 0 && padCount % x == 0)
        {
            keepXSuggestedY = padCount / x;
            hints.Add($"Keep X={x} -> set Y={keepXSuggestedY.Value}.");
        }

        var (bestX, bestY) = FindClosestFactorPair(padCount, x, y);
        if (keepYSuggestedX != bestX || keepXSuggestedY != bestY)
        {
            hints.Add($"Closest exact pair: X={bestX}, Y={bestY}.");
        }

        var hintXForCascade = keepYSuggestedX ?? bestX;
        if (cascade > hintXForCascade)
        {
            hints.Add($"Cascade={cascade} exceeds suggested X={hintXForCascade}; set Cascade <= {hintXForCascade}.");
        }
        else if (cascade > 1)
        {
            var perIc = SplitPerIcColumns(hintXForCascade, cascade);
            hints.Add($"With Cascade={cascade}, suggested per-IC X: {string.Join("/", perIc)}.");
        }

        return string.Join(" ", hints);
    }

    private GridRebuildBuildResult BuildFromDxfLayer(GridRebuildBuildRequest request)
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
            Logger.Warn(CultureInfo.InvariantCulture, "DXF regular source mismatch. {0}", mismatchHint);
            return GridRebuildBuildResult.Failed(
                $"DXF regular source mismatch. {mismatchHint}",
                $"Regular source count mismatch.\n{mismatchHint}",
                mismatchHint);
        }

        var cadLayerSet = new CadPadSet(layerPads);
        try
        {
            var grid = _gridBuildService.BuildFromDxfLayer(cadLayerSet, request.Settings);
            PadOverrideService.ApplyFreeformOverrides(request.ProjectFile, grid);
            return GridRebuildBuildResult.Succeeded(grid, "Ready.");
        }
        catch (InvalidOperationException ex)
        {
            Logger.Warn(CultureInfo.InvariantCulture, "DXF regular source build skipped: {0}", ex.Message);
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

    private GridRebuildBuildResult BuildFromGeneratedGrid(GridRebuildBuildRequest request)
    {
        var cadForBuild = request.GridAlignmentMode == GridAlignmentMode.FromCadBounds
            ? request.BuildCadPadSetForBounds()
            : request.Cad;

        if (request.GridAlignmentMode == GridAlignmentMode.FromCadBounds &&
            cadForBuild is not null &&
            cadForBuild.Pads.Count == 0)
        {
            Logger.Info(CultureInfo.InvariantCulture, "Grid build skipped: no CAD output pads for bounds.");
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
            Logger.Info(CultureInfo.InvariantCulture, "Grid alignment fallback: DXF bounds requested but no DXF loaded.");
        }

        var gridResult = _gridBuildService.Build(cadForBuild, request.Settings, effectiveAlignmentMode);
        PadOverrideService.ApplyFreeformOverrides(request.ProjectFile, gridResult);
        return GridRebuildBuildResult.Succeeded(gridResult, postBuildStatus);
    }

    private static (int X, int Y) FindClosestFactorPair(int product, int preferX, int preferY)
    {
        if (product <= 0)
        {
            return (Math.Max(1, preferX), Math.Max(1, preferY));
        }

        var bestX = product;
        var bestY = 1;
        var bestDistance = int.MaxValue;
        var bestAspectDistance = double.MaxValue;
        var targetAspect = preferY > 0 ? (double)preferX / preferY : 1.0;

        for (var d = 1; d * d <= product; d++)
        {
            if (product % d != 0)
            {
                continue;
            }

            var candidates = new[]
            {
                (X: product / d, Y: d),
                (X: d, Y: product / d),
            };

            foreach (var candidate in candidates)
            {
                var distance = Math.Abs(candidate.X - preferX) + Math.Abs(candidate.Y - preferY);
                var aspect = candidate.Y > 0 ? (double)candidate.X / candidate.Y : double.MaxValue;
                var aspectDistance = Math.Abs(aspect - targetAspect);
                if (distance < bestDistance ||
                    (distance == bestDistance && aspectDistance < bestAspectDistance))
                {
                    bestDistance = distance;
                    bestAspectDistance = aspectDistance;
                    bestX = candidate.X;
                    bestY = candidate.Y;
                }
            }
        }

        return (bestX, bestY);
    }

    private static List<int> SplitPerIcColumns(int totalX, int cascade)
    {
        var safeCascade = Math.Clamp(cascade, 1, Math.Max(1, totalX));
        var baseCols = totalX / safeCascade;
        var remainder = totalX % safeCascade;
        var result = new List<int>(safeCascade);
        for (var ic = 0; ic < safeCascade; ic++)
        {
            result.Add(baseCols + (ic < remainder ? 1 : 0));
        }

        return result;
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

