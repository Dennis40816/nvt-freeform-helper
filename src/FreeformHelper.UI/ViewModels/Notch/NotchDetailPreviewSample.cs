// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.Immutable;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Presentation;

namespace FreeformHelper.UI.ViewModels;

/// <summary>Fixed synthetic evidence for the Dev page; no generation runs on that page.</summary>
public static class NotchDetailPreviewSample
{
    public static NotchV22RowExplanation Evidence { get; } = new(0, 0, 1, 0, 65535,
        [new(1, 0, true, true, false, 15.125, 152, 0.3734567901234568)],
        [Regular(0, 30.25, 60.5, NotchV22ToFullLimit.VirtualAreaCap),
         Regular(1, 19.25, 38.5, NotchV22ToFullLimit.VirtualAreaCap),
         Regular(12, 19.25, 38.5, NotchV22ToFullLimit.VirtualAreaCap),
         Regular(13, 12.25, 16, NotchV22ToFullLimit.ReachableFreeArea)],
        [new(0, 0, .605, 60, true, 1, NotchV22TargetDisposition.RetainedAnchor),
         new(0, 1, .385, 38, true, 1, NotchV22TargetDisposition.Emitted),
         new(0, 12, .385, 38, true, 1, NotchV22TargetDisposition.Emitted),
         new(0, 13, .16, 16, true, 1, NotchV22TargetDisposition.Emitted)],
        new("CurrentGain", 100, 100, 152, 60, [new(1, 38), new(12, 38), new(13, 16)], 152, 16), [],
        [new(0, [0, 136, 1, 38, 12, 38, 0], [new(1, 38), new(12, 38)]),
         new(1, [0, 100, 13, 16, 65535, 0, 1], [new(13, 16)])])
    {
        PreviewPolygons = [Rect(-2, -2, 5, 5)],
    };

    public static ImmutableArray<NotchTableRow> Rows { get; } = Evidence.Rows.Select(row =>
        new NotchTableRow(0, 0, 0, 1, NotchV22Node.FromValues(row.Values.ToArray()), "synthetic preview")).ToImmutableArray();

    public static NotchDetailViewModel Build()
    {
        var cad = new CadPad(1, "synthetic", "SENSOR", Rect(.25, .25, 4.75, 4.75));
        var pads = new[] { Pad(0, -2, -2), Pad(1, 3, -2), Pad(12, -2, 3), Pad(13, 3, 3) };
        var grid = new RegularGrid(2, 2, [-2, 3, 8], [-2, 3, 8], pads);
        var allocations = NotchAllocationService.BuildAllocations(cad, grid).Select(static a =>
            new NotchDetailAllocation(a.Pad.Index, a.Pad.Row, a.Pad.Col, a.Pad.IcIndex, a.Q7, a.Ratio)).ToImmutableArray();
        var source = NotchRowExplanationProjector.Build(Evidence, Rows, NotchCompensationModel.CurrentGain, false);
        var detail = new NotchDetailViewModel(cad, 0, FreeformType.None, allocations, pads, 0,
            allocations.Max(static a => a.Q7), "TH=0 (disabled)", 0, string.Empty, string.Empty, string.Empty,
            Evidence.PreviewPolygons);
        detail.ShowExplanation(new NotchDetailExplanation([source], string.Empty, [],
            [new CadPad(2, "synthetic neighbor", "SENSOR", Rect(5.25, .25, 9.75, 4.75)),
             new CadPad(3, "synthetic neighbor", "SENSOR", Rect(.25, 5.25, 4.75, 9.75))]));
        return detail;
    }

    private static NotchV22RegularExplanation Regular(int diff, double cad, double exact, NotchV22ToFullLimit limit) =>
        new(diff, 0, diff, 25, cad / 4, cad / 4, exact / 4,
            limit == NotchV22ToFullLimit.ReachableFreeArea ? exact / 4 : 25, true, "synthetic", true, limit);

    private static RegularPad Pad(int diff, double x, double y) =>
        new(diff / 12, diff % 12, diff, Rect(x, y, x + 5, y + 5)) { DiffIndex = diff };

    private static Polygon2 Rect(double minX, double minY, double maxX, double maxY) =>
        new([new Point2(minX, minY), new Point2(maxX, minY), new Point2(maxX, maxY), new Point2(minX, maxY)]);
}
