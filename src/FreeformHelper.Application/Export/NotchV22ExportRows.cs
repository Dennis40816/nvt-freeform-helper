// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.Immutable;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Notch;

namespace FreeformHelper.Application.Export;

/// <summary>The V22 firmware projection and profile policy, shared by export and row explanations.</summary>
public static class NotchV22ExportRows
{
    public static ImmutableArray<NotchTableRow> Project(IReadOnlyList<NotchTableRow> rows, int nullValue,
        NotchExportProfile profile)
    {
        var icCount = rows.Count == 0 ? 1 : rows.Max(static row => row.IcIndex) + 1;
        return ProjectNodes(rows, icCount, nullValue, profile).SelectMany(static nodes => nodes).Select(node =>
            new NotchTableRow(node.IcIndex, node.TableDiffIndex, node.RegularPadId, node.CadPadId,
                new NotchV22Node(node.SourceDiffIndex, node.CombinePercent,
                    node.FirstTargetDiffIndex ?? nullValue, node.FirstRatioPercent,
                    node.SecondTargetDiffIndex ?? nullValue, node.SecondRatioPercent, node.Flags), node.Comment))
            .ToImmutableArray();
    }

    internal static IReadOnlyList<IReadOnlyList<NotchV22FirmwareRow>> ProjectNodes(
        IReadOnlyList<NotchTableRow> rows, int icCount, int nullValue, NotchExportProfile profile) =>
        ProjectNodes(rows, icCount, nullValue, profile, out _);

    internal static IReadOnlyList<IReadOnlyList<NotchV22FirmwareRow>> ProjectNodes(
        IReadOnlyList<NotchTableRow> rows, int icCount, int nullValue, NotchExportProfile profile,
        out IReadOnlyList<NotchV22FirmwareRow> sourceRows)
    {
        var projection = NotchV22FirmwareProjector.Project(rows, nullValue, icCount);
        sourceRows = projection.SourceRows;
        return profile == NotchExportProfile.Release
            ? NotchV22FirmwareProjector.OrderNodesByIc(
                projection.SourceRows.Where(static node => !IsNoOp(node)).ToList(), icCount)
            : projection.NodesByIc;
    }

    private static bool IsNoOp(NotchV22FirmwareRow node) =>
        node.CombinePercent == 100 && node.FirstTargetDiffIndex is null && node.FirstRatioPercent == 0 &&
        node.SecondTargetDiffIndex is null && node.SecondRatioPercent == 0;
}
