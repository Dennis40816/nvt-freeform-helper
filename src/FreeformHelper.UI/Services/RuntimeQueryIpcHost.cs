// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using FreeformHelper.UI.ViewModels;
using NLog;
using Nvt.Core.Avalonia.Threading;
using Nvt.Core.RuntimeQuery;

namespace FreeformHelper.UI.Services;

internal static class RuntimeQueryIpcHost
{
    internal const int RequestReadTimeoutMs = 5000;
    internal const int ShutdownTimeoutMs = 1500;
    private static readonly Logger _logger = LogManager.GetLogger("FreeformHelper.UI.Services.RuntimeQueryIpcServer");
    private static readonly Lock _sync = new();
    private static RuntimeQueryIpcServer? _server;

    public static void Start(ShellViewModel shellViewModel)
    {
        ArgumentNullException.ThrowIfNull(shellViewModel);

        lock (_sync)
        {
            if (_server is not null)
            {
                return;
            }

            var useCase = new RuntimeQueryUseCase(shellViewModel);
            _server = new RuntimeQueryIpcServer(
                RuntimeQueryProtocol.PipeName,
                RuntimeQueryProtocol.Version,
                RequestReadTimeoutMs,
                ShutdownTimeoutMs,
                RuntimeQueryErrorMapper.Map,
                LogDiagnostic,
                async (request, _, _) =>
                {
                    if (UiThread.TryGetRunningDispatcher(out var dispatcher))
                    {
                        return await dispatcher!.InvokeAsync(() => useCase.ExecuteAsync(request));
                    }

                    return new RuntimeQueryResponseEnvelope(Ok: false, Data: null,
                        Error: RuntimeQueryErrorMapper.Map(RuntimeQueryFailure.DispatcherUnavailable, null));
                });
            _server.Start();
        }
    }

    public static void Stop()
    {
        _ = StopAsync();
    }

    public static Task StopAsync()
    {
        RuntimeQueryIpcServer? server;
        lock (_sync)
        {
            server = _server;
            _server = null;
        }

        return server?.DisposeAsync().AsTask() ?? Task.CompletedTask;
    }

    private static void LogDiagnostic(RuntimeQueryDiagnostic diagnostic, Exception? exception)
    {
        switch (diagnostic)
        {
            case RuntimeQueryDiagnostic.Started:
                _logger.Info(CultureInfo.InvariantCulture, "Runtime query IPC server started. pipe={0}", RuntimeQueryProtocol.PipeName);
                break;
            case RuntimeQueryDiagnostic.Stopped:
                _logger.Info(CultureInfo.InvariantCulture, "Runtime query IPC server stopped.");
                break;
            case RuntimeQueryDiagnostic.ConnectionFailed:
                _logger.Warn(exception, "Runtime query IPC wait failed.");
                break;
            case RuntimeQueryDiagnostic.RequestFailed:
                _logger.Warn(exception, "Runtime query IPC request handling failed.");
                break;
            case RuntimeQueryDiagnostic.HandlerFailed:
                _logger.Warn(exception, "Runtime query execution failed.");
                break;
            case RuntimeQueryDiagnostic.ShutdownFailed:
                _logger.Debug(exception, "Runtime query IPC stop ignored run loop exception.");
                break;
            case RuntimeQueryDiagnostic.ShutdownTimedOut:
                _logger.Debug(exception, "Runtime query IPC stop reached the shutdown timeout.");
                break;
            case RuntimeQueryDiagnostic.PipeCreationFailed:
                _logger.Warn(exception, "Runtime query IPC pipe creation failed.");
                break;
        }
    }
}
