using System.Globalization;

namespace FreeformHelper.UI.Services;

internal sealed record CadLoadSpinnerDebugEvent(
    long Sequence,
    string TimestampUtc,
    string Source,
    string Detail);

internal sealed record CadLoadSpinnerDebugSnapshot(
    int VmOverlayShowCount,
    int VmOverlayHideCount,
    int ViewHostShowCount,
    int ViewHostHideCount,
    int HostShowRequestCount,
    int HostHideRequestCount,
    int HostStateSyncCount,
    int HostShowIpcSuccessCount,
    int HostShowIpcFailCount,
    int HostHideIpcCount,
    int HostShowRetryCount,
    IReadOnlyList<CadLoadSpinnerDebugEvent> Events);

internal static class CadLoadSpinnerDebugState
{
    private const int MaxEvents = 120;
    private static readonly object Sync = new();
    private static readonly Queue<CadLoadSpinnerDebugEvent> Events = new();
    private static long _sequence;
    private static int _vmOverlayShowCount;
    private static int _vmOverlayHideCount;
    private static int _viewHostShowCount;
    private static int _viewHostHideCount;
    private static int _hostShowRequestCount;
    private static int _hostHideRequestCount;
    private static int _hostStateSyncCount;
    private static int _hostShowIpcSuccessCount;
    private static int _hostShowIpcFailCount;
    private static int _hostHideIpcCount;
    private static int _hostShowRetryCount;

    public static void RecordVmOverlayVisibility(bool isVisible, string source)
    {
        if (isVisible)
        {
            Interlocked.Increment(ref _vmOverlayShowCount);
        }
        else
        {
            Interlocked.Increment(ref _vmOverlayHideCount);
        }

        AddEvent("vm.overlay", $"{source}:{(isVisible ? "show" : "hide")}");
    }

    public static void RecordViewHostSync(string action, bool overlayVisible)
    {
        if (string.Equals(action, "show", StringComparison.OrdinalIgnoreCase))
        {
            Interlocked.Increment(ref _viewHostShowCount);
        }
        else if (string.Equals(action, "hide", StringComparison.OrdinalIgnoreCase))
        {
            Interlocked.Increment(ref _viewHostHideCount);
        }

        AddEvent("view.host", $"action={action},overlayVisible={overlayVisible}");
    }

    public static void RecordHostShowRequest(string source)
    {
        Interlocked.Increment(ref _hostShowRequestCount);
        AddEvent("host.request", $"show:{source}");
    }

    public static void RecordHostHideRequest(string source)
    {
        Interlocked.Increment(ref _hostHideRequestCount);
        AddEvent("host.request", $"hide:{source}");
    }

    public static void RecordHostStateSync(bool showRequestedAtStart)
    {
        Interlocked.Increment(ref _hostStateSyncCount);
        AddEvent("host.sync", $"showRequested={showRequestedAtStart}");
    }

    public static void RecordHostShowIpcResult(bool success, string source)
    {
        if (success)
        {
            Interlocked.Increment(ref _hostShowIpcSuccessCount);
        }
        else
        {
            Interlocked.Increment(ref _hostShowIpcFailCount);
        }

        AddEvent("host.ipc.show", $"{source}:{(success ? "ok" : "fail")}");
    }

    public static void RecordHostHideIpc(string source)
    {
        Interlocked.Increment(ref _hostHideIpcCount);
        AddEvent("host.ipc.hide", source);
    }

    public static void RecordHostShowRetry()
    {
        Interlocked.Increment(ref _hostShowRetryCount);
        AddEvent("host.retry", "show");
    }

    public static CadLoadSpinnerDebugSnapshot GetSnapshot()
    {
        lock (Sync)
        {
            return new CadLoadSpinnerDebugSnapshot(
                VmOverlayShowCount: _vmOverlayShowCount,
                VmOverlayHideCount: _vmOverlayHideCount,
                ViewHostShowCount: _viewHostShowCount,
                ViewHostHideCount: _viewHostHideCount,
                HostShowRequestCount: _hostShowRequestCount,
                HostHideRequestCount: _hostHideRequestCount,
                HostStateSyncCount: _hostStateSyncCount,
                HostShowIpcSuccessCount: _hostShowIpcSuccessCount,
                HostShowIpcFailCount: _hostShowIpcFailCount,
                HostHideIpcCount: _hostHideIpcCount,
                HostShowRetryCount: _hostShowRetryCount,
                Events: Events.ToList());
        }
    }

    internal static void ResetForTest()
    {
        Interlocked.Exchange(ref _sequence, 0);
        Interlocked.Exchange(ref _vmOverlayShowCount, 0);
        Interlocked.Exchange(ref _vmOverlayHideCount, 0);
        Interlocked.Exchange(ref _viewHostShowCount, 0);
        Interlocked.Exchange(ref _viewHostHideCount, 0);
        Interlocked.Exchange(ref _hostShowRequestCount, 0);
        Interlocked.Exchange(ref _hostHideRequestCount, 0);
        Interlocked.Exchange(ref _hostStateSyncCount, 0);
        Interlocked.Exchange(ref _hostShowIpcSuccessCount, 0);
        Interlocked.Exchange(ref _hostShowIpcFailCount, 0);
        Interlocked.Exchange(ref _hostHideIpcCount, 0);
        Interlocked.Exchange(ref _hostShowRetryCount, 0);
        lock (Sync)
        {
            Events.Clear();
        }
    }

    private static void AddEvent(string source, string detail)
    {
        var sequence = Interlocked.Increment(ref _sequence);
        var timestamp = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        var entry = new CadLoadSpinnerDebugEvent(
            Sequence: sequence,
            TimestampUtc: timestamp,
            Source: source,
            Detail: detail);

        lock (Sync)
        {
            Events.Enqueue(entry);
            while (Events.Count > MaxEvents)
            {
                _ = Events.Dequeue();
            }
        }
    }
}
