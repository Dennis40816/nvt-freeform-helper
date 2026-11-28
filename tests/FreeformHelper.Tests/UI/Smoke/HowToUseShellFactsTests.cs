// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class HowToUseShellFactsTests
{
    [AvaloniaFact]
    public async Task DocumentedTabsMatchTheReleaseShellAsync()
    {
        await using var workspace = await MainWindowGlobalInputFixture.CreateAsync();
        var window = workspace.Window;
        window.RequestedThemeVariant = ThemeVariant.Dark;
        var tabs = window.GetVisualDescendants().OfType<ToggleButton>().Where(static button =>
            button.Classes.Contains("shellTab") && button.IsEffectivelyVisible)
            .Select(static button => Assert.IsType<string>(button.Content)).ToArray();
        var shell = Assert.IsType<ShellViewModel>(window.DataContext);
        shell.ShowHowToUseCommand.Execute(null);
        await HeadlessUiPump.FlushQueueAsync();
        var view = Assert.Single(window.GetVisualDescendants().OfType<HowToUseView>());
        var groups = view.GetVisualDescendants().OfType<HidePanelBlock>().ToArray();
        Assert.True(Assert.Single(groups, static group => !group.IsCollapsible).IsExpanded);
        Assert.All(groups.Where(static group => group.IsCollapsible), static group => Assert.False(group.IsExpanded));
        foreach (var group in groups)
            group.IsExpanded = true;
        await HeadlessUiPump.FlushQueueAsync();
        var tabLine = Assert.Single(view.GetVisualDescendants().OfType<SelectableTextBlock>(), static block =>
            block.Text?.StartsWith("- Top tabs:", StringComparison.Ordinal) == true);
        Assert.Equal($"- Top tabs: {string.Join(", ", tabs)}.", tabLine.Text);
        var figures = view.GetVisualDescendants().OfType<Image>().ToArray();
        Assert.Equal(3, figures.Length);
        Assert.All(figures, static figure => Assert.NotNull(figure.Source));
    }
}
