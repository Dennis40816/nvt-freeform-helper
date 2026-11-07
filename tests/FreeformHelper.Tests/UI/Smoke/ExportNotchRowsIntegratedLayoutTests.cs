// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Nvt.Core.Avalonia.Panels;
using Nvt.Core.Avalonia.Primitives;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ExportNotchRowsIntegratedLayoutTests
{
    private static readonly string[] _lowFrequencyFields = ["Flags", "Comment"];
    private static readonly string[] _mainFooterButtons = ["Cancel", "Export"];
    private static readonly string[] _helperFooterButtons = ["Clear", "Cancel", "Apply"];
    private static readonly string[] _columnFooterButtons = ["Cancel", "Apply"];
    private static readonly string[] _expectedRangeKeys = ["9", "12"];

    [AvaloniaFact]
    public async Task MergedResultsHaveOnePresentationOwnerAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var viewModel = CreateSelection();
        viewModel.SearchKeyword = "diff=9";
        var window = new NotchExportSelectionWindow { DataContext = viewModel };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var shell = Assert.Single(window.GetVisualDescendants().OfType<WorkspaceShell>());
            var checks = Assert.IsType<SectionFrame>(window.FindControl<SectionFrame>("ExportChecksSection"));
            var checkPanels = checks.GetVisualDescendants().OfType<CollapsiblePanel>().ToArray();
            var passed = Assert.Single(checkPanels, static panel => panel.Title == "Passed checks");
            Assert.False(passed.IsExpanded);
            passed.IsExpanded = true;
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Single(checkPanels, panel => panel.Title == viewModel.SimulationSafetyExportBadgeText);
            Assert.Single(checkPanels, panel => panel.Title == viewModel.ToFullCoverageBadgeText);
            Assert.Single(VisibleText(window), text => text == viewModel.SimulationSafetyExportBadgeText);
            Assert.Single(VisibleText(window), text => text == viewModel.ToFullCoverageBadgeText);
            Assert.DoesNotContain(Assert.IsType<StackPanel>(shell.SummaryContent).GetVisualDescendants().OfType<CollapsiblePanel>(),
                panel => panel.Title == viewModel.SimulationSafetyExportBadgeText);
            Assert.Single(VisibleText(window), text => text == viewModel.HiddenSelectedRowsHintText);
            Assert.Single(VisibleText(window), text => text == viewModel.ExportSummaryText);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>(),
                text => text.Text == window.Title);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<SelectableTextBlock>(),
                text => text.Text == window.Title);
            var filters = Assert.IsType<StackPanel>(shell.SummaryContent);
            var selectedOnly = Assert.Single(filters.Children.OfType<ToggleButton>(),
                button => button.Command == viewModel.ToggleSelectedOnlyCommand);
            Assert.Equal("Selected only", selectedOnly.Content);
            Assert.Single(window.GetVisualDescendants().OfType<TextBox>(),
                box => box.Text == viewModel.SearchKeyword);
            Assert.DoesNotContain(VisibleText(window), text => text == viewModel.SearchFilterChipText);
            Assert.DoesNotContain(VisibleText(window), text => text == viewModel.ModeFilterChipText);
            Assert.DoesNotContain(VisibleText(window), text => text == viewModel.SelectionFilterChipText);
            Assert.Single(window.GetVisualDescendants().OfType<Button>(),
                button => button.Command == viewModel.ClearSearchKeywordCommand);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task PayloadStaysInInspectorAndLowFrequencyKeepsFlagsAndCommentAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var viewModel = CreateSelection();
        var window = new NotchExportSelectionWindow { DataContext = viewModel };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var inspector = Assert.IsType<StackPanel>(window.FindControl<StackPanel>("InspectorPanel"));
            var detail = Assert.IsType<CollapsiblePanel>(window.FindControl<CollapsiblePanel>("LowFrequencyDetail"));
            Assert.False(detail.IsExpanded);
            viewModel.ShowSelectedRowDetailSection = true;
            await HeadlessUiPump.FlushQueueAsync();
            var detailContent = Assert.IsType<Grid>(detail.Content);
            Assert.Single(inspector.GetVisualDescendants().OfType<SelectableTextBlock>(),
                text => text.Text == viewModel.SelectedRowPayloadCombineText);
            Assert.Single(inspector.GetVisualDescendants().OfType<SelectableTextBlock>(),
                text => text.Text == viewModel.SelectedRowPadRelationText);
            Assert.Equal(_lowFrequencyFields,
                detailContent.Children.OfType<TextBlock>().Where(static text => Grid.GetColumn(text) == 0)
                    .Select(static text => text.Text).ToArray());
            Assert.Equal(new[] { viewModel.SelectedRowFlagsText, viewModel.SelectedRowCommentText },
                detailContent.GetVisualDescendants().OfType<SelectableTextBlock>().Select(static text => text.Text).ToArray());
            Assert.DoesNotContain(VisibleText(window), static text => text is "Payload details" or "Column breakdown" or "Values / Comment");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task EmptyRowsShowInspectorGuidanceAndOneResetEntryAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var viewModel = CreateSelection();
        viewModel.SearchKeyword = "diff=999";
        var window = new NotchExportSelectionWindow { DataContext = viewModel };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Empty(viewModel.Rows);
            Assert.Null(viewModel.SelectedRow);
            Assert.Single(VisibleText(window), static text => text == "Select a row on the left to inspect it.");
            Assert.Single(VisibleText(window), static text => text == "No rows match the view filters.");
            Assert.Single(VisibleText(window), static text => text == "Use Reset filters above to show every row.");
            var reset = Assert.Single(window.GetVisualDescendants().OfType<Button>(),
                button => button.Content as string == "Reset filters");
            Assert.Same(viewModel.ClearSearchKeywordCommand, reset.Command);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task ExportIsOnlyPrimaryAndFooterOrdersCancelBeforeExportOnRightAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var viewModel = CreateSelection();
        var window = new NotchExportSelectionWindow { DataContext = viewModel };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var primary = Assert.Single(window.GetVisualDescendants().OfType<Button>(),
                static button => button.Classes.Contains("actionPrimary"));
            Assert.Equal("Export", primary.Content);
            var actions = Assert.IsType<StackPanel>(window.FindControl<StackPanel>("FooterActions"));
            Assert.Equal(HorizontalAlignment.Right, actions.HorizontalAlignment);
            Assert.Equal(_mainFooterButtons, actions.Children.OfType<Button>().Select(static button => button.Content));
            var replace = Assert.Single(window.GetVisualDescendants().OfType<Button>(),
                button => button.Command == viewModel.UseShownOnlyCommand);
            Assert.Equal(HorizontalAlignment.Right, replace.HorizontalAlignment);
            var copy = Assert.Single(window.GetVisualDescendants().OfType<Button>(),
                static button => button.Content is StackPanel content
                    && content.Children.OfType<TextBlock>().Any(static text => text.Text == "Copy payload"));
            Assert.Equal(HorizontalAlignment.Right, copy.HorizontalAlignment);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task HelperFootersAreRightAlignedAndApplyIsTheirOnlyPrimaryAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var helper = new NotchExportFilterBuilderWindow();
        var column = new NotchExportColumnFilterWindow { DataContext = CreateColumnFilter() };
        try
        {
            helper.Show();
            column.Show();
            await HeadlessUiPump.FlushQueueAsync();
            AssertFooter(helper, _helperFooterButtons);
            AssertFooter(column, _columnFooterButtons);
        }
        finally
        {
            column.Close();
            helper.Close();
        }
    }

    [AvaloniaFact]
    public async Task PartialSelectionAndInvalidRangeHaveVisibleFeedbackAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var selection = CreateSelection();
        selection.Rows[0].IsSelected = false;
        var window = new NotchExportSelectionWindow { DataContext = selection };
        var range = CreateColumnFilter();
        var column = new NotchExportColumnFilterWindow { DataContext = range };
        try
        {
            window.Show();
            column.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var header = Assert.Single(window.GetVisualDescendants().OfType<CheckBox>(),
                static box => AutomationProperties.GetName(box) == "Include all shown rows in export");
            Assert.Null(header.IsChecked);
            selection.VisibleRowsSelection = false;
            await HeadlessUiPump.FlushQueueAsync();
            Assert.True(header.IsChecked);
            Assert.All(selection.Rows, static row => Assert.True(row.IsSelected));
            var rows = Assert.Single(window.GetVisualDescendants().OfType<ListBox>(),
                static list => list.Classes.Contains("notchExportRows"));
            var selectedItem = Assert.Single(rows.GetVisualDescendants().OfType<ListBoxItem>(),
                static item => item.IsSelected);
            var check = Assert.Single(selectedItem.GetVisualDescendants().OfType<CheckBox>());
            Assert.True(Avalonia.Application.Current!.TryGetResource("Nvt.List.SelectedLabelBrush", check.ActualThemeVariant, out var label));
            Assert.Equal(Assert.IsAssignableFrom<ISolidColorBrush>(label).Color, Assert.IsAssignableFrom<ISolidColorBrush>(check.Foreground).Color);
            range.UseRangeFilter = true;
            range.RangeFromText = "9";
            range.RangeToText = "9x";
            await HeadlessUiPump.FlushQueueAsync();
            var apply = Assert.Single(column.GetVisualDescendants().OfType<Button>(),
                static button => button.Content as string == "Apply");
            Assert.False(apply.IsEnabled);
            Assert.Single(VisibleText(column), static text => text == "To must be a number");
            range.RangeToText = "12";
            await HeadlessUiPump.FlushQueueAsync();
            Assert.True(apply.IsEnabled);
            Assert.Equal(_expectedRangeKeys, range.GetSelectedKeys());
        }
        finally
        {
            column.Close();
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task ReopenedQueryRetainsMeaningAndPanelHeadersUseGhostPresenterAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var selection = CreateSelection();
        selection.SearchKeyword = "ic=1 diff=9";
        var helper = new NotchExportFilterBuilderWindow();
        helper.SetInitialQuery(selection.SearchKeyword);
        var window = new NotchExportSelectionWindow { DataContext = selection };
        try
        {
            helper.Show();
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal(selection.SearchKeyword, helper.GeneratedQuery);
            var expectedRows = selection.Rows.ToArray();
            selection.SearchKeyword = helper.GeneratedQuery;
            Assert.Equal(expectedRows, selection.Rows);
            var panel = Assert.Single(window.GetVisualDescendants().OfType<CollapsiblePanel>(),
                static item => item.Title == "Workspace locate");
            var header = Assert.Single(panel.GetVisualDescendants().OfType<ToggleButton>(),
                static button => button.Classes.Contains("panelBlockHeader"));
            Assert.True(header.IsChecked);
            Assert.All(header.GetVisualDescendants().OfType<ContentPresenter>(), static presenter =>
            {
                Assert.Null(presenter.Background);
                Assert.Null(presenter.BorderBrush);
            });
        }
        finally
        {
            window.Close();
            helper.Close();
        }
    }

    private static NotchExportSelectionViewModel CreateSelection() => new(new NotchTable(
    [
        new NotchTableRow(0, 9, 100, 200, new NotchV22Node(9, 95, 10, 5, 65535, 0, 0), "first transfer"),
        new NotchTableRow(1, 12, 101, 201, new NotchV22Node(12, 90, 13, 10, 65535, 0, 0), "second transfer"),
    ]));

    private static NotchExportColumnFilterDialogViewModel CreateColumnFilter() =>
        new(NotchExportColumnFilterField.Diff, "Diff",
        [
            new NotchExportColumnFilterOptionViewModel("9", "9", true),
            new NotchExportColumnFilterOptionViewModel("12", "12", true),
        ]);

    private static IEnumerable<string?> VisibleText(Control root) =>
        root.GetVisualDescendants().OfType<Control>().Where(static control => control.IsEffectivelyVisible)
            .Select(static control => control switch
            {
                SelectableTextBlock text => text.Text,
                TextBlock text => text.Text,
                _ => null,
            }).Where(static text => text is not null);

    private static void AssertFooter(Window window, string[] expected)
    {
        var apply = Assert.Single(window.GetVisualDescendants().OfType<Button>(),
            static button => button.Classes.Contains("actionPrimary"));
        Assert.Equal("Apply", apply.Content);
        var footer = Assert.IsType<Grid>(apply.Parent);
        Assert.Equal(HorizontalAlignment.Right, footer.HorizontalAlignment);
        Assert.Equal(expected, footer.Children.OfType<Button>().Select(static button => button.Content));
        Assert.True(Grid.GetColumn(footer.Children.OfType<Button>().Single(static button => button.Content as string == "Cancel"))
            < Grid.GetColumn(apply));
    }
}
