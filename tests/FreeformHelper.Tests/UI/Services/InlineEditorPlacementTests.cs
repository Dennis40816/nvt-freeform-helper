// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class InlineEditorPlacementTests
{
    private const double Gap = 20.0;
    private static readonly Rect _canvas = new(100, 50, 800, 600);
    private static readonly Size _panel = new(200, 100);

    [Fact]
    public void EditorSitsRightOfTheAnchorAndCenteredOnItVertically()
    {
        var position = InlineEditorPlacement.Compute(_canvas, new Point(300, 300), _panel, Gap);

        Assert.Equal(new Point(320, 250), position);
    }

    [Fact]
    public void EditorStopsAtTheRightGapWhenTheAnchorIsNearTheRightEdge()
    {
        var position = InlineEditorPlacement.Compute(_canvas, new Point(850, 300), _panel, Gap);

        Assert.Equal(new Point(680, 250), position);
    }

    [Fact]
    public void EditorKeepsTheGapToTheTopAndBottomEdges()
    {
        var nearTop = InlineEditorPlacement.Compute(_canvas, new Point(300, 55), _panel, Gap);
        var nearBottom = InlineEditorPlacement.Compute(_canvas, new Point(300, 645), _panel, Gap);

        Assert.Equal(70, nearTop.Y);
        Assert.Equal(530, nearBottom.Y);
    }

    [Fact]
    public void EditorKeepsTheLeftGapWhenTheCanvasIsNarrowerThanTheEditor()
    {
        var narrowCanvas = new Rect(100, 50, 150, 600);

        var position = InlineEditorPlacement.Compute(narrowCanvas, new Point(240, 300), _panel, Gap);

        Assert.Equal(120, position.X);
    }

    [Fact]
    public void EditorMovesLeftOfAnAnchorThatLiesRightOfANarrowCanvas()
    {
        var narrowCanvas = new Rect(100, 50, 150, 600);

        var position = InlineEditorPlacement.Compute(narrowCanvas, new Point(400, 300), _panel, Gap);

        Assert.Equal(180, position.X);
    }

    [Fact]
    public void EditorStaysAtTheTopGapWhenItIsTallerThanTheCanvas()
    {
        var shortCanvas = new Rect(100, 50, 800, 80);

        var position = InlineEditorPlacement.Compute(shortCanvas, new Point(300, 90), _panel, Gap);

        Assert.Equal(70, position.Y);
    }
}
