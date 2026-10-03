using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.Controls;

public sealed class NotchPreviewCanvas : Control
{
    public static readonly StyledProperty<CadPad?> CadPadProperty =
        AvaloniaProperty.Register<NotchPreviewCanvas, CadPad?>(nameof(CadPad));

    public static readonly StyledProperty<IReadOnlyList<RegularPad>> RegularPadsProperty =
        AvaloniaProperty.Register<NotchPreviewCanvas, IReadOnlyList<RegularPad>>(nameof(RegularPads), Array.Empty<RegularPad>());

    public static readonly StyledProperty<IReadOnlyList<Polygon2>> ToFullPolygonsProperty =
        AvaloniaProperty.Register<NotchPreviewCanvas, IReadOnlyList<Polygon2>>(nameof(ToFullPolygons), Array.Empty<Polygon2>());

    public static readonly StyledProperty<FreeformType> FreeformTypeProperty =
        AvaloniaProperty.Register<NotchPreviewCanvas, FreeformType>(nameof(FreeformType));

    private readonly Dictionary<uint, ISolidColorBrush> _brushCache = new();

    public CadPad? CadPad
    {
        get => GetValue(CadPadProperty);
        set => SetValue(CadPadProperty, value);
    }

    public IReadOnlyList<RegularPad> RegularPads
    {
        get => GetValue(RegularPadsProperty);
        set => SetValue(RegularPadsProperty, value);
    }

    public IReadOnlyList<Polygon2> ToFullPolygons
    {
        get => GetValue(ToFullPolygonsProperty);
        set => SetValue(ToFullPolygonsProperty, value);
    }

    public FreeformType FreeformType
    {
        get => GetValue(FreeformTypeProperty);
        set => SetValue(FreeformTypeProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var background = GetResourceBrush("BrushBgSurface", Brushes.Transparent);
        context.FillRectangle(background, new Rect(Bounds.Size));

        if (CadPad is null && RegularPads.Count == 0 && ToFullPolygons.Count == 0)
        {
            return;
        }

        var bounds = BuildWorldBounds(CadPad, RegularPads, ToFullPolygons);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var padding = GetResourceDouble("NotchPreviewPadding", GetResourceDouble("Space12", 12));
        var availableWidth = Math.Max(1, Bounds.Width - padding * 2);
        var availableHeight = Math.Max(1, Bounds.Height - padding * 2);
        var scale = Math.Min(availableWidth / bounds.Width, availableHeight / bounds.Height);
        if (scale <= 0)
        {
            return;
        }

        var viewWidth = bounds.Width * scale;
        var viewHeight = bounds.Height * scale;
        var left = (Bounds.Width - viewWidth) * 0.5;
        var top = (Bounds.Height - viewHeight) * 0.5;
        var tx = left - bounds.MinX * scale;
        var ty = top + bounds.MaxY * scale;
        var worldToScreen = new Matrix(scale, 0, 0, -scale, tx, ty);

        var cadStrokeColor = GetResourceColor("ColorNotchCadStroke", Colors.White);
        var regStrokeColor = GetResourceColor("ColorNotchRegularStroke", cadStrokeColor);
        var toFullStrokeColor = GetResourceColor("ColorNotchToFullOutline", Colors.Orange);
        var hatchColor = GetHatchColor();
        var strokeWidth = GetResourceDouble("NotchPreviewStrokeWidth", 1.0);
        var toFullStrokeWidth = GetResourceDouble("NotchPreviewToFullStrokeWidth", strokeWidth + 0.5);
        var hatchSpacing = GetResourceDouble("NotchPreviewHatchSpacing", 10.0);
        var hatchWidth = GetResourceDouble("NotchPreviewHatchWidth", strokeWidth);

        var cadFill = GetResourceBrush("BrushNotchCadFill", Brushes.Transparent);
        var cadPen = new Pen(GetBrush(cadStrokeColor), strokeWidth);
        var regPen = new Pen(GetBrush(regStrokeColor), strokeWidth);
        var toFullPen = new Pen(GetBrush(toFullStrokeColor), toFullStrokeWidth);
        var hatchPen = new Pen(GetBrush(hatchColor), hatchWidth);

        using (context.PushTransform(worldToScreen))
        {
            if (CadPad is not null)
            {
                var cadGeometry = BuildGeometry(CadPad.Polygon);
                if (cadGeometry is not null)
                {
                    context.DrawGeometry(cadFill, cadPen, cadGeometry);
                }
            }

            if (RegularPads.Count > 0)
            {
                var spacingWorld = hatchSpacing / scale;
                foreach (var pad in RegularPads)
                {
                    var b = pad.Bounds;
                    var rect = new Rect(b.MinX, b.MinY, b.Width, b.Height);
                    context.DrawRectangle(Brushes.Transparent, regPen, rect);
                    DrawHatch(context, rect, hatchPen, spacingWorld);
                }
            }

            if (ToFullPolygons.Count > 0)
            {
                foreach (var polygon in ToFullPolygons)
                {
                    var geometry = BuildGeometry(polygon);
                    if (geometry is null)
                    {
                        continue;
                    }

                    context.DrawGeometry(null, toFullPen, geometry);
                }
            }
        }
    }

    private static Rect2 BuildWorldBounds(CadPad? cadPad, IReadOnlyList<RegularPad> pads, IReadOnlyList<Polygon2> toFullPolygons)
    {
        Rect2? bounds = null;
        if (cadPad is not null)
        {
            bounds = cadPad.Bounds;
        }

        foreach (var pad in pads)
        {
            bounds = bounds is null ? pad.Bounds : Rect2.Union(bounds.Value, pad.Bounds);
        }

        foreach (var polygon in toFullPolygons)
        {
            bounds = bounds is null ? polygon.Bounds : Rect2.Union(bounds.Value, polygon.Bounds);
        }

        return bounds ?? new Rect2(0, 0, 0, 0);
    }

    private Color GetHatchColor()
    {
        return FreeformType switch
        {
            FreeformType.XWay => GetResourceColor("ColorNotchX", Colors.White),
            FreeformType.YWay => GetResourceColor("ColorNotchY", Colors.White),
            FreeformType.XYWay => GetResourceColor("ColorNotchXY", Colors.White),
            _ => GetResourceColor("ColorNotchX", Colors.White),
        };
    }

    private static StreamGeometry? BuildGeometry(Polygon2 polygon)
    {
        if (polygon is null || polygon.Vertices.Length == 0)
        {
            return null;
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(polygon.Vertices[0].X, polygon.Vertices[0].Y), isFilled: true);
            for (var i = 1; i < polygon.Vertices.Length; i++)
            {
                var p = polygon.Vertices[i];
                ctx.LineTo(new Point(p.X, p.Y));
            }
            ctx.EndFigure(isClosed: true);
        }
        return geometry;
    }

    private static void DrawHatch(DrawingContext context, Rect rect, Pen pen, double spacing)
    {
        if (spacing <= 0)
        {
            return;
        }

        using (context.PushClip(rect))
        {
            var start = rect.Left - rect.Height;
            var end = rect.Right + rect.Height;
            for (var x = start; x <= end; x += spacing)
            {
                var p1 = new Point(x, rect.Bottom);
                var p2 = new Point(x + rect.Height, rect.Top);
                context.DrawLine(pen, p1, p2);
            }
        }
    }

    private IBrush GetResourceBrush(string key, IBrush fallback)
    {
        return UiResourceResolver.GetBrush(this, key, fallback, GetBrush);
    }

    private Color GetResourceColor(string key, Color fallback)
    {
        return UiResourceResolver.GetColor(this, key, fallback);
    }

    private double GetResourceDouble(string key, double fallback)
    {
        return UiResourceResolver.GetDouble(this, key, fallback);
    }

    private ISolidColorBrush GetBrush(Color c)
    {
        var key = ((uint)c.A << 24) | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
        if (_brushCache.TryGetValue(key, out var brush))
        {
            return brush;
        }

        var created = new SolidColorBrush(c);
        _brushCache[key] = created;
        return created;
    }
}
