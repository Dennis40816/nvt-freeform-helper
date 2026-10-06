using System.Globalization;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Text.Json;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

// Keep product types and entry points here so the characterization tables can move to Core unchanged.
internal static class RuntimeQueryCharacterizationSubject
{
    private const string PipePath = @"\\.\pipe\freeformhelper.runtime.v1";

    internal sealed record Request(string Version, string Command, IReadOnlyDictionary<string, string>? Args = null);
    internal sealed record Response(bool Ok, string? ErrorCode, string? ErrorMessage, string Json);
    internal sealed record CommandLineResult(bool Handled, int ExitCode, string Stdout);
    internal sealed record ParsedCommandLine(
        bool Parsed, string Command, IReadOnlyDictionary<string, string> Args, int TimeoutMs, bool PrettyJson, string? Error);
    internal sealed record ArgumentResult<T>(bool Found, T Value, Response? Error);

    public static ParsedCommandLine ParseCommandLine(string[] args)
    {
        // The private parser is the only seam that exposes the timeout without timing an IPC round trip.
        var method = typeof(RuntimeQueryCommandLine).GetMethod("TryParseCommandLine", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The command-line parser entry point is missing.");
        object?[] parameters = [args, null, null, null, null, null];
        var parsed = (bool)method.Invoke(null, parameters)!;
        return new ParsedCommandLine(
            parsed, (string)parameters[1]!, (Dictionary<string, string>)parameters[2]!,
            (int)parameters[3]!, (bool)parameters[4]!, (string?)parameters[5]);
    }

    public static CommandLineResult HandleCommandLine(string[] args)
    {
        return CaptureConsole(() =>
        {
            var handled = RuntimeQueryCommandLine.TryHandleQueryCommand(args, out var exitCode);
            return (handled, exitCode);
        });
    }

    public static CommandLineResult RunProgram(string[] args)
    {
        return CaptureConsole(() =>
        {
            FreeformHelper.UI.Program.Main(args);
            return (true, Environment.ExitCode);
        });
    }

    private static CommandLineResult CaptureConsole(Func<(bool Handled, int ExitCode)> action)
    {
        var originalWriter = Console.Out;
        var originalExitCode = Environment.ExitCode;
        using var writer = new StringWriter(CultureInfo.InvariantCulture) { NewLine = Environment.NewLine };
        try
        {
            Console.SetOut(writer);
            var (handled, exitCode) = action();
            return new CommandLineResult(handled, exitCode, writer.ToString());
        }
        finally
        {
            Console.SetOut(originalWriter);
            Environment.ExitCode = originalExitCode;
        }
    }

    public static void RequireUnusedPipe()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("The fixed RuntimeQuery pipe requires the Windows pipe-existence safety check.");
        }

        if (File.Exists(PipePath) || Directory.EnumerateFiles(@"\\.\pipe\")
            .Contains(PipePath, StringComparer.OrdinalIgnoreCase))
        {
            Assert.Skip("The freeformhelper.runtime.v1 pipe already exists; this test must not contact a running tool instance.");
        }
    }

    public static async Task<(CommandLineResult Result, Request Request)> HandleWithReplyAsync(string[] args, string reply)
    {
        RequireUnusedPipe();
        await using var server = new NamedPipeServerStream(
            RuntimeQueryProtocol.PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        using var registration = timeout.Token.Register(() => server.Dispose());
        var serverTask = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync(timeout.Token);
            using var reader = new StreamReader(server, Encoding.UTF8, false, 4096, leaveOpen: true);
            using var writer = new StreamWriter(server, new UTF8Encoding(false), 4096, leaveOpen: true) { AutoFlush = true };
            var json = await reader.ReadLineAsync(timeout.Token);
            await writer.WriteLineAsync(reply).WaitAsync(timeout.Token);
            var request = JsonSerializer.Deserialize<RuntimeQueryRequest>(json!, RuntimeQueryProtocol.CompactJsonOptions)!;
            return new Request(request.Version, request.Command, request.Args);
        }, timeout.Token);

        var result = HandleCommandLine(args);
        var received = await serverTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        return (result, received);
    }

    public static async Task<Response> RouteAsync(
        string? command, IReadOnlyDictionary<string, string>? args,
        string handlerName, Action<IReadOnlyDictionary<string, string>?> handler)
    {
        var router = new RuntimeQueryCommandRouter(
            new Dictionary<string, Func<IReadOnlyDictionary<string, string>?, Task<RuntimeQueryResponseEnvelope>>>(StringComparer.Ordinal)
            {
                [handlerName] = received =>
                {
                    handler(received);
                    return Task.FromResult(RuntimeQueryResponseEnvelope.Success(null));
                }
            });
        return Snapshot(await router.RouteAsync(command, args));
    }

    public static async Task<Response> ExecuteAsync(Request? request)
    {
        using var shell = new ShellViewModel();
        var useCase = new RuntimeQueryUseCase(shell);
        var nativeRequest = request is null ? null : new RuntimeQueryRequest(request.Version, request.Command, request.Args);
        return Snapshot(await useCase.ExecuteAsync(nativeRequest));
    }

    public static ArgumentResult<int> IntArg(IReadOnlyDictionary<string, string>? args, string key, int min, int max)
    {
        var found = RuntimeQueryArgumentParser.TryGetIntArg(args, key, min, max, out var value, out var error);
        return new(found, value, error is null ? null : Snapshot(error));
    }

    public static ArgumentResult<IReadOnlyList<int>> IntListArg(IReadOnlyDictionary<string, string>? args, string key, int min, int max)
    {
        var found = RuntimeQueryArgumentParser.TryGetIntListArg(args, key, min, max, out var value, out var error);
        return new(found, value, error is null ? null : Snapshot(error));
    }

    public static ArgumentResult<double> DoubleArg(IReadOnlyDictionary<string, string>? args, string key, double min, double max)
    {
        var found = RuntimeQueryArgumentParser.TryGetDoubleArg(args, key, min, max, out var value, out var error);
        return new(found, value, error is null ? null : Snapshot(error));
    }

    public static ArgumentResult<string> StringArg(IReadOnlyDictionary<string, string>? args, string key)
    {
        var found = RuntimeQueryArgumentParser.TryGetStringArg(args, key, out var value, out var error);
        return new(found, value, error is null ? null : Snapshot(error));
    }

    public static ArgumentResult<bool> BoolArg(IReadOnlyDictionary<string, string>? args, string key)
    {
        var found = RuntimeQueryArgumentParser.TryGetBoolArg(args, key, out var value, out var error);
        return new(found, value, error is null ? null : Snapshot(error));
    }

    private static Response Snapshot(RuntimeQueryResponseEnvelope response)
    {
        return new Response(response.Ok, response.Error?.Code, response.Error?.Message,
            JsonSerializer.Serialize(response, RuntimeQueryProtocol.PrettyJsonOptions));
    }
}
