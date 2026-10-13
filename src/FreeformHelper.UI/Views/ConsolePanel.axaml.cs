using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Nvt.Core.LogConsole;

namespace FreeformHelper.UI.Views;

/// <summary>
/// The console panel of the shell. It composes the Core console controls over the shell's console session
/// and supplies the clipboard writer and the log file picker that the session needs.
/// </summary>
public partial class ConsolePanel : UserControl
{
    private const string LogFileExtension = "log";

    private ConsoleSession? _session;
    private Func<string, Task<bool>>? _clipboardWriter;
    private Func<Task<Stream?>>? _logStreamOpener;

    public ConsolePanel()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => AttachSession();
        DetachedFromVisualTree += (_, _) => DetachSession();
        DataContextChanged += (_, _) =>
        {
            DetachSession();
            AttachSession();
        };
        ConsoleList.ViewStateRequested += OnViewStateRequested;
    }

    private void AttachSession()
    {
        if (_session is not null || DataContext is not ShellViewModel shell)
        {
            return;
        }

        _session = shell.ConsoleSession;
        _clipboardWriter = SetClipboardTextAsync;
        _logStreamOpener = OpenLogStreamAsync;
        _session.RequestSetClipboardTextAsync = _clipboardWriter;
        _session.RequestOpenLogStreamAsync = _logStreamOpener;
    }

    private void DetachSession()
    {
        if (_session is null)
        {
            return;
        }

        // Two panels share one session. Release only the adapters that this panel installed.
        if (ReferenceEquals(_session.RequestSetClipboardTextAsync, _clipboardWriter))
        {
            _session.RequestSetClipboardTextAsync = null;
        }

        if (ReferenceEquals(_session.RequestOpenLogStreamAsync, _logStreamOpener))
        {
            _session.RequestOpenLogStreamAsync = null;
        }

        _clipboardWriter = null;
        _logStreamOpener = null;
        _session = null;
    }

    private void OnViewStateRequested(object? sender, ConsoleViewState state)
    {
        (DataContext as ShellViewModel)?.ConsoleSession.Controller.RequestViewState(state);
    }

    private async Task<bool> SetClipboardTextAsync(string text)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            return false;
        }

        await clipboard.SetTextAsync(text);
        return true;
    }

    private async Task<Stream?> OpenLogStreamAsync()
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null || !storage.CanSave)
        {
            return null;
        }

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save console log",
            SuggestedFileName = "freeform-helper-console.log",
            DefaultExtension = LogFileExtension,
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Log file") { Patterns = new[] { "*." + LogFileExtension } },
            },
        });
        return file is null ? null : await file.OpenWriteAsync();
    }
}
