// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels.Inspector;

// Each capture wraps one accepted workflow snapshot without exposing the service type.
internal readonly record struct InspectorCadWorkflow(
    Func<int, int?> GetOutputIndex,
    Func<int, int?> GetIcIndex,
    Func<int, bool, NotchV22CompensationResult?, string?, PadInspectorNotchSnapshot?> BuildNotch);

internal readonly record struct InspectorSnapshotPorts(
    Func<int, CadPad?> FindCad,
    Func<int, RegularPad?> FindRegular,
    Func<InspectorCadWorkflow> CaptureWorkflow,
    Func<int, int?> GetOutputOverride,
    Func<int, bool> IsOutputAnchor,
    Func<CadOutputFwDiffAutoMode> ReadAssignmentMode,
    Func<CadPad, NotchCadRowEligibility> BuildRowEligibility,
    Func<int, int> ToDisplayRow);

internal readonly record struct InspectorMatchPorts(
    Func<int, IReadOnlyList<PadMatchLink>> GetRegularLinks,
    Func<int, IReadOnlyList<int>> GetRegularIds,
    Func<int, (int IcIndex, int DiffIndex)?> GetRegularIcDiff,
    Func<int, IReadOnlyList<PadMatchLink>> GetCadLinks,
    Func<int, IReadOnlyList<int>> GetCadIds,
    PadInspectorMatchDetailBuilder.CadDxfIndexLookup TryGetOutputIndex,
    Func<IReadOnlyDictionary<int, FreeformType>> ReadFreeformOverrides);

internal readonly record struct InspectorDisplayPorts(
    Action<string> PropertyChanging,
    Action<string> PropertyChanged,
    Func<PadInspectorSnapshot, PadInspectorDisplay?> BuildDisplay,
    Func<int> ReadSelectedCount,
    Action<bool> WriteRawTraceExpanded);

internal interface IInspectorRefresh
{
    void QueueDeferredCadInspectorSnapshotRefresh(int cadPadId);
    void QueueDeferredCadInspectorSnapshotRefresh(int cadPadId, long selectionRevision);
    void QueueDeferredRegularInspectorSnapshotRefresh(int regularPadId);
    void QueueDeferredRegularInspectorSnapshotRefresh(int regularPadId, long selectionRevision);
    void CancelCad(bool clearPendingState);
    void CancelRegular(bool clearPendingState);
    void UpdatePendingState();
}

internal readonly record struct InspectorRefreshPorts<TContext>(
    Func<long, int, long, CancellationToken, TContext?> CaptureCad,
    Func<TContext, CancellationToken, Task<(NotchV22ResolvedResult Resolved, string? Diagnostics)>> ResolveCadAsync,
    Func<TContext, NotchV22ResolvedResult, string?, bool> ApplyCad,
    Func<long, int, long, bool> CanApplyRegular,
    Func<int, bool, bool, PadInspectorSnapshot?> BuildRegular,
    Action<PadInspectorSnapshot> PublishSnapshot,
    Action<string, Func<Task>> RunBackground,
    Func<Action, CancellationToken, Task<bool>> DispatchAsync,
    Func<int, CancellationToken, Task> DelayAsync,
    Func<Func<Task>, Action<Exception>, Action, Task> RunSafelyAsync,
    Action<TContext, long, int> LogCadApplied,
    Action<int, long, int> LogRegularApplied,
    Action<Exception, int> WarnCad,
    Action<Exception, int> WarnRegular)
    where TContext : class;
