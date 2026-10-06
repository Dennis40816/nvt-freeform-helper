using Xunit;

namespace FreeformHelper.Tests;

[Collection("RuntimeQueryIpcHost")]
public sealed class RuntimeQueryCharacterizationCommandLineTests
{
    private const string SupportedCommands =
        "help, status, selection, terminal, terminal-links, pad, notch, multi-owner, notch-stage, notch-validation, " +
        "load-project, run-step, clear-step, select-cad, select-regular, clear-selection, set-tofull, simulation, export-notch";
    private const string SuccessReply = "{\"ok\":true,\"data\":{\"value\":7},\"error\":null}";

    public static TheoryData<string[], string> ParseErrors => new()
    {
        { ["query", "help", "bare"], "Unexpected token 'bare'. Options must start with '--'." },
        { ["query", "help", "--json-compact", "bare"], "Unexpected token 'bare'. Options must start with '--'." },
        { ["query", " MISSING "], "Unsupported query command 'missing'. Supported: " + SupportedCommands + "." },
        { ["query"], "Usage: query <" + SupportedCommands.Replace(", ", "|", StringComparison.Ordinal) + "> [--key value]" },
        { ["query", "help", "--timeout-ms", "0"], "--timeout-ms must be in [1, 120000]." },
        { ["query", "help", "--timeout-ms=-1"], "--timeout-ms must be in [1, 120000]." },
        { ["query", "help", "--timeout-ms=120001"], "--timeout-ms must be in [1, 120000]." },
        { ["query", "help", "--timeout-ms"], "--timeout-ms must be in [1, 120000]." },
        { ["query", "help", "--timeout-ms", "--json-compact"], "--timeout-ms must be in [1, 120000]." },
        { ["query", "help", "--timeout-ms=abc"], "--timeout-ms must be in [1, 120000]." }
    };

    [Theory]
    [MemberData(nameof(ParseErrors))]
    public void ParseError_PrintsExactPrettyJsonAndReturnsTwo(string[] args, string message)
    {
        var result = RuntimeQueryCharacterizationSubject.HandleCommandLine(args);

        Assert.True(result.Handled);
        Assert.Equal(2, result.ExitCode);
        Assert.Equal(FailureJson("INVALID_ARGUMENTS", message) + Environment.NewLine, result.Stdout);
    }

    public static TheoryData<string[], string, string> OptionForms => new()
    {
        { ["query", " HELP ", "--key", "value"], "key", "value" },
        { ["query", "help", "--key=value"], "key", "value" },
        { ["query", "help", "--key"], "key", "true" },
        { ["query", "help", "--key", "--other"], "key", "true" },
        { ["query", "help", "--key=first", "--KEY", "last"], "key", "last" },
        { ["query", "help", "--key="], "key", "" },
        { ["query", "help", "--key=a=b"], "key", "a=b" },
        { ["query", "help", "--key", " spaced value "], "key", " spaced value " }
    };

    [Theory]
    [MemberData(nameof(OptionForms))]
    public async Task OptionForms_SendExactArgumentsAndPrintExactSuccess(string[] args, string key, string value)
    {
        var (result, request) = await RuntimeQueryCharacterizationSubject.HandleWithReplyAsync(args, SuccessReply);

        Assert.True(result.Handled);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(SuccessJson + Environment.NewLine, result.Stdout);
        Assert.Equal("1", request.Version);
        Assert.Equal("help", request.Command);
        Assert.NotNull(request.Args);
        Assert.Equal(value, request.Args[key]);
        Assert.Equal(args.Contains("--other", StringComparer.Ordinal) ? 2 : 1, request.Args.Count);
        if (request.Args.Count == 2)
        {
            Assert.Equal("true", request.Args["other"]);
        }
    }

    [Theory]
    [InlineData(null, 1500)]
    [InlineData("1", 1)]
    [InlineData("120000", 120000)]
    public void Timeout_DefaultAndInclusiveLimits_AreParsed(string? timeout, int expected)
    {
        string[] args = timeout is null ? ["query", "help"] : ["query", "help", "--timeout-ms", timeout];
        var parsed = RuntimeQueryCharacterizationSubject.ParseCommandLine(args);

        Assert.True(parsed.Parsed);
        Assert.Null(parsed.Error);
        Assert.Equal("help", parsed.Command);
        Assert.Empty(parsed.Args);
        Assert.Equal(expected, parsed.TimeoutMs);
        Assert.True(parsed.PrettyJson);
    }

    public static TheoryData<string[], bool> OutputModes => new()
    {
        { ["query", "help"], true },
        { ["query", "help", "--json-pretty"], true },
        { ["query", "help", "--json-compact"], false },
        { ["query", "help", "--json-compact", "--json-pretty"], true },
        { ["query", "help", "--json-pretty", "--JSON-COMPACT"], false },
        { ["query", "help", "--timeout-ms=120000", "--json-compact"], false }
    };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task OutputMode_PrintsExactJsonAndOmitsCommonOptionsFromRequest(string[] args, bool pretty)
    {
        var (result, request) = await RuntimeQueryCharacterizationSubject.HandleWithReplyAsync(args, SuccessReply);

        Assert.True(result.Handled);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal((pretty ? SuccessJson : SuccessReply) + Environment.NewLine, result.Stdout);
        Assert.Null(request.Args);
    }

    [Theory]
    [InlineData("query")]
    [InlineData("QUERY")]
    [InlineData("Query")]
    [InlineData("qUeRy")]
    public void QueryVerb_IgnoresLetterCase(string verb)
    {
        var result = RuntimeQueryCharacterizationSubject.HandleCommandLine([verb, "help", "bare"]);

        Assert.True(result.Handled);
        Assert.Equal(2, result.ExitCode);
        Assert.Equal(FailureJson("INVALID_ARGUMENTS", "Unexpected token 'bare'. Options must start with '--'.") +
            Environment.NewLine, result.Stdout);
    }

    public static TheoryData<string[]> UnhandledArguments => new()
    {
        { Array.Empty<string>() }, { ["help"] }, { ["status", "query"] },
        { ["--query"] }, { [" query", "help"] }, { ["query ", "help"] }
    };

    [Theory]
    [MemberData(nameof(UnhandledArguments))]
    public void NonQueryArguments_AreNotHandledAndPrintNothing(string[] args)
    {
        var result = RuntimeQueryCharacterizationSubject.HandleCommandLine(args);

        Assert.False(result.Handled);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Stdout);
    }

    [Fact]
    public void NoRunningInstance_PrintsExactFailureAndReturnsOne()
    {
        RuntimeQueryCharacterizationSubject.RequireUnusedPipe();
        var result = RuntimeQueryCharacterizationSubject.HandleCommandLine(["query", "help", "--timeout-ms", "1"]);

        Assert.True(result.Handled);
        Assert.Equal(1, result.ExitCode);
        Assert.Equal(FailureJson("INSTANCE_NOT_RUNNING", "No running FreeformHelper instance responded within timeout.") +
            Environment.NewLine, result.Stdout);
    }

    [Fact]
    public async Task FailedResponse_PrintsExactCompactJsonAndReturnsOne()
    {
        const string reply = "{\"ok\":false,\"data\":null,\"error\":{\"code\":\"TEST_FAILURE\",\"message\":\"Rejected.\"}}";
        var (result, _) = await RuntimeQueryCharacterizationSubject.HandleWithReplyAsync(["query", "help", "--json-compact"], reply);

        Assert.True(result.Handled);
        Assert.Equal(1, result.ExitCode);
        Assert.Equal(reply + Environment.NewLine, result.Stdout);
    }

    [Fact]
    public void Program_QueryParseError_SetsProcessExitCodeAndPrintsExactJson()
    {
        var result = RuntimeQueryCharacterizationSubject.RunProgram(["query", "help", "bare"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(FailureJson("INVALID_ARGUMENTS", "Unexpected token 'bare'. Options must start with '--'.") +
            Environment.NewLine, result.Stdout);
    }

    private static string SuccessJson => string.Join(Environment.NewLine,
        "{", "  \"ok\": true,", "  \"data\": {", "    \"value\": 7", "  },", "  \"error\": null", "}");

    private static string FailureJson(string code, string message)
    {
        // The serializer escapes apostrophes and angle brackets even in human-readable output.
        var escaped = message.Replace("'", "\\u0027", StringComparison.Ordinal)
            .Replace("<", "\\u003C", StringComparison.Ordinal).Replace(">", "\\u003E", StringComparison.Ordinal);
        return string.Join(Environment.NewLine,
            "{", "  \"ok\": false,", "  \"data\": null,", "  \"error\": {",
            "    \"code\": \"" + code + "\",", "    \"message\": \"" + escaped + "\"", "  }", "}");
    }
}
