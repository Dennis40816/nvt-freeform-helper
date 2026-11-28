// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Diagnostics;

namespace FreeformHelper.UI.ViewModels.Inspector;

/// <summary>Coordinates deferred refreshes; each completion can release only its own slot.</summary>
internal sealed class InspectorRefreshOwner<TContext> : IInspectorRefresh
    where TContext : class
{
    private const int PadInspectorDeferredRefreshDelayMs = 200;
    private const int RegularPadInspectorDeferredRefreshDelayMs = 150;
    private readonly PadInspectorRequestState _requests;
    private readonly InspectorRefreshPorts<TContext> _ports;
    private readonly Action<bool> _writePending;
    private readonly string _cadWorkName;
    private readonly string _regularWorkName;

    // Installed on the UI thread; completion uses each slot's existing atomic ownership check.
    private readonly PadInspectorDeferredRefreshSlot _cadSlot = new();
    private readonly PadInspectorDeferredRefreshSlot _regularSlot = new();

    public InspectorRefreshOwner(
        PadInspectorRequestState requests,
        InspectorRefreshPorts<TContext> ports,
        Action<bool> writePending,
        string cadWorkName,
        string regularWorkName)
    {
        _requests = requests;
        _ports = ports;
        _writePending = writePending;
        _cadWorkName = cadWorkName;
        _regularWorkName = regularWorkName;
    }

    public void QueueDeferredCadInspectorSnapshotRefresh(int cadPadId) =>
        QueueDeferredCadInspectorSnapshotRefresh(cadPadId, _requests.ReadSelectionRevision());

    public void QueueDeferredCadInspectorSnapshotRefresh(int cadPadId, long selectionRevision)
    {
        CancelCad(clearPendingState: false);
        var cts = new CancellationTokenSource();
        _cadSlot.Install(cts);
        var requestId = _requests.NextCadRefreshRequestId();
        UpdatePendingState();
        _ports.RunBackground(_cadWorkName,
            () => RunDeferredCadInspectorSnapshotRefreshAsync(requestId, cadPadId, selectionRevision, cts, cts.Token));
    }

    public void QueueDeferredRegularInspectorSnapshotRefresh(int regularPadId) =>
        QueueDeferredRegularInspectorSnapshotRefresh(regularPadId, _requests.ReadSelectionRevision());

    public void QueueDeferredRegularInspectorSnapshotRefresh(int regularPadId, long selectionRevision)
    {
        CancelRegular(clearPendingState: false);
        var cts = new CancellationTokenSource();
        _regularSlot.Install(cts);
        var requestId = _requests.NextRegularRefreshRequestId();
        UpdatePendingState();
        _ports.RunBackground(_regularWorkName,
            () => RunDeferredRegularInspectorSnapshotRefreshAsync(requestId, regularPadId, selectionRevision, cts, cts.Token));
    }

    public void CancelCad(bool clearPendingState)
    {
        _cadSlot.Cancel();
        if (clearPendingState)
        {
            UpdatePendingState();
        }
    }

    public void CancelRegular(bool clearPendingState)
    {
        _regularSlot.Cancel();
        if (clearPendingState)
        {
            UpdatePendingState();
        }
    }

    public void UpdatePendingState() => _writePending(_cadSlot.IsActive || _regularSlot.IsActive);

    private async Task RunDeferredCadInspectorSnapshotRefreshAsync(
        long requestId, int cadPadId, long selectionRevision, CancellationTokenSource cts, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        TContext? context = null;
        await _ports.RunSafelyAsync(async () =>
        {
            await _ports.DelayAsync(PadInspectorDeferredRefreshDelayMs, cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested || requestId != _requests.ReadCadRefreshRequestId())
            {
                return;
            }

            if (!await _ports.DispatchAsync(() =>
            {
                context = _ports.CaptureCad(requestId, cadPadId, selectionRevision, cancellationToken);
            }, cancellationToken))
            {
                return;
            }

            if (context is null)
            {
                return;
            }

            var (resolved, diagnostics) = await _ports.ResolveCadAsync(context, cancellationToken).ConfigureAwait(false);
            _ = await _ports.DispatchAsync(() =>
            {
                if (!_ports.ApplyCad(context, resolved, diagnostics))
                {
                    return;
                }

                _ports.LogCadApplied(context, sw.ElapsedMilliseconds, PadInspectorDeferredRefreshDelayMs);
            }, cancellationToken);
        }, ex => _ports.WarnCad(ex, cadPadId), () => Complete(_cadSlot, cts)).ConfigureAwait(false);
    }

    private async Task RunDeferredRegularInspectorSnapshotRefreshAsync(
        long requestId, int regularPadId, long selectionRevision, CancellationTokenSource cts, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        await _ports.RunSafelyAsync(async () =>
        {
            await _ports.DelayAsync(RegularPadInspectorDeferredRefreshDelayMs, cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested || requestId != _requests.ReadRegularRefreshRequestId())
            {
                return;
            }

            _ = await _ports.DispatchAsync(() =>
            {
                if (!_ports.CanApplyRegular(requestId, regularPadId, selectionRevision))
                {
                    return;
                }

                var snapshot = _ports.BuildRegular(regularPadId, true, true);
                if (snapshot is null)
                {
                    return;
                }

                _ports.PublishSnapshot(snapshot);
                _ports.LogRegularApplied(regularPadId, sw.ElapsedMilliseconds, RegularPadInspectorDeferredRefreshDelayMs);
            }, cancellationToken);
        }, ex => _ports.WarnRegular(ex, regularPadId), () => Complete(_regularSlot, cts)).ConfigureAwait(false);
    }

    private void Complete(PadInspectorDeferredRefreshSlot slot, CancellationTokenSource cts)
    {
        if (slot.TryComplete(cts))
        {
            UpdatePendingState();
        }
    }
}
