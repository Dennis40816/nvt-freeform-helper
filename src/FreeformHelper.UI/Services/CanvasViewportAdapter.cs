using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.UI.Controls;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Shared viewport adapter for workspace pages that host a <see cref="PadCanvas"/>.
/// It centralizes fit lifecycle, view-changed wiring, world-to-overlay transforms, and clipping policy.
/// </summary>
public sealed class CanvasViewportAdapter
{
    private readonly PadCanvas _canvas;
    private readonly Action _onViewportChanged;
    private readonly EventHandler _fitRequestedHandler;
    private readonly EventHandler _viewChangedHandler;
    private bool _isAttached;

    public CanvasViewportAdapter(PadCanvas canvas, Action onViewportChanged)
    {
        _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        _onViewportChanged = onViewportChanged ?? throw new ArgumentNullException(nameof(onViewportChanged));
        _fitRequestedHandler = (_, _) => FitToContent();
        _viewChangedHandler = (_, _) => _onViewportChanged();
    }

    public void Attach(Action<EventHandler> subscribeFitRequested)
    {
        ArgumentNullException.ThrowIfNull(subscribeFitRequested);
        if (_isAttached)
        {
            return;
        }

        _canvas.ViewChanged += _viewChangedHandler;
        subscribeFitRequested(_fitRequestedHandler);
        QueueInitialFit();
        _isAttached = true;
    }

    public void Detach(Action<EventHandler> unsubscribeFitRequested)
    {
        ArgumentNullException.ThrowIfNull(unsubscribeFitRequested);
        if (!_isAttached)
        {
            return;
        }

        _canvas.ViewChanged -= _viewChangedHandler;
        unsubscribeFitRequested(_fitRequestedHandler);
        _isAttached = false;
    }

    public void SetFitBoundsOverride(Rect2? fitBounds)
    {
        _canvas.FitBoundsOverride = fitBounds;
    }

    public PadCanvasViewFrameSnapshot GetViewFrameSnapshot()
    {
        return _canvas.GetViewFrameSnapshot();
    }

    public Point ToOverlayPoint(Canvas overlay, Point2 worldPoint, in PadCanvasViewFrameSnapshot viewFrame)
    {
        ArgumentNullException.ThrowIfNull(overlay);
        var canvasPoint = viewFrame.WorldToScreen.Transform(new Point(worldPoint.X, worldPoint.Y));
        return _canvas.TranslatePoint(canvasPoint, overlay) ?? canvasPoint;
    }

    public Rect ToOverlayRect(Canvas overlay, Rect2 worldBounds, in PadCanvasViewFrameSnapshot viewFrame)
    {
        ArgumentNullException.ThrowIfNull(overlay);
        var topLeft = ToOverlayPoint(overlay, new Point2(worldBounds.MinX, worldBounds.MaxY), viewFrame);
        var bottomRight = ToOverlayPoint(overlay, new Point2(worldBounds.MaxX, worldBounds.MinY), viewFrame);
        return new Rect(topLeft, bottomRight).Normalize();
    }

    public static void ApplyClipPolicy(params Control[] hosts)
    {
        if (hosts is null || hosts.Length == 0)
        {
            return;
        }

        foreach (var host in hosts)
        {
            if (host is null)
            {
                continue;
            }

            host.ClipToBounds = true;
        }
    }

    private void QueueInitialFit()
    {
        Dispatcher.UIThread.Post(FitToContent, DispatcherPriority.Loaded);
    }

    private void FitToContent()
    {
        _canvas.FitToContent();
    }
}
