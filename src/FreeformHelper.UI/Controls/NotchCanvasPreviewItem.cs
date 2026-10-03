using FreeformHelper.Domain.Geometry;

namespace FreeformHelper.UI.Controls;

/// <summary>
/// Canvas overlay payload for Notch 2.2 preview rendering.
/// </summary>
public sealed record NotchCanvasPreviewItem(
    int CadPadId,
    double ToRegularRatio,
    double ToFullRatio,
    bool IsToFullEnabled,
    IReadOnlyList<Polygon2> ToFullSeedPolygons,
    IReadOnlyList<Polygon2> ToFullCandidatePolygons,
    IReadOnlyList<Polygon2> ToFullPolygons,
    IReadOnlyList<Polygon2> ToFullFinalOutlinePolygons);
