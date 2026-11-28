// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using FreeformHelper.UI.Views.WorkspaceSections;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class IndexMappingReportCompactLayoutTests
{
    private static readonly ThemeVariant[] _themes = [ThemeVariant.Dark, ThemeVariant.Light];
    private static readonly string[] _headers = ["CAD", "REG", "Result"];
    private static readonly IndexMappingDecisionFilterMode[] _filterOrder =
    [
        IndexMappingDecisionFilterMode.All, IndexMappingDecisionFilterMode.Unmapped,
        IndexMappingDecisionFilterMode.LowConfidence, IndexMappingDecisionFilterMode.Ambiguous,
        IndexMappingDecisionFilterMode.CountMismatch, IndexMappingDecisionFilterMode.ChangedByMask,
        IndexMappingDecisionFilterMode.RemovedByMask, IndexMappingDecisionFilterMode.DuplicateDiff,
    ];

    [AvaloniaFact]
    public async Task SearchStillMatchesReasonTextAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var model = CreateModel();
        var window = new IndexMappingReportWindow { DataContext = model };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var filters = Assert.Single(window.GetVisualDescendants().OfType<IndexMappingReportFilterBarView>());
            var search = Assert.IsType<TextBox>(filters.FindControl<TextBox>("SearchBox"));
            search.Text = "distinctive reason";
            await HeadlessUiPump.FlushQueueAsync();
            var row = Assert.Single(model.DecisionRows);
            Assert.Equal("A distinctive reason for review.", row.ReasonText);
            Assert.NotEqual(row.StatusDisplay, row.ReasonText);
            Assert.Same(row, model.SelectedDecision);
            search.Text = "absent reason";
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Empty(model.DecisionRows);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task ThreeColumnsKeepDistinctReasonInDetailAndFitMinimumWindowAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var model = CreateModel();
        var window = new IndexMappingReportWindow { DataContext = model };
        try
        {
            window.Width = window.MinWidth;
            window.Height = window.MinHeight;
            window.Show();
            foreach (var theme in _themes)
            {
                window.RequestedThemeVariant = theme;
                await HeadlessUiPump.FlushQueueAsync();
                var left = Assert.Single(window.GetVisualDescendants().OfType<IndexMappingReportLeftPaneView>());
                var header = Assert.Single(left.GetVisualDescendants().OfType<Border>(),
                    static border => border.Classes.Contains("mappingDecisionHeader"));
                Assert.Equal(_headers, header.GetVisualDescendants().OfType<TextBlock>().Select(static text => text.Text));
                var right = Assert.Single(window.GetVisualDescendants().OfType<IndexMappingReportRightPaneView>());
                var reason = Assert.IsType<SelectableTextBlock>(right.FindControl<SelectableTextBlock>("SelectedReason"));
                foreach (var row in model.DecisionRows)
                {
                    model.SelectDecisionCommand.Execute(row);
                    await HeadlessUiPump.FlushQueueAsync();
                    Assert.Equal(row.ReasonText, reason.Text);
                    Assert.Equal(row.ReasonText != row.StatusDisplay, reason.IsEffectivelyVisible);
                }

                Assert.Single(VisibleText(window), text => text == model.OverallStatusSummaryText);
                Assert.Single(VisibleText(window), text => text == model.OpenIssueSummaryText);
                Assert.Single(VisibleText(window), text => text == model.TableRevisionText);
                Assert.Single(VisibleText(window), text => text == model.MaskAuditSummary);
                await AssertContentFitsAsync(window);
                var list = Assert.IsType<ListBox>(left.FindControl<ListBox>("DecisionList"));
                Assert.True(list.Bounds.Height > 0);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task ZeroCountChipsRemainDimmedAndClickableInPictureOrderAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var model = CreateModel();
        var window = new IndexMappingReportWindow { DataContext = model };
        try
        {
            window.Width = window.MinWidth;
            window.Height = window.MinHeight;
            window.Show();
            var counts = new[] { model.TotalDecisionCount, model.UnmappedDecisionCount, model.LowConfidenceDecisionCount,
                model.AmbiguousDecisionCount, model.CountMismatchDecisionCount, model.ChangedByMaskDecisionCount,
                model.RemovedByMaskDecisionCount, model.DuplicateDiffDecisionCount };
            foreach (var theme in _themes)
            {
                window.RequestedThemeVariant = theme;
                await HeadlessUiPump.FlushQueueAsync();
                var filters = Assert.Single(window.GetVisualDescendants().OfType<IndexMappingReportFilterBarView>());
                var chips = filters.GetVisualDescendants().OfType<ToggleButton>()
                    .Where(chip => chip.Command == model.SetDecisionFilterModeCommand).ToArray();
                Assert.Equal(_filterOrder, chips.Select(static chip => Assert.IsType<IndexMappingDecisionFilterMode>(chip.CommandParameter)));
                for (var index = 0; index < chips.Length; index++)
                {
                    var chip = chips[index];
                    Assert.Equal(counts[index] == 0, chip.Classes.Contains("empty"));
                    Assert.Equal(counts[index] == 0 ? Assert.IsType<double>(chip.FindResource("OpacityMuted")) : 1, chip.Opacity);
                    Assert.True(chip.IsEnabled);
                    var center = Position(chip, window) + new global::Avalonia.Vector(chip.Bounds.Width / 2, chip.Bounds.Height / 2);
                    window.MouseDown(center, MouseButton.Left);
                    window.MouseUp(center, MouseButton.Left);
                    await HeadlessUiPump.FlushQueueAsync();
                    Assert.Equal(_filterOrder[index], model.SelectedFilterMode);
                    Assert.True(chip.IsChecked);
                    Assert.Single(chips, static candidate => candidate.IsChecked == true);
                }
            }
        }
        finally
        {
            window.Close();
        }
    }

    private static IEnumerable<string?> VisibleText(Control root) =>
        root.GetVisualDescendants().OfType<SelectableTextBlock>()
            .Where(static text => text.IsEffectivelyVisible).Select(static text => text.Text);

    private static async Task AssertContentFitsAsync(Window window)
    {
        foreach (var control in window.GetVisualDescendants().OfType<Control>()
                     .Where(static control => control.IsEffectivelyVisible && control is Button or SelectableTextBlock or TextBox or TextBlock { Text: { Length: > 0 } }))
        {
            control.BringIntoView();
            await HeadlessUiPump.FlushQueueAsync();
            var position = Position(control, window);
            Assert.True(control.Bounds.Width > 0, $"{control} has no width.");
            Assert.InRange(position.X, -1, window.ClientSize.Width);
            Assert.InRange(position.X + control.Bounds.Width, 0, window.ClientSize.Width + 1);
            Assert.True(control.Bounds.Height + 1 >= control.DesiredSize.Height, $"{control} is vertically clipped.");
            Assert.True(position.Y >= -1 && position.Y + control.Bounds.Height <= window.ClientSize.Height + 1,
                $"{control} cannot be scrolled into view: y={position.Y}, height={control.Bounds.Height}.");
        }
    }

    private static global::Avalonia.Point Position(Control control, Control root) =>
        Assert.IsType<global::Avalonia.Point>(global::Avalonia.VisualExtensions.TranslatePoint(control, default, root));

    private static IndexMappingReportViewModel CreateModel()
    {
        var issue = new DxfRegularMappingIssue(DxfRegularMappingIssueKind.LowConfidence,
            "A distinctive reason for review.", DxfIndex: 1700, CadPadId: 1536, RegularPadIndex: 7, DiffIndex: 1700);
        var unmapped = new DxfRegularMappingIssue(DxfRegularMappingIssueKind.UnmappedCad,
            "Unmapped CAD", DxfIndex: 1701, CadPadId: 1537);
        var report = new DxfRegularMappingReport(
            CadCount: 2, RegularCount: 1, MappedCount: 1, UnmappedCadCount: 1, UnmappedRegularCount: 0,
            LowConfidenceCount: 1, AmbiguousCount: 0, HasIssues: true, Summary: "summary",
            TotalIssueCount: 2, IssuesTruncated: false, Issues: [issue, unmapped], SampleIssues: [issue, unmapped]);
        return new IndexMappingReportViewModel("summary", report);
    }
}
