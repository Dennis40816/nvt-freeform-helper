using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using FreeformHelper.Application.Services;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.Controls;

public sealed class NotchApplySimulationHeatmapView : Control
{
    public static readonly StyledProperty<IReadOnlyList<NotchApplySimulationHeatmapCell>> CellsProperty =
        AvaloniaProperty.Register<NotchApplySimulationHeatmapView, IReadOnlyList<NotchApplySimulationHeatmapCell>>(
            nameof(Cells),
            Array.Empty<NotchApplySimulationHeatmapCell>());

    public static readonly StyledProperty<int> GridRowsProperty =
        AvaloniaProperty.Register<NotchApplySimulationHeatmapView, int>(nameof(GridRows));

    public static readonly StyledProperty<int> GridColsProperty =
        AvaloniaProperty.Register<NotchApplySimulationHeatmapView, int>(nameof(GridCols));

    public static readonly StyledProperty<double> MaxAbsDeltaProperty =
        AvaloniaProperty.Register<NotchApplySimulationHeatmapView, double>(nameof(MaxAbsDelta));

    public static readonly StyledProperty<int> SelectedRegularPadIdProperty =
        AvaloniaProperty.Register<NotchApplySimulationHeatmapView, int>(nameof(SelectedRegularPadId), -1);

    private readonly Dictionary<uint, ISolidColorBrush> _brushCache = new();

    public IReadOnlyList<NotchApplySimulationHeatmapCell> Cells
    {
        get => GetValue(CellsProperty);
        set => SetValue(CellsProperty, value);
    }

    public int GridRows
    {
        get => GetValue(GridRowsProperty);
        set => SetValue(GridRowsProperty, value);
    }

    public int GridCols
    {
        get => GetValue(GridColsProperty);
        set => SetValue(GridColsProperty, value);
    }

    public double MaxAbsDelta
    {
        get => GetValue(MaxAbsDeltaProperty);
        set => SetValue(MaxAbsDeltaProperty, value);
    }

    public int SelectedRegularPadId
    {
        get => GetValue(SelectedRegularPadIdProperty);
        set => SetValue(SelectedRegularPadIdProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var background = GetResourceBrush("BrushBgSurfaceInset", Brushes.Transparent);
        context.FillRectangle(background, new Rect(Bounds.Size));

        if (GridRows <= 0 || GridCols <= 0 || Bounds.Width <= 1 || Bounds.Height <= 1)
        {
            return;
        }

        var padding = GetResourceDouble("NotchApplySimulationHeatmapPadding", 10d);
        var cellGap = GetResourceDouble("NotchApplySimulationHeatmapCellGap", 1d);
        var gridStrokeThickness = GetResourceDouble("NotchApplySimulationHeatmapGridStrokeThickness", 1d);
        var selectedStrokeThickness = GetResourceDouble("NotchApplySimulationHeatmapSelectedStrokeThickness", 2d);
        var minOpacity = GetResourceDouble("NotchApplySimulationHeatmapMinOpacity", 0.12d);
        var maxOpacity = GetResourceDouble("NotchApplySimulationHeatmapMaxOpacity", 0.92d);

        var availableWidth = Math.Max(1d, Bounds.Width - (padding * 2));
        var availableHeight = Math.Max(1d, Bounds.Height - (padding * 2));
        var cellWidth = Math.Max(1d, (availableWidth - ((GridCols - 1) * cellGap)) / GridCols);
        var cellHeight = Math.Max(1d, (availableHeight - ((GridRows - 1) * cellGap)) / GridRows);

        var neutralColor = GetResourceColor("ColorBorderMuted", Colors.Gray);
        var positiveColor = GetResourceColor("ColorSuccess", Colors.Green);
        var negativeColor = GetResourceColor("ColorDanger", Colors.Red);
        var selectedColor = GetResourceColor("ColorAccent", Colors.DeepSkyBlue);

        var neutralPen = new Pen(GetBrush(neutralColor), gridStrokeThickness);
        var selectedPen = new Pen(GetBrush(selectedColor), selectedStrokeThickness);

        foreach (var cell in Cells)
        {
            if (cell.RegularRow < 0 || cell.RegularRow >= GridRows || cell.RegularCol < 0 || cell.RegularCol >= GridCols)
            {
                continue;
            }

            var x = padding + (cell.RegularCol * (cellWidth + cellGap));
            var y = padding + (cell.RegularRow * (cellHeight + cellGap));
            var rect = new Rect(x, y, cellWidth, cellHeight);
            var fillBrush = BuildFillBrush(cell, positiveColor, negativeColor, minOpacity, maxOpacity);
            context.FillRectangle(fillBrush, rect);
            context.DrawRectangle(null, neutralPen, rect);

            if (cell.RegularPadId == SelectedRegularPadId)
            {
                context.DrawRectangle(null, selectedPen, rect.Deflate(-gridStrokeThickness * 0.25));
            }
        }
    }

    private IBrush BuildFillBrush(
        NotchApplySimulationHeatmapCell cell,
        Color positiveColor,
        Color negativeColor,
        double minOpacity,
        double maxOpacity)
    {
        var magnitude = MaxAbsDelta <= 1e-9 ? 0d : Math.Clamp(Math.Abs(cell.DeltaValue) / MaxAbsDelta, 0d, 1d);
        var normalizedOpacity = minOpacity + ((maxOpacity - minOpacity) * magnitude);
        var alpha = (byte)Math.Clamp(Math.Round(normalizedOpacity * 255d), 0d, 255d);
        var baseColor = cell.DeltaValue >= 0d ? positiveColor : negativeColor;
        return GetBrush(Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B));
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

    private ISolidColorBrush GetBrush(Color color)
    {
        var key = ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
        if (_brushCache.TryGetValue(key, out var brush))
        {
            return brush;
        }

        brush = new SolidColorBrush(color);
        _brushCache[key] = brush;
        return brush;
    }
}
