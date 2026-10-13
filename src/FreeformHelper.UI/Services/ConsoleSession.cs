using System.Collections.Immutable;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using Nvt.Core.Avalonia.LogConsole;
using Nvt.Core.LogConsole;
using Nvt.Core.Threading;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Owns the console controller and the app-level console commands for one shell.
/// The controller reads the shared log store. This class supplies the clipboard, the link opener,
/// and the copy-visible and save-log commands that Core leaves to the host.
/// </summary>
public sealed class ConsoleSession : IConsoleClipboard, IDisposable
{
    private readonly LogStore _store;
    private readonly UiEventRunner _uiEvents;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleSession"/> class. Call it on the UI thread.
    /// </summary>
    /// <param name="store">The shared log store. The session never disposes it.</param>
    /// <param name="uiEvents">Reports failures of the commands.</param>
    /// <param name="linkOpener">Supplies link availability and opening.</param>
    public ConsoleSession(LogStore store, UiEventRunner uiEvents, IConsoleLinkOpener linkOpener)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _uiEvents = uiEvents ?? throw new ArgumentNullException(nameof(uiEvents));
        ArgumentNullException.ThrowIfNull(linkOpener);

        var options = new ConsoleProjectionOptions
        {
            Culture = CultureInfo.InvariantCulture,
            AbsoluteTimeZone = TimeZoneInfo.Local
        };
        Controller = new ConsoleController(_store, ImmutableArray<ConsoleSource>.Empty, options)
        {
            LinkOpener = linkOpener,
            Clipboard = this
        };
        CopyVisibleCommand = new AsyncRelayCommand(
            () => _uiEvents.RunAsync("Console.CopyVisible", _ => CopyVisibleAsync()));
        SaveLogCommand = new AsyncRelayCommand(
            () => _uiEvents.RunAsync("Console.SaveLog", _ => SaveLogAsync()));
    }

    /// <summary>Gets the controller that the console controls bind to.</summary>
    public ConsoleController Controller { get; }

    /// <summary>Gets the command that copies every visible row to the clipboard.</summary>
    public IAsyncRelayCommand CopyVisibleCommand { get; }

    /// <summary>Gets the command that saves every visible row to a file the view picks.</summary>
    public IAsyncRelayCommand SaveLogCommand { get; }

    /// <summary>Gets or sets the view's clipboard writer. It returns false when no clipboard is available.</summary>
    public Func<string, Task<bool>>? RequestSetClipboardTextAsync { get; set; }

    /// <summary>
    /// Gets or sets the view's save picker. It returns a writable stream for the picked file, or null when the user cancels.
    /// The session disposes the stream.
    /// </summary>
    public Func<Task<Stream?>>? RequestOpenLogStreamAsync { get; set; }

    /// <inheritdoc />
    public void SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _uiEvents.Run("Console.SetClipboardText", _ => SetClipboardTextAsync(text), CancellationToken.None);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Controller.Dispose();
    }

    private async Task SetClipboardTextAsync(string text)
    {
        if (RequestSetClipboardTextAsync is { } setText)
        {
            await setText(text);
        }
    }

    private async Task CopyVisibleAsync()
    {
        if (RequestSetClipboardTextAsync is not { } setText)
        {
            return;
        }

        using var snapshot = await _store.CaptureLatestAsync();
        using var projection = ConsoleProjector.Project(snapshot, Controller.Filter, Controller.ViewState, Controller.Options);
        await setText(ConsoleExportFormatter.FormatVisible(projection, Controller.ExportOptions));
    }

    private async Task SaveLogAsync()
    {
        if (RequestOpenLogStreamAsync is not { } openStream)
        {
            return;
        }

        var destination = await openStream();
        if (destination is null)
        {
            return;
        }

        await using (destination)
        {
            using var snapshot = await _store.CaptureLatestAsync();
            using var projection = ConsoleProjector.Project(snapshot, Controller.Filter, Controller.ViewState, Controller.Options);
            await ConsoleExportFormatter.WriteLogAsync(destination, projection, Controller.ExportOptions);
        }
    }
}
