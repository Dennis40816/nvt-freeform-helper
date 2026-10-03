using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Services;

internal static class RuntimeQueryResponseBuilder
{
    public static object BuildWorkflowPayload(WorkflowStateSnapshot workflow)
    {
        return new
        {
            hasCad = workflow.HasCad,
            hasGrid = workflow.HasGrid,
            hasStep1Result = workflow.HasStep1Result,
            hasStep2Result = workflow.HasStep2Result,
            hasStep3Result = workflow.HasStep3Result,
            hasStep4Result = workflow.HasStep4Result
        };
    }

    public static object BuildNotchPreviewPayload(FreeformHelperViewModel helper)
    {
        return new
        {
            isVisible = helper.ShowNotchCanvasPreview,
            showToRegularLabels = helper.ShowNotchToRegularLabels,
            stage = (int)Math.Round((double)helper.NotchPreviewVisualizationStep),
            autoPlayEnabled = helper.NotchPreviewAutoPlayEnabled,
            autoPlayIntervalMs = (double)helper.NotchPreviewAutoPlayIntervalMs,
            previewItemCount = helper.NotchCanvasPreviewItems.Count
        };
    }

    public static object BuildNotchCachePayload(
        FreeformHelperViewModel helper,
        RuntimeQueryNotchCacheMetrics queryCacheMetrics)
    {
        var vmMetrics = helper.GetNotchCompensationCacheMetrics();
        var exportMetrics = helper.GetNotchExportGenerationCacheMetrics();
        return new
        {
            step3Compensation = new
            {
                revision = vmMetrics.Revision,
                entryCount = vmMetrics.EntryCount,
                lastClearSize = vmMetrics.LastClearSize,
                hitCount = vmMetrics.HitCount,
                missCount = vmMetrics.MissCount,
                invalidationCount = vmMetrics.InvalidationCount,
                hitRate = vmMetrics.HitRate
            },
            queryCadCache = new
            {
                revision = queryCacheMetrics.Revision,
                entryCount = queryCacheMetrics.EntryCount,
                lastClearSize = queryCacheMetrics.LastClearSize,
                hitCount = queryCacheMetrics.HitCount,
                missCount = queryCacheMetrics.MissCount,
                revisionResetCount = queryCacheMetrics.RevisionResetCount,
                hitRate = queryCacheMetrics.HitRate
            },
            exportGenerationCache = new
            {
                exportMetrics.HasEntry,
                exportMetrics.EntryRowCount,
                exportMetrics.CadSignature,
                exportMetrics.GridSignature,
                exportMetrics.Step3Revision,
                exportMetrics.SettingsFingerprint,
                exportMetrics.HitCount,
                exportMetrics.MissCount,
                exportMetrics.StoreCount,
                exportMetrics.ClearCount
            }
        };
    }

    public static object BuildSelectionPayload(FreeformHelperViewModel helper)
    {
        var selectedCadIds = helper.SelectedCadPadIds
            .OrderBy(id => id)
            .ToList();
        var selectedRegularIndices = helper.SelectedRegularPadIndices
            .OrderBy(index => index)
            .ToList();
        var selectedRegularIndexSet = selectedRegularIndices.ToHashSet();
        var selectedRegularPadIds = helper.RegularPads
            .Where(pad => selectedRegularIndexSet.Contains(pad.Index))
            .Select(pad => pad.RegularPadId)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        var timings = helper.GetLastSelectionTimingSnapshot();

        return new
        {
            selectedCadIds,
            selectedRegularIndices,
            selectedRegularPadIds,
            timings = new
            {
                timings.SummaryMs,
                timings.InspectorMs,
                timings.NotchPreviewMs,
                timings.TotalMs
            }
        };
    }

    public static object BuildCadLoadSpinnerDebugPayload()
    {
        var snapshot = CadLoadSpinnerDebugState.GetSnapshot();
        return new
        {
            snapshot.VmOverlayShowCount,
            snapshot.VmOverlayHideCount,
            snapshot.ViewHostShowCount,
            snapshot.ViewHostHideCount,
            snapshot.HostShowRequestCount,
            snapshot.HostHideRequestCount,
            snapshot.HostStateSyncCount,
            snapshot.HostShowIpcSuccessCount,
            snapshot.HostShowIpcFailCount,
            snapshot.HostHideIpcCount,
            snapshot.HostShowRetryCount,
            events = snapshot.Events.Select(e => new
            {
                e.Sequence,
                e.TimestampUtc,
                e.Source,
                e.Detail
            }).ToList()
        };
    }

    public static object BuildStagePolygonPayload(string stageName, IReadOnlyList<Polygon2> polygons, int limit)
    {
        var returned = polygons
            .Take(limit)
            .Select((polygon, index) => new
            {
                index,
                bounds = new
                {
                    minX = polygon.Bounds.MinX,
                    minY = polygon.Bounds.MinY,
                    maxX = polygon.Bounds.MaxX,
                    maxY = polygon.Bounds.MaxY
                },
                vertices = polygon.Vertices.Select(vertex => new
                {
                    x = vertex.X,
                    y = vertex.Y
                })
            })
            .ToList();

        return new
        {
            name = stageName,
            totalPolygons = polygons.Count,
            returnedPolygons = returned.Count,
            polygons = returned
        };
    }

    public static object BuildValidationTraceRowPayload(NotchValidationTraceRow row)
    {
        var ratioPercent = row.RatioPercent;
        var effectiveArea = row.EffectiveArea;
        var v22 = BuildValidationV22Payload(row.V22);

        return new
        {
            row.KindText,
            row.RowNumber,
            version = row.Version.ToDisplayLabel(),
            row.IcIndex,
            row.SourceDiffIndex,
            row.TargetDiffIndex,
            row.SourceRegularPadId,
            row.TargetRegularPadId,
            row.CadPadId,
            ratioPercent,
            effectiveArea,
            row.SourceText,
            row.TargetText,
            row.NoteText,
            row.ValuesText,
            row.CommentText,
            values = row.Values,
            v22
        };
    }

    private static object? BuildValidationV22Payload(NotchValidationTraceV22Node? v22)
    {
        if (v22 is null)
        {
            return null;
        }

        return new
        {
            v22.Source,
            v22.AnchorDiffIndex,
            v22.CombinePercent,
            v22.TargetDiffIndex1,
            v22.TargetRatioPercent1,
            v22.TargetDiffIndex2,
            v22.TargetRatioPercent2,
            v22.Flags,
            legs = v22.Legs.Select(leg => new
            {
                leg.Slot,
                leg.TargetDiffIndex,
                leg.RatioPercent,
                leg.IsNone
            })
        };
    }
}
