using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using FreeformHelper.Application.Services;
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

    private readonly Lock _sync = new();
    private readonly SemaphoreSlim _stateSyncGate = new(1, 1);
    private readonly string _pipeName = CadLoadSpinnerIpcProtocol.BuildPipeName(Environment.ProcessId);
    private readonly Func<CadLoadSpinnerRequest, bool>? _sendRequestOverride;
    private readonly Func<Task> _startProcess;
    private readonly Func<bool>? _hasProcessExitedOverride;
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
        _startProcess = EnsureProcessStartedAsync;
    }

    internal CadLoadSpinnerProcessHost(Func<CadLoadSpinnerRequest, bool> sendRequest)
    {
        _sendRequestOverride = sendRequest;
        _startProcess = EnsureProcessStartedAsync;
        _warmupTask = Task.CompletedTask;
    }

    // Lets a test play a spinner process that has exited, and the start of a new one.
    internal CadLoadSpinnerProcessHost(
        Func<CadLoadSpinnerRequest, bool> sendRequest,
        Func<Task> startProcess,
        Func<bool> hasProcessExited,
        Task? warmupTask = null)
    {
        _sendRequestOverride = sendRequest;
        _startProcess = startProcess;
        _hasProcessExitedOverride = hasProcessExited;
        _warmupTask = warmupTask ?? Task.CompletedTask;
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

            _warmupTask ??= Task.Run(_startProcess);
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
        _warmupTask ??= Task.Run(_startProcess);
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
            catch (Exception ex) when (ExceptionPolicy.IsRecoverable(ex))
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
                warmupTask = showRequestedAtStart && ResetExitedProcessLocked() ? null : _warmupTask;
            }
            CadLoadSpinnerDebugState.RecordHostStateSync(showRequestedAtStart);

            if (!showRequestedAtStart)
            {
                bool shouldHide;
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
                catch (Exception ex) when (ExceptionPolicy.IsRecoverable(ex))
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

        var serverReady = false;
        try
        {
            await WaitForServerReadyAsync(WarmupTimeoutMs).ConfigureAwait(false);
            serverReady = true;
        }
        finally
        {
            if (!serverReady)
            {
                CleanupProcess(forceKillIfStillRunning: true);
            }
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

    // The spinner is a helper process, so a failed send never ends the user's operation. The three senders below
    // (SendRequest, SendShowRequestCoreLocked, TrySendRequest) catch the failures that sending can end in:
    // IOException and TimeoutException (the pipe; TrySendRequestAsync already answers those two itself),
    // InvalidOperationException (includes ObjectDisposedException; the pipe client is in a bad state),
    // UnauthorizedAccessException (the pipe belongs to another user), NotSupportedException (JsonSerializer)
    // and ArgumentException (includes ArgumentOutOfRangeException; the pipe name or the timeout). The send only
    // writes, so a bad response cannot fail it. Any other exception, out-of-memory included, reaches the caller.
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
        catch (IOException) when (swallowIoErrors)
        {
            LogIgnoredIpcFailure(request);
        }
        catch (TimeoutException) when (swallowIoErrors)
        {
            LogIgnoredIpcFailure(request);
        }
        catch (InvalidOperationException) when (swallowIoErrors)
        {
            LogIgnoredIpcFailure(request);
        }
        catch (UnauthorizedAccessException) when (swallowIoErrors)
        {
            LogIgnoredIpcFailure(request);
        }
        catch (NotSupportedException) when (swallowIoErrors)
        {
            LogIgnoredIpcFailure(request);
        }
        catch (ArgumentException) when (swallowIoErrors)
        {
            LogIgnoredIpcFailure(request);
        }
    }

    private static void LogIgnoredIpcFailure(CadLoadSpinnerRequest request)
    {
        Logger.Debug(CultureInfo.InvariantCulture, "Ignoring CAD load spinner IPC failure for command '{0}'.", request.Command);
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
        if (_hasProcessExitedOverride is not null)
        {
            return !_hasProcessExitedOverride();
        }

        return _sendRequestOverride is not null || (_process is not null && !_process.HasExited);
    }

    // The spinner process can end on its own. The warm-up task is already complete then, so without this reset
    // no new process starts and every Show fails until the program restarts. Clearing the warm-up task makes the
    // next sync start a new process. While the warm-up still runs, it owns the process and cleans up when it fails.
    private bool ResetExitedProcessLocked()
    {
        var exited = _hasProcessExitedOverride is not null
            ? _hasProcessExitedOverride()
            : _sendRequestOverride is null && _process is not null && _process.HasExited;
        if (!exited || _warmupTask is { IsCompleted: false })
        {
            return false;
        }

        if (_process is not null)
        {
            var exitCode = _process.ExitCode;
            Logger.Warn(
                CultureInfo.InvariantCulture,
                "CAD load spinner process exited with code {0} at {1:O}. Starting a new one.",
                exitCode,
                _process.ExitTime);
            CadLoadSpinnerDebugState.RecordHostProcessExit(exitCode);
        }

        CleanupProcess(forceKillIfStillRunning: false);
        return true;
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
        catch (IOException)
        {
            return false;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (ArgumentException)
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
            catch (Exception ex) when (ExceptionPolicy.IsRecoverable(ex))
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
        catch (IOException)
        {
            return false;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
