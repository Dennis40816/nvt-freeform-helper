// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Media;
using FreeformHelper.UI.Presentation;

namespace FreeformHelper.UI.Controls;

public sealed class NotchCoverageBar : Control
{
    public static readonly Avalonia.StyledProperty<NotchCellPresentation?> CellProperty =
        Avalonia.AvaloniaProperty.Register<NotchCoverageBar, NotchCellPresentation?>(nameof(Cell));

    static NotchCoverageBar() => AffectsRender<NotchCoverageBar>(CellProperty);

    public NotchCoverageBar() => ActualThemeVariantChanged += (_, _) => InvalidateVisual();

    public NotchCellPresentation? Cell
    {
        get => GetValue(CellProperty);
        set => SetValue(CellProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var border = new Pen(Resource<IBrush>("NfcBorderMutedBrush"), Resource<double>("NotchPreviewStrokeWidth"));
        var rect = new Avalonia.Rect(Bounds.Size);
        context.DrawRectangle(Resource<IBrush>("NfcSurfaceBrush"), border, rect);
        if (Cell is not { ShowCoverage: true } cell)
        {
            return;
        }

        var covered = rect.Width * Math.Clamp(cell.CadPercent / 100, 0, 1);
        var added = rect.Width * Math.Clamp(cell.AddedPercent / 100, 0, 1);
        var brush = Resource<IBrush>("BrushNotchExplanationArea");
        context.FillRectangle(brush, new Avalonia.Rect(0, 0, covered, rect.Height));
        var hatchRect = new Avalonia.Rect(covered, 0, Math.Min(added, rect.Width - covered), rect.Height);
        using (context.PushClip(hatchRect))
        {
            var pen = new Pen(brush, Resource<double>("NotchExplanationHatchWidth"));
            var spacing = Resource<double>("NotchExplanationHatchSpacing");
            for (var x = hatchRect.Left - rect.Height; x < hatchRect.Right; x += spacing)
            {
                context.DrawLine(pen, new Avalonia.Point(x, rect.Bottom), new Avalonia.Point(x + rect.Height, rect.Top));
            }
        }
    }

    private T Resource<T>(string key) => this.TryFindResource(key, ActualThemeVariant, out var value) && value is T resource
        ? resource : throw new InvalidOperationException($"Missing resource {key}.");
}
