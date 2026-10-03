using System.Diagnostics.CodeAnalysis;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Services;

[SuppressMessage(
    "Performance",
    "CA1822:Mark members as static",
    Justification = "Instance service API is retained for workflow composition stability.")]
public sealed class DxfRegularMappingUseCase
{
    private readonly DxfRegularMappingAnalyzer _analyzer = new();

    public DxfRegularMappingResult Analyze(
        CadPadSet cad,
        RegularGrid grid,
        ProjectSettings settings,
        IReadOnlyDictionary<int, int>? manualOverrides = null,
        IReadOnlyDictionary<int, int>? expectedDiffIndexByCadId = null,
        IReadOnlyDictionary<int, IReadOnlyList<PadMatchLink>>? cadToRegular = null,
        IReadOnlyDictionary<int, int>? cadIcIndexByCadId = null,
        IReadOnlySet<int>? activeRegularPadIds = null,
        IReadOnlyDictionary<int, CadOutputFwDiffAssignmentDecision>? assignmentDecisionByCadId = null,
        Action<double>? reportProgress = null)
    {
        var result = DxfRegularMappingAnalyzer.Analyze(
            cad,
            grid,
            settings.IndexMapping,
            manualOverrides,
            expectedDiffIndexByCadId,
            reportProgress);

        if (cadToRegular is null || cadIcIndexByCadId is null)
        {
            return result;
        }

        var regularPadById = grid.Pads.ToDictionary(pad => pad.RegularPadId);
        var maskAuditRows = DxfRegularMaskAuditService.BuildAuditRows(
            cad.Pads,
            cadToRegular,
            regularPadById,
            cadIcIndexByCadId,
            activeRegularPadIds,
            expectedDiffIndexByCadId,
            manualOverrideDiffByCadId: null,
            assignmentDecisionByCadId);

        var maskChangedCount = maskAuditRows.Count(row => row.Status == DxfRegularMaskAuditStatus.ChangedByMask);
        var maskRemovedCount = maskAuditRows.Count(row => row.Status == DxfRegularMaskAuditStatus.MaskRemovedMatch);
        var report = result.Report with
        {
            Summary = AppendMaskSummary(result.Report.Summary, maskAuditRows.Count, maskChangedCount, maskRemovedCount),
            MaskAuditRows = maskAuditRows,
            MaskChangedCount = maskChangedCount,
            MaskRemovedCount = maskRemovedCount,
        };

        return result with { Report = report };
    }

    private static string AppendMaskSummary(string summary, int maskAuditCount, int maskChangedCount, int maskRemovedCount)
    {
        if (maskAuditCount <= 0)
        {
            return summary;
        }

        return $"{summary}, maskChanged={maskChangedCount}, maskRemoved={maskRemovedCount}";
    }
}
