using Avalonia.Controls;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views;

/// <summary>
/// The rules that decide which coordinate planner overlay items are drawn, labeled and muted,
/// and the screen radius of a pillar. The view calls them while it draws the overlay.
/// </summary>
internal static class CoordinatePlannerOverlayRules
{
    internal static double ComputeScreenRadiusX(
        CanvasViewportAdapter viewportAdapter,
        Canvas overlay,
        in PadCanvasViewFrameSnapshot viewFrame,
        Point2 center,
        double worldRadiusX,
        double fallback)
    {
        if (worldRadiusX <= 1e-9)
        {
            return fallback;
        }

        var centerPoint = viewportAdapter.ToOverlayPoint(overlay, center, viewFrame);
        var offsetPoint = viewportAdapter.ToOverlayPoint(overlay, new Point2(center.X + worldRadiusX, center.Y), viewFrame);
        return Math.Max(fallback, Math.Abs(offsetPoint.X - centerPoint.X));
    }

    internal static double ComputeScreenRadiusY(
        CanvasViewportAdapter viewportAdapter,
        Canvas overlay,
        in PadCanvasViewFrameSnapshot viewFrame,
        Point2 center,
        double worldRadiusY,
        double fallback)
    {
        if (worldRadiusY <= 1e-9)
        {
            return fallback;
        }

        var centerPoint = viewportAdapter.ToOverlayPoint(overlay, center, viewFrame);
        var offsetPoint = viewportAdapter.ToOverlayPoint(overlay, new Point2(center.X, center.Y - worldRadiusY), viewFrame);
        return Math.Max(fallback, Math.Abs(offsetPoint.Y - centerPoint.Y));
    }

    internal static bool ShouldRenderLine(
        CoordinatePlannerLine line,
        CoordinatePlannerWorkspaceViewModel.CoordinatePlannerOverlayMode mode,
        string? selectedKey) =>
        mode switch
        {
            CoordinatePlannerWorkspaceViewModel.CoordinatePlannerOverlayMode.All => true,
            CoordinatePlannerWorkspaceViewModel.CoordinatePlannerOverlayMode.Focus => HasSelectedArtifact(selectedKey) || IsEssentialLine(line) || IsSelected(line.Key, selectedKey),
            _ => IsEssentialLine(line) || IsSelected(line.Key, selectedKey),
        };

    internal static bool ShouldRenderPoint(
        CoordinatePlannerPoint point,
        CoordinatePlannerWorkspaceViewModel.CoordinatePlannerOverlayMode mode,
        string? selectedKey) =>
        mode switch
        {
            CoordinatePlannerWorkspaceViewModel.CoordinatePlannerOverlayMode.All => true,
            CoordinatePlannerWorkspaceViewModel.CoordinatePlannerOverlayMode.Focus => HasSelectedArtifact(selectedKey) || IsEssentialPoint(point) || IsSelected(point.Key, selectedKey),
            _ => IsEssentialPoint(point) || IsSelected(point.Key, selectedKey),
        };

    internal static bool ShouldLabelRectangle(
        CoordinatePlannerWorkspaceViewModel.CoordinatePlannerOverlayMode mode,
        bool isSelected) =>
        isSelected && mode != CoordinatePlannerWorkspaceViewModel.CoordinatePlannerOverlayMode.All;

    internal static bool ShouldLabelLine(
        CoordinatePlannerLine line,
        CoordinatePlannerWorkspaceViewModel.CoordinatePlannerOverlayMode mode,
        bool isSelected) =>
        mode == CoordinatePlannerWorkspaceViewModel.CoordinatePlannerOverlayMode.All ||
        isSelected ||
        line.Kind == CoordinatePlannerLineKind.CustomArrayEdge;

    internal static bool ShouldLabelPoint(
        CoordinatePlannerPoint point,
        CoordinatePlannerWorkspaceViewModel.CoordinatePlannerOverlayMode mode,
        bool isSelected) =>
        (point.Kind != CoordinatePlannerPointKind.CustomArrayDot &&
         mode == CoordinatePlannerWorkspaceViewModel.CoordinatePlannerOverlayMode.All) ||
        isSelected ||
        point.Kind is CoordinatePlannerPointKind.AaCorner or CoordinatePlannerPointKind.BistCorner or CoordinatePlannerPointKind.CustomArrayCorner;

    internal static bool IsEssentialLine(CoordinatePlannerLine line) =>
        line.Kind is CoordinatePlannerLineKind.HorizontalGuide or CoordinatePlannerLineKind.VerticalGuide or CoordinatePlannerLineKind.CustomArrayEdge;

    internal static bool IsEssentialPoint(CoordinatePlannerPoint point) =>
        point.Kind is CoordinatePlannerPointKind.AaCorner or CoordinatePlannerPointKind.BistCorner or CoordinatePlannerPointKind.CustomArrayCorner;

    internal static bool IsMutedOverlay(
        CoordinatePlannerWorkspaceViewModel.CoordinatePlannerOverlayMode mode,
        string? selectedKey,
        string key) =>
        mode == CoordinatePlannerWorkspaceViewModel.CoordinatePlannerOverlayMode.Focus &&
        HasSelectedArtifact(selectedKey) &&
        !IsSelected(key, selectedKey);

    internal static bool IsSelected(string key, string? selectedKey) =>
        string.Equals(key, selectedKey, StringComparison.Ordinal);

    internal static bool HasSelectedArtifact(string? selectedKey) =>
        !string.IsNullOrWhiteSpace(selectedKey);
}
