using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// The fixed option lists that the coordinate planner shows in its combo boxes. The lists do not depend on the
/// session, so every view model shares one copy.
/// </summary>
internal static class CoordinatePlannerPresentationCatalog
{
    public static IReadOnlyList<CoordinatePlannerWorkspaceViewModel.CoordinatePlannerGuideBasisOption> GuideBasisOptions { get; } =
    [
        new(CoordinatePlannerWorkspaceViewModel.CoordinatePlannerGuideBasisMode.ActiveArea, "AA outline"),
        new(CoordinatePlannerWorkspaceViewModel.CoordinatePlannerGuideBasisMode.RegularGrid, "Regular grid"),
    ];

    public static IReadOnlyList<CoordinatePlannerWorkspaceViewModel.CoordinatePlannerGuideGenerationOption> GuideGenerationOptions { get; } =
    [
        new(CoordinatePlannerWorkspaceViewModel.CoordinatePlannerGuideGenerationMode.Count, "Count"),
        new(CoordinatePlannerWorkspaceViewModel.CoordinatePlannerGuideGenerationMode.Pitch, "Pitch"),
        new(CoordinatePlannerWorkspaceViewModel.CoordinatePlannerGuideGenerationMode.ExplicitPositions, "Positions"),
    ];

    public static IReadOnlyList<CoordinatePlannerWorkspaceViewModel.CoordinatePlannerCustomArrayCornerOption> CustomArrayCornerOptions { get; } =
    [
        new("array-tl", "TL"),
        new("array-tr", "TR"),
        new("array-br", "BR"),
        new("array-bl", "BL"),
    ];

    public static IReadOnlyList<CoordinatePlannerWorkspaceViewModel.CoordinatePlannerOverlayModeOption> OverlayModeOptions { get; } =
    [
        new(CoordinatePlannerWorkspaceViewModel.CoordinatePlannerOverlayMode.Focus, "Focus"),
        new(CoordinatePlannerWorkspaceViewModel.CoordinatePlannerOverlayMode.Essentials, "Essentials"),
        new(CoordinatePlannerWorkspaceViewModel.CoordinatePlannerOverlayMode.All, "All"),
    ];
}

/// <summary>
/// Turns the points and lines of a planner snapshot into the text rows that the planner tables show.
/// The text uses the current culture, as the view model did before.
/// </summary>
internal static class CoordinatePlannerRowProjector
{
    public static CoordinatePlannerWorkspaceViewModel.CoordinatePlannerPointRow[] PointRows(
        CoordinatePlannerSnapshot snapshot,
        CoordinatePlannerPointKind kind,
        string category)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return snapshot.Points
            .Where(point => point.Kind == kind)
            .Select(point => new CoordinatePlannerWorkspaceViewModel.CoordinatePlannerPointRow(
                point.Label,
                category,
                $"({point.PixelX:0.###}, {point.PixelY:0.###})",
                $"({point.MachineX:0.###}, {point.MachineY:0.###})",
                $"({point.SafeMachineX:0.###}, {point.SafeMachineY:0.###})"))
            .ToArray();
    }

    public static CoordinatePlannerWorkspaceViewModel.CoordinatePlannerLineRow[] LineRows(
        CoordinatePlannerSnapshot snapshot,
        CoordinatePlannerLineKind kind)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return snapshot.Lines
            .Where(line => line.Kind == kind)
            .Select(static line => new CoordinatePlannerWorkspaceViewModel.CoordinatePlannerLineRow(
                line.Label,
                $"({line.StartPixelX:0.###}, {line.StartPixelY:0.###}) -> ({line.EndPixelX:0.###}, {line.EndPixelY:0.###})",
                $"({line.StartMachineX:0.###}, {line.StartMachineY:0.###}) -> ({line.EndMachineX:0.###}, {line.EndMachineY:0.###})",
                $"({line.SafeStartMachineX:0.###}, {line.SafeStartMachineY:0.###}) -> ({line.SafeEndMachineX:0.###}, {line.SafeEndMachineY:0.###})"))
            .ToArray();
    }
}
