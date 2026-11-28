// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.ViewModels.Inspector;

/// <summary>Captures deferred inputs and accepts results against the shared inspector request stamps.</summary>
internal sealed class InspectorDeferredRefreshOwner
{
    private readonly PadInspectorRequestState _requests;
    private readonly InspectorDeferredContextPorts _context;
    private readonly InspectorDeferredResultPorts _results;

    public InspectorDeferredRefreshOwner(
        PadInspectorRequestState requests, InspectorDeferredContextPorts context, InspectorDeferredResultPorts results)
    {
        _requests = requests;
        _context = context;
        _results = results;
    }

    private bool TryCreateDeferredCadInspectorContext(
        long requestId,
        int cadPadId,
        long selectionRevision,
        out DeferredCadInspectorContext context)
    {
        context = null!;
        if (!_context.TryResolveInputs(cadPadId, out var cad, out var allCadPads, out var activeRegularPadIds))
        {
            return false;
        }

        if (_context.ReadGrid() is null)
        {
            return false;
        }

        var activeRegularSnapshot = activeRegularPadIds is null
            ? null
            : activeRegularPadIds.ToHashSet();
        var allCadSnapshot = allCadPads.ToList();
        var strictOverlapRatio = _context.ReadStrictOverlapRatio();
        var workflowSnapshot = _context.CaptureWorkflow();
        var anchorIcIndex = workflowSnapshot.GetIcIndex(cadPadId);
        var anchorDiffIndex = workflowSnapshot.GetOutputIndex(cadPadId);
        var resolutionRequest = new InspectorDeferredCadRequest(
            cad,
            _context.ReadGrid()!,
            _context.CaptureSettings(),
            allCadSnapshot,
            activeRegularSnapshot,
            _context.ReadCompensationModel(),
            _context.ReadEnableToRegular(),
            _context.ReadEnableToFull(),
            _context.ReadEnableToFullRuleEngine(),
            _context.ReadEnableToFullRuleTrace(),
            strictOverlapRatio,
            anchorIcIndex,
            anchorDiffIndex,
            NotchV22TargetAllocationPolicy.ResolveAreaMode(_context.ReadCompensationModel()));

        context = new DeferredCadInspectorContext(
            RequestId: requestId,
            SelectionRevision: selectionRevision,
            ResolutionRequest: resolutionRequest,
            CadPoolFingerprint: NotchExportFingerprints.ComputeCadPoolFingerprint(allCadSnapshot),
            ActiveRegularHash: NotchExportFingerprints.ComputeActiveRegularHash(activeRegularSnapshot),
            GridFingerprint: _context.GetGridFingerprint(_context.ReadGrid()!),
            PrewarmedResolved: workflowSnapshot.GetCachedResolved(
                cad,
                allCadSnapshot,
                activeRegularSnapshot,
                strictOverlapRatio,
                precomputedResolved: null,
                buildIfMissing: false),
            WorkflowDataSnapshotRevision: _context.ReadWorkflowRevision(),
            CacheRevision: _context.ReadCacheRevision());
        return true;
    }

    public DeferredCadInspectorContext? CaptureDeferredCadInspectorContext(
        long requestId,
        int cadPadId,
        long selectionRevision,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested || requestId != _requests.ReadCadRefreshRequestId())
        {
            return null;
        }

        if (!TryCreateDeferredCadInspectorContext(requestId, cadPadId, selectionRevision, out var created))
        {
            return null;
        }

        return created;
    }

    public async Task<(NotchV22ResolvedResult Resolved, string? Diagnostics)> ResolveDeferredCadInspectorAsync(
        DeferredCadInspectorContext context,
        CancellationToken cancellationToken)
    {
        var resolved = context.PrewarmedResolved;
        if (resolved is null)
        {
            resolved = await _results.BuildResolvedAsync(context.ResolutionRequest, cancellationToken).ConfigureAwait(false);
        }

        var diagnostics = _results.BuildDiagnostics(resolved.Compensation);
        return (resolved, diagnostics);
    }

    public bool TryApplyDeferredCadInspectorSnapshot(
        DeferredCadInspectorContext context,
        NotchV22ResolvedResult resolved,
        string? diagnostics)
    {
        if (!CanApplyDeferredCadInspectorSnapshot(context))
        {
            return false;
        }

        var request = context.ResolutionRequest;
        if (context.PrewarmedResolved is null)
        {
            _ = _context.CaptureWorkflow().GetCachedResolved(
                request.CadPad,
                request.AllCadPads,
                request.ActiveRegularPadIds,
                request.StrictOverlapRatio,
                precomputedResolved: resolved,
                buildIfMissing: true);
        }
        var snapshot = _results.BuildCadSnapshot(request.CadPad.Id, diagnostics);
        if (snapshot is null)
        {
            return false;
        }

        _results.PublishSnapshot(snapshot);
        return true;
    }

    private bool CanApplyDeferredCadInspectorSnapshot(DeferredCadInspectorContext context)
    {
        var request = context.ResolutionRequest;
        if (context.RequestId != _requests.ReadCadRefreshRequestId())
        {
            return false;
        }

        if (context.SelectionRevision != _requests.ReadSelectionRevision())
        {
            return false;
        }

        if (_context.ReadCacheRevision() != context.CacheRevision)
        {
            return false;
        }

        if (_context.ReadWorkflowRevision() != context.WorkflowDataSnapshotRevision)
        {
            return false;
        }

        if (_context.ReadGrid() is null || _context.GetGridFingerprint(_context.ReadGrid()!) != context.GridFingerprint)
        {
            return false;
        }

        if (!_context.TryResolveInputs(
                request.CadPad.Id,
                out var currentCadPad,
                out var currentCadPads,
                out var currentActiveRegularPadIds) ||
            NotchExportFingerprints.ComputeCadPoolFingerprint(currentCadPads) != context.CadPoolFingerprint ||
            NotchExportFingerprints.ComputeActiveRegularHash(currentActiveRegularPadIds) != context.ActiveRegularHash)
        {
            return false;
        }

        if (_context.ReadEnableToRegular() != request.EnableToRegular ||
            _context.ReadEnableToFull() != request.EnableToFull ||
            _context.ReadEnableToFullRuleEngine() != request.EnableToFullRuleEngine ||
            _context.ReadEnableToFullRuleTrace() != request.EnableToFullRuleTrace)
        {
            return false;
        }

        var currentStrictOverlapRatio = _context.ReadStrictOverlapRatio();
        if (Math.Abs(currentStrictOverlapRatio - request.StrictOverlapRatio) > 1e-12)
        {
            return false;
        }

        if (context.PrewarmedResolved is not null &&
            !ReferenceEquals(
                _context.CaptureWorkflow().GetCachedResolved(
                    currentCadPad,
                    currentCadPads,
                    currentActiveRegularPadIds,
                    request.StrictOverlapRatio,
                    precomputedResolved: null,
                    buildIfMissing: false),
                context.PrewarmedResolved))
        {
            return false;
        }

        return _results.ReadSnapshot()?.Cad?.CadPadId == request.CadPad.Id;
    }

    public bool CanApplyDeferredRegularInspectorSnapshot(long requestId, int regularPadId, long selectionRevision)
    {
        if (requestId != _requests.ReadRegularRefreshRequestId())
        {
            return false;
        }

        if (selectionRevision != _requests.ReadSelectionRevision())
        {
            return false;
        }

        return _results.ReadSnapshot()?.Regular?.RegularPadId == regularPadId;
    }
}
