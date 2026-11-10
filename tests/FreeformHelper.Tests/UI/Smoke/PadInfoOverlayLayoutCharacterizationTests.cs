// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.Interaction;
using FreeformHelper.UI.Views;
using Xunit;
using Point = Avalonia.Point;
using ShapePath = Avalonia.Controls.Shapes.Path;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class PadInfoOverlayLayoutCharacterizationTests
{
    [AvaloniaTheory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task PanAndZoomPositionPanelAndConnectorInOffsetOverlayAsync(double zoom)
    {
        await using var workspace = await MainWindowGlobalInputFixture.CreateAsync();
        var layout = new OverlayLayout(workspace);
        await layout.OpenAsync();

        layout.Canvas.FocusSelectionWithMinZoomAt(zoom, new Point(200, 160));
        await HeadlessUiPump.FlushQueueAsync();

        layout.AssertPositions(new Point(220 + (5 * zoom), 100), new Point(200, 160),
            new Point(220 + (5 * zoom), 160));
        Assert.True(layout.Anchor.IsVisible);
        layout.AssertSelectionUnchanged();
    }

    [AvaloniaFact]
    public async Task OffScreenSelectionHidesAnchorAndClampsConnectorToCanvasAsync()
    {
        await using var workspace = await MainWindowGlobalInputFixture.CreateAsync();
        var layout = new OverlayLayout(workspace);
        await layout.OpenAsync();

        layout.Canvas.FocusSelectionWithMinZoomAt(2, new Point(-100, 160));
        await HeadlessUiPump.FlushQueueAsync();

        layout.AssertPositions(new Point(20, 100), new Point(0, 160), new Point(20, 160));
        Assert.False(layout.Anchor.IsVisible);
        layout.AssertSelectionUnchanged();
    }

    [AvaloniaFact]
    public async Task QueuedLayoutUsesReplacementAndLatestViewportWithoutChangingSelectionAsync()
    {
        await using var workspace = await MainWindowGlobalInputFixture.CreateAsync();
        var layout = new OverlayLayout(workspace);
        await layout.OpenAsync();
        var previousGeometry = layout.Link.Data;
        layout.Canvas.FocusSelectionWithMinZoomAt(2, new Point(200, 160));
        // Run ViewChanged at Render, leaving the view's Background layout pending.
        layout.Canvas.Dispatcher.RunJobs(DispatcherPriority.Render);
        Assert.Same(previousGeometry, layout.Link.Data);
        var replacement = workspace.Helper.CadPads[1];
        var replacementContext = new PadInfoContext(new object(), replacement.Bounds, [replacement.Bounds]);

        workspace.Helper.InteractionState.OpenPadInfo(replacementContext);
        layout.AssertPositions(new Point(250, 100), new Point(220, 160), new Point(250, 160));
        layout.Canvas.FocusSelectionWithMinZoomAt(2, new Point(260, 210));
        await HeadlessUiPump.FlushQueueAsync();

        Assert.Same(replacementContext, workspace.Helper.InteractionState.ActivePadInfo);
        layout.AssertPositions(new Point(310, 150), new Point(280, 210), new Point(310, 210));
        Assert.True(layout.Anchor.IsVisible);
        layout.AssertSelectionUnchanged();
    }

    private sealed class OverlayLayout
    {
        private readonly MainWindowGlobalInputFixture _workspace;
        private readonly PadInfoPopover _popover;
        private readonly Canvas _overlay;
        private readonly CadPad _selectedPad;

        public OverlayLayout(MainWindowGlobalInputFixture workspace)
        {
            _workspace = workspace;
            var view = Assert.Single(workspace.Window.GetVisualDescendants().OfType<FreeformHelperView>());
            Canvas = Assert.IsType<PadCanvas>(view.FindControl<PadCanvas>("PadCanvas"));
            _overlay = Assert.IsType<Canvas>(view.FindControl<Canvas>("PadInfoOverlay"));
            _popover = Assert.IsType<PadInfoPopover>(view.FindControl<PadInfoPopover>("PadInfoPopover"));
            Link = Assert.IsType<ShapePath>(view.FindControl<ShapePath>("PadInfoLink"));
            Anchor = Assert.IsType<Avalonia.Controls.Shapes.Ellipse>(
                view.FindControl<Avalonia.Controls.Shapes.Ellipse>("PadInfoAnchor"));
            _selectedPad = workspace.Helper.CadPads[0];
            Assert.Equal(10, _selectedPad.Bounds.Width);
            Assert.Equal(10, _selectedPad.Bounds.Height);
            _popover.MinWidth = 0;
            _popover.MinHeight = 0;
            _popover.Width = 160;
            _popover.Height = 120;
            _overlay.RenderTransform = new TranslateTransform(37, 23);
        }

        public PadCanvas Canvas { get; }
        public ShapePath Link { get; }
        public Avalonia.Controls.Shapes.Ellipse Anchor { get; }

        public async Task OpenAsync()
        {
            _workspace.Helper.ApplyCanvasSelection([_selectedPad.Id], []);
            Canvas.SetSelection([_selectedPad.Id], []);
            Canvas.ResetView();
            Canvas.FocusSelectionWithMinZoomAt(1, new Point(140, 120));
            await HeadlessUiPump.FlushQueueAsync();
            _workspace.Helper.InteractionState.OpenPadInfo(
                new PadInfoContext(new object(), _selectedPad.Bounds, [_selectedPad.Bounds]));
            await HeadlessUiPump.FlushQueueAsync();
            Assert.True(_overlay.IsVisible);
            Assert.Equal(160, _popover.DesiredSize.Width);
            Assert.Equal(120, _popover.DesiredSize.Height);
            Assert.True(Canvas.Bounds.Width > 490);
            Assert.True(Canvas.Bounds.Height > 290);
            Assert.NotEqual(default, Assert.IsType<Point>(Avalonia.VisualExtensions.TranslatePoint(Canvas, default, _overlay)));
        }

        public void AssertPositions(Point panel, Point start, Point end)
        {
            var origin = Assert.IsType<Point>(Avalonia.VisualExtensions.TranslatePoint(Canvas, default, _overlay));
            AssertPoint(Offset(panel, origin), new Point(Avalonia.Controls.Canvas.GetLeft(_popover),
                Avalonia.Controls.Canvas.GetTop(_popover)));
            var geometry = Assert.IsType<PathGeometry>(Link.Data);
            var figure = Assert.Single(Assert.IsType<PathFigures>(geometry.Figures));
            var segment = Assert.IsType<LineSegment>(Assert.Single(Assert.IsType<PathSegments>(figure.Segments)));
            AssertPoint(Offset(start, origin), figure.StartPoint);
            AssertPoint(Offset(end, origin), segment.Point);
        }

        public void AssertSelectionUnchanged()
        {
            Assert.Equal(_selectedPad.Id, Assert.Single(_workspace.Helper.SelectedCadPadIds));
            Assert.Equal(_selectedPad.Id, Assert.Single(_workspace.Helper.InteractionState.SelectedCadIds));
            Assert.Empty(_workspace.Helper.SelectedRegularPadIndices);
            Assert.Empty(_workspace.Helper.InteractionState.SelectedRegularIndices);
        }

        private static Point Offset(Point point, Point origin) => new(point.X + origin.X, point.Y + origin.Y);

        private static void AssertPoint(Point expected, Point actual)
        {
            Assert.Equal(expected.X, actual.X, 6);
            Assert.Equal(expected.Y, actual.Y, 6);
        }
    }
}
