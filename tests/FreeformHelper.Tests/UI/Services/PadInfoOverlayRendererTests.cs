// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;
using PadInfoOverlayRenderer = FreeformHelper.UI.Services.PadInfoOverlayRenderer;
using Point = Avalonia.Point;
using Rect = Avalonia.Rect;
using ShapePath = Avalonia.Controls.Shapes.Path;
using Size = Avalonia.Size;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class PadInfoOverlayRendererTests
{
    [AvaloniaTheory]
    [InlineData(1)]
    [InlineData(2)]
    public void PanAndZoomPositionPanelAndConnectorInOffsetOverlay(double zoom)
    {
        using var layout = new RendererFixture();
        layout.Renderer.UpdateLayout();

        layout.Zoom = zoom;
        layout.Pan = new Point(200 - (5 * zoom), 160 + (5 * zoom));
        layout.Renderer.UpdateLayout();

        layout.AssertPositions(new Point(260 + (5 * zoom), 130), new Point(240, 190),
            new Point(260 + (5 * zoom), 190));
        Assert.True(layout.Anchor.IsVisible);
        Assert.Equal(237, Canvas.GetLeft(layout.Anchor), 6);
        Assert.Equal(187, Canvas.GetTop(layout.Anchor), 6);
    }

    [AvaloniaFact]
    public void OffScreenSelectionHidesAnchorAndClampsConnectorToCanvas()
    {
        using var layout = new RendererFixture();
        layout.Renderer.UpdateLayout();
        Assert.True(layout.Anchor.IsVisible);

        layout.Zoom = 2;
        layout.Pan = new Point(-110, 170);
        layout.Renderer.UpdateLayout();

        layout.AssertPositions(new Point(60, 130), new Point(40, 190), new Point(60, 190));
        Assert.False(layout.Anchor.IsVisible);
    }

    [AvaloniaFact]
    public async Task QueuedLayoutReadsReplacementBoundsAndLatestViewportAsync()
    {
        using var layout = new RendererFixture();
        layout.Renderer.UpdateLayout();
        var previousGeometry = layout.Link.Data;
        layout.Renderer.QueueLayoutUpdate();
        var replacement = new Rect2(10, 0, 20, 10);

        layout.BoundsWorld = replacement;
        layout.SelectedBounds = [replacement];
        layout.Zoom = 2;
        layout.Pan = new Point(250, 220);
        layout.Renderer.QueueLayoutUpdate();
        Assert.Same(previousGeometry, layout.Link.Data);
        await HeadlessUiPump.FlushQueueAsync();

        layout.AssertPositions(new Point(350, 180), new Point(320, 240), new Point(350, 240));
        Assert.True(layout.Anchor.IsVisible);
        Assert.Equal(replacement, layout.BoundsWorld);
        Assert.Equal(replacement, Assert.Single(layout.SelectedBounds));
    }

    [AvaloniaFact]
    public async Task RepeatedRequestsCoalesceAtBackgroundAndAllowTheNextBatchAsync()
    {
        using var layout = new RendererFixture();
        var renderCount = 0;
        layout.Link.PropertyChanged += (_, args) =>
        {
            if (args.Property == ShapePath.DataProperty)
            {
                renderCount++;
            }
        };

        layout.Renderer.QueueLayoutUpdate();
        layout.Renderer.QueueLayoutUpdate();
        layout.Renderer.QueueLayoutUpdate();
        layout.Owner.Dispatcher.RunJobs(DispatcherPriority.Render);
        Assert.Equal(0, renderCount);
        Assert.Null(layout.Link.Data);
        await HeadlessUiPump.FlushQueueAsync();

        Assert.Equal(1, renderCount);
        layout.AssertPositions(new Point(170, 65), new Point(145, 125), new Point(170, 125));
        layout.Renderer.QueueLayoutUpdate();
        await HeadlessUiPump.FlushQueueAsync();
        Assert.Equal(2, renderCount);
    }

    [AvaloniaFact]
    public void DetachedCanvasAndNonNumericResourcesKeepTranslationAndResourceFallbacks()
    {
        using var layout = new RendererFixture();
        layout.Owner.Children.Remove(layout.Canvas);
        layout.Canvas.Measure(new Size(600, 400));
        layout.Canvas.Arrange(new Rect(0, 0, 600, 400));
        Assert.Null(Avalonia.VisualExtensions.TranslatePoint(layout.Canvas, default, layout.Overlay));
        layout.Owner.Resources["PadInfoAnchorClampMargin"] = "invalid";
        layout.Owner.Resources["PadInfoGap"] = "invalid";
        layout.Owner.Resources["PadInfoMinLineLength"] = "invalid";
        layout.Owner.Resources["PadInfoLineInset"] = "invalid";
        layout.Owner.Resources["PadInfoAnchorPanelMargin"] = "invalid";
        layout.Zoom = 2;
        layout.Pan = new Point(190, 170);

        layout.Renderer.UpdateLayout();

        layout.AssertPositions(new Point(230, 100), new Point(200, 160), new Point(230, 160));
        Assert.True(layout.Anchor.IsVisible);
    }

    [AvaloniaFact]
    public void NumericResourcesAreResolvedFromTheSuppliedOwner()
    {
        using var layout = new RendererFixture();
        layout.Owner.Resources["PadInfoGap"] = 30f;
        layout.Owner.Resources["PadInfoMinLineLength"] = 30;
        layout.Owner.Resources["PadInfoLineInset"] = 12d;
        layout.Zoom = 2;
        layout.Pan = new Point(190, 170);

        layout.Renderer.UpdateLayout();

        layout.AssertPositions(new Point(280, 130), new Point(240, 190), new Point(280, 190));
    }

    [AvaloniaFact]
    public async Task QueuedLayoutWaitsForControlsAndSkipsHiddenOrClosedOverlayAsync()
    {
        using var layout = new RendererFixture();
        layout.HasControls = false;
        layout.Renderer.UpdateLayout();
        Assert.Null(layout.Link.Data);
        layout.Renderer.QueueLayoutUpdate();
        layout.HasControls = true;
        await HeadlessUiPump.FlushQueueAsync();
        var geometry = layout.Link.Data;
        Assert.NotNull(geometry);

        layout.Overlay.IsVisible = false;
        layout.Renderer.QueueLayoutUpdate();
        await HeadlessUiPump.FlushQueueAsync();
        Assert.Same(geometry, layout.Link.Data);
        layout.Overlay.IsVisible = true;
        layout.BoundsWorld = null;
        layout.Renderer.QueueLayoutUpdate();
        await HeadlessUiPump.FlushQueueAsync();
        Assert.Same(geometry, layout.Link.Data);
    }

    private sealed class RendererFixture : IDisposable
    {
        private readonly Window _window;

        public RendererFixture()
        {
            Avalonia.Controls.Canvas.SetLeft(Canvas, 40);
            Avalonia.Controls.Canvas.SetTop(Canvas, 30);
            Owner.Children.Add(Canvas);
            Owner.Children.Add(Overlay);
            Overlay.Children.Add(Popover);
            Overlay.Children.Add(Link);
            Overlay.Children.Add(Anchor);
            _window = new Window { Width = 700, Height = 500, Content = Owner };
            HeadlessSessionGuardAttribute.CloseAtTestEnd(_window);
            _window.Show();
            Owner.Measure(new Size(700, 500));
            Owner.Arrange(new Rect(0, 0, 700, 500));
            Renderer = new PadInfoOverlayRenderer(Owner,
                () => HasControls ? Overlay : null,
                () => Popover, () => Link, () => Anchor, () => Canvas,
                WorldToScreen, () => BoundsWorld, () => SelectedBounds);
        }

        public Canvas Owner { get; } = new() { Width = 700, Height = 500 };
        public Border Canvas { get; } = new() { Width = 600, Height = 400 };
        public Canvas Overlay { get; } = new() { Width = 700, Height = 500 };
        public Border Popover { get; } = new() { Width = 160, Height = 120 };
        public ShapePath Link { get; } = new();
        public Ellipse Anchor { get; } = new() { Width = 6, Height = 6 };
        public PadInfoOverlayRenderer Renderer { get; }
        public Rect2? BoundsWorld { get; set; } = new Rect2(0, 0, 10, 10);
        public IReadOnlyList<Rect2> SelectedBounds { get; set; } = [new Rect2(0, 0, 10, 10)];
        public double Zoom { get; set; } = 1;
        public Point Pan { get; set; } = new(100, 100);
        public bool HasControls { get; set; } = true;

        public void AssertPositions(Point panel, Point start, Point end)
        {
            AssertPoint(panel, new Point(Avalonia.Controls.Canvas.GetLeft(Popover), Avalonia.Controls.Canvas.GetTop(Popover)));
            var geometry = Assert.IsType<PathGeometry>(Link.Data);
            var figure = Assert.Single(Assert.IsType<PathFigures>(geometry.Figures));
            var segment = Assert.IsType<LineSegment>(Assert.Single(Assert.IsType<PathSegments>(figure.Segments)));
            AssertPoint(start, figure.StartPoint);
            AssertPoint(end, segment.Point);
        }

        public void Dispose() => _window.Close();

        private Point WorldToScreen(Point2 world) => new((world.X * Zoom) + Pan.X, (-world.Y * Zoom) + Pan.Y);

        private static void AssertPoint(Point expected, Point actual)
        {
            Assert.Equal(expected.X, actual.X, 6);
            Assert.Equal(expected.Y, actual.Y, 6);
        }
    }
}
