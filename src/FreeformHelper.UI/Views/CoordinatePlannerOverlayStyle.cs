using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Nvt.Core.Avalonia.Theme;

namespace FreeformHelper.UI.Views;

/// <summary>
/// The brushes, stroke widths, opacities and label sizes of the coordinate planner overlay.
/// They come from the theme resources of the owning control.
/// </summary>
internal sealed record CoordinatePlannerOverlayStyle(
    IBrush GuideBrush,
    IBrush CornerBrush,
    IBrush BistBrush,
    IBrush ArrayBrush,
    IBrush MutedBrush,
    IBrush LabelBackground,
    IBrush LabelForeground,
    double GuideThickness,
    double BistThickness,
    double PointThickness,
    double FocusThickness,
    double MutedOpacity,
    double FocusOpacity,
    double LabelFontSize,
    double LabelOffset,
    double MinRadiusScreen,
    double ArrayDotMinRadiusScreen,
    CornerRadius LabelCornerRadius,
    Thickness LabelPadding)
{
    public static CoordinatePlannerOverlayStyle Resolve(Control owner)
    {
        var guideBrush = GetBrush(owner, "BrushCoordinatePlannerGuide", Brushes.DeepSkyBlue);
        var cornerBrush = GetBrush(owner, "BrushCoordinatePlannerPoint", Brushes.Gold);
        var bistBrush = GetBrush(owner, "BrushCoordinatePlannerBist", Brushes.Orange);
        var arrayBrush = GetBrush(owner, "BrushCoordinatePlannerArray", guideBrush);
        var mutedBrush = GetBrush(owner, "BrushTextMuted", Brushes.Gray);
        var labelBackground = GetBrush(owner, "BrushCoordinatePlannerLabelBackground", Brushes.Black);
        var labelForeground = GetBrush(owner, "BrushCoordinatePlannerLabelForeground", Brushes.White);
        var guideThickness = UiResourceResolver.GetDouble(owner, "CoordinatePlannerGuideStrokeThickness", 2d);
        var bistThickness = UiResourceResolver.GetDouble(owner, "CoordinatePlannerBistStrokeThickness", 2d);
        var pointThickness = UiResourceResolver.GetDouble(owner, "CoordinatePlannerPointStrokeThickness", 2d);
        var focusThickness = UiResourceResolver.GetDouble(owner, "CoordinatePlannerFocusStrokeThickness", 3d);
        var mutedOpacity = UiResourceResolver.GetDouble(owner, "CoordinatePlannerMutedOverlayOpacity", 0.34d);
        var focusOpacity = UiResourceResolver.GetDouble(owner, "CoordinatePlannerFocusOverlayOpacity", 0.96d);
        var labelFontSize = UiResourceResolver.GetDouble(owner, "CoordinatePlannerLabelFontSize", 12d);
        var labelOffset = UiResourceResolver.GetDouble(owner, "CoordinatePlannerLabelOffset", 10d);
        var minRadiusScreen = UiResourceResolver.GetDouble(owner, "CoordinatePlannerPointMinRadiusScreen", 5d);
        var arrayDotMinRadiusScreen = UiResourceResolver.GetDouble(owner, "CoordinatePlannerArrayPointMinRadiusScreen", 3d);
        var labelCornerRadius = UiResourceResolver.GetCornerRadius(owner, "RadiusSm", new CornerRadius(6));
        var labelPadding = UiResourceResolver.GetThickness(owner, "Inset6_2", new Thickness(6, 2));

        return new CoordinatePlannerOverlayStyle(
            guideBrush,
            cornerBrush,
            bistBrush,
            arrayBrush,
            mutedBrush,
            labelBackground,
            labelForeground,
            guideThickness,
            bistThickness,
            pointThickness,
            focusThickness,
            mutedOpacity,
            focusOpacity,
            labelFontSize,
            labelOffset,
            minRadiusScreen,
            arrayDotMinRadiusScreen,
            labelCornerRadius,
            labelPadding);
    }

    private static IBrush GetBrush(Control owner, string resourceKey, IBrush fallback)
    {
        return UiResourceResolver.GetBrush(owner, resourceKey, fallback, static color => new SolidColorBrush(color));
    }
}
