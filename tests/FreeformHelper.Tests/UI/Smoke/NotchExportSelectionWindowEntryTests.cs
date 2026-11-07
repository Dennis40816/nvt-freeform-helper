// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchExportSelectionWindowEntryTests
{
    [AvaloniaFact]
    public async Task CancelButtonCompletesOnceWithFalseAndReturnsNoTableAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var probe = new UiEventProbe();
        var helper = new FreeformHelperViewModel(null, probe.Runner);
        var view = new FreeformHelperView { DataContext = helper };
        var owner = new Window { Content = view };
        var viewModel = CreateViewModel(probe);
        var completions = new List<bool>();
        try
        {
            owner.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var openSelectionAsync = helper.OpenNotchExportSelectionAsync;
            Assert.NotNull(openSelectionAsync);
            var result = openSelectionAsync(viewModel);
            var window = Assert.Single(owner.OwnedWindows.OfType<NotchExportSelectionWindow>());
            window.CompletionRequested += (_, confirmed) => completions.Add(confirmed);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.True(viewModel.HasSelection);

            await ClickAsync(FindContentButton(window, "Cancel"));

            Assert.Null(await result.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
            Assert.False(Assert.Single(completions));
            Assert.False(window.IsVisible);
            Assert.True(owner.IsVisible);
            Assert.Empty(probe.Reports);
            Assert.Empty(probe.FallbackReports);
        }
        finally
        {
            CloseWindows(owner);
            await helper.WaitForGridRebuildIdleAsync().WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        }
    }

    [AvaloniaFact]
    public async Task ExportButtonCompletesOnceWithTrueAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var probe = new UiEventProbe();
        var viewModel = CreateViewModel(probe);
        var window = new NotchExportSelectionWindow { DataContext = viewModel };
        var completions = new List<bool>();
        window.CompletionRequested += (_, confirmed) => completions.Add(confirmed);
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            Assert.False(viewModel.TryGetExportBlockMessage(out _, out _));

            await ClickAsync(FindContentButton(window, "Export"));

            Assert.True(Assert.Single(completions));
            Assert.False(window.IsVisible);
            Assert.Empty(probe.Reports);
            Assert.Empty(probe.FallbackReports);
        }
        finally
        {
            CloseWindows(window);
        }
    }

    [AvaloniaFact]
    public async Task BlockedExportShowsOwnedWarningAndKeepsWindowOpenAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var probe = new UiEventProbe();
        var viewModel = CreateViewModel(probe);
        NotchApplySimulationDiffCell[] cells =
        [
            new(RegularPadId: 100, RegularRow: 0, RegularCol: 0, IcIndex: 0, DiffIndex: 10,
                BeforeValue: 400d, AfterValue: 500d),
        ];
        viewModel.AttachSimulationSafetyAudit(SimulationSafetyAuditService.Analyze(cells, afterCap: 480d));
        var window = new NotchExportSelectionWindow { DataContext = viewModel };
        var completions = new List<bool>();
        window.CompletionRequested += (_, confirmed) => completions.Add(confirmed);
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            Assert.True(viewModel.TryGetExportBlockMessage(out var title, out var message));

            await ClickAsync(FindContentButton(window, "Export"));

            var dialog = Assert.Single(window.OwnedWindows.OfType<WarningDialog>());
            Assert.Same(window, dialog.Owner);
            Assert.True(dialog.IsVisible);
            Assert.Empty(completions);
            Assert.Equal(title, dialog.FindControl<SelectableTextBlock>("TitleText")?.Text);
            Assert.Equal(message, dialog.FindControl<SelectableTextBlock>("MessageText")?.Text);

            await ClickAsync(FindContentButton(dialog, "OK"));

            Assert.False(dialog.IsVisible);
            Assert.Empty(window.OwnedWindows);
            Assert.True(window.IsVisible);
            Assert.True(window.IsEnabled);
            Assert.Empty(completions);
            Assert.Empty(probe.Reports);
            Assert.Empty(probe.FallbackReports);
        }
        finally
        {
            CloseWindows(window);
        }
    }

    [AvaloniaFact]
    public async Task CopyPayloadButtonWritesSelectedRowToClipboardAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var probe = new UiEventProbe();
        var viewModel = CreateViewModel(probe);
        var window = new NotchExportSelectionWindow { DataContext = viewModel };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            viewModel.SelectPreviewRowCommand.Execute(viewModel.Rows[1]);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Same(viewModel.Rows[1], viewModel.SelectedRow);
            var expected = viewModel.SelectedRowCodePreviewText;
            Assert.False(string.IsNullOrWhiteSpace(expected));
            var clipboard = TopLevel.GetTopLevel(window)!.Clipboard;
            Assert.NotNull(clipboard);
            var previousText = await clipboard.TryGetTextAsync();
            try
            {
                await clipboard.SetTextAsync("Before copying payload");

                await ClickAsync(FindTooltipButton(window, "Copy payload"));

                Assert.Equal(expected, await clipboard.TryGetTextAsync());
                Assert.True(window.IsVisible);
                Assert.Empty(probe.Reports);
                Assert.Empty(probe.FallbackReports);
            }
            finally
            {
                await clipboard.SetTextAsync(previousText ?? string.Empty);
            }
        }
        finally
        {
            CloseWindows(window);
        }
    }

    [AvaloniaFact]
    public async Task FilterBuilderCancelKeepsExportWindowOpenAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var probe = new UiEventProbe();
        var viewModel = CreateViewModel(probe);
        viewModel.SearchKeyword = "ic=1";
        var window = new NotchExportSelectionWindow { DataContext = viewModel };
        var completions = new List<bool>();
        window.CompletionRequested += (_, confirmed) => completions.Add(confirmed);
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();

            await ClickAsync(FindTooltipButton(window, "Filter helper"));

            var builder = Assert.Single(window.OwnedWindows.OfType<NotchExportFilterBuilderWindow>());
            Assert.Same(window, builder.Owner);
            Assert.True(builder.IsVisible);
            var fuzzy = Assert.IsType<TextBox>(builder.FindControl<TextBox>("FuzzyTextBox"));
            Assert.Equal("ic=1", fuzzy.Text);
            fuzzy.Text = "discard this edit";

            await ClickAsync(FindContentButton(builder, "Cancel"));

            Assert.False(builder.IsVisible);
            Assert.Empty(window.OwnedWindows);
            Assert.True(window.IsVisible);
            Assert.True(window.IsEnabled);
            Assert.Equal("ic=1", viewModel.SearchKeyword);
            Assert.Empty(completions);
            Assert.Empty(probe.Reports);
            Assert.Empty(probe.FallbackReports);
        }
        finally
        {
            CloseWindows(window);
        }
    }

    private static NotchExportSelectionViewModel CreateViewModel(UiEventProbe probe)
    {
        NotchTableRow[] rows =
        [
            new(0, 10, 100, 200, new NotchV22Node(10, 95, 11, 5, 65535, 0, 0), "first transfer"),
            new(0, 11, 101, 201, new NotchV22Node(11, 90, 12, 10, 65535, 0, 0), "second transfer"),
        ];
        return new NotchExportSelectionViewModel(new NotchTable(rows)) { UiEvents = probe.Runner };
    }

    private static Button FindContentButton(Window window, string content) =>
        Assert.Single(window.GetVisualDescendants().OfType<Button>(), button => button.Content is string text && text == content);

    private static Button FindTooltipButton(Window window, string tooltip) =>
        Assert.Single(window.GetVisualDescendants().OfType<Button>(), button => ToolTip.GetTip(button) switch
        {
            TextBlock text => text.Text == tooltip,
            string text => text == tooltip,
            _ => false,
        });

    private static async Task ClickAsync(Button button)
    {
        Assert.True(button.IsEffectivelyEnabled);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await HeadlessUiPump.FlushQueueAsync();
    }

    private static void CloseWindows(Window window)
    {
        foreach (var child in window.OwnedWindows.ToArray())
        {
            CloseWindows(child);
        }

        window.Close();
    }
}
