// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Nvt.Core.RuntimeQuery;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed partial class RuntimeQueryContractTests
{
    public static TheoryData<string, string, string> ArgumentFailures => BuildArgumentFailures();

    [AvaloniaTheory]
    [MemberData(nameof(ArgumentFailures))]
    public async Task InvalidArgumentsReturnExactFailureWithoutLoadingDataAsync(string command, string arguments, string message)
    {
        var response = await RuntimeQueryCharacterizationSubject.ExecuteAsync(new("1", command, ParseArguments(arguments)));

        Assert.False(response.Ok);
        Assert.Equal("INVALID_ARGUMENTS", response.ErrorCode);
        Assert.Equal(message, response.ErrorMessage);
        using var json = JsonDocument.Parse(response.Json);
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("data").ValueKind);
    }

    [AvaloniaTheory]
    [InlineData("pad", "cad-id=7", "PAD_NOT_FOUND", "CAD pad 7 is not visible.")]
    [InlineData("pad", "regular-id=7", "PAD_NOT_FOUND", "Regular pad 7 is not visible.")]
    [InlineData("notch", "cad-id=7", "PAD_NOT_FOUND", "CAD pad 7 is not visible.")]
    [InlineData("notch-stage", "cad-id=7", "PAD_NOT_FOUND", "CAD pad 7 is not visible.")]
    [InlineData("notch-validation", "regular-id=7", "NOT_READY", "Validation: build grid first.")]
    [InlineData("select-regular", "regular-id=7", "PAD_NOT_FOUND", "No visible regular pads selected.")]
    [InlineData("run-step", "step=1", "STEP_NOT_READY", "Step 1: import DXF and build grid first.")]
    [InlineData("run-step", "step=2", "STEP_NOT_READY", "Step 2: import DXF and run Step 1 first.")]
    [InlineData("run-step", "step=4", "STEP_NOT_READY", "Step 4: import DXF and build grid first.")]
    [InlineData("run-step", "step=3", "STEP_NOT_READY", "Notch 2.2 preview: build grid first.")]
    [InlineData("export-notch", "format=csv;path={output}", "STEP_EXECUTION_FAILED",
        "Notch export did not produce output file. summary=Notch export: not run.")]
    [InlineData("simulation", "", "NOT_READY",
        "Simulation workspace is not ready. Simulation workspace is not ready for current workflow state.")]
    public async Task UnloadedCommandsCharacterizationPreservesFailuresAsync(
        string command, string arguments, string code, string message)
    {
        var response = await ExecuteUnloadedAsync(command, arguments);

        AssertUnloadedOutcome(response, code, message);
    }

    private static async Task<RuntimeQueryCharacterizationSubject.Response> ExecuteUnloadedAsync(string command, string arguments)
    {
        var outputPath = TestFiles.NewTempFilePath("runtime-contract-unloaded-", ".csv");
        var response = await RuntimeQueryCharacterizationSubject.ExecuteAsync(new("1", command,
            ParseArguments(arguments.Replace("{output}", outputPath, StringComparison.Ordinal))));
        Assert.False(File.Exists(outputPath));
        return response;
    }

    private static void AssertUnloadedOutcome(RuntimeQueryCharacterizationSubject.Response response, string code, string message)
    {
        Assert.False(response.Ok);
        Assert.Equal(code, response.ErrorCode);
        Assert.Equal(message, response.ErrorMessage);
    }

    [AvaloniaTheory]
    [InlineData("request", "", "INVALID_REQUEST", "Empty request payload.")]
    [InlineData("request", "{", "INVALID_JSON", "Expected depth to be zero at the end of the JSON payload.")]
    [InlineData("request-timeout", "", "IPC_REQUEST_TIMEOUT",
        "Runtime query client connected but did not send a request before the server read timeout.")]
    [InlineData("reply", "", "EMPTY_RESPONSE", "Runtime query returned an empty response.")]
    [InlineData("reply", "null", "INVALID_RESPONSE", "Runtime query response could not be parsed.")]
    [InlineData("reply", "{", "IPC_ERROR", "Expected depth to be zero at the end of the JSON payload.")]
    [InlineData("client-timeout", "", "IPC_TIMEOUT", "Runtime query did not complete within timeout.")]
    public async Task PipePayloadFailuresCharacterizationReturnsErrorCodeAndDiagnosticAsync(
        string direction, string payload, string code, string message)
    {
        await AssertPipePayloadFailureAsync(direction, payload, code, message);
    }

    private static async Task AssertPipePayloadFailureAsync(string direction, string payload, string code, string message)
    {
        if (direction == "client-timeout")
        {
            await AssertClientTimeoutAsync(code, message);
            return;
        }

        if (direction == "reply")
        {
            var (result, _) = await RuntimeQueryCharacterizationSubject.HandleWithReplyAsync(["query", "status"], payload);
            Assert.Equal(1, result.ExitCode);
            using var json = JsonDocument.Parse(result.Stdout);
            Assert.False(json.RootElement.GetProperty("ok").GetBoolean());
            var error = json.RootElement.GetProperty("error");
            Assert.Equal(code, error.GetProperty("code").GetString());
            Assert.StartsWith(message, error.GetProperty("message").GetString(), StringComparison.Ordinal);
            return;
        }

        var pipeName = $"nfh.contract.{Guid.NewGuid():N}";
        using var shell = new ShellViewModel { IsDevPageAvailable = true };
        var useCase = new RuntimeQueryUseCase(shell);
        await using var server = new RuntimeQueryIpcServer(pipeName, "1", direction == "request-timeout" ? 100 : 3000, 1500,
            RuntimeQueryErrorMapper.Map, null, (request, _, _) => useCase.ExecuteAsync(request));
        server.Start();
        await using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(3000, TestContext.Current.CancellationToken);
        if (direction == "request")
        {
            using var writer = new StreamWriter(client, new UTF8Encoding(false), 4096, leaveOpen: true) { AutoFlush = true };
            await writer.WriteLineAsync(payload).WaitAsync(TestContext.Current.CancellationToken);
        }

        using var reader = new StreamReader(client, leaveOpen: true);
        var line = await reader.ReadLineAsync(TestContext.Current.CancellationToken).AsTask()
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.NotNull(line);
        using var response = JsonDocument.Parse(line);
        Assert.False(response.RootElement.GetProperty("ok").GetBoolean());
        var failure = response.RootElement.GetProperty("error");
        Assert.Equal(code, failure.GetProperty("code").GetString());
        Assert.StartsWith(message, failure.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    private static async Task AssertClientTimeoutAsync(string code, string message)
    {
        var pipeName = $"nfh.contract.{Guid.NewGuid():N}";
        await using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var clientTask = Task.Run(() => RuntimeQueryIpcClient.SendRequest(pipeName,
            new RuntimeQueryRequest("1", "status", null), 1000, RuntimeQueryErrorMapper.Map), TestContext.Current.CancellationToken);
        await server.WaitForConnectionAsync(TestContext.Current.CancellationToken);
        using var reader = new StreamReader(server, leaveOpen: true);
        Assert.NotNull(await reader.ReadLineAsync(TestContext.Current.CancellationToken));

        var response = await clientTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.False(response.Ok);
        Assert.Equal(code, response.Error?.Code);
        Assert.Equal(message, response.Error?.Message);
    }

    private static TheoryData<string, string, string> BuildArgumentFailures()
    {
        var cases = new TheoryData<string, string, string>
        {
            { "pad", "", "Use exactly one of --cad-id or --regular-id." },
            { "pad", "cad-id=1;regular-id=2", "Use exactly one of --cad-id or --regular-id." },
            { "select-cad", "", "Use --cad-id or --cad-ids for query select-cad." },
            { "select-regular", "", "Use regular-id/regular-ids or regular-index/regular-indices for query select-regular." },
            { "load-project", "", "Argument '--path' is required for query load-project." },
            { "run-step", "", "Argument '--step' is required for query run-step." },
            { "clear-step", "", "Argument '--step' is required for query clear-step." },
            { "set-tofull", "", "Argument '--enable' is required for query set-tofull." },
            { "set-tofull", "enable=maybe", "Argument '--enable' must be true/false (or 1/0, on/off)." },
            { "export-notch", "", "Argument '--format' is required for query export-notch. Use csv (CSV review)|c-v21|c-v22." },
            { "export-notch", "format=csv", "Argument '--path' is required for query export-notch." },
            { "export-notch", "format=binary;path=unused", "Unsupported export format 'binary'. Use csv (CSV review)|c-v21|c-v22." },
        };
        foreach (var command in new[] { "notch", "multi-owner", "notch-stage", "notch-validation" })
        {
            var key = command == "notch-validation" ? "regular-id" : "cad-id";
            cases.Add(command, "", $"Argument '--{key}' is required for query {command}.");
        }

        AddIntegerFailures(cases, "terminal", "tail", 5000);
        AddIntegerFailures(cases, "terminal-links", "tail", 5000);
        AddIntegerFailures(cases, "terminal-links", "limit", 2000);
        AddIntegerFailures(cases, "notch", "limit", 5000, "cad-id=1;");
        AddIntegerFailures(cases, "notch", "target-limit", 5000, "cad-id=1;");
        AddIntegerFailures(cases, "notch", "polygon-limit", 2000, "cad-id=1;");
        AddIntegerFailures(cases, "multi-owner", "limit", 5000, "cad-id=1;");
        AddIntegerFailures(cases, "notch-stage", "polygon-limit", 2000, "cad-id=1;");
        AddIntegerFailures(cases, "run-step", "step", 4);
        AddIntegerFailures(cases, "clear-step", "step", 5);
        foreach (var command in new[] { "pad", "notch", "multi-owner", "notch-stage", "select-cad" })
        {
            cases.Add(command, "cad-id=x", "Argument '--cad-id' must be an integer.");
        }

        foreach (var command in new[] { "pad", "notch-validation", "select-regular", "simulation" })
        {
            cases.Add(command, "regular-id=-1", "Argument '--regular-id' must be in [0, 2147483647].");
        }

        cases.Add("select-cad", "cad-ids=1,x", "Argument '--cad-ids' must be a comma-separated integer list.");
        cases.Add("select-regular", "regular-ids=1,x", "Argument '--regular-ids' must be a comma-separated integer list.");
        cases.Add("select-regular", "regular-index=x", "Argument '--regular-index' must be an integer.");
        cases.Add("select-regular", "regular-indices=1,x", "Argument '--regular-indices' must be a comma-separated integer list.");
        cases.Add("multi-owner", "cad-id=1;overlap-percent=x", "Argument '--overlap-percent' must be numeric.");
        cases.Add("multi-owner", "cad-id=1;overlap-percent=-1", "Argument '--overlap-percent' must be in [0, 100].");
        cases.Add("multi-owner", "cad-id=1;overlap-percent=101", "Argument '--overlap-percent' must be in [0, 100].");
        return cases;
    }

    private static void AddIntegerFailures(
        TheoryData<string, string, string> cases, string command, string key, int max, string prefix = "")
    {
        cases.Add(command, $"{prefix}{key}=x", $"Argument '--{key}' must be an integer.");
        cases.Add(command, $"{prefix}{key}=0", $"Argument '--{key}' must be in [1, {max}].");
        cases.Add(command, $"{prefix}{key}={(max + 1).ToString(CultureInfo.InvariantCulture)}", $"Argument '--{key}' must be in [1, {max}].");
    }

    private static Dictionary<string, string> ParseArguments(string text) => text.Split(';', StringSplitOptions.RemoveEmptyEntries)
        .Select(static item => item.Split('=', 2))
        .ToDictionary(static pair => pair[0], static pair => pair[1], StringComparer.Ordinal);
}
