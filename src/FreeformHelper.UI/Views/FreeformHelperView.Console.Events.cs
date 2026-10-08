using System.Collections.Specialized;
using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;
using AvaloniaEdit;
using FreeformHelper.UI.Logging;
using Nvt.Core.Avalonia.Threading;
using Nvt.Core.Lifecycle;

namespace FreeformHelper.UI.Views;

public sealed partial class FreeformHelperView
{
    // Log collection and expanded shell text changes share one queued refresh. It reads the latest
    // snapshot when it runs, so a burst re-renders and re-parses the console once.
    private CoalescedRefresh? _consoleEntriesRefresh;

    private void AttachConsoleAutoScroll()
    {
        EnsureConsoleEditorReady();
        if (_consoleEditor is not null)
        {
            EnsureConsoleColorizers(_consoleEditor);
            _consoleEditor.TextChanged -= OnConsoleEditorTextChanged;
            _consoleEditor.TextChanged += OnConsoleEditorTextChanged;
            _consoleEditor.PropertyChanged -= OnConsoleEditorPropertyChanged;
            _consoleEditor.PropertyChanged += OnConsoleEditorPropertyChanged;
            _consoleEditor.PointerMoved -= OnConsoleEditorPointerMoved;
            _consoleEditor.PointerMoved += OnConsoleEditorPointerMoved;
            _consoleEditor.PointerExited -= OnConsoleEditorPointerExited;
            _consoleEditor.PointerExited += OnConsoleEditorPointerExited;

            if (_consoleTextView is not null)
            {
                _consoleTextView.ScrollOffsetChanged -= OnConsoleTextViewScrollOffsetChanged;
                _consoleTextView.PointerMoved -= OnConsoleTextViewPointerMoved;
                _consoleTextView.PointerExited -= OnConsoleTextViewPointerExited;
                _consoleTextView.PointerPressed -= OnConsoleTextViewPointerPressed;
                _consoleTextView.PointerReleased -= OnConsoleTextViewPointerReleased;
            }

            _consoleTextView = _consoleEditor.TextArea?.TextView;
            if (_consoleTextView is not null)
            {
                _consoleTextView.ScrollOffsetChanged -= OnConsoleTextViewScrollOffsetChanged;
                _consoleTextView.ScrollOffsetChanged += OnConsoleTextViewScrollOffsetChanged;
                _consoleTextView.PointerMoved -= OnConsoleTextViewPointerMoved;
                _consoleTextView.PointerMoved += OnConsoleTextViewPointerMoved;
                _consoleTextView.PointerExited -= OnConsoleTextViewPointerExited;
                _consoleTextView.PointerExited += OnConsoleTextViewPointerExited;
                _consoleTextView.PointerPressed -= OnConsoleTextViewPointerPressed;
                _consoleTextView.PointerPressed += OnConsoleTextViewPointerPressed;
                _consoleTextView.PointerReleased -= OnConsoleTextViewPointerReleased;
                _consoleTextView.PointerReleased += OnConsoleTextViewPointerReleased;
            }
        }

        _consoleAutoFollow = true;
        SyncConsoleText(GetPreferredConsoleTextSnapshot());

        if (_logEntries is not null)
        {
            return;
        }

        if (AppLogStore.Instance.Entries is INotifyCollectionChanged notify)
        {
            _consoleEntriesRefresh ??= new CoalescedRefresh(
                refresh =>
                {
                    if (UiThread.TryGetRunningDispatcher(out var dispatcher))
                    {
                        dispatcher!.Post(refresh, DispatcherPriority.Background);
                    }
                    else
                    {
                        _consoleEntriesRefresh?.Reset();
                    }
                },
                RefreshConsoleFromLogEntries);
            _logEntries = notify;
            _logEntries.CollectionChanged += OnConsoleEntriesChanged;
        }

        Dispatcher.UIThread.Post(() =>
        {
            EnsureConsoleEditorReady();
            SyncConsoleText(GetPreferredConsoleTextSnapshot());
            if (_consoleEditor is not null)
            {
                EnableConsoleAutoFollowAndScrollToEnd();
            }

            if (_consoleEditor is null)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    EnsureConsoleEditorReady();
                    SyncConsoleText(GetPreferredConsoleTextSnapshot());
                    if (_consoleEditor is not null)
                    {
                        EnableConsoleAutoFollowAndScrollToEnd();
                    }
                }, DispatcherPriority.Background);
            }
        }, DispatcherPriority.Loaded);
    }

    private void DetachConsoleAutoScroll()
    {
        if (_logEntries is not null)
        {
            _logEntries.CollectionChanged -= OnConsoleEntriesChanged;
        }

        if (_consoleEditor is not null)
        {
            _consoleEditor.TextChanged -= OnConsoleEditorTextChanged;
            _consoleEditor.PropertyChanged -= OnConsoleEditorPropertyChanged;
            _consoleEditor.PointerMoved -= OnConsoleEditorPointerMoved;
            _consoleEditor.PointerExited -= OnConsoleEditorPointerExited;
            if (_consoleBaseTextColorizer is not null)
            {
                _consoleEditor.TextArea.TextView.LineTransformers.Remove(_consoleBaseTextColorizer);
            }

            if (_consoleSearchHighlightColorizer is not null)
            {
                _consoleEditor.TextArea.TextView.LineTransformers.Remove(_consoleSearchHighlightColorizer);
            }

            if (_consoleLinkColorizer is not null)
            {
                _consoleEditor.TextArea.TextView.LineTransformers.Remove(_consoleLinkColorizer);
            }
        }

        if (_consoleTextView is not null)
        {
            _consoleTextView.ScrollOffsetChanged -= OnConsoleTextViewScrollOffsetChanged;
            _consoleTextView.PointerMoved -= OnConsoleTextViewPointerMoved;
            _consoleTextView.PointerExited -= OnConsoleTextViewPointerExited;
            _consoleTextView.PointerPressed -= OnConsoleTextViewPointerPressed;
            _consoleTextView.PointerReleased -= OnConsoleTextViewPointerReleased;
            _consoleTextView = null;
        }

        _logEntries = null;
        _consoleEntriesRefresh?.Reset();
        _consoleBaseTextColorizer = null;
        _consoleSearchHighlightColorizer = null;
        _consoleLinkColorizer = null;
        _consoleLinks = Array.Empty<ConsoleLinkSpan>();
        _consoleSearchRanges = Array.Empty<ConsoleTextRange>();
        _consoleSourceText = string.Empty;
        _consoleHoveredLink = null;
        _consolePressedLink = null;
        _consoleEditor = null;
        _consoleAutoFollow = true;
    }

    private void OnConsoleEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _consoleEntriesRefresh?.Request();
    }

    private void RefreshConsoleFromLogEntries()
    {
        // A refresh queued before the view detached must not rebuild the console for a detached view.
        if (_logEntries is null)
        {
            return;
        }

        UpdateConsoleAutoFollowState();
        SyncConsoleText(GetLatestConsoleTextSnapshot());
        if (_consoleAutoFollow)
        {
            ScrollConsoleToEnd(preserveHorizontalOffset: true);
        }
    }

    private void OnConsoleEditorTextChanged(object? sender, EventArgs e)
    {
        RefreshConsoleLinks();
        if (_consoleAutoFollow)
        {
            ScrollConsoleToEnd(preserveHorizontalOffset: true);
        }
    }

    private void OnConsoleEditorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not TextEditor)
        {
            return;
        }

        if (!string.Equals(e.Property.Name, "VerticalOffset", StringComparison.Ordinal) &&
            !string.Equals(e.Property.Name, "ViewportHeight", StringComparison.Ordinal) &&
            !string.Equals(e.Property.Name, "ExtentHeight", StringComparison.Ordinal))
        {
            return;
        }

        UpdateConsoleAutoFollowState();
    }

    private void OnConsoleTextViewScrollOffsetChanged(object? sender, EventArgs e)
    {
        UpdateConsoleAutoFollowState();
    }

    private void OnConsoleEditorPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_consoleEditor is not null && _consoleTextView is not null)
        {
            UpdateConsolePointerCursor(_consoleEditor, e.GetPosition(_consoleTextView));
        }
    }

    private void OnConsoleTextViewPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_consoleEditor is not null && _consoleTextView is not null)
        {
            UpdateConsolePointerCursor(_consoleEditor, e.GetPosition(_consoleTextView));
        }
    }

    private void UpdateConsolePointerCursor(TextEditor editor, Point pointInTextView)
    {
        var link = FindConsoleLinkAtPoint(editor, pointInTextView);
        UpdateHoveredConsoleLink(link);
        SetConsoleCursor(editor, link is null ? ConsoleTextCursor : ConsoleLinkCursor);
    }

    private void OnConsoleTextViewPointerExited(object? sender, PointerEventArgs e)
    {
        if (_consoleEditor is not null)
        {
            SetConsoleCursor(_consoleEditor, ConsoleTextCursor);
        }

        UpdateHoveredConsoleLink(null);
        _consolePressedLink = null;
    }

    private void OnConsoleTextViewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_consoleEditor is null || _consoleTextView is null)
        {
            return;
        }

        _consolePressedLink = FindConsoleLinkAtPoint(_consoleEditor, e.GetPosition(_consoleTextView));
    }

    private void OnConsoleTextViewPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_consoleEditor is null || _consoleTextView is null)
        {
            return;
        }

        TryHandleConsoleLinkActivation(_consoleEditor, e, e.GetPosition(_consoleTextView));
    }

    private static void SetConsoleCursor(TextEditor editor, Cursor cursor)
    {
        editor.Cursor = cursor;
        if (editor.TextArea is not null)
        {
            editor.TextArea.Cursor = cursor;
            editor.TextArea.TextView.Cursor = cursor;
        }
    }

    private void UpdateHoveredConsoleLink(ConsoleLinkSpan? link)
    {
        var current = _consoleHoveredLink;
        var unchanged = current is null && link is null;
        if (!unchanged && current is not null && link is not null)
        {
            unchanged = current.StartOffset == link.StartOffset &&
                        current.Length == link.Length;
        }

        if (unchanged)
        {
            return;
        }

        _consoleHoveredLink = link;
        if (_consoleLinkColorizer is null || _consoleEditor is null)
        {
            return;
        }

        _consoleLinkColorizer.SetHoveredLink(_consoleHoveredLink);
        _consoleEditor.TextArea.TextView.InvalidateVisual();
    }

    private void OnConsoleEditorPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is not TextEditor editor)
        {
            return;
        }

        SetConsoleCursor(editor, ConsoleTextCursor);
        UpdateHoveredConsoleLink(null);
    }

    private void ScrollConsoleToEnd(bool preserveHorizontalOffset)
    {
        if (_consoleEditor is null)
        {
            return;
        }

        if (_consoleEditor.IsKeyboardFocusWithin &&
            _consoleEditor.SelectionLength > 0)
        {
            return;
        }

        var horizontalOffset = preserveHorizontalOffset ? _consoleEditor.HorizontalOffset : 0d;
        _consoleEditor.CaretOffset = _consoleEditor.Text?.Length ?? 0;
        _consoleEditor.ScrollToEnd();
        if (preserveHorizontalOffset)
        {
            _consoleEditor.ScrollToHorizontalOffset(horizontalOffset);
        }
    }
}
