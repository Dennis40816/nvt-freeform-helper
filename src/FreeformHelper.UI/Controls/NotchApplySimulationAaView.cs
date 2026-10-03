using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Controls;

public sealed class NotchApplySimulationAaCellInvokedEventArgs : EventArgs
{
    public NotchApplySimulationAaCellInvokedEventArgs(int regularPadId)
    {
        RegularPadId = regularPadId;
    }

    public int RegularPadId { get; }
}

public sealed class NotchApplySimulationAaView : Control
{
    public static readonly StyledProperty<IReadOnlyList<NotchApplySimulationAaDisplayCell>> CellsProperty =
        AvaloniaProperty.Register<NotchApplySimulationAaView, IReadOnlyList<NotchApplySimulationAaDisplayCell>>(
            nameof(Cells),
            Array.Empty<NotchApplySimulationAaDisplayCell>());

    public static readonly StyledProperty<int> GridRowsProperty =
        AvaloniaProperty.Register<NotchApplySimulationAaView, int>(nameof(GridRows));

    public static readonly StyledProperty<int> GridColsProperty =
        AvaloniaProperty.Register<NotchApplySimulationAaView, int>(nameof(GridCols));

    public static readonly StyledProperty<int> SelectedRegularPadIdProperty =
        AvaloniaProperty.Register<NotchApplySimulationAaView, int>(nameof(SelectedRegularPadId), -1);

    public static readonly StyledProperty<NotchApplySimulationCanvasViewMode> ViewModeProperty =
        AvaloniaProperty.Register<NotchApplySimulationAaView, NotchApplySimulationCanvasViewMode>(
            nameof(ViewMode),
            NotchApplySimulationCanvasViewMode.Delta);

    public static readonly StyledProperty<NotchApplySimulationCanvasColorMode> ColorModeProperty =
        AvaloniaProperty.Register<NotchApplySimulationAaView, NotchApplySimulationCanvasColorMode>(
            nameof(ColorMode),
            NotchApplySimulationCanvasColorMode.Auto);

    public static readonly StyledProperty<double> ThresholdValueProperty =
        AvaloniaProperty.Register<NotchApplySimulationAaView, double>(nameof(ThresholdValue), 5d);

    private readonly Dictionary<uint, ISolidColorBrush> _brushCache = new();
    private readonly List<(Rect Rect, int RegularPadId)> _hitRegions = new();

    public event EventHandler<NotchApplySimulationAaCellInvokedEventArgs>? CellInvoked;

    public IReadOnlyList<NotchApplySimulationAaDisplayCell> Cells
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

    public int SelectedRegularPadId
    {
        get => GetValue(SelectedRegularPadIdProperty);
        set => SetValue(SelectedRegularPadIdProperty, value);
    }

    public NotchApplySimulationCanvasViewMode ViewMode
    {
        get => GetValue(ViewModeProperty);
        set => SetValue(ViewModeProperty, value);
    }

    public NotchApplySimulationCanvasColorMode ColorMode
    {
        get => GetValue(ColorModeProperty);
        set => SetValue(ColorModeProperty, value);
    }

    public double ThresholdValue
    {
        get => GetValue(ThresholdValueProperty);
        set => SetValue(ThresholdValueProperty, value);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var point = e.GetPosition(this);
        foreach (var hitRegion in _hitRegions)
        {
            if (hitRegion.Rect.Contains(point))
            {
                CellInvoked?.Invoke(this, new NotchApplySimulationAaCellInvokedEventArgs(hitRegion.RegularPadId));
                e.Handled = true;
                return;
            }
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        _hitRegions.Clear();

        var background = GetResourceBrush("BrushBgSurfaceInset", Brushes.Transparent);
        context.FillRectangle(background, new Rect(Bounds.Size));

        if (GridRows <= 0 || GridCols <= 0 || Bounds.Width <= 1 || Bounds.Height <= 1)
        {
            return;
        }

        var padding = GetResourceDouble("NotchApplySimulationAaPadding", 10d);
        var cellGap = GetResourceDouble("NotchApplySimulationAaCellGap", 1d);
        var gridStrokeThickness = GetResourceDouble("NotchApplySimulationAaGridStrokeThickness", 1d);
        var selectedStrokeThickness = GetResourceDouble("NotchApplySimulationAaSelectedStrokeThickness", 2d);
        var minOpacity = GetResourceDouble("NotchApplySimulationAaMinOpacity", 0.12d);
        var maxOpacity = GetResourceDouble("NotchApplySimulationAaMaxOpacity", 0.9d);
        var valueFontMin = GetResourceDouble("NotchApplySimulationAaValueFontMin", 9d);
        var valueFontMax = GetResourceDouble("NotchApplySimulationAaValueFontMax", 15d);

        var availableWidth = Math.Max(1d, Bounds.Width - (padding * 2));
        var availableHeight = Math.Max(1d, Bounds.Height - (padding * 2));
        var cellWidth = Math.Max(1d, (availableWidth - ((GridCols - 1) * cellGap)) / GridCols);
        var cellHeight = Math.Max(1d, (availableHeight - ((GridRows - 1) * cellGap)) / GridRows);

        var accentColor = GetResourceColor("ColorSimulationScaleLow", Colors.DeepSkyBlue);
        var zeroColor = GetResourceColor("ColorSimulationScaleZero", Colors.LimeGreen);
        var midColor = GetResourceColor("ColorSimulationScaleMid", Colors.Gold);
        var warmColor = GetResourceColor("ColorSimulationScaleWarm", Colors.Orange);
        var positiveColor = GetResourceColor("ColorSimulationScaleHigh", Colors.IndianRed);
        var neutralColor = GetResourceColor("ColorSimulationScaleNeutral", Colors.Gray);
        var selectedColor = GetResourceColor("ColorAccent", Colors.DeepSkyBlue);
        var textBrush = GetResourceBrush("BrushTextPrimary", Brushes.White);

        var neutralPen = new Pen(GetBrush(neutralColor), gridStrokeThickness);
        var selectedPen = new Pen(GetBrush(selectedColor), selectedStrokeThickness);

        var (minValue, maxValue, maxAbsDelta, autoNegativeClampAbs, autoPositiveClamp) = ResolveExtents();
        var clampedThreshold = Math.Max(0d, ThresholdValue);

        foreach (var cell in Cells)
        {
            if (cell.DisplayRow < 0 || cell.DisplayRow >= GridRows || cell.DisplayCol < 0 || cell.DisplayCol >= GridCols)
            {
                continue;
            }

            var x = padding + (cell.DisplayCol * (cellWidth + cellGap));
            var y = padding + (cell.DisplayRow * (cellHeight + cellGap));
            var rect = new Rect(x, y, cellWidth, cellHeight);
            _hitRegions.Add((rect, cell.RegularPadId));

            var fillBrush = BuildFillBrush(cell, accentColor, zeroColor, midColor, warmColor, positiveColor, neutralColor, minOpacity, maxOpacity, minValue, maxValue, maxAbsDelta, autoNegativeClampAbs, autoPositiveClamp, clampedThreshold);
            context.FillRectangle(fillBrush, rect);
            context.DrawRectangle(null, neutralPen, rect);

            if (cell.RegularPadId == SelectedRegularPadId)
            {
                context.DrawRectangle(null, selectedPen, rect.Deflate(-gridStrokeThickness * 0.25));
            }

            DrawCellText(context, cell, rect, textBrush, valueFontMin, valueFontMax);
        }

        (double MinValue, double MaxValue, double MaxAbsDelta, double AutoNegativeClampAbs, double AutoPositiveClamp) ResolveExtents()
        {
            if (Cells.Count == 0)
            {
                return (0d, 0d, 0d, 0d, 0d);
            }

            var values = Cells.Select(static cell => cell.DisplayValue).ToList();
            var autoScaleRange = SimulationColorScaleResolver.ComputeAutoScaleRange(values);
            return (
                autoScaleRange.Minimum,
                autoScaleRange.Maximum,
                values.Max(static value => Math.Abs(value)),
                autoScaleRange.NegativeClampAbs,
                autoScaleRange.PositiveClamp);
        }
    }

    private static void DrawCellText(
        DrawingContext context,
        NotchApplySimulationAaDisplayCell cell,
        Rect rect,
        IBrush valueBrush,
        double valueFontMin,
        double valueFontMax)
    {
        if (rect.Width < 18d || rect.Height < 14d)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(cell.ValueText))
        {
            return;
        }

        var sizeFactor = Math.Clamp(Math.Min(rect.Width / 4.6d, rect.Height / 1.8d), valueFontMin, valueFontMax);
        var valueLayout = new TextLayout(cell.ValueText, Typeface.Default, sizeFactor, valueBrush);
        var origin = new Point(
            rect.X + Math.Max(0d, (rect.Width - valueLayout.Width) / 2d),
            rect.Y + Math.Max(0d, (rect.Height - valueLayout.Height) / 2d));
        valueLayout.Draw(context, origin);
    }

    private IBrush BuildFillBrush(
        NotchApplySimulationAaDisplayCell cell,
        Color lowColor,
        Color zeroColor,
        Color midColor,
        Color warmColor,
        Color highColor,
        Color neutralColor,
        double minOpacity,
        double maxOpacity,
        double minValue,
        double maxValue,
        double maxAbsDelta,
        double autoNegativeClampAbs,
        double autoPositiveClamp,
        double threshold)
    {
        if (ViewMode == NotchApplySimulationCanvasViewMode.ChangedOnly && !cell.IsChanged)
        {
            return GetBrush(Color.FromArgb(22, neutralColor.R, neutralColor.G, neutralColor.B));
        }

        return ViewMode switch
        {
            NotchApplySimulationCanvasViewMode.Before or NotchApplySimulationCanvasViewMode.After =>
                BuildSingleScaleBrush(
                    cell.DisplayValue,
                    lowColor,
                    zeroColor,
                    midColor,
                    warmColor,
                    highColor,
                    localMinOpacity: minOpacity,
                    localMaxOpacity: maxOpacity,
                    localMin: minValue,
                    localMax: maxValue,
                    localThreshold: threshold),
            _ => BuildDeltaBrush(cell.DisplayValue, lowColor, zeroColor, midColor, warmColor, highColor, minOpacity, maxOpacity, maxAbsDelta, threshold),
        };

        IBrush BuildSingleScaleBrush(double value, Color lowScaleColor, Color zeroScaleColor, Color midScaleColor, Color warmScaleColor, Color highScaleColor, double localMinOpacity, double localMaxOpacity, double localMin, double localMax, double localThreshold)
        {
            var useAutoAsymmetricScale = ColorMode == NotchApplySimulationCanvasColorMode.Auto;
            var signedRange = ColorMode == NotchApplySimulationCanvasColorMode.Threshold
                ? localThreshold
                : Math.Max(Math.Abs(localMin), Math.Abs(localMax));
            var useSignedScale = ColorMode == NotchApplySimulationCanvasColorMode.Threshold &&
                                 localMin < -1e-9;
            if (useSignedScale)
            {
                var signedNormalized = ColorMode == NotchApplySimulationCanvasColorMode.Threshold
                    ? NormalizeThresholdScale(Math.Abs(value), localThreshold)
                    : signedRange <= 1e-9
                        ? 0d
                        : Math.Clamp(Math.Abs(value) / signedRange, 0d, 1d);
                var signedMixed = SimulationColorScaleResolver.ResolveSignedColor(
                    value,
                    signedRange,
                    lowScaleColor,
                    zeroScaleColor,
                    midScaleColor,
                    warmScaleColor,
                    highScaleColor);
                return BuildAlphaBrush(
                    signedMixed,
                    localMinOpacity + ((localMaxOpacity - localMinOpacity) * (0.18d + (0.82d * Math.Pow(signedNormalized, 0.78d)))));
            }

            if (useAutoAsymmetricScale)
            {
                var positiveRange = Math.Max(autoPositiveClamp, Math.Max(localMax, 0d));
                var negativeRange = Math.Max(autoNegativeClampAbs, Math.Abs(Math.Min(localMin, 0d)));
                if (value < 0d)
                {
                    var negativeNormalized = negativeRange <= 1e-9
                        ? 0d
                        : Math.Clamp(Math.Abs(value) / negativeRange, 0d, 1d);
                    var negativeMixed = SimulationColorScaleResolver.ResolveNegativeColor(value, negativeRange, lowScaleColor, zeroScaleColor);
                    return BuildAlphaBrush(
                        negativeMixed,
                        localMinOpacity + ((localMaxOpacity - localMinOpacity) * (0.22d + (0.78d * Math.Pow(negativeNormalized, 1.3d)))));
                }

                var positiveNormalized = positiveRange <= 1e-9
                    ? 0d
                    : Math.Clamp(value / positiveRange, 0d, 1d);
                var positiveMixed = SimulationColorScaleResolver.ResolvePositiveColor(value, positiveRange, zeroScaleColor, midScaleColor, warmScaleColor, highScaleColor);
                return BuildAlphaBrush(
                    positiveMixed,
                    localMinOpacity + ((localMaxOpacity - localMinOpacity) * (0.28d + (0.72d * Math.Pow(positiveNormalized, 0.72d)))));
            }

            var sequentialRange = ColorMode == NotchApplySimulationCanvasColorMode.Threshold
                ? localThreshold
                : Math.Max(localMax, 0d);
            var sequentialNormalized = NormalizeThresholdScale(value, sequentialRange);
            var sequentialMixed = SimulationColorScaleResolver.ResolvePositiveColor(value, sequentialRange, zeroScaleColor, midScaleColor, warmScaleColor, highScaleColor);
            return BuildAlphaBrush(
                sequentialMixed,
                localMinOpacity + ((localMaxOpacity - localMinOpacity) * (0.34d + (0.66d * Math.Pow(sequentialNormalized, 0.78d)))));
        }

        IBrush BuildDeltaBrush(double value, Color lowScaleColor, Color zeroScaleColor, Color midScaleColor, Color warmScaleColor, Color highScaleColor, double localMinOpacity, double localMaxOpacity, double localMaxAbs, double localThreshold)
        {
            if (Math.Abs(value) <= 1e-9)
            {
                return BuildAlphaBrush(zeroScaleColor, localMinOpacity + ((localMaxOpacity - localMinOpacity) * 0.18d));
            }

            var magnitude = ColorMode == NotchApplySimulationCanvasColorMode.Threshold
                ? NormalizeThresholdScale(Math.Abs(value), localThreshold)
                : localMaxAbs <= 1e-9
                    ? 0d
                    : Math.Clamp(Math.Abs(value) / localMaxAbs, 0d, 1d);
            var mixed = SimulationColorScaleResolver.ResolveSignedColor(
                value,
                ColorMode == NotchApplySimulationCanvasColorMode.Threshold ? localThreshold : localMaxAbs,
                lowScaleColor,
                zeroScaleColor,
                midScaleColor,
                warmScaleColor,
                highScaleColor);
            return BuildAlphaBrush(mixed, localMinOpacity + ((localMaxOpacity - localMinOpacity) * (0.30d + (0.70d * Math.Pow(magnitude, 0.78d)))));
        }

        IBrush BuildAlphaBrush(Color baseColor, double opacity)
        {
            var alpha = (byte)Math.Clamp(Math.Round(opacity * 255d), 0d, 255d);
            return GetBrush(Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B));
        }

        static double NormalizeThresholdScale(double value, double localThreshold)
        {
            if (localThreshold <= 1e-9)
            {
                return Math.Abs(value) <= 1e-9 ? 0d : 1d;
            }

            return Math.Clamp(value / localThreshold, 0d, 1d);
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
