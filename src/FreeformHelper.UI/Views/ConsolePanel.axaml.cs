using System.ComponentModel;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using FreeformHelper.UI.ViewModels;
using Nvt.Core.Avalonia.Threading;
using Nvt.Core.Threading;

namespace FreeformHelper.UI.Views;

public partial class ConsolePanel : UserControl
{
    private UiEventRunner? _uiEvents => _shellViewModel?.UiEvents ?? (DataContext as ShellViewModel)?.UiEvents ?? (DataContext as FreeformHelperViewModel)?.UiEvents;

    public static readonly StyledProperty<bool> UseShellHostedBehaviorProperty =
        AvaloniaProperty.Register<ConsolePanel, bool>(nameof(UseShellHostedBehavior));

    private ShellViewModel? _shellViewModel;
    private TextEditor? _consoleEditor;
    private Border? _consoleBorder;
    private string _consoleSourceText = string.Empty;
    private bool _consoleAutoFollow = true;

    public event EventHandler<PointerPressedEventArgs>? ConsolePointerPressed;
    public event EventHandler<PointerReleasedEventArgs>? ConsoleEditorPointerReleased;
    public event EventHandler<RoutedEventArgs>? CopyAllRequested;
    public event EventHandler<RoutedEventArgs>? JumpToBottomRequested;
    public event EventHandler<RoutedEventArgs>? FontIncreaseRequested;
    public event EventHandler<RoutedEventArgs>? FontDecreaseRequested;

    public ConsolePanel()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        DataContextChanged += OnDataContextChanged;
    }

    public bool UseShellHostedBehavior
    {
        get => GetValue(UseShellHostedBehaviorProperty);
        set => SetValue(UseShellHostedBehaviorProperty, value);
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (!UseShellHostedBehavior)
        {
            return;
        }

        AttachShellViewModel();
        ResolveConsoleControls();
        AttachEditorEvents();
        ApplyConsoleRender(GetShellConsoleText());
        SyncConsoleFontSize();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        DetachEditorEvents();
        DetachShellViewModel();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (!UseShellHostedBehavior)
        {
            return;
        }

        DetachShellViewModel();
        AttachShellViewModel();
        ResolveConsoleControls();
        AttachEditorEvents();
        ApplyConsoleRender(GetShellConsoleText());
        SyncConsoleFontSize();
    }

    private void AttachShellViewModel()
    {
        if (_shellViewModel is not null)
        {
            return;
        }

        _shellViewModel = TopLevel.GetTopLevel(this)?.DataContext as ShellViewModel
            ?? DataContext as ShellViewModel;
        if (_shellViewModel is null)
        {
            return;
        }

        _shellViewModel.PropertyChanged += OnShellViewModelPropertyChanged;
    }

    private void DetachShellViewModel()
    {
        if (_shellViewModel is null)
        {
            return;
        }

        _shellViewModel.PropertyChanged -= OnShellViewModelPropertyChanged;
        _shellViewModel = null;
    }

    private void ResolveConsoleControls()
    {
        _consoleEditor ??= this.FindControl<TextEditor>("ConsoleEditor");
        _consoleBorder ??= this.FindControl<Border>("ConsoleBorder");
    }

    private void AttachEditorEvents()
    {
        if (_consoleEditor is null)
        {
            return;
        }

        _consoleEditor.TextChanged -= OnConsoleEditorTextChanged;
        _consoleEditor.TextChanged += OnConsoleEditorTextChanged;
        _consoleEditor.PropertyChanged -= OnConsoleEditorPropertyChanged;
        _consoleEditor.PropertyChanged += OnConsoleEditorPropertyChanged;
    }

    private void DetachEditorEvents()
    {
        if (_consoleEditor is null)
        {
            return;
        }

        _consoleEditor.TextChanged -= OnConsoleEditorTextChanged;
        _consoleEditor.PropertyChanged -= OnConsoleEditorPropertyChanged;
    }

    private void OnShellViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!UseShellHostedBehavior || !UiThread.TryGetRunningDispatcher(out var dispatcher))
        {
            return;
        }

        if (e.PropertyName is nameof(ShellViewModel.ConsoleText)
            or nameof(ShellViewModel.ConsoleSearchText)
            or nameof(ShellViewModel.IsConsoleFilterEnabled)
            or nameof(ShellViewModel.IsConsoleDedupEnabled))
        {
            ApplyConsoleRender(GetShellConsoleText());
            if (_consoleAutoFollow)
            {
                ScrollConsoleToEnd();
            }

            return;
        }

        if (e.PropertyName == nameof(ShellViewModel.ConsoleFontSize))
        {
            SyncConsoleFontSize();
            return;
        }

        if (e.PropertyName == nameof(ShellViewModel.IsConsoleExpanded))
        {
            dispatcher!.Post(() =>
            {
                ResolveConsoleControls();
                ApplyConsoleRender(GetShellConsoleText());
                SyncConsoleFontSize();
                if (_shellViewModel?.IsConsoleExpanded == true)
                {
                    ScrollConsoleToEnd();
                }
            }, DispatcherPriority.Loaded);
        }
    }

    private void OnConsoleEditorTextChanged(object? sender, EventArgs e)
    {
        if (_consoleAutoFollow)
        {
            ScrollConsoleToEnd();
        }
    }

    private void OnConsoleEditorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not TextEditor editor)
        {
            return;
        }

        if (!string.Equals(e.Property.Name, "VerticalOffset", StringComparison.Ordinal) &&
            !string.Equals(e.Property.Name, "ViewportHeight", StringComparison.Ordinal) &&
            !string.Equals(e.Property.Name, "ExtentHeight", StringComparison.Ordinal))
        {
            return;
        }

        _consoleAutoFollow = IsConsoleNearBottom(editor);
    }

    private void ApplyConsoleRender(string sourceText)
    {
        _consoleSourceText = sourceText ?? string.Empty;
        if (_consoleEditor is null)
        {
            return;
        }

        var renderedText = BuildConsoleRenderedText(_consoleSourceText, _shellViewModel?.ConsoleSearchText, _shellViewModel?.IsConsoleFilterEnabled == true);
        if (!string.Equals(_consoleEditor.Text, renderedText, StringComparison.Ordinal))
        {
            _consoleEditor.Text = renderedText;
        }
    }

    private void SyncConsoleFontSize()
    {
        if (_consoleEditor is null || _shellViewModel is null)
        {
            return;
        }

        _consoleEditor.FontSize = _shellViewModel.ConsoleFontSize;
    }

    private string GetShellConsoleText()
    {
        AttachShellViewModel();
        return _shellViewModel?.ConsoleText ?? string.Empty;
    }

    private void ScrollConsoleToEnd()
    {
        if (_consoleEditor is null)
        {
            return;
        }

        _consoleEditor.CaretOffset = _consoleEditor.Text?.Length ?? 0;
        _consoleEditor.ScrollToEnd();
    }

    private void OnConsolePointerPressedInternal(object? sender, PointerPressedEventArgs e)
    {
        _uiEvents?.Run("Console.PointerPressed", _ => HandleConsolePointerPressedAsync(sender, e), CancellationToken.None);
    }

    private async Task HandleConsolePointerPressedAsync(object? sender, PointerPressedEventArgs e)
    {
        if (UseShellHostedBehavior)
        {
            ResolveConsoleControls();
            if (e.Source is Visual visual &&
                (visual.FindAncestorOfType<Button>() is not null || visual.FindAncestorOfType<ToggleButton>() is not null))
            {
                return;
            }

            if (_shellViewModel?.IsConsoleExpanded == true && _consoleEditor is not null && _consoleEditor.IsVisible)
            {
                _consoleEditor.Focus();
            }
            else
            {
                _consoleBorder?.Focus();
            }

            return;
        }

        ConsolePointerPressed?.Invoke(sender, e);
        await Task.CompletedTask;
    }

    private void OnConsoleEditorPointerReleasedInternal(object? sender, PointerReleasedEventArgs e)
    {
        if (UseShellHostedBehavior)
        {
            return;
        }

        ConsoleEditorPointerReleased?.Invoke(sender, e);
    }

    private void OnConsoleCopyAllInternal(object? sender, RoutedEventArgs e)
    {
        _uiEvents?.Run("Console.CopyAll", _ => CopyConsoleAsync(sender, e), CancellationToken.None);
    }

    private async Task CopyConsoleAsync(object? sender, RoutedEventArgs e)
    {
        if (UseShellHostedBehavior)
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is not null)
            {
                await clipboard.SetTextAsync(GetShellConsoleText());
            }

            return;
        }

        CopyAllRequested?.Invoke(sender, e);
    }

    private void OnConsoleJumpToBottomInternal(object? sender, RoutedEventArgs e)
    {
        if (UseShellHostedBehavior)
        {
            _consoleAutoFollow = true;
            ScrollConsoleToEnd();
            return;
        }

        JumpToBottomRequested?.Invoke(sender, e);
    }

    private void OnConsoleFontIncreaseInternal(object? sender, RoutedEventArgs e)
    {
        if (UseShellHostedBehavior)
        {
            AdjustConsoleFontSize(+1d);
            return;
        }

        FontIncreaseRequested?.Invoke(sender, e);
    }

    private void OnConsoleFontDecreaseInternal(object? sender, RoutedEventArgs e)
    {
        if (UseShellHostedBehavior)
        {
            AdjustConsoleFontSize(-1d);
            return;
        }

        FontDecreaseRequested?.Invoke(sender, e);
    }

    private void AdjustConsoleFontSize(double delta)
    {
        AttachShellViewModel();
        if (_shellViewModel is null)
        {
            return;
        }

        var min = GetResourceDouble("ConsoleFontMin", 10d);
        var max = GetResourceDouble("ConsoleFontMax", 32d);
        _shellViewModel.ConsoleFontSize = Math.Clamp(_shellViewModel.ConsoleFontSize + delta, min, max);
    }

    private double GetResourceDouble(string key, double fallback)
    {
        if (this.TryFindResource(key, out var value))
        {
            switch (value)
            {
                case double d:
                    return d;
                case float f:
                    return f;
                case int i:
                    return i;
            }
        }

        return fallback;
    }

    private static string BuildConsoleRenderedText(string sourceText, string? searchText, bool filterEnabled)
    {
        sourceText ??= string.Empty;
        var keyword = searchText?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(keyword) || !filterEnabled)
        {
            return sourceText;
        }

        var lines = sourceText.Split('\n');
        var builder = new StringBuilder(sourceText.Length);
        foreach (var rawLine in lines)
        {
            var line = rawLine.EndsWith('\r') ? rawLine[..^1] : rawLine;
            if (line.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(line);
        }

        return builder.ToString();
    }

    private static bool IsConsoleNearBottom(TextEditor editor)
    {
        if (editor.ExtentHeight <= 0 || editor.ViewportHeight <= 0)
        {
            return true;
        }

        var remaining = editor.ExtentHeight - (editor.VerticalOffset + editor.ViewportHeight);
        return remaining <= 1.0;
    }
}
