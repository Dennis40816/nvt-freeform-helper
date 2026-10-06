using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using NLog;

namespace FreeformHelper.UI.Services;

internal interface ICadLoadSpinnerHost : IDisposable
{
    void Warmup();
    void Show(Window owner);
    void Hide();
}

internal sealed class CadLoadSpinnerProcessHost : ICadLoadSpinnerHost
{
    private enum VisibilityState
    {
        Hidden,
        Visible,
    }

    // Test runners set this switch: under xUnit v3 the test executable is not named testhost.
    internal const string DisableProcessLaunchSwitch = "FreeformHelper.DisableCadLoadSpinnerProcess";
    private const int ConnectTimeoutMs = 250;
    private const int WarmupTimeoutMs = 3000;
    private static readonly TimeSpan ShowRetryDelay = TimeSpan.FromMilliseconds(30);
    private static readonly TimeSpan PostShowMinimumVisible = TimeSpan.FromMilliseconds(120);
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly object _sync = new();
    private readonly SemaphoreSlim _stateSyncGate = new(1, 1);
    private readonly string _pipeName = CadLoadSpinnerIpcProtocol.BuildPipeName(Environment.ProcessId);
    private readonly Func<CadLoadSpinnerRequest, bool>? _sendRequestOverride;
    private Process? _process;
    private Task? _warmupTask;
    private Task _stateSyncTask = Task.CompletedTask;
    private VisibilityState _requestedVisibility = VisibilityState.Hidden;
    private VisibilityState _effectiveVisibility = VisibilityState.Hidden;
    private CadLoadSpinnerRequest? _latestShowRequest;
    private DateTimeOffset _lastShowSentUtc;
    private bool _disposed;

    public CadLoadSpinnerProcessHost()
    {
    }

    internal CadLoadSpinnerProcessHost(Func<CadLoadSpinnerRequest, bool> sendRequest)
    {
        _sendRequestOverride = sendRequest;
        _warmupTask = Task.CompletedTask;
    }

    public void Warmup()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            if ((AppContext.TryGetSwitch(DisableProcessLaunchSwitch, out var launchDisabled) && launchDisabled) ||
                !CanLaunchSpinnerProcessForPath(Environment.ProcessPath))
            {
                return;
            }

            _warmupTask ??= Task.Run(EnsureProcessStartedAsync);
        }
    }

    internal static bool CanLaunchSpinnerProcessForPath(string? processPath)
    {
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return false;
        }

        var processName = Path.GetFileNameWithoutExtension(processPath);
        return !processName.Contains("testhost", StringComparison.OrdinalIgnoreCase);
    }

    public void Show(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        CadLoadSpinnerDebugState.RecordHostShowRequest("show-api");
        var request = BuildShowRequest(owner);
        var sentImmediately = false;
        lock (_sync)
        {
            ThrowIfDisposed();
            _latestShowRequest = request;
            _requestedVisibility = VisibilityState.Visible;
            EnsureWarmupTaskLocked();
            if (HasRunningProcessLocked())
            {
                sentImmediately = SendShowRequestCoreLocked(request);
                CadLoadSpinnerDebugState.RecordHostShowIpcResult(sentImmediately, "show-api-immediate");
            }
        }

        if (sentImmediately)
        {
            return;
        }

        QueueStateSync();
    }

    public void Hide()
    {
        if (_disposed)
        {
            return;
        }

        CadLoadSpinnerDebugState.RecordHostHideRequest("hide-api");
        lock (_sync)
        {
            _requestedVisibility = VisibilityState.Hidden;
        }

        QueueStateSync();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        lock (_sync)
        {
            _requestedVisibility = VisibilityState.Hidden;
            _effectiveVisibility = VisibilityState.Hidden;
        }

        if (HasRunningProcess())
        {
            SendRequest(new CadLoadSpinnerRequest(CadLoadSpinnerIpcProtocol.ShutdownCommand), ConnectTimeoutMs, swallowIoErrors: true);
            CleanupProcess(forceKillIfStillRunning: true);
        }
    }

    internal static ProcessStartInfo BuildStartInfo(string pipeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);

        var spinnerArgs = BuildSpinnerArgs(Environment.ProcessId, pipeName);
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            throw new InvalidOperationException("Environment.ProcessPath is not available for CAD load spinner host.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = processPath,
            UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory,
        };

        if (string.Equals(Path.GetFileName(processPath), "dotnet", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileName(processPath), "dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            var entryAssemblyPath = Assembly.GetEntryAssembly()?.Location;
            if (string.IsNullOrWhiteSpace(entryAssemblyPath))
            {
                throw new InvalidOperationException("Entry assembly path is not available for CAD load spinner host.");
            }

            startInfo.ArgumentList.Add(entryAssemblyPath);
        }

        foreach (var arg in spinnerArgs)
        {
            startInfo.ArgumentList.Add(arg);
        }

        return startInfo;
    }

    internal static string[] BuildSpinnerArgs(int parentProcessId, string pipeName)
    {
        return
        [
            "--cad-load-spinner",
            "--parent-pid", parentProcessId.ToString(CultureInfo.InvariantCulture),
            "--pipe-name", pipeName,
        ];
    }

    private static CadLoadSpinnerRequest BuildShowRequest(Window owner)
    {
        var ownerLeft = owner.Position.X;
        var ownerTop = owner.Position.Y;
        var ownerWidth = Math.Max(1, (int)Math.Round(owner.Bounds.Width * owner.RenderScaling, MidpointRounding.AwayFromZero));
        var ownerHeight = Math.Max(1, (int)Math.Round(owner.Bounds.Height * owner.RenderScaling, MidpointRounding.AwayFromZero));
        return new CadLoadSpinnerRequest(
            CadLoadSpinnerIpcProtocol.ShowCommand,
            OwnerLeft: ownerLeft,
            OwnerTop: ownerTop,
            OwnerWidth: ownerWidth,
            OwnerHeight: ownerHeight);
    }

    private void EnsureWarmupTaskLocked()
    {
        _warmupTask ??= Task.Run(EnsureProcessStartedAsync);
    }

    private void QueueStateSync()
    {
        _stateSyncTask = Task.Run(async () =>
        {
            await _stateSyncGate.WaitAsync().ConfigureAwait(false);
            try
            {
                await SyncRequestedVisibilityStateAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to sync CAD load spinner visibility state.");
            }
            finally
            {
                _stateSyncGate.Release();
            }
        });
    }

    private async Task SyncRequestedVisibilityStateAsync()
    {
        while (true)
        {
            bool showRequestedAtStart;
            CadLoadSpinnerRequest? showRequest;
            Task? warmupTask;
            var showSent = false;

            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                showRequestedAtStart = _requestedVisibility == VisibilityState.Visible;
                showRequest = _latestShowRequest;
                warmupTask = _warmupTask;
            }
            CadLoadSpinnerDebugState.RecordHostStateSync(showRequestedAtStart);

            if (!showRequestedAtStart)
            {
                var shouldHide = false;
                lock (_sync)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    shouldHide = _requestedVisibility == VisibilityState.Hidden &&
                        _effectiveVisibility == VisibilityState.Visible;
                }

                if (!shouldHide)
                {
                    // Nothing is visible, so there is nothing to sync. Looping here would spin this
                    // thread-pool thread until the next Show; a later Show or Hide queues its own sync.
                    return;
                }

                if (HasRunningProcess())
                {
                    CadLoadSpinnerDebugState.RecordHostHideIpc("state-sync-hide");
                    SendHideRequest("state-sync-hide");
                }

                lock (_sync)
                {
                    if (_requestedVisibility == VisibilityState.Visible)
                    {
                        continue;
                    }
                }

                return;
            }

            if (warmupTask is null)
            {
                lock (_sync)
                {
                    EnsureWarmupTaskLocked();
                    warmupTask = _warmupTask;
                }
            }

            if (warmupTask is not null)
            {
                try
                {
                    await warmupTask.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "CAD load spinner warmup failed.");
                    CleanupProcess(forceKillIfStillRunning: true);
                    return;
                }
            }

            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                if (showRequest is not null && HasRunningProcessLocked())
                {
                    showSent = SendShowRequestCoreLocked(showRequest);
                    CadLoadSpinnerDebugState.RecordHostShowIpcResult(showSent, "state-sync");
                }
            }

            if (!showSent)
            {
                bool stillShowRequested;
                lock (_sync)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    stillShowRequested = _requestedVisibility == VisibilityState.Visible;
                }

                if (stillShowRequested)
                {
                    CadLoadSpinnerDebugState.RecordHostShowRetry();
                    await Task.Delay(ShowRetryDelay).ConfigureAwait(false);
                    continue;
                }
            }

            bool shouldHideAfterShow;
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                shouldHideAfterShow = _requestedVisibility == VisibilityState.Hidden;
            }

            if (!shouldHideAfterShow)
            {
                return;
            }

            if (showSent)
            {
                DateTimeOffset showSentUtc;
                lock (_sync)
                {
                    showSentUtc = _lastShowSentUtc;
                }

                var elapsed = DateTimeOffset.UtcNow - showSentUtc;
                var remaining = PostShowMinimumVisible - elapsed;
                if (remaining > TimeSpan.Zero)
                {
                    await Task.Delay(remaining).ConfigureAwait(false);
                }
            }

            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                shouldHideAfterShow = _requestedVisibility == VisibilityState.Hidden;
            }

            if (!shouldHideAfterShow)
            {
                continue;
            }

            if (HasRunningProcess())
            {
                CadLoadSpinnerDebugState.RecordHostHideIpc("state-sync-post-show");
                SendHideRequest("state-sync-post-show");
            }

            lock (_sync)
            {
                if (_requestedVisibility == VisibilityState.Visible)
                {
                    continue;
                }
            }

            return;
        }
    }

    private async Task EnsureProcessStartedAsync()
    {
        if (HasRunningProcess())
        {
            return;
        }

        Process? processToWait;
        lock (_sync)
        {
            if (_process is not null && !_process.HasExited)
            {
                return;
            }

            var startInfo = BuildStartInfo(_pipeName);
            _process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start CAD load spinner process.");
            processToWait = _process;
        }

        try
        {
            await WaitForServerReadyAsync(WarmupTimeoutMs).ConfigureAwait(false);
        }
        catch
        {
            CleanupProcess(forceKillIfStillRunning: true);
            throw;
        }

        if (processToWait is not null && processToWait.HasExited)
        {
            CleanupProcess(forceKillIfStillRunning: false);
            throw new InvalidOperationException("CAD load spinner process exited before becoming ready.");
        }
    }

    private async Task WaitForServerReadyAsync(int timeoutMs)
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.ElapsedMilliseconds < timeoutMs)
        {
            if (await TrySendRequestAsync(new CadLoadSpinnerRequest(CadLoadSpinnerIpcProtocol.PingCommand), ConnectTimeoutMs).ConfigureAwait(false))
            {
                return;
            }

            await Task.Delay(25).ConfigureAwait(false);
        }

        throw new TimeoutException("CAD load spinner process did not become ready within timeout.");
    }

    private void SendRequest(CadLoadSpinnerRequest request, int timeoutMs, bool swallowIoErrors = false)
    {
        try
        {
            var ok = TrySendRequestAsync(request, timeoutMs).GetAwaiter().GetResult();
            if (!ok && !swallowIoErrors)
            {
                throw new IOException($"CAD load spinner IPC command '{request.Command}' failed.");
            }
        }
        catch when (swallowIoErrors)
        {
            Logger.Debug(CultureInfo.InvariantCulture, "Ignoring CAD load spinner IPC failure for command '{0}'.", request.Command);
        }
    }

    private async Task<bool> TrySendRequestAsync(CadLoadSpinnerRequest request, int timeoutMs)
    {
        if (_sendRequestOverride is not null)
        {
            return _sendRequestOverride(request);
        }

        try
        {
            using var client = new NamedPipeClientStream(
                ".",
                _pipeName,
                PipeDirection.Out,
                PipeOptions.Asynchronous);
            await client.ConnectAsync(timeoutMs).ConfigureAwait(false);
            await using var writer = new StreamWriter(
                client,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                bufferSize: 1024,
                leaveOpen: true)
            {
                AutoFlush = true
            };
            var payload = JsonSerializer.Serialize(request, CadLoadSpinnerIpcProtocol.JsonOptions);
            await writer.WriteLineAsync(payload).ConfigureAwait(false);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private bool HasRunningProcess()
    {
        lock (_sync)
        {
            return HasRunningProcessLocked();
        }
    }

    private bool HasRunningProcessLocked()
    {
        return _sendRequestOverride is not null || (_process is not null && !_process.HasExited);
    }

    private bool SendShowRequestCoreLocked(CadLoadSpinnerRequest request)
    {
        try
        {
            var ok = TrySendRequestAsync(request, ConnectTimeoutMs).GetAwaiter().GetResult();
            if (!ok)
            {
                return false;
            }

            _lastShowSentUtc = DateTimeOffset.UtcNow;
            _effectiveVisibility = VisibilityState.Visible;
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal Task GetLatestStateSyncTask() => _stateSyncTask;

    internal (string Requested, string Effective) GetVisibilityStateSnapshot()
    {
        lock (_sync)
        {
            return (_requestedVisibility.ToString(), _effectiveVisibility.ToString());
        }
    }

    private void CleanupProcess(bool forceKillIfStillRunning)
    {
        lock (_sync)
        {
            if (_process is null)
            {
                _warmupTask = null;
                return;
            }

            try
            {
                if (forceKillIfStillRunning && !_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                    _process.WaitForExit(500);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to cleanup CAD load spinner process.");
            }
            finally
            {
                _process.Dispose();
                _process = null;
                _warmupTask = null;
                _effectiveVisibility = VisibilityState.Hidden;
            }
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private void SendHideRequest(string origin)
    {
        if (TrySendRequest(new CadLoadSpinnerRequest(CadLoadSpinnerIpcProtocol.HideCommand), ConnectTimeoutMs))
        {
            lock (_sync)
            {
                _effectiveVisibility = VisibilityState.Hidden;
            }
        }
        else
        {
            Logger.Debug(
                CultureInfo.InvariantCulture,
                "Ignoring CAD load spinner IPC failure for command '{0}' ({1}).",
                CadLoadSpinnerIpcProtocol.HideCommand,
                origin);
        }
    }

    private bool TrySendRequest(CadLoadSpinnerRequest request, int timeoutMs)
    {
        try
        {
            return TrySendRequestAsync(request, timeoutMs).GetAwaiter().GetResult();
        }
        catch
        {
            return false;
        }
    }
}
