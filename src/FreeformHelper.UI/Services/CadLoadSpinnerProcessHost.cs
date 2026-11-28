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
    private static readonly TimeSpan _showRetryDelay = TimeSpan.FromMilliseconds(30);
    private static readonly TimeSpan _postShowMinimumVisible = TimeSpan.FromMilliseconds(120);
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    private readonly Lock _sync = new();
    private readonly Queue<CadLoadSpinnerRequest> _pendingRequests = new();
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
    private bool _showRequestPending;
    private bool _sendInProgress;
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
        lock (_sync)
        {
            ThrowIfDisposed();
            _latestShowRequest = request;
            _showRequestPending = true;
            _requestedVisibility = VisibilityState.Visible;
            EnsureWarmupTaskLocked();
            if (HasRunningProcessLocked())
            {
                // Preserve each running-process transition, even if Hide follows before the worker runs.
                _pendingRequests.Enqueue(request);
            }
            QueueStateSync();
        }
    }

    public void Hide()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            CadLoadSpinnerDebugState.RecordHostHideRequest("hide-api");
            _requestedVisibility = VisibilityState.Hidden;
            if (HasRunningProcessLocked() && (_pendingRequests.Count > 0 || _sendInProgress ||
                _effectiveVisibility == VisibilityState.Visible))
            {
                _pendingRequests.Enqueue(new CadLoadSpinnerRequest(CadLoadSpinnerIpcProtocol.HideCommand));
            }
            QueueStateSync();
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _requestedVisibility = VisibilityState.Hidden;
            _effectiveVisibility = VisibilityState.Hidden;
            _pendingRequests.Clear();
            if (_sendInProgress)
            {
                QueueStateSync(shutdown: true);
                return;
            }
        }

        ShutdownProcess();
    }

    private void ShutdownProcess()
    {
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

    private void QueueStateSync(bool shutdown = false)
    {
        // Keep FIFO ordering. The continuation also runs after a failed predecessor, so one failure does not
        // poison later requests, and the failed task keeps its exception for its observer.
        _stateSyncTask = _stateSyncTask.ContinueWith(
            _ => RunStateSyncAsync(shutdown),
            CancellationToken.None,
            TaskContinuationOptions.DenyChildAttach,
            TaskScheduler.Default).Unwrap();
    }

    private async Task RunStateSyncAsync(bool shutdown)
    {
        try
        {
            if (shutdown)
            {
                ShutdownProcess();
            }
            else
            {
                await SyncRequestedVisibilityStateAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ExceptionPolicy.IsRecoverable(ex))
        {
            _logger.Warn(ex, "Failed to sync CAD load spinner visibility state.");
        }
    }

    private async Task SyncRequestedVisibilityStateAsync()
    {
        while (TryCaptureStateSyncSnapshot(out var request, out var warmupTask, out var queued))
        {
            var show = request.Command == CadLoadSpinnerIpcProtocol.ShowCommand;
            // A show that did not go straight to a ready process keeps the spinner up briefly before a Hide.
            var delayedShow = show && (!queued || warmupTask is { IsCompleted: false });
            if (warmupTask is not null)
            {
                try
                {
                    await warmupTask.ConfigureAwait(false);
                }
                catch (Exception ex) when (ExceptionPolicy.IsRecoverable(ex))
                {
                    _logger.Warn(ex, "CAD load spinner warmup failed.");
                    CleanupProcess(forceKillIfStillRunning: true);
                    return;
                }
            }

            var sent = await SendVisibilityRequestAsync(request, queued).ConfigureAwait(false);
            if (!show && !sent)
            {
                return;
            }
            if (show && !sent)
            {
                lock (_sync)
                {
                    if (_disposed || _requestedVisibility != VisibilityState.Visible)
                    {
                        continue;
                    }
                }

                CadLoadSpinnerDebugState.RecordHostShowRetry();
                await Task.Delay(_showRetryDelay, TimeProvider.System, CancellationToken.None).ConfigureAwait(false);
            }
            else if (delayedShow && IsHideRequested())
            {
                await Task.Delay(_postShowMinimumVisible, TimeProvider.System, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    private bool IsHideRequested()
    {
        lock (_sync)
        {
            return !_disposed && _requestedVisibility == VisibilityState.Hidden;
        }
    }

    private bool TryCaptureStateSyncSnapshot(out CadLoadSpinnerRequest request, out Task? warmupTask, out bool queued)
    {
        lock (_sync)
        {
            request = null!;
            warmupTask = null;
            queued = false;
            if (_disposed)
            {
                return false;
            }

            CadLoadSpinnerDebugState.RecordHostStateSync(_requestedVisibility == VisibilityState.Visible);
            queued = _pendingRequests.TryDequeue(out var pending);
            if (queued)
            {
                request = pending!;
            }
            else if (_requestedVisibility == VisibilityState.Visible &&
                (_showRequestPending || _effectiveVisibility == VisibilityState.Hidden))
            {
                request = _latestShowRequest!;
            }
            else if (_requestedVisibility == VisibilityState.Hidden && _effectiveVisibility == VisibilityState.Visible)
            {
                request = new CadLoadSpinnerRequest(CadLoadSpinnerIpcProtocol.HideCommand);
            }
            else
            {
                return false;
            }

            var show = request.Command == CadLoadSpinnerIpcProtocol.ShowCommand;
            if (show)
            {
                ResetExitedProcessLocked();
                EnsureWarmupTaskLocked();
                warmupTask = _warmupTask;
            }
            return true;
        }
    }

    private async Task<bool> SendVisibilityRequestAsync(CadLoadSpinnerRequest request, bool queued)
    {
        var show = request.Command == CadLoadSpinnerIpcProtocol.ShowCommand;
        lock (_sync)
        {
            if (_disposed || !HasRunningProcessLocked() || (!show &&
                (_effectiveVisibility == VisibilityState.Hidden || (!queued && _requestedVisibility == VisibilityState.Visible))))
            {
                return false;
            }
            _sendInProgress = true;
        }

        try
        {
            var sent = await TrySendVisibilityRequestAsync(request, queued).ConfigureAwait(false);
            RecordVisibilitySendResult(request, sent, queued);
            return sent;
        }
        finally
        {
            lock (_sync)
            {
                _sendInProgress = false;
            }
        }
    }

    private void RecordVisibilitySendResult(CadLoadSpinnerRequest request, bool sent, bool queued)
    {
        var show = request.Command == CadLoadSpinnerIpcProtocol.ShowCommand;
        if (show)
        {
            CadLoadSpinnerDebugState.RecordHostShowIpcResult(sent, queued ? "state-sync-queued-show" : "state-sync-show");
        }
        if (!sent)
        {
            LogIgnoredIpcFailure(request);
        }

        lock (_sync)
        {
            if (!sent || _disposed)
            {
                return;
            }
            _effectiveVisibility = show ? VisibilityState.Visible : VisibilityState.Hidden;
            if (show && ReferenceEquals(request, _latestShowRequest))
            {
                _showRequestPending = false;
            }
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

            await Task.Delay(TimeSpan.FromMilliseconds(25), TimeProvider.System, CancellationToken.None).ConfigureAwait(false);
        }

        throw new TimeoutException("CAD load spinner process did not become ready within timeout.");
    }

    // The spinner is a helper process, so an expected send failure never ends the user's operation.
    // SendRequest and TrySendVisibilityRequestAsync catch the failures that sending can end in:
    // IOException and TimeoutException (the pipe; TrySendRequestAsync already answers those two itself),
    // InvalidOperationException (includes ObjectDisposedException; the pipe client is in a bad state),
    // UnauthorizedAccessException (the pipe belongs to another user), NotSupportedException (JsonSerializer)
    // and ArgumentException (includes ArgumentOutOfRangeException; the pipe name or the timeout). The send only
    // writes, so a bad response cannot fail it. Other visibility failures reach the background state sync:
    // recoverable ones are logged there, and out-of-memory faults its task.
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
        _logger.Debug(CultureInfo.InvariantCulture, "Ignoring CAD load spinner IPC failure for command '{0}'.", request.Command);
    }

    private async Task<bool> TrySendRequestAsync(CadLoadSpinnerRequest request, int timeoutMs)
    {
        if (_sendRequestOverride is not null)
        {
            return _sendRequestOverride(request);
        }

        using var timeout = new CancellationTokenSource();
        if (request.Command == CadLoadSpinnerIpcProtocol.ShutdownCommand)
        {
            timeout.CancelAfter(timeoutMs);
        }
        try
        {
            using var client = new NamedPipeClientStream(
                ".",
                _pipeName,
                PipeDirection.Out,
                PipeOptions.Asynchronous);
            // Disposal stays synchronous when idle; bound shutdown's write as well as its connection.
            using var cancellation = timeout.Token.Register(client.Dispose);
            await client.ConnectAsync(timeoutMs, timeout.Token).ConfigureAwait(false);
            await using var writer = new StreamWriter(
                client,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                bufferSize: 1024,
                leaveOpen: true)
            {
                AutoFlush = true
            };
            var payload = JsonSerializer.Serialize(request, CadLoadSpinnerIpcProtocol.JsonOptions);
            await writer.WriteLineAsync(payload.AsMemory(), timeout.Token).ConfigureAwait(false);
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
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            return false;
        }
        catch (ObjectDisposedException) when (timeout.IsCancellationRequested)
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
            _logger.Warn(
                CultureInfo.InvariantCulture,
                "CAD load spinner process exited with code {0} at {1:O}. Starting a new one.",
                exitCode,
                _process.ExitTime);
            CadLoadSpinnerDebugState.RecordHostProcessExit(exitCode);
        }

        CleanupProcess(forceKillIfStillRunning: false);
        return true;
    }

    internal Task LatestStateSyncTask => _stateSyncTask;

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
                _logger.Warn(ex, "Failed to cleanup CAD load spinner process.");
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

    private async Task<bool> TrySendVisibilityRequestAsync(CadLoadSpinnerRequest request, bool queued)
    {
        try
        {
            if (request.Command == CadLoadSpinnerIpcProtocol.HideCommand)
            {
                CadLoadSpinnerDebugState.RecordHostHideIpc(queued ? "state-sync-queued-hide" : "state-sync-hide");
            }
            return await TrySendRequestAsync(request, ConnectTimeoutMs).ConfigureAwait(false);
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
