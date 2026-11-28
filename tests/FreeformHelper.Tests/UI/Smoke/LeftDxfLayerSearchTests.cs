// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class LeftDxfLayerSearchTests
{
    [AvaloniaFact]
    public async Task TypingFiltersAndHighlightsWithoutChangingLayerSelectionAsync()
    {
        using var files = new LoadingModeTestScope();
        var vm = await DxfCharacterizationWorkspace.OpenAsync(files);
        var view = new LeftDxfPanel { DataContext = vm };
        var window = new Window { Width = 600, Height = 1000, Content = view };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var selection = vm.LayerToggles.Select(static layer => (layer.Name, layer.IsSelected)).ToArray();

            SearchBox(view).Focus();
            window.KeyTextInput("ALP");
            await HeadlessUiPump.FlushQueueAsync();

            Assert.True(Row(view, "alpha").IsEffectivelyVisible);
            Assert.False(Row(view, "beta").IsVisible);
            var highlight = Assert.Single(Row(view, "alpha").GetVisualDescendants().OfType<HighlightedTextBlock>());
            var range = Assert.Single(highlight.Ranges);
            Assert.Equal((0, 3), (range.Start, range.Length));
            Assert.False(Hint(view).IsVisible);
            Assert.Equal(selection, vm.LayerToggles.Select(static layer => (layer.Name, layer.IsSelected)).ToArray());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task NoMatchShowsTheHintAndTheClearButtonRestoresEveryLayerAsync()
    {
        using var files = new LoadingModeTestScope();
        var vm = await DxfCharacterizationWorkspace.OpenAsync(files);
        var view = new LeftDxfPanel { DataContext = vm };
        var window = new Window { Width = 600, Height = 1000, Content = view };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            Assert.False(Hint(view).IsVisible);

            SearchBox(view).Text = "missing-layer";
            await HeadlessUiPump.FlushQueueAsync();
            Assert.True(Hint(view).IsVisible);
            Assert.False(Row(view, "alpha").IsVisible);
            Assert.False(Row(view, "beta").IsVisible);

            var clear = Search(view).FindControl<Button>("ClearSearchButton")!;
            clear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await HeadlessUiPump.FlushQueueAsync();

            Assert.Equal(string.Empty, SearchBox(view).Text);
            Assert.False(Hint(view).IsVisible);
            Assert.True(Row(view, "alpha").IsVisible);
            Assert.True(Row(view, "beta").IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task EscapeInTheSearchBoxClearsTheQueryAsync()
    {
        using var files = new LoadingModeTestScope();
        var vm = await DxfCharacterizationWorkspace.OpenAsync(files);
        var view = new LeftDxfPanel { DataContext = vm };
        var window = new Window { Width = 600, Height = 1000, Content = view };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            SearchBox(view).Focus();
            window.KeyTextInput("beta");
            await HeadlessUiPump.FlushQueueAsync();
            Assert.False(Row(view, "alpha").IsVisible);

            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            await HeadlessUiPump.FlushQueueAsync();

            Assert.Equal(string.Empty, SearchBox(view).Text);
            Assert.True(Row(view, "alpha").IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    private static LayerSearchBox Search(LeftDxfPanel view) =>
        Assert.Single(view.GetVisualDescendants().OfType<LayerSearchBox>());

    private static TextBox SearchBox(LeftDxfPanel view) => Search(view).FindControl<TextBox>("SearchBox")!;

    private static ToggleButton Row(LeftDxfPanel view, string layer) =>
        Assert.Single(view.GetVisualDescendants().OfType<ToggleButton>(),
            button => button.Classes.Contains("layerToggle") &&
                button.DataContext is LayerToggle toggle && toggle.Name == layer);

    private static SelectableTextBlock Hint(LeftDxfPanel view) =>
        view.FindControl<SelectableTextBlock>("NoLayerMatchHint")!;
}
