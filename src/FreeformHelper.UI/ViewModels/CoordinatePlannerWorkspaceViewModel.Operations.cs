using System.Globalization;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class CoordinatePlannerWorkspaceViewModel
{
    private async Task ExportPreviewPngAsync()
    {
        if (RequestExportPreviewPngAsync is null)
        {
            StatusText = "Coordinate PNG export is unavailable in the current host.";
            return;
        }

        var exported = await RequestExportPreviewPngAsync();
        StatusText = exported
            ? "Coordinate preview exported."
            : "Coordinate preview export cancelled.";
    }

    private async Task CopyArtifactsAsync()
    {
        if (SelectedArtifactRow is null)
        {
            await CopyAllArtifactsAsync();
            return;
        }

        await CopySelectedArtifactAsync();
    }

    private async Task CopySelectedArtifactAsync()
    {
        if (SelectedArtifactRow is null)
        {
            StatusText = "Select a coordinate row before copying a selected artifact.";
            return;
        }

        await CopyArtifactRowsAsync([SelectedArtifactRow], selectedOnly: true);
    }

    private Task CopyAllArtifactsAsync()
    {
        return CopyArtifactRowsAsync(ArtifactRows.ToArray(), selectedOnly: false);
    }

    private async Task CopyArtifactRowsAsync(
        CoordinateArtifactRow[] rows,
        bool selectedOnly)
    {
        if (RequestSetClipboardTextAsync is null)
        {
            StatusText = "Coordinate clipboard copy is unavailable in the current host.";
            return;
        }

        if (rows.Length == 0)
        {
            StatusText = "No coordinate rows are available to copy.";
            return;
        }

        var copied = await RequestSetClipboardTextAsync(CoordinateArtifactService.ExportClipboardTable(rows));
        StatusText = copied
            ? BuildExportStatus("Copied", rows.Length, selectedOnly)
            : "Coordinate clipboard copy failed.";
    }

    private Task ExportArtifactsCsvAsync()
    {
        var rows = ArtifactRows.ToArray();
        return SaveArtifactsTextAsync(
            "Export Coordinate Artifacts CSV",
            "coordinate_artifacts.csv",
            "csv",
            "CSV",
            ["*.csv"],
            CoordinateArtifactService.ExportCsv(rows),
            rows.Length);
    }

    private Task ExportArtifactsJsonAsync()
    {
        var rows = ArtifactRows.ToArray();
        return SaveArtifactsTextAsync(
            "Export Coordinate Artifacts JSON",
            "coordinate_artifacts.json",
            "json",
            "JSON",
            ["*.json"],
            CoordinateArtifactService.ExportJson(_artifactSnapshot with { Rows = rows }),
            rows.Length);
    }

    private async Task SaveArtifactsTextAsync(
        string title,
        string suggestedFileName,
        string defaultExtension,
        string fileTypeName,
        IReadOnlyList<string> patterns,
        string content,
        int rowCount)
    {
        if (RequestSaveTextFileAsync is null)
        {
            StatusText = "Coordinate text export is unavailable in the current host.";
            return;
        }

        if (rowCount == 0)
        {
            StatusText = "No coordinate rows are available to export.";
            return;
        }

        var request = new CoordinatePlannerTextExportRequest(
            title,
            suggestedFileName,
            defaultExtension,
            fileTypeName,
            patterns,
            content);
        var exported = await RequestSaveTextFileAsync(request);
        StatusText = exported
            ? BuildExportStatus($"Exported {fileTypeName}", rowCount, selectedOnly: false)
            : $"Coordinate {fileTypeName} export cancelled.";
    }

    private void AddCustomPoint()
    {
        var label = NormalizeCustomLabel(CustomPointLabelText, "Point");
        var key = (_nextCustomPointId++).ToString(CultureInfo.InvariantCulture);
        _customPointRequests.Add(new CoordinateCustomPointRequest(
            key,
            label,
            (double)CustomPointMachineX,
            (double)CustomPointMachineY));
        RefreshCustomRecipeRows();
        RebuildSnapshot();
        StatusText = $"Custom point '{label}' added.";
    }

    private void AddCustomPath()
    {
        var label = NormalizeCustomLabel(CustomPathLabelText, "Path");
        var key = (_nextCustomPathId++).ToString(CultureInfo.InvariantCulture);
        _customPathRequests.Add(new CoordinateCustomPathRequest(
            key,
            label,
            (double)CustomPathStartMachineX,
            (double)CustomPathStartMachineY,
            (double)CustomPathEndMachineX,
            (double)CustomPathEndMachineY,
            Math.Max(2, ClampNonNegativeInt(CustomPathStepCount))));
        RefreshCustomRecipeRows();
        RebuildSnapshot();
        StatusText = $"Custom path '{label}' added.";
    }

    private void ClearCustomArtifacts()
    {
        _customPointRequests.Clear();
        _customPathRequests.Clear();
        RefreshCustomRecipeRows();
        RebuildSnapshot();
        StatusText = "Custom coordinate artifacts cleared.";
    }

    private void SyncFromWorkspace()
    {
        MachineOriginX = 0m;
        MachineOriginY = 0m;
        MachineWidth = (decimal)_session.DefaultMachineWidth;
        MachineHeight = (decimal)_session.DefaultMachineHeight;
        PixelWidth = _session.DefaultPixelWidth;
        PixelHeight = _session.DefaultPixelHeight;
        UseActiveAreaSafeCorners();
        StatusText = "Coordinate planner synced from current workspace.";
    }

    private void UseActiveAreaSafeCorners()
    {
        var topLeft = _snapshot.Points.FirstOrDefault(static point => point.Key == "aa-tl");
        var topRight = _snapshot.Points.FirstOrDefault(static point => point.Key == "aa-tr");
        var bottomRight = _snapshot.Points.FirstOrDefault(static point => point.Key == "aa-br");
        var bottomLeft = _snapshot.Points.FirstOrDefault(static point => point.Key == "aa-bl");
        if (topLeft is null ||
            topRight is null ||
            bottomRight is null ||
            bottomLeft is null)
        {
            StatusText = "AA corners are unavailable for 4-point array initialization.";
            return;
        }

        SetCustomArrayCorners(
            topLeft.SafeMachineX,
            topLeft.SafeMachineY,
            topRight.SafeMachineX,
            topRight.SafeMachineY,
            bottomRight.SafeMachineX,
            bottomRight.SafeMachineY,
            bottomLeft.SafeMachineX,
            bottomLeft.SafeMachineY);
        ShowCustomArray = true;
        StatusText = "4-point array corners synced from current safe AA corners.";
    }

    private void UpdateCadPadsForCanvas()
    {
        _cadPadsForCanvas = SelectedLayerOption.Mode switch
        {
            CoordinatePlannerCadLayerMode.None => Array.Empty<CadPad>(),
            CoordinatePlannerCadLayerMode.AllVisible => _session.CadPads.ToArray(),
            CoordinatePlannerCadLayerMode.SingleLayer => _session.CadPads
                .Where(pad => string.Equals(pad.Layer, SelectedLayerOption.LayerName, StringComparison.Ordinal))
                .ToArray(),
            _ => Array.Empty<CadPad>(),
        };

        OnPropertyChanged(nameof(CadPadsForCanvas));
        OnPropertyChanged(nameof(ShowCad));
        OnPropertyChanged(nameof(LayerSummaryText));
    }

    private void RebuildSnapshot()
    {
        var selectedArtifactKey = SelectedArtifactRow?.Key;
        var result = CoordinatePlannerWorkspaceUseCase.BuildSnapshot(
            _session,
            BuildCurrentRequest(),
            ResolveSelectedAaOutlineLayerName(),
            ToGuideBasisKind(SelectedGuideBasisOption.Mode));
        _snapshot = result.Snapshot;
        _previewBounds = Rect2.Union(result.Snapshot.ActiveAreaBounds, result.GuideBounds);
        _activeAreaSourceText = result.ActiveAreaSourceText;
        _guideSourceText = result.GuideSourceText;
        _artifactSnapshot = CoordinateArtifactService.BuildSnapshot(
            _snapshot,
            result.GuideBounds,
            _activeAreaSourceText,
            _guideSourceText);
        AaCornerRows = _snapshot.Points
            .Where(static point => point.Kind == CoordinatePlannerPointKind.AaCorner)
            .Select(static point => new CoordinatePlannerPointRow(
                point.Label,
                "AA corner",
                $"({point.PixelX:0.###}, {point.PixelY:0.###})",
                $"({point.MachineX:0.###}, {point.MachineY:0.###})",
                $"({point.SafeMachineX:0.###}, {point.SafeMachineY:0.###})"))
            .ToArray();
        BistCornerRows = _snapshot.Points
            .Where(static point => point.Kind == CoordinatePlannerPointKind.BistCorner)
            .Select(static point => new CoordinatePlannerPointRow(
                point.Label,
                "BIST corner",
                $"({point.PixelX:0.###}, {point.PixelY:0.###})",
                $"({point.MachineX:0.###}, {point.MachineY:0.###})",
                $"({point.SafeMachineX:0.###}, {point.SafeMachineY:0.###})"))
            .ToArray();
        CustomArrayCornerRows = _snapshot.Points
            .Where(static point => point.Kind == CoordinatePlannerPointKind.CustomArrayCorner)
            .Select(static point => new CoordinatePlannerPointRow(
                point.Label,
                "4-point corner",
                $"({point.PixelX:0.###}, {point.PixelY:0.###})",
                $"({point.MachineX:0.###}, {point.MachineY:0.###})",
                $"({point.SafeMachineX:0.###}, {point.SafeMachineY:0.###})"))
            .ToArray();
        CustomArrayPointRows = _snapshot.Points
            .Where(static point => point.Kind == CoordinatePlannerPointKind.CustomArrayDot)
            .Select(static point => new CoordinatePlannerPointRow(
                point.Label,
                "Array dot",
                $"({point.PixelX:0.###}, {point.PixelY:0.###})",
                $"({point.MachineX:0.###}, {point.MachineY:0.###})",
                $"({point.SafeMachineX:0.###}, {point.SafeMachineY:0.###})"))
            .ToArray();
        HorizontalGuideRows = _snapshot.Lines
            .Where(static line => line.Kind == CoordinatePlannerLineKind.HorizontalGuide)
            .Select(static line => new CoordinatePlannerLineRow(
                line.Label,
                $"({line.StartPixelX:0.###}, {line.StartPixelY:0.###}) -> ({line.EndPixelX:0.###}, {line.EndPixelY:0.###})",
                $"({line.StartMachineX:0.###}, {line.StartMachineY:0.###}) -> ({line.EndMachineX:0.###}, {line.EndMachineY:0.###})",
                $"({line.SafeStartMachineX:0.###}, {line.SafeStartMachineY:0.###}) -> ({line.SafeEndMachineX:0.###}, {line.SafeEndMachineY:0.###})"))
            .ToArray();
        VerticalGuideRows = _snapshot.Lines
            .Where(static line => line.Kind == CoordinatePlannerLineKind.VerticalGuide)
            .Select(static line => new CoordinatePlannerLineRow(
                line.Label,
                $"({line.StartPixelX:0.###}, {line.StartPixelY:0.###}) -> ({line.EndPixelX:0.###}, {line.EndPixelY:0.###})",
                $"({line.StartMachineX:0.###}, {line.StartMachineY:0.###}) -> ({line.EndMachineX:0.###}, {line.EndMachineY:0.###})",
                $"({line.SafeStartMachineX:0.###}, {line.SafeStartMachineY:0.###}) -> ({line.SafeEndMachineX:0.###}, {line.SafeEndMachineY:0.###})"))
            .ToArray();
        ArtifactRows = SortArtifactRows(_artifactSnapshot.Rows);
        SelectedArtifactRow = ArtifactRows.FirstOrDefault(row => string.Equals(row.Key, selectedArtifactKey, StringComparison.Ordinal));

        StatusText = string.Format(
            CultureInfo.InvariantCulture,
            "Coordinate planner ready. Corners {0}, guides {1}, BIST {2}, array dots {3}.",
            AaCornerRows.Count,
            HorizontalGuideRows.Count + VerticalGuideRows.Count,
            HasBistRectangle ? "on" : "off",
            CustomArrayPointRows.Count);

        OnPropertyChanged(nameof(Snapshot));
        OnPropertyChanged(nameof(ArtifactSnapshot));
        OnPropertyChanged(nameof(PreviewBounds));
        OnPropertyChanged(nameof(AaCornerRows));
        OnPropertyChanged(nameof(BistCornerRows));
        OnPropertyChanged(nameof(CustomArrayCornerRows));
        OnPropertyChanged(nameof(CustomArrayPointRows));
        OnPropertyChanged(nameof(HorizontalGuideRows));
        OnPropertyChanged(nameof(VerticalGuideRows));
        OnPropertyChanged(nameof(ArtifactRows));
        OnPropertyChanged(nameof(HasBistRectangle));
        OnPropertyChanged(nameof(HasCustomArray));
        OnPropertyChanged(nameof(MachineSummaryText));
        OnPropertyChanged(nameof(PixelSummaryText));
        OnPropertyChanged(nameof(CopperSummaryText));
        OnPropertyChanged(nameof(GuideSummaryText));
        OnPropertyChanged(nameof(ArtifactSummaryText));
        OnPropertyChanged(nameof(ArtifactSortStatusText));
        OnPropertyChanged(nameof(SafeCoordinatePolicyText));
        OnPropertyChanged(nameof(BistRectangleSummaryText));
        OnPropertyChanged(nameof(CustomArraySummaryText));
        OnPropertyChanged(nameof(SelectedCustomArrayCornerMachineX));
        OnPropertyChanged(nameof(SelectedCustomArrayCornerMachineY));
        OnPropertyChanged(nameof(SelectedCustomArrayCornerSummaryText));
        OnPropertyChanged(nameof(HasCustomArtifacts));
        OnPropertyChanged(nameof(CustomArtifactSummaryText));
        OnPropertyChanged(nameof(ActiveAreaSourceSummaryText));
        OnPropertyChanged(nameof(GuideSourceSummaryText));
    }

    private Task ShowArtifactDetailAsync(CoordinateArtifactRow? row)
    {
        if (row is null)
        {
            return Task.CompletedTask;
        }

        SelectedArtifactRow = row;
        var request = RequestArtifactDetailAsync;
        if (request is null) return Task.CompletedTask;
        return UiEvents?.RunAsync("CoordinatePlanner.ArtifactDetail", _ => request(row), CancellationToken.None)
            ?? request(row);
    }

    private void SortArtifactRows(string? key)
    {
        var normalizedKey = string.IsNullOrWhiteSpace(key) ? "Default" : key.Trim();
        if (string.Equals(ArtifactSortKey, normalizedKey, StringComparison.Ordinal))
        {
            ArtifactSortDescending = !ArtifactSortDescending;
        }
        else
        {
            ArtifactSortKey = normalizedKey;
            ArtifactSortDescending = false;
        }

        ArtifactRows = SortArtifactRows(ArtifactRows);
        OnPropertyChanged(nameof(ArtifactRows));
        OnPropertyChanged(nameof(ArtifactSortStatusText));
    }

    private CoordinateArtifactRow[] SortArtifactRows(
        IEnumerable<CoordinateArtifactRow> rows)
    {
        var sortedRows = ArtifactSortKey switch
        {
            "Kind" => rows.OrderBy(static row => row.Kind, StringComparer.Ordinal)
                .ThenBy(static row => row.Recipe, StringComparer.Ordinal)
                .ThenBy(static row => row.Label, StringComparer.Ordinal),
            "Label" => rows.OrderBy(static row => row.Label, StringComparer.Ordinal),
            "Pixel" => rows.OrderBy(static row => row.SortPixelX)
                .ThenBy(static row => row.SortPixelY),
            "Machine" => rows.OrderBy(static row => row.SortMachineX)
                .ThenBy(static row => row.SortMachineY),
            "Safe" => rows.OrderBy(static row => row.SortSafeMachineX)
                .ThenBy(static row => row.SortSafeMachineY),
            _ => rows.OrderBy(static row => row.SortOrder),
        };

        return (ArtifactSortDescending ? sortedRows.Reverse() : sortedRows).ToArray();
    }

    private static string GetArtifactSortDisplay(string sortKey) =>
        sortKey switch
        {
            "Kind" => "Kind",
            "Label" => "Label",
            "Pixel" => "Pixel",
            "Machine" => "Machine",
            "Safe" => "Safe",
            _ => "Source",
        };

    private static string BuildExportStatus(string verb, int rowCount, bool selectedOnly)
    {
        var scope = selectedOnly ? "selected coordinate row" : "coordinate row(s)";
        return string.Format(CultureInfo.InvariantCulture, "{0} {1} {2}.", verb, rowCount, scope);
    }

    private CoordinatePlannerRequest BuildCurrentRequest()
    {
        return new CoordinatePlannerRequest(
            MachineOriginX: (double)MachineOriginX,
            MachineOriginY: (double)MachineOriginY,
            MachineWidth: Math.Max(0.001, (double)MachineWidth),
            MachineHeight: Math.Max(0.001, (double)MachineHeight),
            PixelWidth: ClampPositiveInt(PixelWidth),
            PixelHeight: ClampPositiveInt(PixelHeight),
            HorizontalGuideCount: ClampNonNegativeInt(HorizontalGuideCount),
            VerticalGuideCount: ClampNonNegativeInt(VerticalGuideCount),
            CopperPillarDiameter: Math.Max(0d, (double)CopperPillarDiameter),
            ShowBistRectangle: ShowBistRectangle,
            ShowCustomArray: ShowCustomArray,
            CustomArrayColumnCount: ClampNonNegativeInt(CustomArrayColumnCount),
            CustomArrayRowCount: ClampNonNegativeInt(CustomArrayRowCount),
            CustomArrayTopLeftMachineX: (double)CustomArrayTopLeftMachineX,
            CustomArrayTopLeftMachineY: (double)CustomArrayTopLeftMachineY,
            CustomArrayTopRightMachineX: (double)CustomArrayTopRightMachineX,
            CustomArrayTopRightMachineY: (double)CustomArrayTopRightMachineY,
            CustomArrayBottomRightMachineX: (double)CustomArrayBottomRightMachineX,
            CustomArrayBottomRightMachineY: (double)CustomArrayBottomRightMachineY,
            CustomArrayBottomLeftMachineX: (double)CustomArrayBottomLeftMachineX,
            CustomArrayBottomLeftMachineY: (double)CustomArrayBottomLeftMachineY,
            HorizontalGuideMode: ToApplicationGuideMode(SelectedHorizontalGuideGenerationOption.Mode),
            VerticalGuideMode: ToApplicationGuideMode(SelectedVerticalGuideGenerationOption.Mode),
            HorizontalGuidePitch: Math.Max(0d, (double)HorizontalGuidePitch),
            VerticalGuidePitch: Math.Max(0d, (double)VerticalGuidePitch),
            HorizontalGuideInset: Math.Max(0d, (double)HorizontalGuideInset),
            VerticalGuideInset: Math.Max(0d, (double)VerticalGuideInset),
            HorizontalGuidePositions: ParseGuidePositionList(HorizontalGuidePositionListText),
            VerticalGuidePositions: ParseGuidePositionList(VerticalGuidePositionListText),
            CustomPoints: _customPointRequests.ToArray(),
            CustomPaths: _customPathRequests.ToArray());
    }

    private static CoordinatePlannerRequest BuildRequestFromDefaults(CoordinatePlannerWorkspaceSession session)
    {
        return new CoordinatePlannerRequest(
            0d,
            0d,
            Math.Max(0.001, session.DefaultMachineWidth),
            Math.Max(0.001, session.DefaultMachineHeight),
            Math.Max(1, session.DefaultPixelWidth),
            Math.Max(1, session.DefaultPixelHeight),
            5,
            5,
            0d,
            true,
            false,
            5,
            5,
            0d,
            0d,
            Math.Max(0.001, session.DefaultMachineWidth),
            0d,
            Math.Max(0.001, session.DefaultMachineWidth),
            Math.Max(0.001, session.DefaultMachineHeight),
            0d,
            Math.Max(0.001, session.DefaultMachineHeight));
    }

    private static List<CoordinatePlannerCadLayerOption> BuildLayerOptions(IReadOnlyList<string> layerNames)
    {
        var options = new List<CoordinatePlannerCadLayerOption>
        {
            new(CoordinatePlannerCadLayerMode.None, null, "Regular only"),
            new(CoordinatePlannerCadLayerMode.AllVisible, null, "All active CAD"),
        };
        options.AddRange(layerNames.Select(name => new CoordinatePlannerCadLayerOption(CoordinatePlannerCadLayerMode.SingleLayer, name, name)));
        return options;
    }

    private static List<CoordinatePlannerBoundsOption> BuildAaBoundsOptions(IReadOnlyList<string> layerNames)
    {
        var options = new List<CoordinatePlannerBoundsOption>
        {
            new(CoordinatePlannerBoundsMode.RegularGrid, null, "Regular grid bounds"),
        };
        options.AddRange(layerNames.Select(name => new CoordinatePlannerBoundsOption(CoordinatePlannerBoundsMode.LayerBounds, name, $"Layer: {name}")));
        return options;
    }

    private static CoordinatePlannerBoundsOption ResolveInitialAaBoundsOption(
        IReadOnlyList<CoordinatePlannerBoundsOption> options,
        IReadOnlyCollection<string> layerNames,
        string? preferredLayerName)
    {
        if (options.Count == 0)
        {
            return default;
        }

        var resolvedPreferredLayerName = CoordinatePlannerActiveAreaResolver.ResolvePreferredLayerName(
            layerNames,
            preferredLayerName);
        if (!string.IsNullOrWhiteSpace(resolvedPreferredLayerName))
        {
            var preferred = options.FirstOrDefault(option =>
                option.Mode == CoordinatePlannerBoundsMode.LayerBounds &&
                string.Equals(option.LayerName, resolvedPreferredLayerName, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(preferred.Display))
            {
                return preferred;
            }
        }

        return options[0];
    }

    public CoordinatePlannerWorkspacePreferences BuildWorkspacePreferences()
    {
        var selectedAaOutlineLayerName = ResolveSelectedAaOutlineLayerName();
        return new CoordinatePlannerWorkspacePreferences(
            ClampPositiveInt(PixelWidth),
            ClampPositiveInt(PixelHeight),
            selectedAaOutlineLayerName ?? string.Empty);
    }

    private string? ResolveSelectedAaOutlineLayerName()
    {
        if (SelectedAaBoundsOption.Mode != CoordinatePlannerBoundsMode.LayerBounds)
        {
            return null;
        }

        var layerName = SelectedAaBoundsOption.LayerName?.Trim();
        return string.IsNullOrWhiteSpace(layerName) ? null : layerName;
    }

    private static CoordinatePlannerGuideBasisKind ToGuideBasisKind(CoordinatePlannerGuideBasisMode mode) =>
        mode == CoordinatePlannerGuideBasisMode.RegularGrid
            ? CoordinatePlannerGuideBasisKind.RegularGrid
            : CoordinatePlannerGuideBasisKind.ActiveArea;

    private static CoordinateGuideGenerationMode ToApplicationGuideMode(CoordinatePlannerGuideGenerationMode mode) =>
        mode switch
        {
            CoordinatePlannerGuideGenerationMode.Pitch => CoordinateGuideGenerationMode.Pitch,
            CoordinatePlannerGuideGenerationMode.ExplicitPositions => CoordinateGuideGenerationMode.ExplicitPositions,
            _ => CoordinateGuideGenerationMode.Count,
        };

    private static double[] ParseGuidePositionList(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<double>();
        }

        return text
            .Split([',', ';', ' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static part => double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? (double?)value : null)
            .Where(static value => value.HasValue)
            .Select(static value => Math.Max(0d, value!.Value))
            .ToArray();
    }

    private static string BuildGuideModeSummaryText(
        CoordinatePlannerGuideGenerationMode mode,
        int count,
        decimal pitch,
        decimal inset,
        string explicitPositions)
    {
        return mode switch
        {
            CoordinatePlannerGuideGenerationMode.Pitch => string.Format(
                CultureInfo.InvariantCulture,
                "pitch {0:0.###} mm, inset {1:0.###}",
                Math.Max(0m, pitch),
                Math.Max(0m, inset)),
            CoordinatePlannerGuideGenerationMode.ExplicitPositions => string.Format(
                CultureInfo.InvariantCulture,
                "{0} explicit, inset {1:0.###}",
                ParseGuidePositionList(explicitPositions).Length,
                Math.Max(0m, inset)),
            _ => string.Format(
                CultureInfo.InvariantCulture,
                "{0} count, inset {1:0.###}",
                count,
                Math.Max(0m, inset)),
        };
    }

    private void RefreshCustomRecipeRows()
    {
        _customRecipeRows.Clear();
        foreach (var point in _customPointRequests)
        {
            _customRecipeRows.Add(new CoordinatePlannerCustomRecipeRow(
                $"custom-point-{point.Key}",
                point.Label,
                "Point",
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Machine ({0:0.###}, {1:0.###})",
                    point.MachineX,
                    point.MachineY)));
        }

        foreach (var path in _customPathRequests)
        {
            _customRecipeRows.Add(new CoordinatePlannerCustomRecipeRow(
                $"custom-path-{path.Key}",
                path.Label,
                "Path",
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Machine ({0:0.###}, {1:0.###}) -> ({2:0.###}, {3:0.###}) · {4} step(s)",
                    path.StartMachineX,
                    path.StartMachineY,
                    path.EndMachineX,
                    path.EndMachineY,
                    Math.Max(0, path.StepCount))));
        }

        ClearCustomArtifactsCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(HasCustomArtifacts));
        OnPropertyChanged(nameof(CustomArtifactSummaryText));
    }

    private static string NormalizeCustomLabel(string label, string fallback)
    {
        var normalized = label.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    private void PublishWorkspacePreferencesChanged()
    {
        WorkspacePreferencesChanged?.Invoke(BuildWorkspacePreferences());
    }

    private void SetCustomArrayCorners(
        double topLeftX,
        double topLeftY,
        double topRightX,
        double topRightY,
        double bottomRightX,
        double bottomRightY,
        double bottomLeftX,
        double bottomLeftY)
    {
        _isBatchUpdatingCustomArrayCorners = true;
        try
        {
            CustomArrayTopLeftMachineX = (decimal)topLeftX;
            CustomArrayTopLeftMachineY = (decimal)topLeftY;
            CustomArrayTopRightMachineX = (decimal)topRightX;
            CustomArrayTopRightMachineY = (decimal)topRightY;
            CustomArrayBottomRightMachineX = (decimal)bottomRightX;
            CustomArrayBottomRightMachineY = (decimal)bottomRightY;
            CustomArrayBottomLeftMachineX = (decimal)bottomLeftX;
            CustomArrayBottomLeftMachineY = (decimal)bottomLeftY;
        }
        finally
        {
            _isBatchUpdatingCustomArrayCorners = false;
        }

        RebuildSnapshot();
        NotifySelectedCustomArrayCornerChanged();
    }

    private static int ClampPositiveInt(decimal value)
    {
        var rounded = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        return Math.Max(1, rounded);
    }

    private static int ClampNonNegativeInt(decimal value)
    {
        var rounded = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        return Math.Max(0, rounded);
    }

}
