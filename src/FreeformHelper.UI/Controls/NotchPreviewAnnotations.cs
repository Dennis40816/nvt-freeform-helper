// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media;

namespace FreeformHelper.UI.Controls;

internal static class NotchPreviewAnnotations
{
    internal static void Draw(NotchPreviewCanvas canvas, DrawingContext context, Avalonia.Matrix transform)
    {
        if (canvas.Explanation is not { } explanation)
        {
            return;
        }

        var source = canvas.RegularPads.FirstOrDefault(pad => pad.RegularPadId == explanation.SourceRegularPadId);
        var textBrush = Resource<IBrush>(canvas, "NfcTextBrush");
        var mutedBrush = Resource<IBrush>(canvas, "NfcTextMutedBrush");
        var font = new Typeface(Resource<FontFamily>(canvas, "FontFamilyUi"));
        var valueFont = new Typeface(font.FontFamily, weight: FontWeight.SemiBold);
        var cadCenter = canvas.CadPad?.Centroid;
        var fontSize = Resource<double>(canvas, "FontSizeSm");
        var inset = Resource<double>(canvas, "Space4");
        var lineHeight = Resource<double>(canvas, "Space16");
        var pen = new Pen(textBrush, Resource<double>(canvas, "NotchPreviewStrokeWidth"));
        foreach (var pad in canvas.RegularPads)
        {
            var cell = explanation.Cells.FirstOrDefault(item => item.RegularPadIds.Contains(pad.RegularPadId));
            if (pad.IcIndex != explanation.IcIndex)
            {
                continue;
            }

            var topLeft = transform.Transform(new Avalonia.Point(pad.Bounds.MinX, pad.Bounds.MaxY));
            var bottomRight = transform.Transform(new Avalonia.Point(pad.Bounds.MaxX, pad.Bounds.MinY));
            var label = new FormattedText(cell?.CellText ?? $"diff {pad.DiffIndex}", CultureInfo.InvariantCulture, Avalonia.Media.FlowDirection.LeftToRight,
                font, fontSize, mutedBrush);
            var value = new FormattedText(cell?.PreviewText ?? "—", CultureInfo.InvariantCulture, Avalonia.Media.FlowDirection.LeftToRight,
                valueFont, Resource<double>(canvas, "FontSizeMd"), textBrush);
            var right = cadCenter.HasValue && pad.Centroid.X > cadCenter.Value.X;
            var bottom = cadCenter.HasValue && pad.Centroid.Y < cadCenter.Value.Y;
            var y = bottom ? bottomRight.Y - inset - lineHeight - value.Height : topLeft.Y + inset;
            context.DrawText(label, new Avalonia.Point(right ? bottomRight.X - inset - label.Width : topLeft.X + inset, y));
            context.DrawText(value, new Avalonia.Point(right ? bottomRight.X - inset - value.Width : topLeft.X + inset, y + lineHeight));
            if (source is not null && cell is { IsSource: false, InRow: not null and not 0 })
            {
                var start = transform.Transform(new Avalonia.Point(source.Bounds.MinX + source.Bounds.Width / 2,
                    source.Bounds.MinY + source.Bounds.Height / 2));
                var end = transform.Transform(new Avalonia.Point(pad.Bounds.MinX + pad.Bounds.Width / 2,
                    pad.Bounds.MinY + pad.Bounds.Height / 2));
                DrawArrow(canvas, context, pen, start, end);
            }
        }
    }

    private static void DrawArrow(Control canvas, DrawingContext context, Pen pen, Avalonia.Point start, Avalonia.Point end)
    {
        Avalonia.Vector delta = end - start;
        if (delta.Length <= 0)
        {
            return;
        }

        var unit = delta / delta.Length;
        var size = Resource<double>(canvas, "NotchExplanationArrowSize");
        var tail = end - unit * size;
        var perpendicular = new Avalonia.Vector(-unit.Y, unit.X) * size / 2;
        context.DrawLine(pen, start, tail);
        var head = new StreamGeometry();
        using (var shape = head.Open())
        {
            shape.BeginFigure(end, isFilled: true);
            shape.LineTo(tail + perpendicular);
            shape.LineTo(tail - perpendicular);
            shape.EndFigure(isClosed: true);
        }
        context.DrawGeometry(pen.Brush, null, head);
    }

    private static T Resource<T>(Control control, string key) => control.TryFindResource(key, control.ActualThemeVariant, out var value) && value is T resource
        ? resource : throw new InvalidOperationException($"Missing resource {key}.");
}
