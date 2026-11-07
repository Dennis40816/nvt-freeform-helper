// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfOverlapReportIntegratedLayoutTests
{
    private const string Summary = "DXF warnings: 2 overlapping pad pairs.";
    private static readonly string[] _issues = ["Overlap: CAD #1 with CAD #2", "Overlap: CAD #3 with CAD #4"];

    [AvaloniaFact]
    public async Task SummaryAndIssueCountLeadTheReportWithoutRepeatingTheWindowTitleAsync()
    {
        var vm = new DxfOverlapReportViewModel(Summary, _issues);
        await WithThemesAsync(vm, window =>
        {
            AssertHeader(window, Summary, "Issues (2)");
            Assert.Equal(string.Join(Environment.NewLine, _issues), ReportBody(window).Text);
            AssertCopyFooter(window);
            return Task.CompletedTask;
        });
    }

    [AvaloniaFact]
    public async Task EmptyReportLeadsWithNoIssuesAndRetainsItsSelectableReportTextAsync()
    {
        var vm = new DxfOverlapReportViewModel("DXF check: no overlaps detected.", []);
        await WithThemesAsync(vm, window =>
        {
            AssertHeader(window, vm.Summary, "No issues");
            var report = ReportBody(window);
            Assert.True(report.IsEffectivelyVisible);
            Assert.True(report.IsReadOnly);
            report.SelectAll();
            Assert.Equal("No overlap issues.", report.Text);
            Assert.Equal("No overlap issues.", report.SelectedText);
            AssertCopyFooter(window);
            return Task.CompletedTask;
        });
    }

    [AvaloniaFact]
    public async Task SelectableBodyScrollsInRemainingHeightAndGrowsWithTheWindowAsync()
    {
        var issues = Enumerable.Range(1, 100).Select(static index => $"Overlap pair {index}: CAD pads intersect.").ToArray();
        var vm = new DxfOverlapReportViewModel(Summary, issues);
        await WithThemesAsync(vm, async window =>
        {
            window.Height = window.MinHeight;
            await HeadlessUiPump.FlushQueueAsync();
            var layout = Assert.IsType<Grid>(window.FindControl<Grid>("ReportLayout"));
            Assert.True(layout.RowDefinitions[^1].Height.IsStar);
            var report = ReportBody(window);
            var bodyLayout = Assert.IsType<Grid>(Assert.IsType<Border>(report.Parent).Parent);
            Assert.True(bodyLayout.RowDefinitions[0].Height.IsStar);
            Assert.True(report.IsReadOnly);
            Assert.True(report.AcceptsReturn);
            Assert.Equal(TextWrapping.Wrap, report.TextWrapping);
            Assert.Equal(ScrollBarVisibility.Auto, ScrollViewer.GetVerticalScrollBarVisibility(report));
            Assert.Equal(ScrollBarVisibility.Disabled, ScrollViewer.GetHorizontalScrollBarVisibility(report));
            report.SelectAll();
            Assert.Equal(vm.ReportText, report.SelectedText);
            var scroll = Assert.Single(report.GetVisualDescendants().OfType<ScrollViewer>());
            Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
            AssertBodyAndFooterFit(window);

            var originalHeight = report.Bounds.Height;
            window.Height += 160;
            await HeadlessUiPump.FlushQueueAsync();
            Assert.True(report.Bounds.Height > originalHeight);
            AssertBodyAndFooterFit(window);
            Assert.Equal(string.Join(Environment.NewLine, issues), report.Text);
        });
    }

    [AvaloniaFact]
    public async Task CopyResultAppearsOnlyAfterTheUnchangedReportTextIsCopiedAsync()
    {
        await AssertCopyFeedbackAsync(new DxfOverlapReportViewModel(Summary, _issues), string.Join(Environment.NewLine, _issues));
        await AssertCopyFeedbackAsync(new DxfOverlapReportViewModel(Summary, []), "No overlap issues.");
    }

    [AvaloniaFact]
    public async Task FailedCopyClearsPreviousSuccessAndUsesTheExistingFailureRouteAsync()
    {
        var vm = new DxfOverlapReportViewModel(Summary, _issues);
        var probe = new UiEventProbe();
        var failure = new InvalidOperationException("clipboard unavailable");
        var copyFails = false;
        var seams = new DxfOverlapReportSeams(probe.Runner, _ => copyFails ? Task.FromException(failure) : Task.CompletedTask);
        await WithWindowAsync(vm, ThemeVariant.Dark, async window =>
        {
            var button = AssertCopyFooter(window);
            UiEventProbe.Click(button);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.True(CopyResult(window).IsEffectivelyVisible);

            copyFails = true;
            UiEventProbe.Click(button);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.False(CopyResult(window).IsEffectivelyVisible);
            Assert.False(vm.HasCopyResult);
            var report = Assert.Single(probe.Reports);
            Assert.Equal("DxfOverlap.CopyAll", report.Operation);
            Assert.Same(failure, report.Exception);
            Assert.Equal(string.Join(Environment.NewLine, _issues), ReportBody(window).Text);
            Assert.True(button.IsEffectivelyEnabled);
        }, seams);
    }

    private static async Task AssertCopyFeedbackAsync(DxfOverlapReportViewModel vm, string expectedText)
    {
        var probe = new UiEventProbe();
        var writtenText = string.Empty;
        using var writeGate = new SemaphoreSlim(0);
        var seams = new DxfOverlapReportSeams(probe.Runner, async text =>
        {
            writtenText = text;
            await writeGate.WaitAsync();
        });
        await WithWindowAsync(vm, ThemeVariant.Dark, async window =>
        {
            var feedback = CopyResult(window);
            Assert.False(feedback.IsEffectivelyVisible);
            Assert.Equal(string.Empty, feedback.Text);
            var button = AssertCopyFooter(window);
            UiEventProbe.Click(button);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal(expectedText, writtenText);
            Assert.False(feedback.IsEffectivelyVisible);
            Assert.False(vm.HasCopyResult);

            writeGate.Release();
            await HeadlessUiPump.WaitUntilAsync(() => vm.HasCopyResult, "Copy completion did not reach the report.");
            await HeadlessUiPump.FlushQueueAsync();
            Assert.True(feedback.IsEffectivelyVisible);
            Assert.Equal("Copied report text to clipboard.", feedback.Text);
            Assert.Equal(TextWrapping.NoWrap, feedback.TextWrapping);
            Assert.Equal(expectedText, ReportBody(window).Text);
            Assert.Equal(expectedText, vm.ReportText);
            Assert.Empty(probe.Reports);
        }, seams);
    }

    private static void AssertHeader(DxfOverlapReportWindow window, string summary, string issueTitle)
    {
        Assert.Equal("DXF overlaps report", window.Title);
        var layout = Assert.IsType<Grid>(window.FindControl<Grid>("ReportLayout"));
        var header = Assert.IsType<StackPanel>(window.FindControl<StackPanel>("ReportSummary"));
        Assert.Same(header, layout.Children[0]);
        Assert.Equal(0, Grid.GetRow(header));
        Assert.Collection(header.Children,
            control => Assert.Equal(summary, Assert.IsType<SelectableTextBlock>(control).Text),
            control => Assert.Equal(issueTitle, Assert.IsType<SelectableTextBlock>(control).Text));
        var content = Assert.IsType<Border>(window.Content);
        Assert.DoesNotContain(content.GetVisualDescendants().OfType<SelectableTextBlock>(), label => label.Text == window.Title);
    }

    private static Button AssertCopyFooter(DxfOverlapReportWindow window)
    {
        var content = Assert.IsType<Border>(window.Content);
        var button = Assert.Single(content.GetLogicalDescendants().OfType<Button>());
        Assert.Equal("Copy all", button.Content);
        Assert.Equal("Copy overlap report text.", TipText(button));
        Assert.Contains("actionPrimary", button.Classes);
        Assert.True(button.IsEffectivelyEnabled);
        Assert.Equal(HorizontalAlignment.Right, button.HorizontalAlignment);
        var footer = Assert.IsType<Grid>(window.FindControl<Grid>("ReportFooter"));
        Assert.Contains(button, footer.Children);
        Assert.Equal(1, Grid.GetColumn(button));
        Assert.Equal(0, Grid.GetColumn(CopyResult(window)));
        return button;
    }

    private static void AssertBodyAndFooterFit(DxfOverlapReportWindow window)
    {
        var report = ReportBody(window);
        var footer = Assert.IsType<Grid>(window.FindControl<Grid>("ReportFooter"));
        var bodyTop = report.TranslatePoint(default, window);
        var footerTop = footer.TranslatePoint(default, window);
        Assert.NotNull(bodyTop);
        Assert.NotNull(footerTop);
        Assert.True(report.Bounds.Height > 0);
        Assert.True(bodyTop.Value.Y + report.Bounds.Height <= footerTop.Value.Y);
        Assert.True(footerTop.Value.Y + footer.Bounds.Height <= window.ClientSize.Height);
    }

    private static TextBox ReportBody(DxfOverlapReportWindow window) =>
        Assert.IsType<TextBox>(window.FindControl<TextBox>("ReportTextBox"));

    private static TextBlock CopyResult(DxfOverlapReportWindow window) =>
        Assert.IsType<TextBlock>(window.FindControl<TextBlock>("CopyResult"));

    private static string? TipText(Control control) => ToolTip.GetTip(control) switch
    {
        string text => text,
        TextBlock text => text.Text,
        _ => null,
    };

    private static async Task WithThemesAsync(DxfOverlapReportViewModel vm, Func<DxfOverlapReportWindow, Task> inspect)
    {
        await WithWindowAsync(vm, ThemeVariant.Dark, inspect);
        await WithWindowAsync(vm, ThemeVariant.Light, inspect);
    }

    private static async Task WithWindowAsync(DxfOverlapReportViewModel vm, ThemeVariant theme,
        Func<DxfOverlapReportWindow, Task> inspect, DxfOverlapReportSeams? seams = null)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var window = new DxfOverlapReportWindow(seams) { DataContext = vm, RequestedThemeVariant = theme };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            await inspect(window);
        }
        finally
        {
            window.Close();
        }
    }
}
