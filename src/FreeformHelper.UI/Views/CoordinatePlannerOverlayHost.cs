using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views;

/// <summary>Hosts the coordinate planner overlay and cached viewport controls.</summary>
internal sealed class CoordinatePlannerOverlayHost(
    Control owner,
    Func<CoordinatePlannerWorkspaceViewModel?> getViewModel,
    Action<Ellipse, string, string> configureHandle)
{
    private PadCanvas? _canvas;
    private Canvas? _overlayCanvas;
    private Canvas? _overlayLabelCanvas;
    private Grid? _previewHost;
    private CanvasViewportAdapter? _viewportAdapter;

    public CanvasViewportAdapter? ViewportAdapterIfCreated => _viewportAdapter;

    public PadCanvas FindCanvas()
    {
        return _canvas ??= owner.FindControl<PadCanvas>("CoordinatePadCanvas")
               ?? throw new InvalidOperationException("Coordinate planner pad canvas not found.");
    }

    public Canvas FindOverlayCanvas()
    {
        return _overlayCanvas ??= owner.FindControl<Canvas>("CoordinateOverlayCanvas")
               ?? throw new InvalidOperationException("Coordinate planner overlay canvas not found.");
    }

    public Canvas FindOverlayLabelCanvas()
    {
        return _overlayLabelCanvas ??= owner.FindControl<Canvas>("CoordinateOverlayLabelCanvas")
               ?? throw new InvalidOperationException("Coordinate planner overlay label canvas not found.");
    }

    public Grid FindPreviewHost()
    {
        return _previewHost ??= owner.FindControl<Grid>("CoordinatePreviewHost")
               ?? throw new InvalidOperationException("Coordinate preview host not found.");
    }

    public CanvasViewportAdapter FindViewportAdapter()
    {
        return _viewportAdapter ??= new CanvasViewportAdapter(FindCanvas(), Render);
    }

    public void Render()
    {
        var overlayShapes = FindOverlayCanvas();
        var overlayLabels = FindOverlayLabelCanvas();
        overlayShapes.Children.Clear();
        overlayLabels.Children.Clear();
        if (getViewModel() is not { } viewModel)
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
            CoordinatePlannerOverlayStyle.Resolve(owner),
            viewModel.SelectedOverlayModeOption.Mode,
            viewModel.SelectedArtifactRow?.Key,
            configureHandle);
        renderer.Draw();
    }

    public void Clear()
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
