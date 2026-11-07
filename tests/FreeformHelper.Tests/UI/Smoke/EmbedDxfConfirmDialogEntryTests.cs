// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class EmbedDxfConfirmDialogEntryTests
{
    [AvaloniaFact]
    public async Task EmbedDxfConfirmation_EmbedReturnsTrueAndSkipReturnsFalseAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var viewModel = new FreeformHelperViewModel();
        var view = new FreeformHelperView { DataContext = viewModel };
        var owner = new Window { Content = view };
        try
        {
            owner.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var confirmEmbedDxfAsync = Assert.IsType<Func<Task<bool>>>(viewModel.ConfirmEmbedDxfAsync);

            Assert.True(await ChooseEmbedDxfAsync(owner, confirmEmbedDxfAsync, "Embed"));
            Assert.False(await ChooseEmbedDxfAsync(owner, confirmEmbedDxfAsync, "Skip"));
        }
        finally
        {
            foreach (var window in owner.OwnedWindows.ToArray())
            {
                window.Close();
            }

            owner.Close();
            await FreeformHelperViewModelTests.WaitForGridRebuildAsync(viewModel);
        }
    }

    private static async Task<bool> ChooseEmbedDxfAsync(Window owner, Func<Task<bool>> confirmEmbedDxfAsync, string buttonText)
    {
        var result = confirmEmbedDxfAsync();
        await HeadlessUiPump.WaitUntilAsync(
            () => owner.OwnedWindows.OfType<ConfirmDialog>().Any(static dialog => dialog.IsVisible),
            "The attached view did not open the Embed DXF confirmation dialog.");
        var dialog = Assert.Single(owner.OwnedWindows.OfType<ConfirmDialog>(), static window => window.IsVisible);
        await HeadlessUiPump.FlushQueueAsync();
        Assert.False(result.IsCompleted);
        var button = Assert.Single(dialog.GetVisualDescendants().OfType<Button>(),
            candidate => HasContentText(candidate.Content, buttonText));

        UiEventProbe.Click(button);
        await HeadlessUiPump.FlushQueueAsync();
        var confirmed = await result.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.False(dialog.IsVisible);
        Assert.DoesNotContain(dialog, owner.OwnedWindows);
        Assert.True(owner.IsVisible);
        Assert.True(owner.IsEnabled);
        return confirmed;
    }

    private static bool HasContentText(object? content, string text)
    {
        return content switch
        {
            string value => string.Equals(value, text, StringComparison.Ordinal),
            TextBlock block => string.Equals(block.Text, text, StringComparison.Ordinal),
            Control control => control.GetVisualDescendants().OfType<TextBlock>()
                .Any(block => string.Equals(block.Text, text, StringComparison.Ordinal)),
            _ => false,
        };
    }
}
