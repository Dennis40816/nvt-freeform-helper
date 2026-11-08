using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using FreeformHelper.Application.Services;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.Services;
using static FreeformHelper.UI.Views.CoordinatePlannerOverlayRules;
using OverlayMode = FreeformHelper.UI.ViewModels.CoordinatePlannerWorkspaceViewModel.CoordinatePlannerOverlayMode;

namespace FreeformHelper.UI.Views;

/// <summary>
/// Draws the rectangles, lines and points of the coordinate planner into the two overlay canvases.
/// </summary>
internal sealed class CoordinatePlannerOverlayRenderer
{
    private readonly Canvas _overlayShapes;
    private readonly Canvas _overlayLabels;
    private readonly CanvasViewportAdapter _viewportAdapter;
    private readonly PadCanvasViewFrameSnapshot _viewFrame;
    private readonly CoordinatePlannerSnapshot _snapshot;
    private readonly CoordinatePlannerOverlayStyle _style;
    private readonly OverlayMode _mode;
    private readonly string? _selectedKey;
    private readonly Action<Ellipse, string, string> _configureArrayCornerHandle;

    public CoordinatePlannerOverlayRenderer(
        Canvas overlayShapes,
        Canvas overlayLabels,
        CanvasViewportAdapter viewportAdapter,
        PadCanvasViewFrameSnapshot viewFrame,
        CoordinatePlannerSnapshot snapshot,
        CoordinatePlannerOverlayStyle style,
        OverlayMode mode,
        string? selectedKey,
        Action<Ellipse, string, string> configureArrayCornerHandle)
    {
        _overlayShapes = overlayShapes;
        _overlayLabels = overlayLabels;
        _viewportAdapter = viewportAdapter;
        _viewFrame = viewFrame;
        _snapshot = snapshot;
        _style = style;
        _mode = mode;
        _selectedKey = selectedKey;
        _configureArrayCornerHandle = configureArrayCornerHandle;
    }

    public void Draw()
    {
        AddRectangles();
        AddLines();
        AddPoints();
    }

    private void AddRectangles()
    {
        foreach (var rect in _snapshot.Rectangles)
        {
            var isSelected = IsSelected(rect.Key, _selectedKey);
            var isMuted = IsMutedOverlay(_mode, _selectedKey, rect.Key);
            var brush = isMuted ? _style.MutedBrush : rect.Kind == CoordinatePlannerRectangleKind.BistCenter ? _style.BistBrush : _style.GuideBrush;
            var screenRect = _viewportAdapter.ToOverlayRect(_overlayShapes, rect.WorldBounds, _viewFrame);
            var rectangle = new Rectangle
            {
                Width = screenRect.Width,
                Height = screenRect.Height,
                Stroke = brush,
                StrokeThickness = isSelected ? _style.FocusThickness : _style.BistThickness,
                Fill = Brushes.Transparent,
                StrokeDashArray = new AvaloniaList<double> { 6, 4 },
                Opacity = isMuted ? _style.MutedOpacity : _style.FocusOpacity,
            };
            Canvas.SetLeft(rectangle, screenRect.X);
            Canvas.SetTop(rectangle, screenRect.Y);
            _overlayShapes.Children.Add(rectangle);
            if (ShouldLabelRectangle(_mode, isSelected))
            {
                AddLabel(rect.Label, new Point(screenRect.X + _style.LabelOffset, screenRect.Y - _style.LabelOffset));
            }
        }
    }

    private void AddLines()
    {
        foreach (var line in _snapshot.Lines)
        {
            if (!ShouldRenderLine(line, _mode, _selectedKey))
            {
                continue;
            }

            var isSelected = IsSelected(line.Key, _selectedKey);
            var isMuted = IsMutedOverlay(_mode, _selectedKey, line.Key);
            var colorBrush = line.Kind switch
            {
                CoordinatePlannerLineKind.HorizontalGuide => _style.GuideBrush,
                CoordinatePlannerLineKind.VerticalGuide => _style.BistBrush,
                CoordinatePlannerLineKind.CustomArrayEdge => _style.ArrayBrush,
                _ => _style.GuideBrush,
            };
            var brush = isMuted ? _style.MutedBrush : colorBrush;
            var start = _viewportAdapter.ToOverlayPoint(_overlayShapes, line.SafeStartWorld, _viewFrame);
            var end = _viewportAdapter.ToOverlayPoint(_overlayShapes, line.SafeEndWorld, _viewFrame);
            _overlayShapes.Children.Add(new Line
            {
                StartPoint = start,
                EndPoint = end,
                Stroke = brush,
                StrokeThickness = isSelected ? _style.FocusThickness : _style.GuideThickness,
                Opacity = isMuted ? _style.MutedOpacity : _style.FocusOpacity,
            });
            if (ShouldLabelLine(line, _mode, isSelected))
            {
                AddLabel(line.Label, new Point(start.X + _style.LabelOffset, start.Y - _style.LabelOffset));
            }
        }
    }

    private void AddPoints()
    {
        foreach (var point in _snapshot.Points)
        {
            if (!ShouldRenderPoint(point, _mode, _selectedKey))
            {
                continue;
            }

            var isSelected = IsSelected(point.Key, _selectedKey);
            var isMuted = IsMutedOverlay(_mode, _selectedKey, point.Key);
            var (colorBrush, radiusFloor) = point.Kind switch
            {
                CoordinatePlannerPointKind.AaCorner => (_style.CornerBrush, _style.MinRadiusScreen),
                CoordinatePlannerPointKind.BistCorner => (_style.BistBrush, _style.MinRadiusScreen),
                CoordinatePlannerPointKind.CustomArrayCorner => (_style.ArrayBrush, _style.MinRadiusScreen),
                CoordinatePlannerPointKind.CustomArrayDot => (_style.ArrayBrush, _style.ArrayDotMinRadiusScreen),
                _ => (_style.CornerBrush, _style.MinRadiusScreen),
            };
            var brush = isMuted ? _style.MutedBrush : colorBrush;
            var center = _viewportAdapter.ToOverlayPoint(_overlayShapes, point.SafeWorld, _viewFrame);
            var radiusX = ComputeScreenRadiusX(_viewportAdapter, _overlayShapes, _viewFrame, point.SafeWorld, _snapshot.WorldPillarRadiusX, radiusFloor);
            var radiusY = ComputeScreenRadiusY(_viewportAdapter, _overlayShapes, _viewFrame, point.SafeWorld, _snapshot.WorldPillarRadiusY, radiusFloor);
            var ellipse = new Ellipse
            {
                Width = radiusX * 2d,
                Height = radiusY * 2d,
                Stroke = brush,
                StrokeThickness = isSelected ? _style.FocusThickness : _style.PointThickness,
                Fill = isSelected ? brush : Brushes.Transparent,
                Opacity = isMuted ? _style.MutedOpacity : _style.FocusOpacity,
            };
            if (point.Kind == CoordinatePlannerPointKind.CustomArrayCorner)
            {
                _configureArrayCornerHandle(ellipse, point.Key, point.Label);
            }

            Canvas.SetLeft(ellipse, center.X - radiusX);
            Canvas.SetTop(ellipse, center.Y - radiusY);
            _overlayShapes.Children.Add(ellipse);
            if (ShouldLabelPoint(point, _mode, isSelected))
            {
                AddLabel(point.Label, new Point(center.X + _style.LabelOffset, center.Y - _style.LabelOffset));
            }
        }
    }

    private void AddLabel(string text, Point position)
    {
        _overlayLabels.Children.Add(BuildLabel(
            text,
            position,
            _style.LabelBackground,
            _style.LabelForeground,
            _style.LabelFontSize,
            _style.LabelCornerRadius,
            _style.LabelPadding));
    }

    private static Border BuildLabel(
        string text,
        Point position,
        IBrush background,
        IBrush foreground,
        double fontSize,
        CornerRadius cornerRadius,
        Thickness padding)
    {
        var border = new Border
        {
            Background = background,
            CornerRadius = cornerRadius,
            Padding = padding,
            Child = new TextBlock
            {
                Text = text,
                Foreground = foreground,
                FontSize = fontSize,
                FontWeight = FontWeight.SemiBold,
            },
        };
        Canvas.SetLeft(border, position.X);
        Canvas.SetTop(border, position.Y);
        return border;
    }
}
