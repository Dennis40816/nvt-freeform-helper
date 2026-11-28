// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Nvt.Core.Avalonia.LogConsole;
using Nvt.Core.Avalonia.Panels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class WorkspacePaneCharacterizationTests
{
    [AvaloniaFact]
    public async Task LeftSectionsKeepTheirExistingActionsAndLayerBindingsAsync()
    {
        await using var fixture = await MainWindowGlobalInputFixture.CreateAsync(width: 1920);
        var left = Assert.Single(fixture.Window.GetVisualDescendants().OfType<LeftDxfPanel>());
        var sections = left.GetVisualDescendants().OfType<CollapsiblePanel>().ToArray();
        Assert.Equal(["Import & DXF check", "Layers", "DXF edits", "Advanced edit"], sections.Select(panel => panel.Title));
        sections[0].IsExpanded = true;
        HeadlessUiPump.Drain(fixture.Window);
        var check = Assert.Single(left.GetVisualDescendants().OfType<Button>(),
            button => ReferenceEquals(button.Command, fixture.Helper.CheckDxfQualityCommand));
        Assert.Same(fixture.Helper, check.DataContext);
        var layers = left.GetVisualDescendants().OfType<ItemsControl>()
            .Single(items => ReferenceEquals(items.ItemsSource, fixture.Helper.LayerToggles));
        Assert.Same(fixture.Helper.LayerToggles, layers.ItemsSource);
        var toggle = Assert.Single(left.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>(),
            button => button.DataContext is LayerToggle layer && layer.Name == "alpha");
        toggle.IsChecked = false;
        Assert.False(fixture.Helper.LayerToggles.Single(layer => layer.Name == "alpha").IsSelected);
    }

    [AvaloniaFact]
    public async Task ConsoleHeaderKeepsTheSessionAndAppCommandsWhenToggledAsync()
    {
        await using var fixture = await MainWindowGlobalInputFixture.CreateAsync();
        var shell = Assert.IsType<ShellViewModel>(fixture.Window.DataContext);
        var console = Assert.Single(fixture.Window.GetVisualDescendants().OfType<ConsolePanel>());
        var header = Assert.Single(console.GetVisualDescendants().OfType<ConsoleHeader>());
        Assert.Same(shell.ConsoleSession.Controller, header.Controller);
        Assert.Same(shell.ConsoleSession.Controller.CopySelectionCommand, header.CopySelectedCommand);
        Assert.Same(shell.ConsoleSession.CopyVisibleCommand, header.CopyVisibleCommand);
        Assert.Same(shell.ConsoleSession.SaveLogCommand, header.SaveLogCommand);
        var toggle = console.FindControl<Avalonia.Controls.Primitives.ToggleButton>("ConsoleToggleButton")!;
        Assert.False(shell.IsConsoleExpanded);
        toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        toggle.IsChecked = true;
        HeadlessUiPump.Drain(fixture.Window);
        Assert.True(shell.IsConsoleExpanded);
        Assert.True(Assert.Single(console.GetVisualDescendants().OfType<ConsoleToolbar>()).IsEffectivelyVisible);
        shell.IsConsoleExpanded = false;
        HeadlessUiPump.Drain(fixture.Window);
        Assert.False(toggle.IsChecked);
        Assert.False(Assert.Single(console.GetVisualDescendants().OfType<ConsoleToolbar>()).IsEffectivelyVisible);
    }
}
