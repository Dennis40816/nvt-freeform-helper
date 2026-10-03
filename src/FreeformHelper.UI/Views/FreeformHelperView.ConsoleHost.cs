using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;

namespace FreeformHelper.UI.Views;

public sealed partial class FreeformHelperView
{
    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ViewModels.ShellViewModel vm)
        {
            return;
        }

        if (e.PropertyName == nameof(ViewModels.ShellViewModel.IsConsoleExpanded))
        {
            EnsureConsoleEditorReady();
            SyncConsoleRowHeight(vm.IsConsoleExpanded);
            if (vm.IsConsoleExpanded)
            {
                AttachConsoleAutoScroll();
                SyncConsoleText(vm.ConsoleText);
                SyncConsoleFontSize(vm.ConsoleFontSize);
                EnableConsoleAutoFollowAndScrollToEnd();
                Dispatcher.UIThread.Post(RestoreConsoleEditorContent, DispatcherPriority.Render);
            }

            return;
        }

        if (e.PropertyName == nameof(ViewModels.ShellViewModel.ConsoleText))
        {
            if (vm.IsConsoleExpanded && _consoleEntriesRefresh is not null)
            {
                _consoleEntriesRefresh.Request();
            }
            else
            {
                SyncConsoleText(vm.ConsoleText);
            }

            return;
        }

        if (e.PropertyName == nameof(ViewModels.ShellViewModel.ConsoleSearchText) ||
            e.PropertyName == nameof(ViewModels.ShellViewModel.IsConsoleFilterEnabled))
        {
            RefreshConsolePresentation();
            return;
        }

        if (e.PropertyName == nameof(ViewModels.ShellViewModel.ConsoleFontSize))
        {
            SyncConsoleFontSize(vm.ConsoleFontSize);
        }
    }

    private void EnsureShellViewModel()
    {
        if (_shellViewModel is not null)
        {
            return;
        }

        _shellViewModel = _topLevel?.DataContext as ViewModels.ShellViewModel
            ?? DataContext as ViewModels.ShellViewModel;
        if (_shellViewModel is not null)
        {
            _shellViewModel.PropertyChanged += OnShellPropertyChanged;
            SyncConsoleText(_shellViewModel.ConsoleText);
            SyncConsoleFontSize(_shellViewModel.ConsoleFontSize);
        }
    }

    private void SyncConsoleRowHeight(bool isExpanded)
    {
        if (_consoleRow is null)
        {
            return;
        }

        if (isExpanded)
        {
            var heightResource = this.FindResource("ConsolePanelRowHeight");
            if (heightResource is GridLength gridLength)
            {
                _consoleRow.Height = gridLength;
            }
            else if (heightResource is double heightDouble)
            {
                _consoleRow.Height = new GridLength(heightDouble);
            }

            var minResource = this.FindResource("ConsolePanelMinHeight");
            if (minResource is double minDouble)
            {
                _consoleRow.MinHeight = minDouble;
            }
        }
        else
        {
            _consoleRow.Height = GridLength.Auto;
            var collapsedMin = GetResourceDouble("ConsoleCollapsedMinHeight");
            _consoleRow.MinHeight = collapsedMin > 0 ? collapsedMin : 0;
        }
    }

    private void SyncConsoleText(string? text)
    {
        if (!EnsureConsoleEditorReady())
        {
            _consoleSourceText = text ?? string.Empty;
            return;
        }

        ApplyConsoleRender(text ?? string.Empty);
    }

    private void SyncConsoleFontSize(double size)
    {
        if (!EnsureConsoleEditorReady())
        {
            return;
        }

        _consoleEditor!.FontSize = size;
    }

    private IBrush? GetResourceBrush(string key)
    {
        if (this.TryFindResource(key, out var value) && value is IBrush brush)
        {
            return brush;
        }

        return null;
    }

    private double GetResourceDouble(string key, double fallback = 0.0)
    {
        if (this.TryFindResource(key, out var value))
        {
            switch (value)
            {
                case double doubleValue:
                    return doubleValue;
                case float floatValue:
                    return floatValue;
                case int intValue:
                    return intValue;
            }
        }

        return fallback;
    }

    private bool EnsureConsoleEditorReady()
    {
        if (_consoleEditor is not null)
        {
            return true;
        }

        ResolveConsoleControls();
        if (_consoleEditor is not null)
        {
            return true;
        }

        if (_shellViewModel?.IsConsoleExpanded != true)
        {
            return false;
        }

        ResolveConsoleControls();
        return _consoleEditor is not null;
    }

    private void ResolveConsoleShellControls()
    {
        _consolePanel ??= this.FindControl<ConsolePanel>("ConsolePanelHost");
        _consoleBorder ??= _consolePanel?.FindControl<Border>("ConsoleBorder")
            ?? this.GetVisualDescendants().OfType<Border>()
                .FirstOrDefault(control => string.Equals(control.Name, "ConsoleBorder", StringComparison.Ordinal));
    }

    private void ResolveConsoleControls()
    {
        var editorResolvedNow = false;
        _consolePanel ??= this.FindControl<ConsolePanel>("ConsolePanelHost");
        if (_consoleEditor is null)
        {
            _consoleEditor = _consolePanel?.FindControl<TextEditor>("ConsoleEditor")
                ?? this.GetVisualDescendants().OfType<TextEditor>()
                    .FirstOrDefault(control => string.Equals(control.Name, "ConsoleEditor", StringComparison.Ordinal));
            editorResolvedNow = _consoleEditor is not null;
        }

        _consoleBorder ??= _consolePanel?.FindControl<Border>("ConsoleBorder")
            ?? this.GetVisualDescendants().OfType<Border>()
                .FirstOrDefault(control => string.Equals(control.Name, "ConsoleBorder", StringComparison.Ordinal));

        ApplyConsoleEditorTheme();
        if (editorResolvedNow)
        {
            RestoreConsoleEditorContent();
        }
    }

    private void ApplyConsoleEditorTheme()
    {
        if (_consoleEditor?.TextArea is null)
        {
            return;
        }

        var foreground = GetResourceBrush("BrushTextPrimary");
        if (foreground is not null)
        {
            _consoleEditor.Foreground = foreground;
            _consoleEditor.TextArea.Foreground = foreground;
            _consoleEditor.TextArea.TextView.SetValue(TextBlock.ForegroundProperty, foreground);
            _consoleEditor.TextArea.TextView.InvalidateVisual();
        }
    }

    private void RestoreConsoleEditorContent()
    {
        if (_consoleEditor?.TextArea is null)
        {
            return;
        }

        EnsureShellViewModel();
        EnsureConsoleColorizers(_consoleEditor);

        ApplyConsoleRender(GetPreferredConsoleTextSnapshot());

        if (_shellViewModel is not null)
        {
            _consoleEditor.FontSize = _shellViewModel.ConsoleFontSize;
        }

        RefreshConsoleLinks();
        _consoleEditor.TextArea.TextView.InvalidateVisual();
        _consoleEditor.TextArea.TextView.InvalidateArrange();
        _consoleEditor.InvalidateVisual();
    }
}
