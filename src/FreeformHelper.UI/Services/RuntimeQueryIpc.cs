using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using FreeformHelper.UI.ViewModels;
using NLog;

namespace FreeformHelper.UI.Services;

internal static class RuntimeQueryIpcHost
{
    private static readonly object Sync = new();
    private static RuntimeQueryIpcServer? _server;

    public static void Start(ShellViewModel shellViewModel)
    {
        ArgumentNullException.ThrowIfNull(shellViewModel);

        lock (Sync)
        {
            if (_server is not null)
            {
                return;
            }

            _server = new RuntimeQueryIpcServer(shellViewModel);
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
        lock (Sync)
        {
            server = _server;
            _server = null;
        }

        return server?.DisposeAsync().AsTask() ?? Task.CompletedTask;
    }
}

internal sealed class RuntimeQueryIpcServer : IDisposable, IAsyncDisposable
{
    private const int RequestReadTimeoutMs = 5000;
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly RuntimeQueryUseCase _useCase;
    private readonly CancellationTokenSource _lifecycleCts = new();
    private Task? _runLoopTask;
    private int _disposed;

    public RuntimeQueryIpcServer(ShellViewModel shellViewModel)
    {
        _useCase = new RuntimeQueryUseCase(shellViewModel);
    }

    public void Start()
    {
        if (_runLoopTask is not null)
        {
            return;
        }

        _runLoopTask = Task.Run(() => RunLoopAsync(_lifecycleCts.Token));
        Logger.Info(CultureInfo.InvariantCulture, "Runtime query IPC server started. pipe={0}", RuntimeQueryProtocol.PipeName);
    }

    public void Dispose()
    {
        _ = DisposeAsync().AsTask();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _lifecycleCts.Cancel();
        var runLoopTask = _runLoopTask;
        if (runLoopTask is not null)
        {
            try
            {
                await runLoopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation-related failures.
            }
            catch (ObjectDisposedException)
            {
                // Ignore disposal races during process shutdown.
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Runtime query IPC stop ignored run loop exception.");
            }
        }

        _lifecycleCts.Dispose();
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var pipe = new NamedPipeServerStream(
                RuntimeQueryProtocol.PipeName,
                PipeDirection.InOut,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try
            {
                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Runtime query IPC wait failed.");
                continue;
            }

            try
            {
                await HandleConnectionAsync(pipe, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Runtime query IPC request handling failed.");
            }
        }

        Logger.Info(CultureInfo.InvariantCulture, "Runtime query IPC server stopped.");
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(
            pipe,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 4096,
            leaveOpen: true);
        using var writer = new StreamWriter(
            pipe,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            bufferSize: 4096,
            leaveOpen: true)
        {
            AutoFlush = true
        };

        using var readTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        readTimeoutCts.CancelAfter(RequestReadTimeoutMs);

        string? requestJson;
        try
        {
            requestJson = await reader.ReadLineAsync(readTimeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var timeoutResponse = RuntimeQueryResponseEnvelope.Failure(
                code: "IPC_REQUEST_TIMEOUT",
                message: "Runtime query client connected but did not send a request before the server read timeout.");
            await writer.WriteLineAsync(JsonSerializer.Serialize(timeoutResponse, RuntimeQueryProtocol.CompactJsonOptions))
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(requestJson))
        {
            var emptyResponse = RuntimeQueryResponseEnvelope.Failure(
                code: "INVALID_REQUEST",
                message: "Empty request payload.");
            await writer.WriteLineAsync(JsonSerializer.Serialize(emptyResponse, RuntimeQueryProtocol.CompactJsonOptions))
                .ConfigureAwait(false);
            return;
        }

        RuntimeQueryResponseEnvelope response;
        try
        {
            var request = JsonSerializer.Deserialize<RuntimeQueryRequest>(requestJson, RuntimeQueryProtocol.CompactJsonOptions);
            if (UiThread.TryGetRunningDispatcher(out var dispatcher))
            {
                response = await dispatcher!.InvokeAsync(() => _useCase.ExecuteAsync(request));
            }
            else
            {
                response = RuntimeQueryResponseEnvelope.Failure(
                    code: "IPC_ERROR",
                    message: "The UI dispatcher is unavailable.");
            }
        }
        catch (JsonException ex)
        {
            response = RuntimeQueryResponseEnvelope.Failure(
                code: "INVALID_JSON",
                message: ex.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Runtime query execution failed.");
            response = RuntimeQueryResponseEnvelope.Failure(
                code: "IPC_ERROR",
                message: ex.Message);
        }

        var responseJson = JsonSerializer.Serialize(response, RuntimeQueryProtocol.CompactJsonOptions);
        await writer.WriteLineAsync(responseJson).ConfigureAwait(false);
    }
}

internal static class RuntimeQueryCommandLine
{
    private static readonly string[] SupportedCommands =
    {
        "help",
        "status",
        "selection",
        "terminal",
        "terminal-links",
        "pad",
        "notch",
        "multi-owner",
        "notch-stage",
        "notch-validation",
        "load-project",
        "run-step",
        "clear-step",
        "select-cad",
        "select-regular",
        "clear-selection",
        "set-tofull",
        "simulation",
        "export-notch",
    };

    public static bool TryHandleQueryCommand(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (args.Length == 0 || !string.Equals(args[0], "query", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!TryParseCommandLine(args, out var command, out var commandArgs, out var timeoutMs, out var prettyJson, out var parseError))
        {
            var error = RuntimeQueryResponseEnvelope.Failure("INVALID_ARGUMENTS", parseError ?? "Invalid arguments.");
            Console.WriteLine(JsonSerializer.Serialize(error, RuntimeQueryProtocol.PrettyJsonOptions));
            exitCode = 2;
            return true;
        }

        var request = new RuntimeQueryRequest(
            Version: RuntimeQueryProtocol.Version,
            Command: command,
            Args: commandArgs.Count == 0 ? null : commandArgs);
        var response = SendRequest(request, timeoutMs);
        var options = prettyJson ? RuntimeQueryProtocol.PrettyJsonOptions : RuntimeQueryProtocol.CompactJsonOptions;
        Console.WriteLine(JsonSerializer.Serialize(response, options));
        exitCode = response.Ok ? 0 : 1;
        return true;
    }

    internal static RuntimeQueryResponseEnvelope SendRequest(RuntimeQueryRequest request, int timeoutMs)
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".",
                RuntimeQueryProtocol.PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            var startedAt = Stopwatch.GetTimestamp();
            client.Connect(timeoutMs);
            using var timeoutCts = new CancellationTokenSource(GetRemainingTimeout(timeoutMs, startedAt));
            var cancellationToken = timeoutCts.Token;

            using var reader = new StreamReader(
                client,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 4096,
                leaveOpen: true);
            using var writer = new StreamWriter(
                client,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                bufferSize: 4096,
                leaveOpen: true)
            {
                AutoFlush = true
            };

            var requestJson = JsonSerializer.Serialize(request, RuntimeQueryProtocol.CompactJsonOptions);
            writer.WriteLineAsync(requestJson)
                .WaitAsync(cancellationToken)
                .GetAwaiter()
                .GetResult();

            var responseJson = reader.ReadLineAsync(cancellationToken)
                .AsTask()
                .GetAwaiter()
                .GetResult();
            if (string.IsNullOrWhiteSpace(responseJson))
            {
                return RuntimeQueryResponseEnvelope.Failure(
                    code: "EMPTY_RESPONSE",
                    message: "Runtime query returned an empty response.");
            }

            var response = JsonSerializer.Deserialize<RuntimeQueryResponseEnvelope>(
                responseJson,
                RuntimeQueryProtocol.CompactJsonOptions);
            return response ?? RuntimeQueryResponseEnvelope.Failure(
                code: "INVALID_RESPONSE",
                message: "Runtime query response could not be parsed.");
        }
        catch (TimeoutException)
        {
            return RuntimeQueryResponseEnvelope.Failure(
                code: "INSTANCE_NOT_RUNNING",
                message: "No running FreeformHelper instance responded within timeout.");
        }
        catch (OperationCanceledException)
        {
            return RuntimeQueryResponseEnvelope.Failure(
                code: "IPC_TIMEOUT",
                message: "Runtime query did not complete within timeout.");
        }
        catch (IOException ex)
        {
            return RuntimeQueryResponseEnvelope.Failure(
                code: "IPC_IO_ERROR",
                message: ex.Message);
        }
        catch (Exception ex)
        {
            return RuntimeQueryResponseEnvelope.Failure(
                code: "IPC_ERROR",
                message: ex.Message);
        }
    }

    private static int GetRemainingTimeout(int timeoutMs, long startedAt)
    {
        var elapsedMs = (int)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        return Math.Max(1, timeoutMs - elapsedMs);
    }

    private static bool TryParseCommandLine(
        string[] args,
        out string command,
        out Dictionary<string, string> commandArgs,
        out int timeoutMs,
        out bool prettyJson,
        out string? error)
    {
        command = string.Empty;
        commandArgs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        timeoutMs = 1500;
        prettyJson = true;
        error = null;

        if (args.Length < 2)
        {
            error = $"Usage: query <{string.Join("|", SupportedCommands)}> [--key value]";
            return false;
        }

        command = args[1].Trim().ToLowerInvariant();
        if (!SupportedCommands.Contains(command, StringComparer.Ordinal))
        {
            error = $"Unsupported query command '{command}'. Supported: {string.Join(", ", SupportedCommands)}.";
            return false;
        }

        for (var i = 2; i < args.Length; i++)
        {
            var token = args[i];
            if (string.Equals(token, "--json-compact", StringComparison.OrdinalIgnoreCase))
            {
                prettyJson = false;
                continue;
            }

            if (string.Equals(token, "--json-pretty", StringComparison.OrdinalIgnoreCase))
            {
                prettyJson = true;
                continue;
            }

            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                error = $"Unexpected token '{token}'. Options must start with '--'.";
                return false;
            }

            string key;
            string value;
            var equalIndex = token.IndexOf('=');
            if (equalIndex > 2)
            {
                key = token.Substring(2, equalIndex - 2);
                value = token[(equalIndex + 1)..];
            }
            else
            {
                key = token[2..];
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    value = args[++i];
                }
                else
                {
                    value = "true";
                }
            }

            if (string.Equals(key, "timeout-ms", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedTimeout) ||
                    parsedTimeout <= 0 ||
                    parsedTimeout > 120000)
                {
                    error = "--timeout-ms must be in [1, 120000].";
                    return false;
                }

                timeoutMs = parsedTimeout;
                continue;
            }

            commandArgs[key] = value;
        }

        return true;
    }
}

