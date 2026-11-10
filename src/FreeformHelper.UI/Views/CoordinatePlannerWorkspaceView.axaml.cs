using System.ComponentModel;

using Avalonia.Controls;
using Avalonia.Threading;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views;

public sealed partial class CoordinatePlannerWorkspaceView : UserControl
{
    private readonly CoordinatePlannerArrayCornerDragController _arrayCornerDrag;
    private readonly CoordinatePlannerOverlayHost _overlayHost;
    private readonly CoordinatePlannerHostRequests _hostRequests;
    private CoordinatePlannerWorkspaceViewModel? _attachedViewModel;
    private bool _statusToastPending;

    public CoordinatePlannerWorkspaceView()
    {
        _arrayCornerDrag = new CoordinatePlannerArrayCornerDragController(
            this, () => DataContext as CoordinatePlannerWorkspaceViewModel, () => GetCanvas(this));
        _overlayHost = new CoordinatePlannerOverlayHost(
            this, () => DataContext as CoordinatePlannerWorkspaceViewModel, _arrayCornerDrag.ConfigureHandle);
        _hostRequests = new CoordinatePlannerHostRequests(() => TopLevel.GetTopLevel(this));
        InitializeComponent();
        PointerMoved += _arrayCornerDrag.OnPointerMoved;
        PointerReleased += _arrayCornerDrag.OnPointerReleased;
        AttachedToVisualTree += (_, _) => AttachDataContext(DataContext as CoordinatePlannerWorkspaceViewModel);
        DetachedFromVisualTree += (_, _) => AttachDataContext(null);
        DataContextChanged += (_, _) => AttachDataContext(DataContext as CoordinatePlannerWorkspaceViewModel);

        static PadCanvas GetCanvas(CoordinatePlannerWorkspaceView owner) => owner._overlayHost.FindCanvas();
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
            if (_overlayHost.ViewportAdapterIfCreated is not null)
            {
                _overlayHost.ViewportAdapterIfCreated.Detach(handler => previousViewModel.FitCanvasRequested -= handler);
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
            if (_overlayHost.ViewportAdapterIfCreated is not null)
            {
                _overlayHost.ViewportAdapterIfCreated.SetFitBoundsOverride(null);
            }

            _overlayHost.Clear();
            return;
        }

        _attachedViewModel.PropertyChanged += OnViewModelPropertyChanged;
        _attachedViewModel.RequestArtifactDetailAsync = _hostRequests.ShowArtifactDetailAsync;
        _attachedViewModel.RequestExportPreviewPngAsync = ExportPreviewImageAsync;
        _attachedViewModel.RequestSetClipboardTextAsync = _hostRequests.SetClipboardTextAsync;
        _attachedViewModel.RequestSaveTextFileAsync = _hostRequests.SaveTextFileAsync;
        var viewportAdapter = _overlayHost.FindViewportAdapter();
        CanvasViewportAdapter.ApplyClipPolicy(
            _overlayHost.FindPreviewHost(),
            _overlayHost.FindOverlayCanvas(),
            _overlayHost.FindOverlayLabelCanvas());
        viewportAdapter.SetFitBoundsOverride(_attachedViewModel.PreviewBounds);
        viewportAdapter.Attach(handler => _attachedViewModel.FitCanvasRequested += handler);
        Dispatcher.Post(
            _overlayHost.Render,
            DispatcherPriority.Loaded);
        QueueStatusToast();
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
                _overlayHost.ViewportAdapterIfCreated is not null)
            {
                _overlayHost.ViewportAdapterIfCreated.SetFitBoundsOverride(_attachedViewModel.PreviewBounds);
            }

            _overlayHost.Render();
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
}
