// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Globalization;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Export;

internal static class NotchExportRangeCheck
{
    private const int V21RowWidth = 9;

    internal static NotchCExportProjection ProjectAndValidate(
        NotchTable table,
        RegularGrid grid,
        ProjectSettings settings,
        int nullValue,
        NotchExportProfile profile)
    {
        var v22Rows = table.Rows
            .Where(static row => row.Version == NotchAlgorithmVersion.V22)
            .ToList();
        var v21Rows = table.Rows
            .Where(static row => row.Version == NotchAlgorithmVersion.V21)
            .OrderBy(static row => row.IcIndex)
            .ThenBy(static row => row.DiffIndex)
            .ToList();

        if (v22Rows.Count > 0 && v21Rows.Count > 0)
        {
            throw new InvalidOperationException(
                "C export is version-specific. Select C v2.1 or C v2.2 so the generated FW file has one table layout.");
        }

        if (v22Rows.Count > 0)
        {
            var nodes = ProjectV22(v22Rows, ResolveIcCount(v22Rows, grid), nullValue, profile);
            ValidateV22Nodes(nodes);
            return new NotchCExportProjection(Array.Empty<IReadOnlyList<NotchV21FirmwareNode>>(), nodes);
        }

        if (v21Rows.Count > 0)
        {
            ValidateV21Rows(v21Rows);
            var nodes = NotchV21FirmwareProjector.Project(
                v21Rows, nullValue, ResolveIcCount(v21Rows, grid), settings.Notch.ComputationMode).NodesByIc;
            ValidateV21Nodes(nodes, v21Rows, settings.Notch.ComputationMode);
            return new NotchCExportProjection(nodes, Array.Empty<IReadOnlyList<NotchV22FirmwareRow>>());
        }

        return new NotchCExportProjection(
            Array.Empty<IReadOnlyList<NotchV21FirmwareNode>>(), Array.Empty<IReadOnlyList<NotchV22FirmwareRow>>());
    }

    private static int ResolveIcCount(IReadOnlyList<NotchTableRow> rows, RegularGrid grid)
    {
        var maxRow = rows.MaxBy(static row => row.IcIndex)!;
        var maxPad = grid.Pads.MaxBy(static pad => pad.IcIndex);
        var maxIcIndex = Math.Max(maxRow.IcIndex, maxPad?.IcIndex ?? -1);
        if (maxIcIndex > byte.MaxValue)
        {
            var context = maxRow.IcIndex == maxIcIndex
                ? DescribeRow(maxRow.IcIndex, maxRow.DiffIndex, maxRow.CadPadId)
                : DescribeRow(maxPad!.IcIndex, maxPad.DiffIndex, maxPad.MatchedCadPadId);
            throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture,
                $"IC count must be at most 256 (IC index in [0,255]) at {context}: {(long)maxIcIndex + 1}."));
        }

        return Math.Max(1, maxIcIndex + 1);
    }

    private static IReadOnlyList<IReadOnlyList<NotchV22FirmwareRow>> ProjectV22(
        IReadOnlyList<NotchTableRow> rows,
        int icCount,
        int nullValue,
        NotchExportProfile profile)
    {
        var projection = NotchV22FirmwareProjector.Project(rows, nullValue, icCount);
        return profile == NotchExportProfile.Release
            ? NotchV22FirmwareProjector.OrderNodesByIc(
                projection.SourceRows.Where(static node => !IsNoOpNode(node)).ToList(), icCount)
            : projection.NodesByIc;
    }

    private static bool IsNoOpNode(NotchV22FirmwareRow node)
    {
        return node.CombinePercent == 100 &&
               node.FirstTargetDiffIndex is null &&
               node.FirstRatioPercent == 0 &&
               node.SecondTargetDiffIndex is null &&
               node.SecondRatioPercent == 0;
    }

    private static void ValidateV21Rows(IReadOnlyList<NotchTableRow> rows)
    {
        foreach (var row in rows)
        {
            if (row.Values.Length < V21RowWidth)
            {
                var context = DescribeRow(row.IcIndex, row.DiffIndex, row.CadPadId);
                throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture,
                    $"V21 row must have at least 9 values at {context}: {row.Values.Length}."));
            }
        }
    }

    private static void ValidateV21Nodes(
        IReadOnlyList<IReadOnlyList<NotchV21FirmwareNode>> nodesByIc,
        IReadOnlyList<NotchTableRow> rows,
        NotchComputationMode computationMode)
    {
        for (var icIndex = 0; icIndex < nodesByIc.Count; icIndex++)
        {
            var nodes = nodesByIc[icIndex];
            if (nodes.Count > ushort.MaxValue)
            {
                ThrowNodeCount(nodes.Count, DescribeV21Node(icIndex, nodes[ushort.MaxValue], rows, computationMode));
            }

            foreach (var node in nodes)
            {
                if (IsDiffIndexValid(node.DestinationDiffIndex) &&
                    IsDiffIndexValid(node.FirstDiffIndex) && IsDiffIndexValid(node.SecondDiffIndex))
                {
                    continue;
                }

                var context = DescribeV21Node(icIndex, node, rows, computationMode);
                ValidateDiffIndex(node.DestinationDiffIndex, "Anchor diff index", context);
                ValidateDiffIndex(node.FirstDiffIndex, "First target diff index", context);
                ValidateDiffIndex(node.SecondDiffIndex, "Second target diff index", context);
            }
        }
    }

    private static string DescribeV21Node(
        int icIndex,
        NotchV21FirmwareNode node,
        IReadOnlyList<NotchTableRow> rows,
        NotchComputationMode computationMode)
    {
        // Use the input only for row identity; all limits are checked on the emitted node.
        var sourceDiff = computationMode == NotchComputationMode.CadAllocation
            ? node.FirstDiffIndex
            : node.DestinationDiffIndex;
        var row = rows.FirstOrDefault(candidate => candidate.IcIndex == icIndex && candidate.Values[0] == sourceDiff &&
            (computationMode == NotchComputationMode.CadAllocation
                ? candidate.Values[0] == node.DestinationDiffIndex || candidate.Values[3] == node.DestinationDiffIndex ||
                  candidate.Values[6] == node.DestinationDiffIndex
                : candidate.Values[3] == node.FirstDiffIndex && candidate.Values[6] == node.SecondDiffIndex));
        return DescribeRow(icIndex, row?.DiffIndex ?? node.DestinationDiffIndex, row?.CadPadId);
    }

    private static void ValidateV22Nodes(IReadOnlyList<IReadOnlyList<NotchV22FirmwareRow>> nodesByIc)
    {
        foreach (var nodes in nodesByIc)
        {
            if (nodes.Count > ushort.MaxValue)
            {
                var overflowNode = nodes[ushort.MaxValue];
                ThrowNodeCount(nodes.Count,
                    DescribeRow(overflowNode.IcIndex, overflowNode.TableDiffIndex, overflowNode.CadPadId));
            }

            foreach (var node in nodes)
            {
                if (IsDiffIndexValid(node.SourceDiffIndex) && IsDiffIndexValid(node.FirstTargetDiffIndex) &&
                    IsDiffIndexValid(node.SecondTargetDiffIndex) && node.Flags is >= 0 and <= byte.MaxValue)
                {
                    continue;
                }

                var context = DescribeRow(node.IcIndex, node.TableDiffIndex, node.CadPadId);
                ValidateDiffIndex(node.SourceDiffIndex, "Anchor diff index", context);
                ValidateDiffIndex(node.FirstTargetDiffIndex, "First target diff index", context);
                ValidateDiffIndex(node.SecondTargetDiffIndex, "Second target diff index", context);
                if (node.Flags is < 0 or > byte.MaxValue)
                {
                    throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture,
                        $"V22 flags must be in [0,255] at {context}: {node.Flags}."));
                }
            }
        }
    }

    private static bool IsDiffIndexValid(int? value) => value is null or (>= 0 and <= ushort.MaxValue);

    private static void ValidateDiffIndex(int? value, string field, string context)
    {
        if (!IsDiffIndexValid(value))
        {
            throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture,
                $"{field} must be in [0,65535] at {context}: {value}."));
        }
    }

    private static void ThrowNodeCount(int count, string context)
    {
        throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture,
            $"Node count must be at most 65535 at {context}: {count}."));
    }

    private static string DescribeRow(int icIndex, int diffIndex, int? cadPadId)
    {
        var cad = cadPadId.HasValue
            ? string.Create(CultureInfo.InvariantCulture, $", CAD pad id {cadPadId.Value}")
            : string.Empty;
        return string.Create(CultureInfo.InvariantCulture, $"IC index {icIndex}, diff index {diffIndex}{cad}");
    }
}

internal sealed record NotchCExportProjection(
    IReadOnlyList<IReadOnlyList<NotchV21FirmwareNode>> V21Nodes,
    IReadOnlyList<IReadOnlyList<NotchV22FirmwareRow>> V22Nodes);
