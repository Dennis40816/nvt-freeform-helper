using System.Text;
using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Nvt.Core.LogConsole;
using Nvt.Core.Threading;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ConsoleSessionTests
{
    [AvaloniaFact]
    public async Task CopyVisibleCommand_WithAClipboard_WritesTheVisibleRowsOnce()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var sourceId = $"session-copy-{Guid.NewGuid():N}";
        using var store = new LogStore();
        store.Add(LogLevel.Info, sourceId, "first line", new DateTimeOffset(2026, 10, 13, 1, 2, 3, TimeSpan.Zero));
        store.Add(LogLevel.Error, sourceId, "second line", new DateTimeOffset(2026, 10, 13, 1, 2, 4, TimeSpan.Zero));
        using var session = new ConsoleSession(store, new UiEventRunner(static (_, _) => { }, static (_, _) => { }), new ConsoleLinkOpener());
        string? copied = null;
        session.RequestSetClipboardTextAsync = text =>
        {
            copied = text;
            return Task.FromResult(true);
        };

        await session.CopyVisibleCommand.ExecuteAsync(null);

        Assert.NotNull(copied);
        Assert.Contains("first line", copied, StringComparison.Ordinal);
        Assert.Contains("second line", copied, StringComparison.Ordinal);
        Assert.Contains(sourceId, copied, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task CopyVisibleCommand_WithoutAClipboard_DoesNothing()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var store = new LogStore();
        store.Add(LogLevel.Info, "session-none", "line", DateTimeOffset.UtcNow);
        using var session = new ConsoleSession(store, new UiEventRunner(static (_, _) => { }, static (_, _) => { }), new ConsoleLinkOpener());

        var exception = await Record.ExceptionAsync(() => session.CopyVisibleCommand.ExecuteAsync(null));

        Assert.Null(exception);
    }

    [AvaloniaFact]
    public async Task SaveLogCommand_WithAStream_WritesTheVisibleRowsAndDisposesTheStream()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var store = new LogStore();
        store.Add(LogLevel.Warn, "session-save", "saved line", new DateTimeOffset(2026, 10, 13, 1, 2, 3, TimeSpan.Zero));
        using var session = new ConsoleSession(store, new UiEventRunner(static (_, _) => { }, static (_, _) => { }), new ConsoleLinkOpener());
        var stream = new TrackingStream();
        session.RequestOpenLogStreamAsync = () => Task.FromResult<Stream?>(stream);

        await session.SaveLogCommand.ExecuteAsync(null);

        Assert.Contains("saved line", Encoding.UTF8.GetString(stream.Written), StringComparison.Ordinal);
        Assert.True(stream.WasDisposed);
    }

    [AvaloniaFact]
    public async Task SaveLogCommand_WhenThePickerIsCancelled_AsksOnceAndWritesNothing()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var store = new LogStore();
        using var session = new ConsoleSession(store, new UiEventRunner(static (_, _) => { }, static (_, _) => { }), new ConsoleLinkOpener());
        var requests = 0;
        session.RequestOpenLogStreamAsync = () =>
        {
            requests++;
            return Task.FromResult<Stream?>(null);
        };

        await session.SaveLogCommand.ExecuteAsync(null);

        Assert.Equal(1, requests);
    }

    private sealed class TrackingStream : MemoryStream
    {
        public bool WasDisposed { get; private set; }

        public byte[] Written { get; private set; } = [];

        protected override void Dispose(bool disposing)
        {
            if (!WasDisposed)
            {
                Written = ToArray();
                WasDisposed = true;
            }

            base.Dispose(disposing);
        }
    }
}
