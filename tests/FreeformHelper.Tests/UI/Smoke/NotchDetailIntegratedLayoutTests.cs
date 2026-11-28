// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.Presentation;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchDetailIntegratedLayoutTests
{
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExplanationColumnsContainTheirContentAtMinimumWidthAsync(bool dark)
    {
        var model = NotchDetailPreviewSample.Build();
        model.SelectedSource = model.SelectedSource! with
        {
            AddedTitle = "To Full (support only)",
            Cells = model.SelectedSource.Cells.SetItem(0, model.SelectedSource.Cells[0] with { DiffIndex = 120 })
                .SetItem(1, model.SelectedSource.Cells[1] with { OmissionReason = "guard rounded to 0" }),
            DifferenceText = "rounding and drops: −1.50",
        };
        await WithWindowAsync(model, async (window, content) =>
        {
            window.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            window.Width = 420;
            await HeadlessUiPump.FlushQueueAsync();
            var header = Assert.Single(content.GetVisualDescendants().OfType<Grid>(),
                static grid => grid.Classes.Contains("explainTableHeader"));
            var tables = content.GetVisualDescendants().OfType<Grid>()
                .Where(static grid => grid.Classes.Contains("explainCellRow")).Prepend(header);
            foreach (var table in tables)
            {
                Assert.All(table.Children.Where(static cell => cell.IsVisible), cell =>
                {
                    var slot = LayoutInformation.GetPreviousArrangeBounds(cell)!.Value;
                    foreach (var part in cell.GetVisualDescendants().Prepend(cell).OfType<Control>())
                    {
                        var x = part.TranslatePoint(default, table)!.Value.X;
                        Assert.True(x >= slot.Left - .5 && x + part.Bounds.Width <= slot.Right + .5,
                            $"Column {Grid.GetColumn(cell)}: content [{x}, {x + part.Bounds.Width}] exceeds {slot}.");
                        if (part is TextBlock text)
                        {
                            Assert.True(text.TextLayout.Width <= text.Bounds.Width + .5);
                        }
                    }
                });
            }
        });
    }

    [AvaloniaTheory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task ExplanationRowsShareOneCentreLineAndFitTheDesignWindowAsync(bool dark, bool omission)
    {
        var model = NotchDetailPreviewSample.Build();
        if (omission)
        {
            var source = model.SelectedSource!;
            model.SelectedSource = source with
            {
                Cells = source.Cells.SetItem(1, source.Cells[1] with { OmissionReason = "guard rounded to 0" }),
            };
        }
        await WithWindowAsync(model, async (window, content) =>
        {
            window.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal(760, window.Height);
            var scroll = Assert.IsType<ScrollViewer>(window.FindControl<ScrollViewer>("DetailScroll"));
            Assert.True(scroll.Extent.Height <= scroll.Viewport.Height);
            var rows = content.GetVisualDescendants().OfType<Grid>()
                .Where(static grid => grid.Classes.Contains("explainCellRow")).ToArray();
            Assert.NotEmpty(rows);
            foreach (var row in rows)
            {
                var firstLine = row.Children.Where(static child => child.IsVisible && Grid.GetRow(child) == 0).ToArray();
                var centre = firstLine[0].Bounds.Center.Y;
                Assert.All(firstLine, child => Assert.Equal(centre, child.Bounds.Center.Y, precision: 1));
                Assert.All(firstLine.OfType<TextBlock>(), text => Assert.Single(text.TextLayout.TextLines));
                Assert.All(row.Children.OfType<TextBlock>().Where(static text => text.IsVisible && Grid.GetRow(text) == 1),
                    text => Assert.Single(text.TextLayout.TextLines));
            }
        });
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AllocationHeaderStaysOutsideItsScrollingRowsInBothThemesAsync(bool dark)
    {
        var model = NotchDetailPreviewSample.Build();
        var allocations = Enumerable.Range(0, 40).Select(static index =>
            new NotchDetailAllocation(index, index / 3, index % 3, 0, 64, .5)).ToArray();
        var many = new NotchDetailViewModel(model.CadPad, 0, model.FreeformType, allocations,
            model.PreviewRegularPads, 0, 64, "TH=0 (disabled)", 0, "", "", "", model.ToFullPolygons);
        many.ShowExplanation(new NotchDetailExplanation(model.Sources, "", [], model.NeighborCadPads));
        await WithWindowAsync(many, async (window, content) =>
        {
            window.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            var toggle = Assert.IsType<ToggleButton>(content.FindControl<ToggleButton>("AllocationsToggle"));
            Assert.False(toggle.IsChecked);
            toggle.IsChecked = true;
            await HeadlessUiPump.FlushQueueAsync();
            var header = Assert.IsType<Border>(content.FindControl<Border>("AllocationHeader"));
            var list = Assert.IsType<ListBox>(content.FindControl<ListBox>("AllocationList"));
            var scroll = Assert.Single(list.GetVisualDescendants().OfType<ScrollViewer>());
            Assert.Contains("workspaceTableHeader", header.Classes);
            Assert.Contains("workspaceDataList", list.Classes);
            string[] expected1 = ["Regular id", "Coord", "Q7", "Ratio"];
            Assert.Equal(expected1,
                header.GetVisualDescendants().OfType<SelectableTextBlock>().Select(static block => block.Text));
            Assert.Equal(ScrollBarVisibility.Auto, scroll.VerticalScrollBarVisibility);
            Assert.Equal(ScrollBarVisibility.Disabled, scroll.HorizontalScrollBarVisibility);
            Assert.DoesNotContain(scroll, header.GetVisualAncestors());
            Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
            var position = header.TranslatePoint(default, window);
            scroll.Offset = new Vector(0, scroll.Extent.Height - scroll.Viewport.Height);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.True(scroll.Offset.Y > 0);
            Assert.Equal(position, header.TranslatePoint(default, window));
            Assert.True(header.IsEffectivelyVisible);
        });
    }

    [AvaloniaFact]
    public async Task ExplanationOpensWithMainSectionsAndTheWholeContentCanScrollAsync()
    {
        var panel = NotchHolePanelFixture.Build([NotchHolePanelFixture.Circle(27, 27, 4.2)],
            gridOrigin: NotchHolePanelFixture.StaggerForwardOrigin);
        var model = await Task.Run(() => NotchDetailTestBuilder.Build(panel.Cad.Pads[0], 0, panel.Cad,
            panel.Grid, NotchHolePanelFixture.CreateSettings(true), null, new Dictionary<int, int>()));
        await WithWindowAsync(model, async (window, content) =>
        {
            var explanation = Assert.IsType<NotchExplanationView>(content.FindControl<NotchExplanationView>("Explanation"));
            Assert.Same(model.SelectedSource, explanation.DataContext);
            Assert.True(explanation.FindControl<StackPanel>("OutputRowsSection")!.IsEffectivelyVisible);
            Assert.True(explanation.FindControl<StackPanel>("WhySection")!.IsEffectivelyVisible);
            Assert.True(explanation.FindControl<Border>("FormulaCard")!.IsEffectivelyVisible);
            var preview = Assert.Single(content.GetVisualDescendants().OfType<NotchPreviewCanvas>());
            Assert.Equal(232, preview.Height);
            Assert.Same(model.CadPad, preview.CadPad);
            Assert.Same(model.PreviewRegularPads, preview.RegularPads);
            Assert.Same(model.SelectedSource, preview.Explanation);
            Assert.False(content.FindControl<Grid>("AllocationSection")!.IsEffectivelyVisible);
            content.FindControl<ToggleButton>("AllocationsToggle")!.IsChecked = true;
            window.Height = window.MinHeight;
            await HeadlessUiPump.FlushQueueAsync();
            var scroll = Assert.IsType<ScrollViewer>(window.FindControl<ScrollViewer>("DetailScroll"));
            Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
            scroll.Offset = new Vector(0, scroll.Extent.Height - scroll.Viewport.Height);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.True(scroll.Offset.Y > 0);
            Assert.Equal(232, preview.Bounds.Height);
        });
    }

    [AvaloniaTheory]
    [InlineData(64, 63, true)]
    [InlineData(64, 64, false)]
    [InlineData(64, 65, false)]
    [InlineData(0, 0, false)]
    public async Task BelowThresholdWarningKeepsItsPredicateAndOldRatioLinesAreRemovedAsync(int threshold, int maximum, bool below)
    {
        var sample = NotchDetailPreviewSample.Build();
        var model = new NotchDetailViewModel(sample.CadPad, 0, sample.FreeformType, sample.Allocations,
            sample.PreviewRegularPads, threshold, maximum, "TH=0 (disabled)", 0,
            "Undo NF ratio", "To Full ratio", "Combined ratio", sample.ToFullPolygons);
        await WithWindowAsync(model, (window, content) =>
        {
            var blocks = content.GetVisualDescendants().OfType<SelectableTextBlock>().ToArray();
            var warning = Assert.Single(blocks, static block => block.Text == "Below TH (skip notch output)");
            Assert.Equal(below, model.IsBelowThreshold);
            Assert.Equal(below, warning.IsEffectivelyVisible);
            Assert.Contains(blocks, block => block.Text == model.CadTitle);
            Assert.Contains(blocks, block => block.Text == model.CadLayer);
            Assert.Contains(blocks, block => block.Text == model.FreeformLabel);
            Assert.DoesNotContain(blocks, block => block.Text == model.ToRegularRatioText ||
                block.Text == model.ToFullRatioText || block.Text == model.CombinedRatioText);
            return Task.CompletedTask;
        });
    }

    [AvaloniaFact]
    public async Task ExpandedAllocationsKeepTheirExistingValuesAsync()
    {
        var model = NotchDetailPreviewSample.Build();
        await WithWindowAsync(model, async (window, content) =>
        {
            content.FindControl<ToggleButton>("AllocationsToggle")!.IsChecked = true;
            await HeadlessUiPump.FlushQueueAsync();
            var list = Assert.IsType<ListBox>(content.FindControl<ListBox>("AllocationList"));
            // The list virtualizes against the page viewport, so bring it on screen before reading its rows.
            list.BringIntoView();
            await HeadlessUiPump.FlushQueueAsync();
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Same(model.Allocations, list.ItemsSource);
            Assert.Equal(model.Allocations.Count, list.ItemCount);
            Assert.All(model.Allocations, allocation =>
            {
                var text = list.GetVisualDescendants().OfType<SelectableTextBlock>()
                    .Where(block => ReferenceEquals(block.DataContext, allocation)).Select(static block => block.Text).ToArray();
                Assert.Contains(allocation.RegularLabel, text);
                Assert.Contains(allocation.GridLabel, text);
                Assert.Contains(allocation.Q7Text, text);
                Assert.Contains(allocation.RatioText, text);
            });
        });
    }

    private static async Task WithWindowAsync(NotchDetailViewModel model, Func<NotchDetailWindow, NotchDetailContent, Task> assertions)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var window = new NotchDetailWindow { DataContext = model };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            await assertions(window, Assert.IsType<NotchDetailContent>(window.FindControl<NotchDetailContent>("DetailContent")));
        }
        finally
        {
            window.Close();
        }
    }
}
