// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Nvt.Core.Avalonia.Panels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class PadInfoCanvasLayoutTests
{
    [AvaloniaTheory]
    [InlineData(false, false, "Dark")]
    [InlineData(false, false, "Light")]
    [InlineData(true, false, "Dark")]
    [InlineData(true, false, "Light")]
    [InlineData(false, true, "Dark")]
    [InlineData(false, true, "Light")]
    [InlineData(true, true, "Dark")]
    [InlineData(true, true, "Light")]
    public async Task CanvasPopoverFitsControlsAndKeepsContentOutsideScrollLaneAsync(bool cad, bool longText, string theme)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var app = Assert.IsType<FreeformHelper.UI.App>(Avalonia.Application.Current);
        var previousTheme = app.RequestedThemeVariant;
        app.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        var popover = new PadInfoPopover { DataContext = cad ? CreateCadInfo(longText) : CreateRegularInfo(longText) };
        var canvas = new Canvas { Children = { popover } };
        Canvas.SetLeft(popover, 24);
        Canvas.SetTop(popover, 16);
        var window = new Window
        {
            Content = canvas,
            Width = 420,
            Height = 640,
            RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light,
        };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            foreach (var section in popover.GetVisualDescendants().OfType<CollapsiblePanel>())
            {
                section.SetCurrentValue(CollapsiblePanel.IsExpandedProperty, true);
            }

            await HeadlessUiPump.FlushQueueAsync();
            var panel = Assert.Single(popover.GetVisualDescendants().OfType<Border>(),
                static border => border.Classes.Contains("padInfoPanel"));
            Assert.InRange(panel.Bounds.Width, 300, panel.MaxWidth);
            var scroll = Assert.Single(popover.GetVisualDescendants().OfType<ScrollViewer>(),
                viewer => viewer.Name == (cad ? "CadPadInfoScroll" : "RegularPadInfoScroll"));
            var lane = Assert.Single(scroll.GetVisualDescendants().OfType<ScrollBar>(),
                static bar => bar.Orientation == Orientation.Vertical && bar.IsEffectivelyVisible);
            Assert.Equal(14, lane.Bounds.Width, precision: 2);
            Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
            AssertContentFits(scroll, lane);
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"Panel={panel.Bounds.Width}, viewport={scroll.Viewport.Width}, content={((Border)scroll.Content!).Bounds.Width}, lane={lane.Bounds.Width}");
            if (longText)
            {
                var text = Assert.Single(scroll.GetVisualDescendants().OfType<SelectableTextBlock>(),
                    block => block.Text == (cad ? ((CadPadInfoViewModel)popover.DataContext!).LayerText
                        : ((RegularPadInfoViewModel)popover.DataContext!).OwnerSummaryText));
                Assert.Equal(TextWrapping.Wrap, text.TextWrapping);
                Assert.True(text.TextLayout.TextLines.Count > 1, "Long text must wrap inside the capped panel.");
            }
        }
        finally
        {
            window.Close();
            app.RequestedThemeVariant = previousTheme;
        }
    }

    private static void AssertContentFits(ScrollViewer scroll, ScrollBar lane)
    {
        var laneLeft = OriginX(lane, scroll);
        var content = Assert.IsType<Border>(scroll.Content);
        Assert.True(OriginX(content, scroll) + content.Bounds.Width <= laneLeft + 1,
            $"Content right {OriginX(content, scroll) + content.Bounds.Width} exceeds scroll lane left {laneLeft}.");
        var controls = content.GetVisualDescendants().OfType<Control>()
            .Where(static control => control.IsEffectivelyVisible && control is Button or NumberScrubber).ToArray();
        Assert.NotEmpty(controls);
        foreach (var control in controls)
        {
            Assert.True(OriginX(control, scroll) >= 0);
            Assert.True(OriginX(control, scroll) + control.Bounds.Width <= laneLeft + 1,
                $"{control.GetType().Name} extends into the scroll lane.");
            Assert.True(control.Bounds.Width >= control.DesiredSize.Width - 1,
                $"{control.GetType().Name} is clipped horizontally.");
        }

        Assert.All(controls.OfType<NumberScrubber>(), static input => Assert.True(input.Bounds.Width >= 80));
    }

    private static double OriginX(Control control, Control root) =>
        Assert.IsType<Avalonia.Point>(Avalonia.VisualExtensions.TranslatePoint(control, default, root)).X;

    private static RegularPadInfoViewModel CreateRegularInfo(bool longText) => new(
        pads: [TestGeometryFactory.CreateRegularPad(7, 5, 389, 0, 0, 9.6875, 8.7, diffIndex: 229)],
        toDisplayRow: static row => row,
        getDxfIndex: static _ => 112,
        getMatchedCadPadIds: static _ => [],
        getMatchedCadLinks: static _ => [],
        applyPadSize: static (_, _, _, _) => { },
        resetPadSize: static (_, _) => { },
        setFreeform: static (_, _) => { },
        getCrossIcOwnerShares: _ => longText
            ? [(123456789, 25), (123456790, 25), (123456791, 25), (123456792, 25)] : [],
        getNotchRowsByRegularPad: static _ => [],
        highlightCadOwnerPads: null,
        focusRegularMatches: null,
        highlightRegularMatches: null);

    private static CadPadInfoViewModel CreateCadInfo(bool longText) => new(
        pads: [TestGeometryFactory.CreateCadPad(274, longText ? new string('W', 256) : "AA", 0, 0, 10, 8)],
        getDxfIndex: static _ => 112,
        getCadOutputFwDiffOverride: null,
        isDxfIndexAnchor: null,
        getCustomValue: static _ => 0,
        applyCustomValue: null,
        setCadOutputFwDiffOverride: static (_, _) => { },
        clearCadOutputFwDiffOverride: null,
        setDxfIndexAnchor: null,
        clearDxfIndexAnchor: null);
}
