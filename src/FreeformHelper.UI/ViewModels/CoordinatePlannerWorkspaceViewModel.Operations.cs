using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class CoordinatePlannerWorkspaceViewModel
{
    private void RebuildSnapshot() => _presentation.Rebuild(
        BuildCurrentRequest(), _geometry.SelectedAaOutlineLayerName, _guides.BasisKind);

    private void UseActiveAreaSafeCorners() => _array.UseActiveAreaSafeCorners();

    private void ReplaceArtifactRows(CoordinateArtifactRow[] rows) => _presentation.ReplaceArtifactRows(rows);

    private Task RunArtifactDetailAsync(Func<Task> request) =>
        UiEvents?.RunAsync("CoordinatePlanner.ArtifactDetail", _ => request(), CancellationToken.None) ?? request();

    private CoordinatePlannerRenderState BuildRender(
        CoordinatePlannerRequest request,
        string? outlineLayer,
        CoordinatePlannerGuideBasisKind guideBasisKind)
    {
        var result = CoordinatePlannerWorkspaceUseCase.BuildSnapshot(
            _session,
            request,
            outlineLayer,
            guideBasisKind);
        var snapshot = result.Snapshot;
        var previewBounds = Rect2.Union(result.Snapshot.ActiveAreaBounds, result.GuideBounds);
        var activeAreaSourceText = result.ActiveAreaSourceText;
        var guideSourceText = result.GuideSourceText;
        var artifactSnapshot = CoordinateArtifactService.BuildSnapshot(
            snapshot,
            result.GuideBounds,
            activeAreaSourceText,
            guideSourceText);
        var aaCornerRows = CoordinatePlannerRowProjector.PointRows(snapshot, CoordinatePlannerPointKind.AaCorner, "AA corner");
        var customArrayCornerRows = CoordinatePlannerRowProjector.PointRows(
            snapshot, CoordinatePlannerPointKind.CustomArrayCorner, "4-point corner");
        var customArrayPointRows = CoordinatePlannerRowProjector.PointRows(
            snapshot, CoordinatePlannerPointKind.CustomArrayDot, "Array dot");
        var horizontalGuideRows = CoordinatePlannerRowProjector.LineRows(snapshot, CoordinatePlannerLineKind.HorizontalGuide);
        var verticalGuideRows = CoordinatePlannerRowProjector.LineRows(snapshot, CoordinatePlannerLineKind.VerticalGuide);
        return new(snapshot, artifactSnapshot, previewBounds, activeAreaSourceText, guideSourceText, Array.Empty<CadPad>(),
            aaCornerRows, customArrayCornerRows, customArrayPointRows, horizontalGuideRows, verticalGuideRows, artifactSnapshot.Rows);
    }

    private CoordinatePlannerRequest BuildCurrentRequest() => CoordinatePlannerRequestBuilder.Build(
        _geometry.CaptureInput(), _guides.CaptureHorizontalInput(), _guides.CaptureVerticalInput(),
        _array.CaptureInput(), _array.PointRequests(), _array.PathRequests());

    private static CoordinatePlannerRequest BuildRequestFromDefaults(CoordinatePlannerWorkspaceSession session)
    {
        return CoordinatePlannerRequestBuilder.FromDefaults(
            session.DefaultMachineWidth,
            session.DefaultMachineHeight,
            session.DefaultPixelWidth,
            session.DefaultPixelHeight);
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
        List<CoordinatePlannerBoundsOption> options,
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

    private static CoordinatePlannerGuideBasisKind ToGuideBasisKind(CoordinatePlannerGuideBasisMode mode) =>
        mode == CoordinatePlannerGuideBasisMode.RegularGrid
            ? CoordinatePlannerGuideBasisKind.RegularGrid
            : CoordinatePlannerGuideBasisKind.ActiveArea;

    public CoordinatePlannerWorkspacePreferences BuildWorkspacePreferences() => _geometry.BuildWorkspacePreferences();
}
