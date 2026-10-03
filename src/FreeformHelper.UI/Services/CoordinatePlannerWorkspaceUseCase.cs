using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;

namespace FreeformHelper.UI.Services;

public enum CoordinatePlannerGuideBasisKind
{
    ActiveArea,
    RegularGrid,
}

public sealed record CoordinatePlannerWorkspaceBuildResult(
    CoordinatePlannerSnapshot Snapshot,
    CoordinatePlannerActiveAreaResolution ActiveAreaResolution,
    CoordinatePlannerGuideBasisKind GuideBasisKind,
    Rect2 GuideBounds,
    string GuideSourceText)
{
    public string ActiveAreaSourceText => ActiveAreaResolution.SourceText;
}

public sealed class CoordinatePlannerWorkspaceUseCase
{
    public static CoordinatePlannerWorkspaceBuildResult BuildSnapshot(
        CoordinatePlannerWorkspaceSession session,
        CoordinatePlannerRequest request,
        string? activeAreaOutlineLayer,
        CoordinatePlannerGuideBasisKind guideBasisKind = CoordinatePlannerGuideBasisKind.ActiveArea)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);

        var activeAreaResolution = CoordinatePlannerActiveAreaResolver.Resolve(session, activeAreaOutlineLayer);
        var guideBounds = guideBasisKind == CoordinatePlannerGuideBasisKind.RegularGrid
            ? session.Grid.Bounds
            : activeAreaResolution.Bounds;
        var snapshot = CoordinatePlannerComputationService.BuildSnapshot(activeAreaResolution.Bounds, guideBounds, request);
        var guideSourceText = guideBasisKind == CoordinatePlannerGuideBasisKind.RegularGrid
            ? "Guide reference: regular grid bounds"
            : $"Guide reference: {activeAreaResolution.SourceText}";
        return new CoordinatePlannerWorkspaceBuildResult(
            snapshot,
            activeAreaResolution,
            guideBasisKind,
            guideBounds,
            guideSourceText);
    }
}
