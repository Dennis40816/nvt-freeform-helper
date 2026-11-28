using System.Globalization;
using Avalonia.Threading;
using FreeformHelper.Application.Services;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels.Inspector;
using Nvt.Core.Avalonia.Threading;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    internal const string DeferredCadInspectorRefreshWorkName = "deferred-cad-inspector-refresh";
    internal const string DeferredRegularInspectorRefreshWorkName = "deferred-regular-inspector-refresh";

    private InspectorRefreshOwner<DeferredCadInspectorContext> CreateInspectorRefreshOwner(
        PadInspectorRequestState requests, Action<bool> writePending)
    {
        var deferred = new InspectorDeferredRefreshOwner(
            requests,
            new InspectorDeferredContextPorts(
                TryResolveNotchComputationInputs,
                () => _grid,
                () => CreateExportSettingsSnapshot(_projectFile.Settings),
                () => SelectedNotchCompensationModelOption.Value,
                () => EnableToRegular,
                () => EnableToFull,
                () => EnableToFullRuleEngine,
                () => EnableToFullRuleTrace,
                GetStrictOverlapRatioForNotchCompensation,
                CaptureDeferredInspectorWorkflow,
                BuildNotchComputationGridFingerprint,
                () => _workflowDataSnapshotRevision,
                () => _notchComputationSession.Revision),
            new InspectorDeferredResultPorts(
                BuildDeferredInspectorResolvedAsync,
                BuildToFullDiagnosticsText,
                (id, diagnostics) => BuildCadPadInspectorSnapshot(
                    id, includeExpensiveNotchDetails: true, precomputedDiagnostics: diagnostics),
                () => PadInspectorUi.Snapshot,
                snapshot => PadInspectorUi.Snapshot = snapshot));
        return new InspectorRefreshOwner<DeferredCadInspectorContext>(
            requests,
            new InspectorRefreshPorts<DeferredCadInspectorContext>(
                deferred.CaptureDeferredCadInspectorContext,
                deferred.ResolveDeferredCadInspectorAsync,
                deferred.TryApplyDeferredCadInspectorSnapshot,
                deferred.CanApplyDeferredRegularInspectorSnapshot,
                BuildRegularPadInspectorSnapshot,
                snapshot => PadInspectorUi.Snapshot = snapshot,
                (name, work) => BackgroundWork.Run(name, work),
                DispatchInspectorRefreshAsync,
                (delay, token) => Task.Delay(TimeSpan.FromMilliseconds(delay), TimeProvider.System, token),
                RunInspectorRefreshSafelyAsync,
                (context, elapsed, delay) => _logger.Debug(CultureInfo.InvariantCulture,
                    "Pad inspector deferred refresh applied: CAD={0}, elapsed={1}ms (delay={2}ms).",
                    context.ResolutionRequest.CadPad.Id, elapsed, delay),
                (id, elapsed, delay) => _logger.Debug(CultureInfo.InvariantCulture,
                    "Pad inspector deferred refresh applied: REG={0}, elapsed={1}ms (delay={2}ms).",
                    id, elapsed, delay),
                (ex, id) => _logger.Warn(ex, "Pad inspector deferred refresh failed: CAD={0}.", id),
                (ex, id) => _logger.Warn(ex, "Pad inspector deferred refresh failed: REG={0}.", id)),
            writePending,
            DeferredCadInspectorRefreshWorkName,
            DeferredRegularInspectorRefreshWorkName);
    }

    private InspectorDeferredCadWorkflow CaptureDeferredInspectorWorkflow()
    {
        var snapshot = BuildWorkflowDataSnapshot();
        return new InspectorDeferredCadWorkflow(
            id => snapshot.TryGetCadIcIndex(id, out var index) ? index : (int?)null,
            id => snapshot.TryGetCadOutputFwDiffIndex(id, out var index) ? index : (int?)null,
            (cad, allCad, activeRegular, strictOverlap, precomputed, buildIfMissing) => GetOrBuildCadV22ResolvedResultCached(
                cad, allCad, activeRegular, snapshot, strictOverlap,
                precomputedResolved: precomputed, buildIfMissing: buildIfMissing));
    }

    private static Task<NotchV22ResolvedResult> BuildDeferredInspectorResolvedAsync(
        InspectorDeferredCadRequest request, CancellationToken cancellationToken)
    {
        var resolutionRequest = new NotchResolvedResultRequest(
            request.CadPad, request.Grid, request.Settings, request.AllCadPads, request.ActiveRegularPadIds,
            request.CompensationModel, request.EnableToRegular, request.EnableToFull,
            request.EnableToFullRuleEngine, request.EnableToFullRuleTrace, request.StrictOverlapRatio,
            request.AnchorIcIndex, request.AnchorDiffIndex, request.AllocationAreaMode);
        return Task.Run(() => NotchDetailUseCase.BuildResolvedResult(resolutionRequest), cancellationToken);
    }

    public void QueueDeferredCadInspectorSnapshotRefresh(int cadPadId) =>
        Inspector.Refresh.QueueDeferredCadInspectorSnapshotRefresh(cadPadId);

    public void QueueDeferredCadInspectorSnapshotRefresh(int cadPadId, long selectionRevision) =>
        Inspector.Refresh.QueueDeferredCadInspectorSnapshotRefresh(cadPadId, selectionRevision);

    public void QueueDeferredRegularInspectorSnapshotRefresh(int regularPadId) =>
        Inspector.Refresh.QueueDeferredRegularInspectorSnapshotRefresh(regularPadId);

    public void QueueDeferredRegularInspectorSnapshotRefresh(int regularPadId, long selectionRevision) =>
        Inspector.Refresh.QueueDeferredRegularInspectorSnapshotRefresh(regularPadId, selectionRevision);

    private void CancelDeferredCadInspectorSnapshotRefresh(bool clearPendingState) =>
        Inspector.Refresh.CancelCad(clearPendingState);

    private void CancelDeferredRegularInspectorSnapshotRefresh(bool clearPendingState) =>
        Inspector.Refresh.CancelRegular(clearPendingState);

    private static async Task<bool> DispatchInspectorRefreshAsync(Action action, CancellationToken cancellationToken)
    {
        if (!UiThread.TryGetRunningDispatcher(out var dispatcher))
        {
            return false;
        }

        await dispatcher!.InvokeAsync(action, DispatcherPriority.Background, cancellationToken);
        return true;
    }

    private static async Task RunInspectorRefreshSafelyAsync(Func<Task> work, Action<Exception> warn, Action complete)
    {
        try
        {
            await work().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Deferred refresh canceled.
        }
        catch (Exception ex) when (ExceptionPolicy.IsRecoverable(ex))
        {
            warn(ex);
        }
        finally
        {
            complete();
        }
    }
}
