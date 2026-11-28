// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels.Inspector;

internal delegate bool InspectorNotchInputLookup(
    int cadPadId, out CadPad cad, out IReadOnlyList<CadPad> allCadPads, out IReadOnlySet<int>? activeRegularPadIds);

internal delegate NotchV22ResolvedResult? InspectorResolvedCacheLookup(
    CadPad cad, IReadOnlyList<CadPad> allCadPads, IReadOnlySet<int>? activeRegularPadIds,
    double strictOverlapRatio, NotchV22ResolvedResult? precomputedResolved, bool buildIfMissing);

// These delegates all read the same accepted workflow snapshot.
internal readonly record struct InspectorDeferredCadWorkflow(
    Func<int, int?> GetIcIndex,
    Func<int, int?> GetOutputIndex,
    InspectorResolvedCacheLookup GetCachedResolved);

internal readonly record struct InspectorDeferredContextPorts(
    InspectorNotchInputLookup TryResolveInputs,
    Func<RegularGrid?> ReadGrid,
    Func<ProjectSettings> CaptureSettings,
    Func<NotchCompensationModel> ReadCompensationModel,
    Func<bool> ReadEnableToRegular,
    Func<bool> ReadEnableToFull,
    Func<bool> ReadEnableToFullRuleEngine,
    Func<bool> ReadEnableToFullRuleTrace,
    Func<double> ReadStrictOverlapRatio,
    Func<InspectorDeferredCadWorkflow> CaptureWorkflow,
    Func<RegularGrid, ulong> GetGridFingerprint,
    Func<int> ReadWorkflowRevision,
    Func<int> ReadCacheRevision);

internal readonly record struct InspectorDeferredResultPorts(
    Func<InspectorDeferredCadRequest, CancellationToken, Task<NotchV22ResolvedResult>> BuildResolvedAsync,
    Func<NotchV22CompensationResult, string?> BuildDiagnostics,
    Func<int, string?, PadInspectorSnapshot?> BuildCadSnapshot,
    Func<PadInspectorSnapshot?> ReadSnapshot,
    Action<PadInspectorSnapshot> PublishSnapshot);

// A captured request contains values only; the host adapts it to the existing Notch service request.
internal sealed record InspectorDeferredCadRequest(
    CadPad CadPad, RegularGrid Grid, ProjectSettings Settings,
    IReadOnlyList<CadPad> AllCadPads,
    IReadOnlySet<int>? ActiveRegularPadIds,
    NotchCompensationModel CompensationModel,
    bool EnableToRegular, bool EnableToFull,
    bool EnableToFullRuleEngine, bool EnableToFullRuleTrace,
    double StrictOverlapRatio, int? AnchorIcIndex, int? AnchorDiffIndex,
    NotchV22TargetAllocationAreaMode AllocationAreaMode);

internal sealed record DeferredCadInspectorContext(
    long RequestId,
    long SelectionRevision,
    InspectorDeferredCadRequest ResolutionRequest,
    ulong CadPoolFingerprint,
    int ActiveRegularHash,
    ulong GridFingerprint,
    NotchV22ResolvedResult? PrewarmedResolved,
    int WorkflowDataSnapshotRevision,
    int CacheRevision);
