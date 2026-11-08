using System.Globalization;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Nvt.Core.LogConsole;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class RuntimeQueryUseCaseTests
{
    private const int TerminalLoggerColumnWidth = 56;
    private const int TerminalTailCount = 5000;

    [Fact]
    public async Task ExecuteAsync_QueryTerminal_ReturnsOneFormattedLinePerEntry()
    {
        var logger = $"rq-{Guid.NewGuid():N}";
        var timestamp = new DateTimeOffset(2026, 10, 13, 8, 15, 42, TimeSpan.Zero);
        using var store = await CreateStoreAsync(timestamp, LogLevel.Warn, logger, "Pad 12 moved");
        using var shell = new ShellViewModel { ConsoleStore = store, IsDevPageAvailable = true };
        var useCase = new RuntimeQueryUseCase(shell);

        var response = await useCase.ExecuteAsync(TerminalRequest("terminal", ("tail", TerminalTailCount.ToString(CultureInfo.InvariantCulture))));

        Assert.True(response.Ok, $"{response.Error?.Code}:{response.Error?.Message}");
        var line = Assert.Single(ReadLines(response.Data), candidate => candidate.Contains(logger, StringComparison.Ordinal));
        var expectedTime = timestamp.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        Assert.Equal($"{expectedTime} | WARN  | {logger.PadRight(TerminalLoggerColumnWidth)} | Pad 12 moved", line);
    }

    [Fact]
    public async Task ExecuteAsync_QueryTerminal_ReportsTotalAndReturnedLineCounts()
    {
        using var store = await CreateStoreAsync(DateTimeOffset.UnixEpoch, LogLevel.Info, "rq-count", "count line");
        using var shell = new ShellViewModel { ConsoleStore = store, IsDevPageAvailable = true };
        var useCase = new RuntimeQueryUseCase(shell);

        var response = await useCase.ExecuteAsync(TerminalRequest("terminal", ("tail", "1")));

        Assert.True(response.Ok, $"{response.Error?.Code}:{response.Error?.Message}");
        var root = SerializeToRootElement(response.Data);
        Assert.Equal(1, root.GetProperty("returnedLines").GetInt32());
        Assert.Equal(1, root.GetProperty("totalLines").GetInt32());
    }

    [Fact]
    public async Task ExecuteAsync_QueryTerminal_RejectsATailBelowOne()
    {
        using var shell = new ShellViewModel { IsDevPageAvailable = true };
        var useCase = new RuntimeQueryUseCase(shell);

        var response = await useCase.ExecuteAsync(TerminalRequest("terminal", ("tail", "0")));

        Assert.False(response.Ok);
        Assert.NotNull(response.Error);
    }

    [Fact]
    public async Task ExecuteAsync_QueryTerminalLinks_ReturnsPathAndUrlTargetsWithTheirOffsets()
    {
        var logger = $"rq-links-{Guid.NewGuid():N}";
        const string path = "C:/work/export/notch_review.csv";
        const string url = "https://github.com/Dennis40816/nvt-freeform-helper";
        using var store = await CreateStoreAsync(DateTimeOffset.UnixEpoch, LogLevel.Info, logger, $"see {path} and {url}");
        using var shell = new ShellViewModel { ConsoleStore = store, IsDevPageAvailable = true };
        var useCase = new RuntimeQueryUseCase(shell);

        var response = await useCase.ExecuteAsync(TerminalRequest("terminal-links", ("tail", TerminalTailCount.ToString(CultureInfo.InvariantCulture))));

        Assert.True(response.Ok, $"{response.Error?.Code}:{response.Error?.Message}");
        var links = ReadLinks(response.Data, logger);
        var pathLink = Assert.Single(links, link => link.Target == path);
        var urlLink = Assert.Single(links, link => link.Target == url);
        Assert.False(pathLink.IsUrl);
        Assert.True(urlLink.IsUrl);
        Assert.Equal(pathLink.LineText.IndexOf(path, StringComparison.Ordinal), pathLink.StartOffset);
        Assert.Equal(path.Length, pathLink.Length);
        Assert.Equal(urlLink.LineText.IndexOf(url, StringComparison.Ordinal), urlLink.StartOffset);
        Assert.Equal(url.Length, urlLink.Length);
    }

    [Fact]
    public async Task ExecuteAsync_QueryTerminalLinks_WithLimitOne_ReturnsOneLink()
    {
        var logger = $"rq-limit-{Guid.NewGuid():N}";
        using var store = await CreateStoreAsync(DateTimeOffset.UnixEpoch, LogLevel.Info, logger, "open C:/work/a.txt then C:/work/b.txt");
        using var shell = new ShellViewModel { ConsoleStore = store, IsDevPageAvailable = true };
        var useCase = new RuntimeQueryUseCase(shell);

        var response = await useCase.ExecuteAsync(TerminalRequest(
            "terminal-links",
            ("tail", TerminalTailCount.ToString(CultureInfo.InvariantCulture)),
            ("limit", "1")));

        Assert.True(response.Ok, $"{response.Error?.Code}:{response.Error?.Message}");
        var root = SerializeToRootElement(response.Data);
        Assert.Equal(1, root.GetProperty("links").GetArrayLength());
    }

    // Each test reads a store of its own, so parallel tests that write to the process-wide store cannot push the line out of the tail.
    private static async Task<LogStore> CreateStoreAsync(DateTimeOffset timestamp, LogLevel level, string logger, string message)
    {
        var store = new LogStore();
        store.Add(level, logger, message, timestamp);
        using var snapshot = await store.CaptureLatestAsync();
        return store;
    }

    private static RuntimeQueryRequest TerminalRequest(string command, params (string Name, string Value)[] args)
    {
        var dictionary = args.ToDictionary(static arg => arg.Name, static arg => arg.Value, StringComparer.Ordinal);
        return new RuntimeQueryRequest(
            Version: RuntimeQueryProtocol.Version,
            Command: command,
            Args: dictionary.Count == 0 ? null : dictionary);
    }

    private static List<string> ReadLines(object? data)
    {
        var root = SerializeToRootElement(data);
        return root.GetProperty("lines").EnumerateArray().Select(static line => line.GetString() ?? string.Empty).ToList();
    }

    private static List<TerminalLinkRow> ReadLinks(object? data, string logger)
    {
        var root = SerializeToRootElement(data);
        return root.GetProperty("links").EnumerateArray()
            .Select(static link => new TerminalLinkRow(
                link.GetProperty("target").GetString() ?? string.Empty,
                link.GetProperty("isUrl").GetBoolean(),
                link.GetProperty("startOffset").GetInt32(),
                link.GetProperty("length").GetInt32(),
                link.GetProperty("lineText").GetString() ?? string.Empty))
            .Where(link => link.LineText.Contains(logger, StringComparison.Ordinal))
            .ToList();
    }

    private sealed record TerminalLinkRow(string Target, bool IsUrl, int StartOffset, int Length, string LineText);

    [Fact]
    public async Task ExecuteAsync_PadRejectsInvalidSelectorsAndRoutesARegularIdToItsSnapshot()
    {
        using var culture = new NotchQueryCultureScope();
        using var shell = new ShellViewModel { IsDevPageAvailable = true };
        shell.FreeformHelper.RegularPads.Add(CreateRegularPad(100, 0, 0, 0, 0, 10, 10, 0, 10));
        var useCase = new RuntimeQueryUseCase(shell);
        (IReadOnlyDictionary<string, string>? Args, string Message)[] cases =
        [
            (null, "Use exactly one of --cad-id or --regular-id."),
            (new Dictionary<string, string> { ["cad-id"] = "210", ["regular-id"] = "100" },
                "Use exactly one of --cad-id or --regular-id."),
            (new Dictionary<string, string> { ["cad-id"] = "bad" }, "Argument '--cad-id' must be an integer."),
            (new Dictionary<string, string> { ["regular-id"] = "-1" },
                "Argument '--regular-id' must be in [0, 2147483647]."),
        ];

        foreach (var (args, message) in cases)
        {
            var response = await useCase.ExecuteAsync(new RuntimeQueryRequest(RuntimeQueryProtocol.Version, "pad", args));

            AssertInvalidNotchArguments(response, message);
        }

        var regularResponse = await useCase.ExecuteAsync(new RuntimeQueryRequest(
            RuntimeQueryProtocol.Version, "pad", new Dictionary<string, string> { ["regular-id"] = "100" }));

        Assert.True(regularResponse.Ok, regularResponse.Error?.Message);
        var root = SerializeToRootElement(regularResponse.Data);
        Assert.Equal("regular", root.GetProperty("kind").GetString());
        Assert.Equal(100, root.GetProperty("regularPad").GetProperty("regularPadId").GetInt32());
        Assert.Equal(10, root.GetProperty("regularPad").GetProperty("diffIndex").GetInt32());
        Assert.Equal(100, root.GetProperty("snapshot").GetProperty("regular").GetProperty("regularPadId").GetInt32());
        Assert.Empty(shell.FreeformHelper.SelectedRegularPadIndices);
    }

    [Fact]
    public async Task ExecuteAsync_NotchCommandsRequireTheCorrectWellFormedPadIdentifier()
    {
        using var culture = new NotchQueryCultureScope();
        using var shell = new ShellViewModel { IsDevPageAvailable = true };
        var useCase = new RuntimeQueryUseCase(shell);
        (string Command, string Key)[] commands =
        [
            ("notch", "cad-id"),
            ("multi-owner", "cad-id"),
            ("notch-stage", "cad-id"),
            ("notch-validation", "regular-id"),
        ];

        foreach (var (command, key) in commands)
        {
            var wrongKey = key == "cad-id" ? "regular-id" : "cad-id";
            var missing = await useCase.ExecuteAsync(new RuntimeQueryRequest(
                RuntimeQueryProtocol.Version, command, new Dictionary<string, string> { [wrongKey] = "100" }));
            var malformed = await useCase.ExecuteAsync(new RuntimeQueryRequest(
                RuntimeQueryProtocol.Version, command, new Dictionary<string, string> { [key] = "bad" }));

            AssertInvalidNotchArguments(missing, $"Argument '--{key}' is required for query {command}.");
            AssertInvalidNotchArguments(malformed, $"Argument '--{key}' must be an integer.");
        }
    }

    [Fact]
    public async Task ExecuteAsync_NotchCommandsRejectInvalidLimitsAndOverlapInsteadOfUsingDefaults()
    {
        using var culture = new NotchQueryCultureScope();
        using var shell = new ShellViewModel { IsDevPageAvailable = true };
        var useCase = new RuntimeQueryUseCase(shell);
        (string Command, string Key, string Value, string Message)[] cases =
        [
            ("notch", "limit", "0", "Argument '--limit' must be in [1, 5000]."),
            ("notch", "target-limit", "5001", "Argument '--target-limit' must be in [1, 5000]."),
            ("notch", "polygon-limit", "2001", "Argument '--polygon-limit' must be in [1, 2000]."),
            ("multi-owner", "limit", "many", "Argument '--limit' must be an integer."),
            ("multi-owner", "overlap-percent", "100.1", "Argument '--overlap-percent' must be in [0, 100]."),
            ("notch-stage", "polygon-limit", "0", "Argument '--polygon-limit' must be in [1, 2000]."),
        ];

        foreach (var (command, key, value, message) in cases)
        {
            var args = new Dictionary<string, string> { ["cad-id"] = "210", [key] = value };
            var response = await useCase.ExecuteAsync(new RuntimeQueryRequest(RuntimeQueryProtocol.Version, command, args));

            AssertInvalidNotchArguments(response, message);
        }
    }

    [Fact]
    public async Task ExecuteAsync_NotchValidationDistinguishesMissingGridPadAndGeneratedTable()
    {
        using var culture = new NotchQueryCultureScope();
        using var emptyShell = new ShellViewModel { IsDevPageAvailable = true };
        var args = new Dictionary<string, string> { ["regular-id"] = "100" };
        var noGrid = await new RuntimeQueryUseCase(emptyShell).ExecuteAsync(
            new RuntimeQueryRequest(RuntimeQueryProtocol.Version, "notch-validation", args));

        Assert.False(noGrid.Ok);
        Assert.Equal("NOT_READY", noGrid.Error?.Code);
        Assert.Equal("Validation: build grid first.", noGrid.Error?.Message);

        using var shell = BuildShellWithNotchValidationTable(BuildTypedValidationTable());
        var useCase = new RuntimeQueryUseCase(shell);
        var missingPad = await useCase.ExecuteAsync(new RuntimeQueryRequest(
            RuntimeQueryProtocol.Version, "notch-validation", new Dictionary<string, string> { ["regular-id"] = "999" }));

        Assert.False(missingPad.Ok);
        Assert.Equal("PAD_NOT_FOUND", missingPad.Error?.Code);
        Assert.Equal("Validation: REG 999 not found.", missingPad.Error?.Message);

        shell.FreeformHelper.NotchCacheForTests.LastGeneratedTable = null;
        var noTable = await useCase.ExecuteAsync(new RuntimeQueryRequest(RuntimeQueryProtocol.Version, "notch-validation", args));

        Assert.False(noTable.Ok);
        Assert.Equal("NOT_READY", noTable.Error?.Code);
        Assert.Equal("Validation: no generated notch table. Run Step 5 Export once first.", noTable.Error?.Message);
    }

    private static void AssertInvalidNotchArguments(RuntimeQueryResponseEnvelope response, string message)
    {
        Assert.False(response.Ok);
        Assert.Equal("INVALID_ARGUMENTS", response.Error?.Code);
        Assert.Equal(message, response.Error?.Message);
        Assert.Null(response.Data);
    }

    private sealed class NotchQueryCultureScope : IDisposable
    {
        private readonly CultureInfo _previousCulture = CultureInfo.CurrentCulture;

        public NotchQueryCultureScope()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _previousCulture;
        }
    }
}
