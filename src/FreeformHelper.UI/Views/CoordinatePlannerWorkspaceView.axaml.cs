using System.ComponentModel;

using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FreeformHelper.Application.Services;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views;

public sealed partial class CoordinatePlannerWorkspaceView : UserControl
{
    private static readonly string[] _allFilesPatterns = ["*"];
    private PadCanvas? _canvas;
    private Canvas? _overlayCanvas;
    private Canvas? _overlayLabelCanvas;
    private Grid? _previewHost;
    private CanvasViewportAdapter? _viewportAdapter;
    private CoordinatePlannerWorkspaceViewModel? _attachedViewModel;
    private string? _draggedArrayCornerKey;
    private bool _statusToastPending;

    public CoordinatePlannerWorkspaceView()
    {
        InitializeComponent();
        PointerMoved += OnArrayCornerPointerMoved;
        PointerReleased += OnArrayCornerPointerReleased;
        AttachedToVisualTree += (_, _) => AttachDataContext(DataContext as CoordinatePlannerWorkspaceViewModel);
        DetachedFromVisualTree += (_, _) => AttachDataContext(null);
        DataContextChanged += (_, _) => AttachDataContext(DataContext as CoordinatePlannerWorkspaceViewModel);
    }

    private void AttachDataContext(CoordinatePlannerWorkspaceViewModel? viewModel)
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

            _attachedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _attachedViewModel.RequestArtifactDetailAsync = null;
            _attachedViewModel.RequestExportPreviewPngAsync = null;
            _attachedViewModel.RequestSetClipboardTextAsync = null;
            _attachedViewModel.RequestSaveTextFileAsync = null;
        }

        _attachedViewModel = viewModel;
        if (_attachedViewModel is null)
        {
            if (_viewportAdapter is not null)
            {
                _viewportAdapter.SetFitBoundsOverride(null);
            }

            ClearOverlay();
            return;
        }

        _attachedViewModel.PropertyChanged += OnViewModelPropertyChanged;
        _attachedViewModel.RequestArtifactDetailAsync = ShowArtifactDetailAsync;
        _attachedViewModel.RequestExportPreviewPngAsync = ExportPreviewImageAsync;
        _attachedViewModel.RequestSetClipboardTextAsync = SetClipboardTextAsync;
        _attachedViewModel.RequestSaveTextFileAsync = SaveTextFileAsync;
        var viewportAdapter = FindViewportAdapter();
        CanvasViewportAdapter.ApplyClipPolicy(
            FindPreviewHost(),
            FindOverlayCanvas(),
            FindOverlayLabelCanvas());
        viewportAdapter.SetFitBoundsOverride(_attachedViewModel.PreviewBounds);
        viewportAdapter.Attach(handler => _attachedViewModel.FitCanvasRequested += handler);
        Dispatcher.Post(
            RenderOverlay,
            DispatcherPriority.Loaded);
        QueueStatusToast();
    }

    private PadCanvas FindCanvas()
    {
        return _canvas ??= this.FindControl<PadCanvas>("CoordinatePadCanvas")
               ?? throw new InvalidOperationException("Coordinate planner pad canvas not found.");
    }

    private Canvas FindOverlayCanvas()
    {
        return _overlayCanvas ??= this.FindControl<Canvas>("CoordinateOverlayCanvas")
               ?? throw new InvalidOperationException("Coordinate planner overlay canvas not found.");
    }

    private Canvas FindOverlayLabelCanvas()
    {
        return _overlayLabelCanvas ??= this.FindControl<Canvas>("CoordinateOverlayLabelCanvas")
               ?? throw new InvalidOperationException("Coordinate planner overlay label canvas not found.");
    }

    private Grid FindPreviewHost()
    {
        return _previewHost ??= this.FindControl<Grid>("CoordinatePreviewHost")
               ?? throw new InvalidOperationException("Coordinate preview host not found.");
    }

    private CanvasViewportAdapter FindViewportAdapter()
    {
        return _viewportAdapter ??= new CanvasViewportAdapter(FindCanvas(), RenderOverlay);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CoordinatePlannerWorkspaceViewModel.StatusText))
        {
            QueueStatusToast();
        }

        if (e.PropertyName is nameof(CoordinatePlannerWorkspaceViewModel.Snapshot) or
            nameof(CoordinatePlannerWorkspaceViewModel.CadPadsForCanvas) or
            nameof(CoordinatePlannerWorkspaceViewModel.SelectedArtifactRow) or
            nameof(CoordinatePlannerWorkspaceViewModel.SelectedOverlayModeOption))
        {
            if (e.PropertyName == nameof(CoordinatePlannerWorkspaceViewModel.Snapshot) &&
                _attachedViewModel is not null &&
                _viewportAdapter is not null)
            {
                _viewportAdapter.SetFitBoundsOverride(_attachedViewModel.PreviewBounds);
            }

            RenderOverlay();
        }
    }

    private void QueueStatusToast()
    {
        if (_statusToastPending)
        {
            return;
        }

        _statusToastPending = true;
        Dispatcher.Post(() =>
        {
            _statusToastPending = false;
            ShowStatusToast();
        });
    }

    private void ShowStatusToast()
    {
        if (_attachedViewModel is not null && TopLevel.GetTopLevel(this) is MainWindow owner)
        {
            owner.ShowTopToast(_attachedViewModel.StatusText);
        }
    }

    private async Task ShowArtifactDetailAsync(CoordinateArtifactRow row)
    {
        var dialog = new CoordinateArtifactDetailWindow
        {
            DataContext = row,
        };

        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            await dialog.ShowDialog(owner);
            return;
        }

        dialog.Show();
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

    private async Task<bool> SaveTextFileAsync(CoordinatePlannerWorkspaceViewModel.CoordinatePlannerTextExportRequest request)
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
                new("All files") { Patterns = _allFilesPatterns },
            },
        });
        if (file is null)
        {
            return false;
        }

        await TextExportWriter.WriteAsync(file.Path.LocalPath, request.Content, includeBom: true, CancellationToken.None);
        return true;
    }

    private void RenderOverlay()
    {
        var overlayShapes = FindOverlayCanvas();
        var overlayLabels = FindOverlayLabelCanvas();
        overlayShapes.Children.Clear();
        overlayLabels.Children.Clear();
        if (DataContext is not CoordinatePlannerWorkspaceViewModel viewModel)
        {
            return;
        }

        var viewportAdapter = FindViewportAdapter();
        var canvas = FindCanvas();
        if (canvas.Bounds.Width <= 1 || canvas.Bounds.Height <= 1)
        {
            return;
        }

        var viewFrame = viewportAdapter.GetViewFrameSnapshot();
        var renderer = new CoordinatePlannerOverlayRenderer(
            overlayShapes,
            overlayLabels,
            viewportAdapter,
            viewFrame,
            viewModel.Snapshot,
            CoordinatePlannerOverlayStyle.Resolve(this),
            viewModel.SelectedOverlayModeOption.Mode,
            viewModel.SelectedArtifactRow?.Key,
            ConfigureArrayCornerHandle);
        renderer.Draw();
    }

    private void ConfigureArrayCornerHandle(
        Ellipse handle,
        string key,
        string label)
    {
        handle.Cursor = new Cursor(StandardCursorType.Hand);
        ToolTip.SetTip(handle, label);
        handle.PointerPressed += (_, e) => OnArrayCornerHandlePointerPressed(key, e);
    }

    private void OnArrayCornerHandlePointerPressed(
        string key,
        PointerPressedEventArgs e)
    {
        if (DataContext is not CoordinatePlannerWorkspaceViewModel viewModel ||
            !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed ||
            !viewModel.BeginCustomArrayCornerEdit(key))
        {
            return;
        }

        _draggedArrayCornerKey = key;
        e.Pointer.Capture(this);
        MoveDraggedArrayCorner(e);
        e.Handled = true;
    }

    private void OnArrayCornerPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggedArrayCornerKey is null)
        {
            return;
        }

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            CompleteDraggedArrayCorner(e);
            return;
        }

        MoveDraggedArrayCorner(e);
        e.Handled = true;
    }

    private void OnArrayCornerPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_draggedArrayCornerKey is null)
        {
            return;
        }

        MoveDraggedArrayCorner(e);
        CompleteDraggedArrayCorner(e);
        e.Handled = true;
    }

    private void MoveDraggedArrayCorner(PointerEventArgs e)
    {
        if (_draggedArrayCornerKey is null ||
            DataContext is not CoordinatePlannerWorkspaceViewModel viewModel)
        {
            return;
        }

        var canvas = FindCanvas();
        var worldPoint = canvas.ScreenToWorld(e.GetPosition(canvas));
        viewModel.MoveCustomArrayCornerToWorldPoint(_draggedArrayCornerKey, worldPoint);
    }

    private void CompleteDraggedArrayCorner(PointerEventArgs e)
    {
        if (_draggedArrayCornerKey is null)
        {
            return;
        }

        if (DataContext is CoordinatePlannerWorkspaceViewModel viewModel)
        {
            viewModel.CompleteCustomArrayCornerEdit(_draggedArrayCornerKey);
        }

        _draggedArrayCornerKey = null;
        e.Pointer.Capture(null);
    }

    private void ClearOverlay()
    {
        if (_overlayCanvas is not null)
        {
            _overlayCanvas.Children.Clear();
        }

        if (_overlayLabelCanvas is not null)
        {
            _overlayLabelCanvas.Children.Clear();
        }
    }
}
