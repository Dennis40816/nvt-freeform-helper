// Copyright (c) 2026 Dennis Liu. All rights reserved.

using CommunityToolkit.Mvvm.ComponentModel;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// The "Corner circles" form of the coordinate planner. The user sets a radius in active-area pixels. The model finds
/// the largest rectangle whose four corner circles fit inside the active area and can build the image that has exactly
/// the pixel size of the active area. The active area is the rectangle (0, 0) to (width, height) in pixel space.
/// </summary>
public sealed partial class CoordinatePlannerCornerCircleModel : ObservableObject
{
    private int _pixelWidth;
    private int _pixelHeight;

    [ObservableProperty]
    private decimal _radius = 50m;

    [ObservableProperty]
    private bool _drawCenterRectangle;

    [ObservableProperty]
    private CornerCircleLayoutResult? _layout;

    [ObservableProperty]
    private string _statusText = string.Empty;

    /// <summary>Set by the view. Saves the image and returns true when a file was written.</summary>
    public Func<CornerCircleImage, Task<bool>>? RequestExportAsync { get; set; }

    /// <summary>Starts with the pixel size of the active area. The layout is calculated at once.</summary>
    public CoordinatePlannerCornerCircleModel(int pixelWidth = 0, int pixelHeight = 0)
    {
        _pixelWidth = pixelWidth;
        _pixelHeight = pixelHeight;
        Recalculate();
    }

    /// <summary>Raised after the layout was recalculated, so the view can redraw the preview.</summary>
    public event EventHandler? LayoutChanged;

    /// <summary>True when the layout has four circles that fit.</summary>
    public bool HasLayout => Layout is { Status: CornerCircleLayoutStatus.Ok };

    /// <summary>Sets the active-area pixel size and recalculates.</summary>
    public void SetPixelSize(int pixelWidth, int pixelHeight)
    {
        if (_pixelWidth == pixelWidth && _pixelHeight == pixelHeight)
        {
            return;
        }

        _pixelWidth = pixelWidth;
        _pixelHeight = pixelHeight;
        Recalculate();
    }

    /// <summary>Builds the image with exactly the active-area pixel size. Returns null when there is no layout.</summary>
    public CornerCircleImage? BuildImage()
    {
        return Layout is { Status: CornerCircleLayoutStatus.Ok } layout
            ? CornerCircleImageRenderer.Render(_pixelWidth, _pixelHeight, layout, DrawCenterRectangle)
            : null;
    }

    partial void OnRadiusChanged(decimal value) => Recalculate();

    partial void OnLayoutChanged(CornerCircleLayoutResult? value)
    {
        OnPropertyChanged(nameof(HasLayout));
    }

    private void Recalculate()
    {
        if (!CornerCircleImageRenderer.IsSupportedSize(_pixelWidth, _pixelHeight))
        {
            Layout = null;
            StatusText = _pixelWidth < 1 || _pixelHeight < 1
                ? "The active area has no pixel size."
                : "The active area is too large for an image.";
        }
        else
        {
            var area = new Polygon2(
            [
                new Point2(0, 0),
                new Point2(_pixelWidth, 0),
                new Point2(_pixelWidth, _pixelHeight),
                new Point2(0, _pixelHeight),
            ]);
            var result = CornerCircleLayoutCalculator.Calculate(new CornerCircleLayoutRequest(area, (double)Radius));
            Layout = result;
            StatusText = BuildStatusText(result);
        }

        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string BuildStatusText(CornerCircleLayoutResult result)
    {
        if (result.Status != CornerCircleLayoutStatus.Ok)
        {
            return result.Message;
        }

        var rectangle = result.CenterRectangle;
        return string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "Circle centers: {0:0.###} x {1:0.###} px rectangle, from ({2:0.###}, {3:0.###}) to ({4:0.###}, {5:0.###}).",
            rectangle.Width,
            rectangle.Height,
            rectangle.MinX,
            rectangle.MinY,
            rectangle.MaxX,
            rectangle.MaxY);
    }
}
