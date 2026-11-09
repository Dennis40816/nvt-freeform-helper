using System.Diagnostics;
using System.Globalization;
using Avalonia.Threading;
using FreeformHelper.Application.Services;
using FreeformHelper.UI.Services;
using Nvt.Core.Avalonia.Threading;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    internal const string DeferredCadInspectorRefreshWorkName = "deferred-cad-inspector-refresh";
    internal const string DeferredRegularInspectorRefreshWorkName = "deferred-regular-inspector-refresh";

    private const int PadInspectorDeferredRefreshDelayMs = 200;
    private const int RegularPadInspectorDeferredRefreshDelayMs = 150;

    private readonly PadInspectorDeferredRefreshSlot _padInspectorDeferredCadSlot = new();
    private readonly PadInspectorDeferredRefreshSlot _padInspectorDeferredRegularSlot = new();

    public void QueueDeferredCadInspectorSnapshotRefresh(int cadPadId)
    {
        QueueDeferredCadInspectorSnapshotRefresh(
            cadPadId,
            _padInspectorRequests.ReadSelectionRevision());
    }

    public void QueueDeferredCadInspectorSnapshotRefresh(int cadPadId, long selectionRevision)
    {
        CancelDeferredCadInspectorSnapshotRefresh(clearPendingState: false);
        var cts = new CancellationTokenSource();
        _padInspectorDeferredCadSlot.Install(cts);
        var requestId = _padInspectorRequests.NextCadRefreshRequestId();
        UpdatePadInspectorDeferredRefreshPendingState();
        BackgroundWork.Run(
            DeferredCadInspectorRefreshWorkName,
            () => RunDeferredCadInspectorSnapshotRefreshAsync(requestId, cadPadId, selectionRevision, cts, cts.Token));
    }

    public void QueueDeferredRegularInspectorSnapshotRefresh(int regularPadId)
    {
        QueueDeferredRegularInspectorSnapshotRefresh(
            regularPadId,
            _padInspectorRequests.ReadSelectionRevision());
    }

    public void QueueDeferredRegularInspectorSnapshotRefresh(int regularPadId, long selectionRevision)
    {
        CancelDeferredRegularInspectorSnapshotRefresh(clearPendingState: false);
        var cts = new CancellationTokenSource();
        _padInspectorDeferredRegularSlot.Install(cts);
        var requestId = _padInspectorRequests.NextRegularRefreshRequestId();
        UpdatePadInspectorDeferredRefreshPendingState();
        BackgroundWork.Run(
            DeferredRegularInspectorRefreshWorkName,
            () => RunDeferredRegularInspectorSnapshotRefreshAsync(requestId, regularPadId, selectionRevision, cts, cts.Token));
    }

    private void CancelDeferredCadInspectorSnapshotRefresh(bool clearPendingState)
    {
        _padInspectorDeferredCadSlot.Cancel();

        if (clearPendingState)
        {
            UpdatePadInspectorDeferredRefreshPendingState();
        }
    }

    private void CancelDeferredRegularInspectorSnapshotRefresh(bool clearPendingState)
    {
        _padInspectorDeferredRegularSlot.Cancel();

        if (clearPendingState)
        {
            UpdatePadInspectorDeferredRefreshPendingState();
        }
    }

    private bool TryCreateDeferredCadInspectorContext(
        long requestId,
        int cadPadId,
        long selectionRevision,
        out DeferredCadInspectorContext context)
    {
        context = null!;
        if (!TryResolveNotchComputationInputs(cadPadId, out var cad, out var allCadPads, out var activeRegularPadIds))
        {
            return false;
        }

        if (_grid is null)
        {
            return false;
        }

        var activeRegularSnapshot = activeRegularPadIds is null
            ? null
            : activeRegularPadIds.ToHashSet();
        var allCadSnapshot = allCadPads.ToList();
        var strictOverlapRatio = GetStrictOverlapRatioForNotchCompensation();
        var workflowSnapshot = BuildWorkflowDataSnapshot();
        var anchorIcIndex = workflowSnapshot.TryGetCadIcIndex(cadPadId, out var icIndex)
            ? icIndex
            : (int?)null;
        var anchorDiffIndex = workflowSnapshot.TryGetCadOutputFwDiffIndex(cadPadId, out var diffIndex)
            ? diffIndex
            : (int?)null;
        var resolutionRequest = new NotchResolvedResultRequest(
            cad,
            _grid,
            CreateExportSettingsSnapshot(_projectFile.Settings),
            allCadSnapshot,
            activeRegularSnapshot,
            SelectedNotchCompensationModelOption.Value,
            EnableToRegular,
            EnableToFull,
            EnableToFullRuleEngine,
            EnableToFullRuleTrace,
            strictOverlapRatio,
            anchorIcIndex,
            anchorDiffIndex,
            NotchV22TargetAllocationPolicy.ResolveAreaMode(SelectedNotchCompensationModelOption.Value));

        context = new DeferredCadInspectorContext(
            RequestId: requestId,
            SelectionRevision: selectionRevision,
            ResolutionRequest: resolutionRequest,
            CadPoolFingerprint: ComputeCadPoolFingerprint(allCadSnapshot),
            ActiveRegularHash: ComputeActiveRegularHash(activeRegularSnapshot),
            GridFingerprint: BuildNotchComputationGridFingerprint(_grid),
            PrewarmedResolved: GetOrBuildCadV22ResolvedResultCached(
                cad,
                allCadSnapshot,
                activeRegularSnapshot,
                workflowSnapshot,
                strictOverlapRatio,
                buildIfMissing: false),
            WorkflowDataSnapshotRevision: _workflowDataSnapshotRevision,
            CacheRevision: _notchComputationSession.Revision);
        return true;
    }

    private async Task RunDeferredCadInspectorSnapshotRefreshAsync(
        long requestId,
        int cadPadId,
        long selectionRevision,
        CancellationTokenSource cts,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        DeferredCadInspectorContext? context = null;
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(PadInspectorDeferredRefreshDelayMs), TimeProvider.System, cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested || requestId != _padInspectorRequests.ReadCadRefreshRequestId())
            {
                return;
            }

            if (!UiThread.TryGetRunningDispatcher(out var dispatcher))
            {
                return;
            }

            await dispatcher!.InvokeAsync(() =>
            {
                context = CaptureDeferredCadInspectorContext(requestId, cadPadId, selectionRevision, cancellationToken);
            }, DispatcherPriority.Background, cancellationToken);

            if (context is null)
            {
                return;
            }

            var (resolved, diagnostics) = await ResolveDeferredCadInspectorAsync(context, cancellationToken).ConfigureAwait(false);

            if (!UiThread.TryGetRunningDispatcher(out dispatcher))
            {
                return;
            }

            await dispatcher!.InvokeAsync(() =>
            {
                if (!TryApplyDeferredCadInspectorSnapshot(context, resolved, diagnostics))
                {
                    return;
                }

                _logger.Debug(CultureInfo.InvariantCulture, "Pad inspector deferred refresh applied: CAD={0}, elapsed={1}ms (delay={2}ms).",
                    context.ResolutionRequest.CadPad.Id,
                    sw.ElapsedMilliseconds,
                    PadInspectorDeferredRefreshDelayMs);
            }, DispatcherPriority.Background, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Deferred refresh canceled.
        }
        catch (Exception ex) when (ExceptionPolicy.IsRecoverable(ex))
        {
            _logger.Warn(
                ex,
                "Pad inspector deferred refresh failed: CAD={0}.",
                cadPadId);
        }
        finally
        {
            if (_padInspectorDeferredCadSlot.TryComplete(cts))
            {
                UpdatePadInspectorDeferredRefreshPendingState();
            }
        }
    }

    private DeferredCadInspectorContext? CaptureDeferredCadInspectorContext(
        long requestId,
        int cadPadId,
        long selectionRevision,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested || requestId != _padInspectorRequests.ReadCadRefreshRequestId())
        {
            return null;
        }

        if (!TryCreateDeferredCadInspectorContext(requestId, cadPadId, selectionRevision, out var created))
        {
            return null;
        }

        return created;
    }

    private static async Task<(NotchV22ResolvedResult Resolved, string? Diagnostics)> ResolveDeferredCadInspectorAsync(
        DeferredCadInspectorContext context,
        CancellationToken cancellationToken)
    {
        var resolved = context.PrewarmedResolved;
        if (resolved is null)
        {
            resolved = await Task.Run(
                () => NotchDetailUseCase.BuildResolvedResult(context.ResolutionRequest),
                cancellationToken).ConfigureAwait(false);
        }

        var diagnostics = BuildToFullDiagnosticsText(resolved.Compensation);
        return (resolved, diagnostics);
    }

    private bool TryApplyDeferredCadInspectorSnapshot(
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
            _ = GetOrBuildCadV22ResolvedResultCached(
                request.CadPad,
                request.AllCadPads,
                request.ActiveRegularPadIds,
                BuildWorkflowDataSnapshot(),
                request.StrictOverlapRatio,
                precomputedResolved: resolved);
        }
        var snapshot = BuildCadPadInspectorSnapshot(
            request.CadPad.Id,
            includeExpensiveNotchDetails: true,
            precomputedDiagnostics: diagnostics);
        if (snapshot is null)
        {
            return false;
        }

        PadInspectorUi.Snapshot = snapshot;
        return true;
    }

    private async Task RunDeferredRegularInspectorSnapshotRefreshAsync(
        long requestId,
        int regularPadId,
        long selectionRevision,
        CancellationTokenSource cts,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(RegularPadInspectorDeferredRefreshDelayMs), TimeProvider.System, cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested ||
                requestId != _padInspectorRequests.ReadRegularRefreshRequestId())
            {
                return;
            }

            if (!UiThread.TryGetRunningDispatcher(out var dispatcher))
            {
                return;
            }

            await dispatcher!.InvokeAsync(() =>
            {
                if (!CanApplyDeferredRegularInspectorSnapshot(requestId, regularPadId, selectionRevision))
                {
                    return;
                }

                var snapshot = BuildRegularPadInspectorSnapshot(
                    regularPadId,
                    includeMatchDetails: true,
                    includeRuleTrace: true);
                if (snapshot is null)
                {
                    return;
                }

                PadInspectorUi.Snapshot = snapshot;
                _logger.Debug(CultureInfo.InvariantCulture, "Pad inspector deferred refresh applied: REG={0}, elapsed={1}ms (delay={2}ms).",
                    regularPadId,
                    sw.ElapsedMilliseconds,
                    RegularPadInspectorDeferredRefreshDelayMs);
            }, DispatcherPriority.Background, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Deferred refresh canceled.
        }
        catch (Exception ex) when (ExceptionPolicy.IsRecoverable(ex))
        {
            _logger.Warn(
                ex,
                "Pad inspector deferred refresh failed: REG={0}.",
                regularPadId);
        }
        finally
        {
            if (_padInspectorDeferredRegularSlot.TryComplete(cts))
            {
                UpdatePadInspectorDeferredRefreshPendingState();
            }
        }
    }

    private bool CanApplyDeferredCadInspectorSnapshot(DeferredCadInspectorContext context)
    {
        var request = context.ResolutionRequest;
        if (context.RequestId != _padInspectorRequests.ReadCadRefreshRequestId())
        {
            return false;
        }

        if (context.SelectionRevision != _padInspectorRequests.ReadSelectionRevision())
        {
            return false;
        }

        if (_notchComputationSession.Revision != context.CacheRevision)
        {
            return false;
        }

        if (_workflowDataSnapshotRevision != context.WorkflowDataSnapshotRevision)
        {
            return false;
        }

        if (_grid is null || BuildNotchComputationGridFingerprint(_grid) != context.GridFingerprint)
        {
            return false;
        }

        if (!TryResolveNotchComputationInputs(
                request.CadPad.Id,
                out var currentCadPad,
                out var currentCadPads,
                out var currentActiveRegularPadIds) ||
            ComputeCadPoolFingerprint(currentCadPads) != context.CadPoolFingerprint ||
            ComputeActiveRegularHash(currentActiveRegularPadIds) != context.ActiveRegularHash)
        {
            return false;
        }

        if (EnableToRegular != request.EnableToRegular ||
            EnableToFull != request.EnableToFull ||
            EnableToFullRuleEngine != request.EnableToFullRuleEngine ||
            EnableToFullRuleTrace != request.EnableToFullRuleTrace)
        {
            return false;
        }

        var currentStrictOverlapRatio = GetStrictOverlapRatioForNotchCompensation();
        if (Math.Abs(currentStrictOverlapRatio - request.StrictOverlapRatio) > 1e-12)
        {
            return false;
        }

        if (context.PrewarmedResolved is not null &&
            !ReferenceEquals(
                GetOrBuildCadV22ResolvedResultCached(
                    currentCadPad,
                    currentCadPads,
                    currentActiveRegularPadIds,
                    BuildWorkflowDataSnapshot(),
                    request.StrictOverlapRatio,
                    buildIfMissing: false),
                context.PrewarmedResolved))
        {
            return false;
        }

        return PadInspectorUi.Snapshot?.Cad?.CadPadId == request.CadPad.Id;
    }

    private bool CanApplyDeferredRegularInspectorSnapshot(long requestId, int regularPadId, long selectionRevision)
    {
        if (requestId != _padInspectorRequests.ReadRegularRefreshRequestId())
        {
            return false;
        }

        if (selectionRevision != _padInspectorRequests.ReadSelectionRevision())
        {
            return false;
        }

        return PadInspectorUi.Snapshot?.Regular?.RegularPadId == regularPadId;
    }

    private void UpdatePadInspectorDeferredRefreshPendingState()
    {
        PadInspectorUi.IsDeferredRefreshPending =
            _padInspectorDeferredCadSlot.IsActive ||
            _padInspectorDeferredRegularSlot.IsActive;
    }

    private sealed record DeferredCadInspectorContext(
        long RequestId,
        long SelectionRevision,
        NotchResolvedResultRequest ResolutionRequest,
        ulong CadPoolFingerprint,
        int ActiveRegularHash,
        ulong GridFingerprint,
        NotchV22ResolvedResult? PrewarmedResolved,
        int WorkflowDataSnapshotRevision,
        int CacheRevision);
}

