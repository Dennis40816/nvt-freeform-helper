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
        AppConsoleLogWrite(timestamp, LogLevel.Warn, logger, "Pad 12 moved");
        using var shell = new ShellViewModel();
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
        AppConsoleLogWrite(DateTimeOffset.UnixEpoch, LogLevel.Info, $"rq-count-{Guid.NewGuid():N}", "count line");
        using var shell = new ShellViewModel();
        var useCase = new RuntimeQueryUseCase(shell);

        var response = await useCase.ExecuteAsync(TerminalRequest("terminal", ("tail", "1")));

        Assert.True(response.Ok, $"{response.Error?.Code}:{response.Error?.Message}");
        var root = SerializeToRootElement(response.Data);
        Assert.Equal(1, root.GetProperty("returnedLines").GetInt32());
        Assert.True(root.GetProperty("totalLines").GetInt32() >= 1);
    }

    [Fact]
    public async Task ExecuteAsync_QueryTerminal_RejectsATailBelowOne()
    {
        using var shell = new ShellViewModel();
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
        AppConsoleLogWrite(DateTimeOffset.UnixEpoch, LogLevel.Info, logger, $"see {path} and {url}");
        using var shell = new ShellViewModel();
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
        AppConsoleLogWrite(DateTimeOffset.UnixEpoch, LogLevel.Info, logger, "open C:/work/a.txt then C:/work/b.txt");
        using var shell = new ShellViewModel();
        var useCase = new RuntimeQueryUseCase(shell);

        var response = await useCase.ExecuteAsync(TerminalRequest(
            "terminal-links",
            ("tail", TerminalTailCount.ToString(CultureInfo.InvariantCulture)),
            ("limit", "1")));

        Assert.True(response.Ok, $"{response.Error?.Code}:{response.Error?.Message}");
        var root = SerializeToRootElement(response.Data);
        Assert.Equal(1, root.GetProperty("links").GetArrayLength());
    }

    private static void AppConsoleLogWrite(DateTimeOffset timestamp, LogLevel level, string logger, string message)
    {
        FreeformHelper.UI.Logging.AppConsoleLog.Write(timestamp, level, logger, message);
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
}
