// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchDetailIntegratedLayoutTests
{
    private static readonly string[] _columnLabels = ["Regular id", "Coord", "Q7", "Ratio"];
    private static readonly ThemeVariant[] _themes = [ThemeVariant.Dark, ThemeVariant.Light];
    private static readonly int[] _q7Values = [0, 64, 128, 255];
    private static readonly string[] _q7Texts = ["0/128", "64/128", "128/128", "255/128"];
    private static readonly string[] _ratioTexts = ["0.0 %", "50.0 %", "100.0 %", "199.2 %"];
    private static readonly (int Threshold, int Maximum, bool Below)[] _thresholdCases =
        [(64, 63, true), (64, 64, false), (64, 65, false), (0, 0, false)];

    [AvaloniaFact]
    public async Task ColumnHeadersStayVisibleOutsideTheScrollingRowsInBothThemesAsync()
    {
        await WithWindowAsync(CreateModel(), async window =>
        {
            var header = Assert.IsType<Border>(window.FindControl<Border>("AllocationHeader"));
            var list = Assert.IsType<ListBox>(window.FindControl<ListBox>("AllocationList"));
            Assert.Contains("workspaceTableHeader", header.Classes);
            Assert.Contains("workspaceDataList", list.Classes);
            Assert.Equal(_columnLabels, header.GetVisualDescendants().OfType<SelectableTextBlock>()
                .Select(static block => block.Text));
            foreach (var theme in _themes)
            {
                window.RequestedThemeVariant = theme;
                await HeadlessUiPump.FlushQueueAsync();
                var scroll = Assert.Single(list.GetVisualDescendants().OfType<ScrollViewer>());
                Assert.Equal(ScrollBarVisibility.Auto, scroll.VerticalScrollBarVisibility);
                Assert.Equal(ScrollBarVisibility.Disabled, scroll.HorizontalScrollBarVisibility);
                Assert.DoesNotContain(scroll, header.GetVisualAncestors());
                Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
                scroll.Offset = default;
                await HeadlessUiPump.FlushQueueAsync();
                var headerPosition = header.TranslatePoint(default, window);
                Assert.NotNull(headerPosition);

                scroll.Offset = new Vector(0, scroll.Extent.Height - scroll.Viewport.Height);
                await HeadlessUiPump.FlushQueueAsync();

                Assert.True(scroll.Offset.Y > 0);
                Assert.True(header.IsEffectivelyVisible);
                Assert.Equal(headerPosition, header.TranslatePoint(default, window));
                Assert.True(header.Bounds.Height > 0);
            }
        });
    }

    [AvaloniaFact]
    public async Task AllocationsUseRemainingHeightWhileThePreviewKeepsItsHeightAsync()
    {
        var model = CreateModel(threshold: 64, maximum: 63);
        await WithWindowAsync(model, async window =>
        {
            var layout = Assert.IsType<Grid>(window.FindControl<Grid>("DetailLayout"));
            var section = Assert.IsType<Grid>(window.FindControl<Grid>("AllocationSection"));
            var header = Assert.IsType<Border>(window.FindControl<Border>("AllocationHeader"));
            var list = Assert.IsType<ListBox>(window.FindControl<ListBox>("AllocationList"));
            var preview = Assert.Single(window.GetVisualDescendants().OfType<NotchPreviewCanvas>());
            Assert.True(layout.RowDefinitions[^1].Height.IsStar);
            Assert.Equal(layout.RowDefinitions.Count - 1, Grid.GetRow(section));
            Assert.True(section.RowDefinitions[^1].Height.IsStar);
            Assert.Equal(section.RowDefinitions.Count - 1, Grid.GetRow(list));
            Assert.True(double.IsPositiveInfinity(list.MaxHeight));
            Assert.Equal(220d, preview.Height);
            Assert.Same(model.CadPad, preview.CadPad);
            Assert.Same(model.PreviewRegularPads, preview.RegularPads);
            Assert.Same(model.ToFullPolygons, preview.ToFullPolygons);
            Assert.Equal(model.FreeformType, preview.FreeformType);

            window.Width = window.MinWidth;
            window.Height = window.MinHeight;
            await HeadlessUiPump.FlushQueueAsync();
            var scroll = Assert.Single(list.GetVisualDescendants().OfType<ScrollViewer>());
            Assert.True(scroll.Viewport.Height > 0);
            Assert.Equal(220d, preview.Bounds.Height);
            Assert.Equal(layout.Bounds.Height, section.Bounds.Bottom, precision: 2);
            Assert.Equal(section.Bounds.Height - header.Bounds.Height - section.RowSpacing, list.Bounds.Height, precision: 2);
            var listHeight = list.Bounds.Height;
            var windowHeight = window.Bounds.Height;

            window.Height += 180;
            await HeadlessUiPump.FlushQueueAsync();

            Assert.True(list.Bounds.Height > listHeight);
            Assert.Equal(window.Bounds.Height - windowHeight, list.Bounds.Height - listHeight, precision: 2);
            Assert.Equal(220d, preview.Bounds.Height);
        });
    }

    [AvaloniaFact]
    public async Task BelowThresholdWarningFollowsTheExistingThresholdPredicateAsync()
    {
        await WithWindowAsync(CreateModel(), async window =>
        {
            foreach (var (threshold, maximum, below) in _thresholdCases)
            {
                var model = CreateModel(threshold, maximum);
                window.DataContext = model;
                await HeadlessUiPump.FlushQueueAsync();
                var blocks = window.GetVisualDescendants().OfType<SelectableTextBlock>().ToArray();
                var warning = Assert.Single(blocks, static block => block.Text == "Below TH (skip notch output)");
                Assert.Equal(below, model.IsBelowThreshold);
                Assert.Equal(below, warning.IsEffectivelyVisible);
                var visibleText = blocks.Where(static block => block.IsEffectivelyVisible).Select(static block => block.Text);
                Assert.Contains(model.CadTitle, visibleText);
                Assert.Contains(model.CadLayer, visibleText);
                Assert.Contains(model.FreeformLabel, visibleText);
                Assert.Contains(model.ToRegularRatioText, visibleText);
                Assert.Contains(model.ToFullRatioText, visibleText);
                Assert.Contains(model.CombinedRatioText, visibleText);
                Assert.Contains(model.ThresholdSummary, visibleText);
            }
        });
    }

    [AvaloniaFact]
    public async Task AllocationRowsKeepExistingQ7AndRatioTextAsync()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var model = CreateModel(allocationCount: 4);
            await WithWindowAsync(model, async window =>
            {
                window.Height += 180;
                await HeadlessUiPump.FlushQueueAsync();
                var list = Assert.IsType<ListBox>(window.FindControl<ListBox>("AllocationList"));
                Assert.Same(model.Allocations, list.ItemsSource);
                Assert.Equal(model.Allocations.Count, list.ItemCount);
                Assert.Single(window.GetVisualDescendants().OfType<SelectableTextBlock>(),
                    static block => block.Text == "Allocations (Q7)");
                for (var index = 0; index < model.Allocations.Count; index++)
                {
                    var allocation = model.Allocations[index];
                    var text = list.GetVisualDescendants().OfType<SelectableTextBlock>()
                        .Where(block => ReferenceEquals(block.DataContext, allocation)).Select(static block => block.Text).ToArray();
                    Assert.Contains(allocation.RegularLabel, text);
                    Assert.Contains(allocation.GridLabel, text);
                    Assert.Equal(_q7Texts[index], allocation.Q7Text);
                    Assert.Equal(_ratioTexts[index], allocation.RatioText);
                    Assert.Contains(allocation.Q7Text, text);
                    Assert.Contains(allocation.RatioText, text);
                }
            });
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    private static async Task WithWindowAsync(NotchDetailViewModel model, Func<NotchDetailWindow, Task> assertions)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var window = new NotchDetailWindow { DataContext = model };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            await assertions(window);
        }
        finally
        {
            window.Close();
        }
    }

    private static NotchDetailViewModel CreateModel(int threshold = 64, int maximum = 255, int allocationCount = 40)
    {
        var polygon = new Polygon2([new Point2(0, 0), new Point2(2, 0), new Point2(2, 1), new Point2(0, 1)]);
        var cad = new CadPad(201, "PAD_201", "signal-a", polygon);
        var allocations = Enumerable.Range(0, allocationCount).Select(index =>
        {
            var q7 = Math.Min(_q7Values[index % _q7Values.Length], maximum);
            return new NotchDetailAllocation(index + 101, index / 3, index % 3, 0, q7, q7 / 128d);
        }).ToArray();
        return new NotchDetailViewModel(cad, 7, FreeformType.XWay, allocations, [], threshold, maximum,
            "TH: 50.0%", 1, "ToRegular: 50.0%", "ToFull: 50.0%", "Combined: 100.0%", []);
    }
}
