using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views;

public sealed partial class SimulationWorkspaceView : UserControl
{
    private static readonly string[] CsvFilePatterns = ["*.csv"];
    private static readonly string[] AllFilesPatterns = ["*"];
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
        _attachedViewModel.PickOpenDiffCsvPathsAsync = PickOpenDiffCsvPathsAsync;
        _attachedViewModel.RequestSetClipboardTextAsync = SetClipboardTextAsync;
        _attachedViewModel.RequestSaveTextFileAsync = SaveTextFileAsync;
        viewportAdapter.Attach(handler => _attachedViewModel.FitCanvasRequested += handler);
        _attachedViewModel.RefreshExternalBindings();
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

    private async void SimulationPadCanvas_RegularPadActivated(object? sender, PadCanvas.RegularPadContextRequestedEventArgs e)
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
        await FocusInlineEditorAsync();
    }

    private void SimulationPadCanvas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
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

        switch (e.Key)
        {
            case Key.B:
                viewModel.SetCanvasViewMode(NotchApplySimulationCanvasViewMode.Before);
                e.Handled = true;
                break;
            case Key.A:
                viewModel.SetCanvasViewMode(NotchApplySimulationCanvasViewMode.After);
                e.Handled = true;
                break;
            case Key.D:
                viewModel.SetCanvasViewMode(NotchApplySimulationCanvasViewMode.Delta);
                e.Handled = true;
                break;
            case Key.C:
                viewModel.SetCanvasViewMode(NotchApplySimulationCanvasViewMode.ChangedOnly);
                e.Handled = true;
                break;
            case Key.V:
                viewModel.ToggleBeforeAfterCanvasView();
                e.Handled = true;
                break;
        }
    }

    private async Task<IReadOnlyList<string>> PickOpenDiffCsvPathsAsync()
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null)
        {
            return Array.Empty<string>();
        }

        var result = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = true,
            Title = "Import Diff CSV",
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("CSV") { Patterns = CsvFilePatterns },
                new("All files") { Patterns = AllFilesPatterns },
            },
        });

        return result is { Count: > 0 }
            ? result.Select(static file => file.Path.LocalPath).ToArray()
            : Array.Empty<string>();
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

    private async Task<bool> SaveTextFileAsync(SimulationWorkspaceViewModel.SimulationTextExportRequest request)
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null)
        {
            return false;
        }

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = request.Title,
            DefaultExtension = request.DefaultExtension,
            SuggestedFileName = request.SuggestedFileName,
            FileTypeChoices = new List<FilePickerFileType>
            {
                new(request.FileTypeName) { Patterns = request.Patterns },
                new("All files") { Patterns = AllFilesPatterns },
            },
        });
        if (file is null)
        {
            return false;
        }

        await File.WriteAllTextAsync(file.Path.LocalPath, request.Content, Encoding.UTF8);
        return true;
    }

    private void SimulationPadCanvas_SelectionChanged(object? sender, PadCanvasSelectionChangedEventArgs e)
    {
        if (DataContext is not SimulationWorkspaceViewModel viewModel)
        {
            return;
        }

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
        var isVisible = _isInlineEditorOpen &&
                        DataContext is SimulationWorkspaceViewModel viewModel &&
                        viewModel.ShowSelectedOverrideEditor &&
                        viewModel.HasSelectedRegular;
        overlay.IsVisible = isVisible;
        card.IsVisible = isVisible;
    }

    private void QueueInlineEditorLayoutUpdate()
    {
        if (_isInlineEditorLayoutQueued)
        {
            return;
        }

        _isInlineEditorLayoutQueued = true;
        Dispatcher.UIThread.Post(() =>
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
        if (!_isInlineEditorOpen ||
            DataContext is not SimulationWorkspaceViewModel viewModel ||
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
        card.IsVisible = true;

        var panelSize = MeasureOverlayCard(card, overlay.Bounds.Size);
        var canvasOrigin = canvas.TranslatePoint(new Point(0, 0), overlay) ?? new Point(0, 0);
        var canvasRect = new Rect(canvasOrigin, canvas.Bounds.Size);
        var selectionRect = BuildPadScreenRect(viewportAdapter, overlay, regularPad.Bounds);
        var anchor = selectionRect.Center;
        var gap = UiResourceResolver.GetDouble(this, "PadInfoGap", 20.0);
        var minX = canvasRect.Left + gap;
        var maxX = Math.Max(minX, canvasRect.Right - panelSize.Width - gap);
        var x = Math.Min(anchor.X + gap, maxX);
        if (x + panelSize.Width > canvasRect.Right - gap)
        {
            x = Math.Max(minX, anchor.X - gap - panelSize.Width);
        }

        var y = Math.Clamp(
            anchor.Y - panelSize.Height * 0.5,
            canvasRect.Top + gap,
            Math.Max(canvasRect.Top + gap, canvasRect.Bottom - panelSize.Height - gap));

        Canvas.SetLeft(card, x);
        Canvas.SetTop(card, y);
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
        await Dispatcher.UIThread.InvokeAsync(
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
        if (_isInlineEditorOpen)
        {
            QueueInlineEditorLayoutUpdate();
        }
    }

}
