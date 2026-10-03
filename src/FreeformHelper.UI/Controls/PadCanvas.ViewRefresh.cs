using Avalonia;
using Avalonia.Threading;
using FreeformHelper.Domain.Geometry;

namespace FreeformHelper.UI.Controls;

public sealed partial class PadCanvas
{
    private long _viewFrameRevision;
    private bool _isViewRefreshQueued;
    private bool _queuedViewRefreshNeedsViewChanged;
    private DispatcherTimer? _transientLowDetailTimer;
    private long _transientLowDetailUntilTicks;
    private bool _isViewFrameSnapshotDirty = true;
    private PadCanvasViewFrameSnapshot _currentViewFrameSnapshot;

    public PadCanvasViewFrameSnapshot GetViewFrameSnapshot()
    {
        return ResolveViewFrameSnapshot();
    }

    internal static bool ComputeLowDetailMode(
        bool isMiddlePanning,
        bool isSpacePanning,
        bool isBoxSelecting,
        double zoom,
        double lowDetailZoomThreshold,
        long transientLowDetailUntilTicks,
        long nowTicks)
    {
        return isMiddlePanning ||
               isSpacePanning ||
               isBoxSelecting ||
               zoom <= lowDetailZoomThreshold ||
               nowTicks < transientLowDetailUntilTicks;
    }

    internal static bool ShouldDeferSecondaryVisuals(
        bool isMiddlePanning,
        bool isSpacePanning,
        bool isBoxSelecting,
        long transientLowDetailUntilTicks,
        long nowTicks)
    {
        return isMiddlePanning ||
               isSpacePanning ||
               isBoxSelecting ||
               nowTicks < transientLowDetailUntilTicks;
    }

    private bool ShouldDeferSecondaryVisuals()
    {
        return ShouldDeferSecondaryVisuals(
            _isMiddlePanning,
            _isSpacePanning,
            _isBoxSelecting,
            _transientLowDetailUntilTicks,
            DateTime.UtcNow.Ticks);
    }

    private void RequestViewRefresh(bool raiseViewChanged = true)
    {
        RequestVisualRefresh(raiseViewChanged);
    }

    private void RequestVisualRefresh(bool raiseViewChanged = false)
    {
        _queuedViewRefreshNeedsViewChanged |= raiseViewChanged;
        if (_isViewRefreshQueued)
        {
            return;
        }

        _isViewRefreshQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _isViewRefreshQueued = false;
            var shouldRaiseViewChanged = _queuedViewRefreshNeedsViewChanged;
            _queuedViewRefreshNeedsViewChanged = false;
            InvalidateVisual();
            if (shouldRaiseViewChanged)
            {
                ResolveViewFrameSnapshot();
                RaiseViewChanged();
            }
        }, DispatcherPriority.Render);
    }

    private void BeginTransientLowDetailNavigation()
    {
        var durationMs = Math.Max(0.0, GetResourceDouble("CanvasTransientLowDetailDurationMs", 120.0));
        if (durationMs <= 0)
        {
            return;
        }

        _transientLowDetailUntilTicks = DateTime.UtcNow.AddMilliseconds(durationMs).Ticks;
        _transientLowDetailTimer ??= CreateTransientLowDetailTimer();

        _transientLowDetailTimer.Stop();
        _transientLowDetailTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(16.0, durationMs));
        _transientLowDetailTimer.Start();
    }

    private DispatcherTimer CreateTransientLowDetailTimer()
    {
        var timer = new DispatcherTimer(DispatcherPriority.Render);
        timer.Tick += (_, _) =>
        {
            var remainingTicks = _transientLowDetailUntilTicks - DateTime.UtcNow.Ticks;
            if (remainingTicks > 0)
            {
                timer.Interval = TimeSpan.FromTicks(Math.Max(remainingTicks, TimeSpan.FromMilliseconds(16).Ticks));
                return;
            }

            timer.Stop();
            RequestViewRefresh(raiseViewChanged: false);
        };

        return timer;
    }

    private PadCanvasViewFrameSnapshot ResolveViewFrameSnapshot()
    {
        var boundsSize = Bounds.Size;
        if (!_isViewFrameSnapshotDirty &&
            _currentViewFrameSnapshot.BoundsSize == boundsSize &&
            _currentViewFrameSnapshot.Zoom.Equals(_zoom) &&
            _currentViewFrameSnapshot.Pan.Equals(_pan))
        {
            return _currentViewFrameSnapshot;
        }

        var worldViewport = CreateWorldViewport(_zoom, _pan, boundsSize);
        _currentViewFrameSnapshot = new PadCanvasViewFrameSnapshot(
            Revision: Interlocked.Increment(ref _viewFrameRevision),
            Zoom: _zoom,
            Pan: _pan,
            BoundsSize: boundsSize,
            WorldViewport: worldViewport,
            WorldToScreen: new Matrix(_zoom, 0, 0, -_zoom, _pan.X, _pan.Y));
        _isViewFrameSnapshotDirty = false;
        return _currentViewFrameSnapshot;
    }

    private void InvalidateViewFrameSnapshot()
    {
        _isViewFrameSnapshotDirty = true;
    }

    private static Rect2 CreateWorldViewport(double zoom, Vector pan, Size boundsSize)
    {
        var safeZoom = Math.Max(zoom, 1e-6);
        var topLeft = ScreenToWorldSnapshot(new Point(0, 0), safeZoom, pan);
        var bottomRight = ScreenToWorldSnapshot(new Point(boundsSize.Width, boundsSize.Height), safeZoom, pan);
        return new Rect2(
            Math.Min(topLeft.X, bottomRight.X),
            Math.Min(topLeft.Y, bottomRight.Y),
            Math.Max(topLeft.X, bottomRight.X),
            Math.Max(topLeft.Y, bottomRight.Y));
    }

    private static Point2 ScreenToWorldSnapshot(Point screenPoint, double zoom, Vector pan)
    {
        return new Point2((screenPoint.X - pan.X) / zoom, -(screenPoint.Y - pan.Y) / zoom);
    }
}

public readonly record struct PadCanvasViewFrameSnapshot(
    long Revision,
    double Zoom,
    Vector Pan,
    Size BoundsSize,
    Rect2 WorldViewport,
    Matrix WorldToScreen);
