using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Nvt.Core.Avalonia.Theme;
using Nvt.Core.Threading;

namespace FreeformHelper.UI.Views;

public sealed partial class SimulationWorkspaceView : UserControl
{
    private UiEventRunner? _uiEvents => (DataContext as SimulationWorkspaceViewModel)?.UiEvents;

    private static readonly string[] _csvFilePatterns = ["*.csv"];
    private static readonly string[] _allFilesPatterns = ["*"];
    private PadCanvas? _canvas;
    private SimulationWorkspaceViewModel? _attachedViewModel;
    private int _hoveredRegularPadId = -1;
    private Canvas? _inlineEditorOverlay;
    private Border? _inlineEditorCard;
    private Control? _inlineEditorValueInput;
    private bool _isInlineEditorOpen;
    private bool _isInlineEditorLayoutQueued;
    private CanvasViewportAdapter? _viewportAdapter;

    public SimulationWorkspaceView()
    {
        InitializeComponent();
        InitializeSimulationConsole();
        AttachedToVisualTree += (_, _) => AttachDataContext(DataContext as SimulationWorkspaceViewModel);
        DetachedFromVisualTree += (_, _) => AttachDataContext(null);
        DataContextChanged += (_, _) => AttachDataContext(DataContext as SimulationWorkspaceViewModel);
    }

    private void AttachDataContext(SimulationWorkspaceViewModel? viewModel)
    {
        if (ReferenceEquals(_attachedViewModel, viewModel))
        {
            return;
        }

        if (_attachedViewModel is not null)
        {
            var previousViewModel = _attachedViewModel;
            if (_viewportAdapter is not null)
            {
                _viewportAdapter.Detach(handler => previousViewModel.FitCanvasRequested -= handler);
            }

            _attachedViewModel.PropertyChanged -= OnWorkspacePresentationChanged;
            _attachedViewModel.PickOpenDiffCsvPathsAsync = null;
            _attachedViewModel.RequestSetClipboardTextAsync = null;
            _attachedViewModel.RequestSaveTextFileAsync = null;
            _attachedViewModel.RefreshExternalBindings();
        }

        _attachedViewModel = viewModel;
        if (_attachedViewModel is null)
        {
            return;
        }

        var viewportAdapter = FindViewportAdapter();
        CanvasViewportAdapter.ApplyClipPolicy(FindCanvas(), FindInlineEditorOverlay());
        _attachedViewModel.PropertyChanged += OnWorkspacePresentationChanged;
        _attachedViewModel.PickOpenDiffCsvPathsAsync = PickOpenDiffCsvPathsAsync;
        _attachedViewModel.RequestSetClipboardTextAsync = SetClipboardTextAsync;
        _attachedViewModel.RequestSaveTextFileAsync = SaveTextFileAsync;
        viewportAdapter.Attach(handler => _attachedViewModel.FitCanvasRequested += handler);
        _attachedViewModel.RefreshExternalBindings();
        UpdateInlineEditorVisibility();
        QueueInlineEditorLayoutUpdate();
    }

    private PadCanvas FindCanvas()
    {
        return _canvas ??= this.FindControl<PadCanvas>("SimulationPadCanvas")
               ?? throw new InvalidOperationException("Simulation pad canvas not found.");
    }

    private CanvasViewportAdapter FindViewportAdapter()
    {
        return _viewportAdapter ??= new CanvasViewportAdapter(FindCanvas(), OnViewportChanged);
    }

    private void SimulationPadCanvas_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is not SimulationWorkspaceViewModel viewModel)
        {
            return;
        }

        HideRiskLocation();
        var canvas = FindCanvas();
        if (viewModel.IsCopperSource)
        {
            viewModel.MoveCopperToWorldPoint(canvas.ScreenToWorld(e.GetPosition(canvas)));
            CloseHoverToolTip(canvas);
            return;
        }

        var regularPadId = canvas.TryGetRegularPadIndexAt(e.GetPosition(canvas));
        if (!regularPadId.HasValue)
        {
            CloseHoverToolTip(canvas);
            return;
        }

        if (_hoveredRegularPadId == regularPadId.Value)
        {
            return;
        }

        var hoverText = viewModel.BuildHoverTipText(regularPadId.Value);
        if (string.IsNullOrWhiteSpace(hoverText))
        {
            CloseHoverToolTip(canvas);
            return;
        }

        _hoveredRegularPadId = regularPadId.Value;
        ToolTip.SetTip(canvas, hoverText);
        ToolTip.SetIsOpen(canvas, true);
    }

    private void SimulationPadCanvas_PointerExited(object? sender, PointerEventArgs e)
    {
        CloseHoverToolTip(FindCanvas());
    }

    private void SimulationPadCanvas_RegularPadActivated(object? sender, PadCanvas.RegularPadContextRequestedEventArgs e)
    {
        if (DataContext is not SimulationWorkspaceViewModel viewModel)
        {
            return;
        }

        viewModel.SelectRegularPad(e.Pad.RegularPadId);
        if (!viewModel.IsManualSource)
        {
            CloseInlineEditor();
            return;
        }

        OpenInlineEditor();
        _uiEvents?.Run("Simulation.ActivateRegularPad", _ => FocusInlineEditorAsync(), CancellationToken.None);
    }

    private void SimulationPadCanvas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        HideRiskLocation();
        var canvas = FindCanvas();
        canvas.Focus();
        if (DataContext is SimulationWorkspaceViewModel viewModel &&
            viewModel.IsCopperSource &&
            e.GetCurrentPoint(canvas).Properties.IsLeftButtonPressed)
        {
            viewModel.MoveCopperToWorldPoint(canvas.ScreenToWorld(e.GetPosition(canvas)));
            CloseInlineEditor();
            CloseHoverToolTip(canvas);
            return;
        }

        if (!_isInlineEditorOpen || e.ClickCount >= 2)
        {
            return;
        }

        CloseInlineEditor();
    }

    private void SimulationPadCanvas_KeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not SimulationWorkspaceViewModel viewModel)
        {
            return;
        }

        if (SimulationCanvasKeyMap.TryApply(e.Key, viewModel))
        {
            e.Handled = true;
        }
    }

    private void SimulationPadCanvas_SelectionChanged(object? sender, PadCanvasSelectionChangedEventArgs e)
    {
        if (DataContext is not SimulationWorkspaceViewModel viewModel)
        {
            return;
        }

        HideRiskLocation();
        SimulationSelectionCount.Text = SimulationWorkspaceViewModel.FormatSelectionCount(e.SelectedRegularPadIndices.Length);
        var regularId = e.SelectedRegularPadIndices.Length > 0 ? e.SelectedRegularPadIndices[0] : -1;
        viewModel.SelectRegularPad(regularId);
        if (regularId < 0)
        {
            CloseInlineEditor();
            return;
        }

        if (_isInlineEditorOpen)
        {
            CloseInlineEditor();
        }
    }

    private void CloseHoverToolTip(PadCanvas canvas)
    {
        _hoveredRegularPadId = -1;
        ToolTip.SetIsOpen(canvas, false);
    }

    private void OpenInlineEditor()
    {
        _isInlineEditorOpen = true;
        UpdateInlineEditorVisibility();
        QueueInlineEditorLayoutUpdate();
    }

    private void CloseInlineEditor()
    {
        _isInlineEditorOpen = false;
        UpdateInlineEditorVisibility();
    }

    private void UpdateInlineEditorVisibility()
    {
        var overlay = FindInlineEditorOverlay();
        var card = FindInlineEditorCard();
        var canEdit = DataContext is SimulationWorkspaceViewModel viewModel &&
                      viewModel.ShowSelectedOverrideEditor && viewModel.HasSelectedRegular;
        overlay.IsVisible = canEdit;
        card.IsVisible = canEdit && _isInlineEditorOpen;
        SimulationInlineEditAction.IsVisible = canEdit && !_isInlineEditorOpen;
    }

    private void QueueInlineEditorLayoutUpdate()
    {
        if (_isInlineEditorLayoutQueued)
        {
            return;
        }

        _isInlineEditorLayoutQueued = true;
        Dispatcher.Post(() =>
        {
            _isInlineEditorLayoutQueued = false;
            UpdateInlineEditorLayout();
        }, DispatcherPriority.Background);
    }

    private void UpdateInlineEditorLayout()
    {
        var overlay = FindInlineEditorOverlay();
        var card = FindInlineEditorCard();
        var viewportAdapter = FindViewportAdapter();
        var canvas = FindCanvas();
        if (DataContext is not SimulationWorkspaceViewModel viewModel ||
            !viewModel.ShowSelectedOverrideEditor ||
            !viewModel.HasSelectedRegular)
        {
            overlay.IsVisible = false;
            card.IsVisible = false;
            return;
        }

        var regularPad = viewModel.RegularPads.FirstOrDefault(pad => pad.RegularPadId == viewModel.SelectedRegularPadId);
        if (regularPad is null)
        {
            overlay.IsVisible = false;
            card.IsVisible = false;
            return;
        }

        overlay.IsVisible = true;
        card.IsVisible = _isInlineEditorOpen;
        SimulationInlineEditAction.IsVisible = !_isInlineEditorOpen;
        Control editor = _isInlineEditorOpen ? card : SimulationInlineEditAction;
        var panelSize = MeasureOverlayCard(editor, overlay.Bounds.Size);
        var canvasOrigin = canvas.TranslatePoint(new Point(0, 0), overlay) ?? new Point(0, 0);
        var canvasRect = new Rect(canvasOrigin, canvas.Bounds.Size);
        var selectionRect = BuildPadScreenRect(viewportAdapter, overlay, regularPad.Bounds);
        var anchor = selectionRect.Center;
        var gap = UiResourceResolver.GetDouble(this, "PadInfoGap", 20.0);
        var position = InlineEditorPlacement.Compute(canvasRect, anchor, panelSize, gap);

        Canvas.SetLeft(editor, position.X);
        Canvas.SetTop(editor, position.Y);
    }

    private static Size MeasureOverlayCard(Control card, Size available)
    {
        card.Measure(available);
        var desired = card.DesiredSize;
        return new Size(Math.Max(10, desired.Width), Math.Max(10, desired.Height));
    }

    private static Rect BuildPadScreenRect(CanvasViewportAdapter viewportAdapter, Canvas overlay, Rect2 worldBounds)
    {
        var viewFrame = viewportAdapter.GetViewFrameSnapshot();
        return viewportAdapter.ToOverlayRect(overlay, worldBounds, viewFrame);
    }

    private async Task FocusInlineEditorAsync()
    {
        await Dispatcher.InvokeAsync(
            () =>
            {
                QueueInlineEditorLayoutUpdate();
                FindInlineEditorValueInput().Focus();
            },
            DispatcherPriority.Loaded);
    }

    private Canvas FindInlineEditorOverlay()
    {
        return _inlineEditorOverlay ??= this.FindControl<Canvas>("SimulationInlineEditorOverlay")
               ?? throw new InvalidOperationException("Simulation inline editor overlay not found.");
    }

    private Border FindInlineEditorCard()
    {
        return _inlineEditorCard ??= this.FindControl<Border>("SimulationInlineEditorCard")
               ?? throw new InvalidOperationException("Simulation inline editor card not found.");
    }

    private Control FindInlineEditorValueInput()
    {
        return _inlineEditorValueInput ??= this.FindControl<Control>("SimulationInlineEditorValueInput")
               ?? throw new InvalidOperationException("Simulation inline editor value input not found.");
    }

    private void OnViewportChanged()
    {
        QueueInlineEditorLayoutUpdate();
        PositionRiskLocation();
    }

}
