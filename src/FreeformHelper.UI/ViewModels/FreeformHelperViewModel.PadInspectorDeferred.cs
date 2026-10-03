using System.Diagnostics;
using System.Globalization;
using Avalonia.Threading;
using FreeformHelper.Application.Services;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private const int PadInspectorDeferredRefreshDelayMs = 200;
    private const int RegularPadInspectorDeferredRefreshDelayMs = 150;

    private CancellationTokenSource? _padInspectorDeferredRefreshCts;
    private long _padInspectorDeferredRefreshRequestId;
    private CancellationTokenSource? _regularPadInspectorDeferredRefreshCts;
    private long _regularPadInspectorDeferredRefreshRequestId;

    public void QueueDeferredCadInspectorSnapshotRefresh(int cadPadId)
    {
        QueueDeferredCadInspectorSnapshotRefresh(
            cadPadId,
            Interlocked.Read(ref _padInspectorSelectionRevision));
    }

    public void QueueDeferredCadInspectorSnapshotRefresh(int cadPadId, long selectionRevision)
    {
        CancelDeferredCadInspectorSnapshotRefresh(clearPendingState: false);
        var cts = new CancellationTokenSource();
        _padInspectorDeferredRefreshCts = cts;
        var requestId = Interlocked.Increment(ref _padInspectorDeferredRefreshRequestId);
        UpdatePadInspectorDeferredRefreshPendingState();
        _ = RunDeferredCadInspectorSnapshotRefreshAsync(requestId, cadPadId, selectionRevision, cts, cts.Token);
    }

    public void QueueDeferredRegularInspectorSnapshotRefresh(int regularPadId)
    {
        QueueDeferredRegularInspectorSnapshotRefresh(
            regularPadId,
            Interlocked.Read(ref _padInspectorSelectionRevision));
    }

    public void QueueDeferredRegularInspectorSnapshotRefresh(int regularPadId, long selectionRevision)
    {
        CancelDeferredRegularInspectorSnapshotRefresh(clearPendingState: false);
        var cts = new CancellationTokenSource();
        _regularPadInspectorDeferredRefreshCts = cts;
        var requestId = Interlocked.Increment(ref _regularPadInspectorDeferredRefreshRequestId);
        UpdatePadInspectorDeferredRefreshPendingState();
        _ = RunDeferredRegularInspectorSnapshotRefreshAsync(requestId, regularPadId, selectionRevision, cts, cts.Token);
    }

    private void CancelDeferredCadInspectorSnapshotRefresh(bool clearPendingState)
    {
        var cts = Interlocked.Exchange(ref _padInspectorDeferredRefreshCts, null);
        if (cts is not null)
        {
            try
            {
                cts.Cancel();
            }
            finally
            {
                cts.Dispose();
            }
        }

        if (clearPendingState)
        {
            UpdatePadInspectorDeferredRefreshPendingState();
        }
    }

    private void CancelDeferredRegularInspectorSnapshotRefresh(bool clearPendingState)
    {
        var cts = Interlocked.Exchange(ref _regularPadInspectorDeferredRefreshCts, null);
        if (cts is not null)
        {
            try
            {
                cts.Cancel();
            }
            finally
            {
                cts.Dispose();
            }
        }

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
            CacheRevision: _notchCompensationCacheRevision);
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
            await Task.Delay(PadInspectorDeferredRefreshDelayMs, cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested || requestId != Interlocked.Read(ref _padInspectorDeferredRefreshRequestId))
            {
                return;
            }

            if (!UiThread.TryGetRunningDispatcher(out var dispatcher))
            {
                return;
            }

            await dispatcher!.InvokeAsync(() =>
            {
                if (cancellationToken.IsCancellationRequested || requestId != Interlocked.Read(ref _padInspectorDeferredRefreshRequestId))
                {
                    return;
                }

                if (!TryCreateDeferredCadInspectorContext(requestId, cadPadId, selectionRevision, out var created))
                {
                    return;
                }

                context = created;
            }, DispatcherPriority.Background, cancellationToken);

            if (context is null)
            {
                return;
            }

            var resolved = context.PrewarmedResolved;
            if (resolved is null)
            {
                resolved = await Task.Run(
                    () => NotchDetailUseCase.BuildResolvedResult(context.ResolutionRequest),
                    cancellationToken).ConfigureAwait(false);
            }
            var diagnostics = BuildToFullDiagnosticsText(resolved.Compensation);

            if (!UiThread.TryGetRunningDispatcher(out dispatcher))
            {
                return;
            }

            await dispatcher!.InvokeAsync(() =>
            {
                if (!CanApplyDeferredCadInspectorSnapshot(context))
                {
                    return;
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
                    return;
                }

                CurrentPadInspectorSnapshot = snapshot;
                Logger.Debug(CultureInfo.InvariantCulture, "Pad inspector deferred refresh applied: CAD={0}, elapsed={1}ms (delay={2}ms).",
                    request.CadPad.Id,
                    sw.ElapsedMilliseconds,
                    PadInspectorDeferredRefreshDelayMs);
            }, DispatcherPriority.Background, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Deferred refresh canceled.
        }
        catch (Exception ex)
        {
            Logger.Warn(
                ex,
                "Pad inspector deferred refresh failed: CAD={0}.",
                cadPadId);
        }
        finally
        {
            if (ReferenceEquals(Interlocked.CompareExchange(ref _padInspectorDeferredRefreshCts, null, cts), cts))
            {
                cts.Dispose();
                UpdatePadInspectorDeferredRefreshPendingState();
            }
        }
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
            await Task.Delay(RegularPadInspectorDeferredRefreshDelayMs, cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested ||
                requestId != Interlocked.Read(ref _regularPadInspectorDeferredRefreshRequestId))
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

                CurrentPadInspectorSnapshot = snapshot;
                Logger.Debug(CultureInfo.InvariantCulture, "Pad inspector deferred refresh applied: REG={0}, elapsed={1}ms (delay={2}ms).",
                    regularPadId,
                    sw.ElapsedMilliseconds,
                    RegularPadInspectorDeferredRefreshDelayMs);
            }, DispatcherPriority.Background, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Deferred refresh canceled.
        }
        catch (Exception ex)
        {
            Logger.Warn(
                ex,
                "Pad inspector deferred refresh failed: REG={0}.",
                regularPadId);
        }
        finally
        {
            if (ReferenceEquals(Interlocked.CompareExchange(ref _regularPadInspectorDeferredRefreshCts, null, cts), cts))
            {
                cts.Dispose();
                UpdatePadInspectorDeferredRefreshPendingState();
            }
        }
    }

    private bool CanApplyDeferredCadInspectorSnapshot(DeferredCadInspectorContext context)
    {
        var request = context.ResolutionRequest;
        if (context.RequestId != Interlocked.Read(ref _padInspectorDeferredRefreshRequestId))
        {
            return false;
        }

        if (context.SelectionRevision != Interlocked.Read(ref _padInspectorSelectionRevision))
        {
            return false;
        }

        if (_notchCompensationCacheRevision != context.CacheRevision)
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

        return CurrentPadInspectorSnapshot?.Cad?.CadPadId == request.CadPad.Id;
    }

    private bool CanApplyDeferredRegularInspectorSnapshot(long requestId, int regularPadId, long selectionRevision)
    {
        if (requestId != Interlocked.Read(ref _regularPadInspectorDeferredRefreshRequestId))
        {
            return false;
        }

        if (selectionRevision != Interlocked.Read(ref _padInspectorSelectionRevision))
        {
            return false;
        }

        return CurrentPadInspectorSnapshot?.Regular?.RegularPadId == regularPadId;
    }

    private void UpdatePadInspectorDeferredRefreshPendingState()
    {
        IsPadInspectorDeferredRefreshPending =
            _padInspectorDeferredRefreshCts is not null ||
            _regularPadInspectorDeferredRefreshCts is not null;
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

