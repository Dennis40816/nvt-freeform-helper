// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchExportFilterBuilderWindowEntryTests
{
    private const string TextQuery = "ic=1 diff=20 reg=4820 cad=3777 a=20 \"transfer row\"";
    private const string NoOpQuery = "ic=1 diff=20 reg=4820 cad=3777 a=20 status=noop \"transfer row\"";
    private static readonly string[] _inputNames =
        ["IcTextBox", "DiffTextBox", "RegTextBox", "CadTextBox", "AnchorTextBox", "FuzzyTextBox"];

    [AvaloniaFact]
    public async Task TypingAndStatusSelection_UpdateGeneratedQueryAndPreviewAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var owner = new Window();
        var window = new NotchExportFilterBuilderWindow();
        try
        {
            owner.Show();
            window.Show(owner);
            await HeadlessUiPump.FlushQueueAsync();

            FillTextInputs(window);
            await HeadlessUiPump.FlushQueueAsync();

            Assert.Equal(TextQuery, window.GeneratedQuery);
            Assert.Equal(TextQuery, FindTextBox(window, "PreviewTextBox").Text);

            FindStatusComboBox(window).SelectedIndex = 2;
            await HeadlessUiPump.FlushQueueAsync();

            Assert.Equal(NoOpQuery, window.GeneratedQuery);
            Assert.Equal(NoOpQuery, FindTextBox(window, "PreviewTextBox").Text);
            Assert.True(window.IsVisible);
        }
        finally
        {
            window.Close();
            CloseWindows(owner);
        }
    }

    [AvaloniaFact]
    public async Task Apply_ReturnsTrueWithBuiltQueryAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var owner = new Window();
        var window = new NotchExportFilterBuilderWindow();
        try
        {
            owner.Show();
            var result = window.ShowDialog<bool>(owner);
            await HeadlessUiPump.FlushQueueAsync();
            FillTextInputs(window);
            FindStatusComboBox(window).SelectedIndex = 2;
            await HeadlessUiPump.FlushQueueAsync();

            await ClickButtonAsync(window, "Apply");

            Assert.True(await result.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
            Assert.Equal(NoOpQuery, window.GeneratedQuery);
            Assert.False(window.IsVisible);
            Assert.DoesNotContain(window, owner.OwnedWindows);
            Assert.True(owner.IsEnabled);
        }
        finally
        {
            window.Close();
            CloseWindows(owner);
        }
    }

    [AvaloniaFact]
    public async Task Clear_EmptiesInputsAndQueryAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var owner = new Window();
        var window = new NotchExportFilterBuilderWindow();
        try
        {
            owner.Show();
            window.Show(owner);
            await HeadlessUiPump.FlushQueueAsync();
            FillTextInputs(window);
            FindStatusComboBox(window).SelectedIndex = 2;
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal(NoOpQuery, window.GeneratedQuery);

            await ClickButtonAsync(window, "Clear");

            Assert.All(_inputNames, name => Assert.Equal(string.Empty, FindTextBox(window, name).Text));
            Assert.Equal(0, FindStatusComboBox(window).SelectedIndex);
            Assert.Equal(string.Empty, window.GeneratedQuery);
            Assert.Equal("(empty)", FindTextBox(window, "PreviewTextBox").Text);
            Assert.True(window.IsVisible);
        }
        finally
        {
            window.Close();
            CloseWindows(owner);
        }
    }

    [AvaloniaFact]
    public async Task Cancel_ReturnsFalseAndPreservesOwnerQueryAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var probe = new UiEventProbe();
        var selection = CreateSelection(probe);
        var startingRows = selection.Rows.ToArray();
        var startingQuery = selection.SearchKeyword;
        var owner = new NotchExportSelectionWindow { DataContext = selection };
        var window = new NotchExportFilterBuilderWindow();
        window.SetInitialQuery(startingQuery);
        try
        {
            owner.Show();
            var result = window.ShowDialog<bool>(owner);
            await HeadlessUiPump.FlushQueueAsync();
            FindTextBox(window, "FuzzyTextBox").Text = "diff=11";
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal("diff=11", window.GeneratedQuery);
            await ClickButtonAsync(window, "Cancel");

            Assert.False(await result.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
            Assert.False(window.IsVisible);

            // Exercise the real caller so Cancel cannot replace the existing search filter.
            var helperButton = Assert.Single(owner.GetVisualDescendants().OfType<Button>(),
                static button => AutomationProperties.GetName(button) == "Filter helper");
            helperButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await HeadlessUiPump.FlushQueueAsync();
            var opened = Assert.Single(owner.OwnedWindows.OfType<NotchExportFilterBuilderWindow>());
            Assert.Equal(startingQuery, opened.GeneratedQuery);
            FindTextBox(opened, "FuzzyTextBox").Text = "diff=11";
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal("diff=11", opened.GeneratedQuery);
            await ClickButtonAsync(opened, "Cancel");

            Assert.False(opened.IsVisible);
            Assert.Empty(owner.OwnedWindows);
            Assert.Equal(startingQuery, selection.SearchKeyword);
            Assert.Equal(startingRows, selection.Rows.ToArray());
            Assert.True(owner.IsEnabled);
            Assert.Empty(probe.Reports);
            Assert.Empty(probe.FallbackReports);
        }
        finally
        {
            window.Close();
            CloseWindows(owner);
        }
    }

    private static NotchExportSelectionViewModel CreateSelection(UiEventProbe probe)
    {
        var table = new NotchTable(
        [
            new NotchTableRow(0, 9, 100, 200, new NotchV22Node(9, 100, 10, 25, 65535, 0, 0), "transfer-a"),
            new NotchTableRow(0, 11, 101, 201, new NotchV22Node(11, 100, 12, 25, 65535, 0, 0), "transfer-b"),
        ]);
        var selection = new NotchExportSelectionViewModel(table) { UiEvents = probe.Runner, SearchKeyword = "diff=9" };
        Assert.Single(selection.Rows);
        return selection;
    }

    private static void FillTextInputs(NotchExportFilterBuilderWindow window)
    {
        FindTextBox(window, "IcTextBox").Text = " 1 ";
        FindTextBox(window, "DiffTextBox").Text = " 20 ";
        FindTextBox(window, "RegTextBox").Text = " 4820 ";
        FindTextBox(window, "CadTextBox").Text = " 3777 ";
        FindTextBox(window, "AnchorTextBox").Text = " 20 ";
        FindTextBox(window, "FuzzyTextBox").Text = " transfer row ";
    }

    private static TextBox FindTextBox(Window window, string name)
    {
        return Assert.IsType<TextBox>(window.FindControl<TextBox>(name));
    }

    private static ComboBox FindStatusComboBox(Window window)
    {
        return Assert.IsType<ComboBox>(window.FindControl<ComboBox>("StatusComboBox"));
    }

    private static async Task ClickButtonAsync(Control root, string content)
    {
        var button = Assert.Single(root.GetVisualDescendants().OfType<Button>(),
            button => button.Content is string text && text == content);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await HeadlessUiPump.FlushQueueAsync();
    }

    private static void CloseWindows(Window owner)
    {
        foreach (var window in owner.OwnedWindows.ToArray())
        {
            window.Close();
        }

        owner.Close();
    }
}
