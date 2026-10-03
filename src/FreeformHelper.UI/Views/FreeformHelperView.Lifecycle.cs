using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views;

public sealed partial class FreeformHelperView
{
    /// <summary>
    /// Event handler for when the control is attached to the visual tree.
    /// Used to attach ViewModel, global event handlers, and cache UI controls.
    /// </summary>
    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _isViewAttached = true;
        ResolveConsoleShellControls();

        if (DataContext is FreeformHelperViewModel viewModel)
        {
            AttachViewModel(viewModel);
        }

        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel is not null)
        {
            _topLevel.KeyDown += OnTopLevelKeyDown;
        }

        _shellViewModel = _topLevel?.DataContext as ViewModels.ShellViewModel;
        if (_shellViewModel is not null)
        {
            _consolePanel ??= this.FindControl<ConsolePanel>("ConsolePanelHost");
            if (_consolePanel is not null)
            {
                _consolePanel.DataContext = _shellViewModel;
            }

            _shellViewModel.PropertyChanged += OnShellPropertyChanged;
            EnsureConsoleEditorReady();
            SyncConsoleRowHeight(_shellViewModel.IsConsoleExpanded);
        }

        ScheduleDeferredUiHooks();
    }

    /// <summary>
    /// Event handler for when the control is detached from the visual tree.
    /// Used to detach ViewModel and global event handlers to prevent memory leaks.
    /// </summary>
    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _isViewAttached = false;
        if (DataContext is FreeformHelperViewModel viewModel)
        {
            DetachViewModel(viewModel);
        }

        if (_topLevel is not null)
        {
            _topLevel.KeyDown -= OnTopLevelKeyDown;
            _topLevel = null;
        }

        if (_initialFitWindow is not null && _initialFitHandler is not null)
        {
            _initialFitWindow.LayoutUpdated -= _initialFitHandler;
            _initialFitWindow = null;
            _initialFitHandler = null;
        }

        DetachConsoleAutoScroll();

        if (_shellViewModel is not null)
        {
            _shellViewModel.PropertyChanged -= OnShellPropertyChanged;
            _shellViewModel = null;
        }

        if (_settingsWindow is not null)
        {
            _settingsWindow.Close();
            _settingsWindow = null;
        }

        _consolePanel = null;
    }

    /// <summary>
    /// Locates the <see cref="PadCanvas"/> control within the view.
    /// </summary>
    /// <returns>The <see cref="PadCanvas"/> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the PadCanvas is not found.</exception>
    private PadCanvas FindCanvas()
        => this.FindControl<PadCanvas>("PadCanvas")
            ?? throw new InvalidOperationException("PadCanvas not found in FreeformHelperView.");

    /// <summary>
    /// Caches references to controls related to the pad information popover.
    /// Also wires up events for the popover.
    /// </summary>
    private void CachePadInfoControls()
    {
        _padInfoOverlay = this.FindControl<Canvas>("PadInfoOverlay");
        _padInfoPopover = this.FindControl<PadInfoPopover>("PadInfoPopover");
        _padInfoLink = this.FindControl<Avalonia.Controls.Shapes.Path>("PadInfoLink");
        _padInfoAnchor = this.FindControl<Avalonia.Controls.Shapes.Ellipse>("PadInfoAnchor");

        if (_padInfoPopover is not null)
        {
            _padInfoPopover.ApplyCloseRequested += (_, _) => ClosePadInfoViaState();
            _padInfoPopover.DiscardCloseRequested += (_, _) => ClosePadInfoViaState();
            _padInfoPopover.CloseConfirmDismissed += (_, _) =>
            {
                _pendingPadInfoContext = null;
                SetPadAreaHitTest(true);
            };
            _padInfoPopover
                .GetObservable(PadInfoPopover.IsCloseConfirmVisibleProperty)
                .Subscribe(visible => SetPadAreaHitTest(!visible));
        }
    }

    /// <summary>
    /// Event handler for pointer presses on the root element of the view.
    /// Used to close pad info if clicked outside, and to set focus to the canvas.
    /// </summary>
    private void OnRootPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ClosePadInfoIfOutside(e))
        {
            return;
        }

        CloseRootMenusIfOutside(e.Source);

        if (e.Source is Visual sourceVisual &&
            (sourceVisual is ConsolePanel || sourceVisual.FindAncestorOfType<ConsolePanel>() is not null))
        {
            return;
        }

        if (ShouldClearFocus(e.Source))
        {
            _canvas?.Focus();
        }
    }

    private void ScheduleInitialFit(FreeformHelperViewModel viewModel)
    {
        if (viewModel is null)
        {
            return;
        }

        var window = TopLevel.GetTopLevel(this) as Window;
        if (window is null)
        {
            viewModel.EnsureInitialFit();
            return;
        }

        var expectedState = window.WindowState;
        void Handler(object? sender, EventArgs e)
        {
            if (expectedState == WindowState.Maximized && window.WindowState != WindowState.Maximized)
            {
                return;
            }

            if (window.Bounds.Width <= 0 || window.Bounds.Height <= 0)
            {
                return;
            }

            window.LayoutUpdated -= Handler;
            _initialFitWindow = null;
            _initialFitHandler = null;
            viewModel.EnsureInitialFit();
        }

        _initialFitWindow = window;
        _initialFitHandler = Handler;
        window.LayoutUpdated += Handler;
        Dispatcher.UIThread.Post(() => Handler(null, EventArgs.Empty), DispatcherPriority.Loaded);
    }

    private void OnLayerTogglePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed is false)
        {
            return;
        }

        if (sender is not ToggleButton control)
        {
            return;
        }

        if (control.DataContext is not FreeformHelperViewModel.LayerToggle toggle)
        {
            return;
        }

        if (DataContext is not FreeformHelperViewModel viewModel)
        {
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            viewModel.ApplyLayerSelection(toggle, soloSelect: true);
            e.Handled = true;
        }
    }

    private void ScheduleDeferredUiHooks()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!_isViewAttached)
            {
                return;
            }

            AttachConsoleAutoScroll();
            CachePadInfoControls();
            StartupPerfTracker.Mark("workspace.startup-ready-signal");

            StartupPerfTracker.Mark("workspace.deferred-ui-hooks-attached");

            Dispatcher.UIThread.Post(() =>
            {
                if (!_isViewAttached || _shellViewModel is null)
                {
                    return;
                }

                SyncConsoleText(_shellViewModel.ConsoleText);
                SyncConsoleFontSize(_shellViewModel.ConsoleFontSize);
                StartupPerfTracker.Mark("workspace.console-initial-sync");
            }, DispatcherPriority.Background);
        }, DispatcherPriority.Background);
    }
}
