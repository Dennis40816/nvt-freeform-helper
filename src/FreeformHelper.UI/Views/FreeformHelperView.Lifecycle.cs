using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FreeformHelper.Application.Services;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using NLog;

namespace FreeformHelper.UI.Views;

/// <summary>
/// Shell-level console hosting for <see cref="FreeformHelperView"/>: the row height of the console panel.
/// The console content lives in <see cref="ConsolePanel"/>.
/// </summary>
public sealed partial class FreeformHelperView
{
    /// <summary>
    /// Event handler for when the control is attached to the visual tree.
    /// Used to attach ViewModel, global event handlers, and cache UI controls.
    /// </summary>
    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _isViewAttached = true;

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
            _shellViewModel.PropertyChanged += OnShellPropertyChanged;
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

        PadInfoSession.WirePopoverEvents();
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

        if (MainWindowInputSources.ShouldClearFocus(e.Source))
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
            if (!InitialCanvasFitPolicy.IsLayoutReady(
                expectedState == WindowState.Maximized,
                window.WindowState == WindowState.Maximized,
                window.Bounds.Width,
                window.Bounds.Height))
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
        Dispatcher.Post(() => Handler(null, EventArgs.Empty), DispatcherPriority.Loaded);
    }

    private void OnLayerTogglePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not ToggleButton control)
        {
            return;
        }

        if (control.DataContext is not LayerToggle toggle)
        {
            return;
        }

        if (DataContext is not FreeformHelperViewModel viewModel)
        {
            return;
        }

        if (LayerTogglePointerPolicy.IsSoloRequest(e.GetCurrentPoint(this).Properties.IsLeftButtonPressed, e.KeyModifiers))
        {
            viewModel.ApplyLayerSelection(toggle, soloSelect: true);
            e.Handled = true;
        }
    }

    private void ScheduleDeferredUiHooks()
    {
        Dispatcher.Post(() =>
        {
            if (!_isViewAttached)
            {
                return;
            }

            CachePadInfoControls();
            StartupPerfTracker.Mark("workspace.startup-ready-signal");

            StartupPerfTracker.Mark("workspace.deferred-ui-hooks-attached");
        }, DispatcherPriority.Background);
    }

    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ViewModels.ShellViewModel vm)
        {
            return;
        }

        if (e.PropertyName == nameof(ViewModels.ShellViewModel.IsConsoleExpanded))
        {
            SyncConsoleRowHeight(vm.IsConsoleExpanded);
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
        }
    }

    private void SyncConsoleRowHeight(bool isExpanded)
    {
        WorkspaceLayoutResourcePolicy.ApplyConsoleRowHeight(this, _consoleRow, isExpanded);
    }

    private readonly GridLength?[] _savedPanelWidths = new GridLength?[5];

    private void SetLeftPanelVisible(bool isVisible)
    {
        SetPanelVisible(0, LeftPanel, isVisible, ref _savedPanelWidths[0]);
        CanvasOverlayControls.ShowLeftPanelButtonVisible = !isVisible;
    }

    private void SetRightPanelVisible(bool isVisible)
    {
        SetPanelVisible(4, RightPanel, isVisible, ref _savedPanelWidths[4]);
        CanvasOverlayControls.ShowRightPanelButtonVisible = !isVisible;
    }

    private void SetPanelVisible(int column, Control panel, bool isVisible, ref GridLength? savedWidth)
    {
        if (LayoutGrid.ColumnDefinitions.Count <= column) return;
        var definition = LayoutGrid.ColumnDefinitions[column];
        if (!isVisible && panel.IsVisible) savedWidth = definition.Width;
        if (isVisible && savedWidth is { } width) definition.Width = width;
        if (!isVisible) definition.Width = new GridLength(0);
        panel.IsVisible = isVisible;
    }

    private void HideLeftPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => SetLeftPanelVisible(false);
    private void ShowLeftPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => SetLeftPanelVisible(true);
    private void HideRightPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => SetRightPanelVisible(false);
    private void ShowRightPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => SetRightPanelVisible(true);
}
