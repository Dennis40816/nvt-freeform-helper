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
using Nvt.Core.Avalonia.LogConsole;
using Nvt.Core.Avalonia.Panels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class WorkspaceRailAndConsoleTests
{
    [AvaloniaTheory]
    [InlineData(1024, false)]
    [InlineData(1366, false)]
    [InlineData(1367, true)]
    [InlineData(1920, true)]
    public async Task InitialPanelVisibilityUsesTheWindowWidthAsync(int width, bool expanded)
    {
        await using var fixture = await MainWindowGlobalInputFixture.CreateAsync(width: width, height: 768);
        var view = Workspace(fixture.Window);
        Assert.Equal(expanded, view.FindControl<Border>("LeftPanel")!.IsVisible);
        Assert.Equal(!expanded, Rail(view).IsEffectivelyVisible);
        Assert.True(view.FindControl<Border>("RightPanel")!.IsVisible);
        Assert.Equal(0, view.FindControl<ScrollViewer>("LeftSettingsScroll")!.Offset.Y);
    }

    [AvaloniaTheory]
    [InlineData(1366, true, 1920)]
    [InlineData(1920, false, 1366)]
    public async Task UserChoiceSurvivesResizeAndNavigationAsync(int initialWidth, bool show, int resizedWidth)
    {
        await using var fixture = await MainWindowGlobalInputFixture.CreateAsync(width: initialWidth);
        var view = Workspace(fixture.Window);
        var panel = view.FindControl<Border>("LeftPanel")!;
        var button = show ? Rail(view).FindControl<Button>("ExpandButton")! : HideButton(panel);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        fixture.Window.Width = resizedWidth;
        HeadlessUiPump.Drain(fixture.Window);
        Assert.Equal(show, panel.IsVisible);
        fixture.Window.Width = initialWidth;
        HeadlessUiPump.Drain(fixture.Window);
        Assert.Equal(show, panel.IsVisible);
        var shell = Assert.IsType<ShellViewModel>(fixture.Window.DataContext);
        shell.ShowHowToUseCommand.Execute(null);
        HeadlessUiPump.Drain(fixture.Window);
        shell.ShowWorkspaceCommand.Execute(null);
        HeadlessUiPump.Drain(fixture.Window);
        Assert.Equal(show, Workspace(fixture.Window).FindControl<Border>("LeftPanel")!.IsVisible);
    }

    [AvaloniaTheory]
    [InlineData("Dxf", "Import & DXF check")]
    [InlineData("Layers", "Layers")]
    [InlineData("Edits", "DXF edits")]
    public async Task RailShowsExpandsAndFocusesTheRequestedSectionAsync(string item, string title)
    {
        await using var fixture = await MainWindowGlobalInputFixture.CreateAsync(width: 1366, height: 768);
        var view = Workspace(fixture.Window);
        fixture.Helper.LayerToggles = new System.Collections.ObjectModel.ObservableCollection<LayerToggle>(
            Enumerable.Range(0, 40).Select(index => new LayerToggle($"Layer {index}")));
        RailButton(view, "Expand").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        HeadlessUiPump.Drain(fixture.Window);
        view.FindControl<ScrollViewer>("LeftSettingsScroll")!.ScrollToEnd();
        HeadlessUiPump.Drain(fixture.Window);
        HideButton(view.FindControl<Border>("LeftPanel")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        HeadlessUiPump.Drain(fixture.Window);
        var sections = view.FindControl<LeftDxfPanel>("LeftSections")!;
        var target = sections.GetVisualDescendants().OfType<CollapsiblePanel>().Single(panel => panel.Title == title);
        target.IsExpanded = false;
        var button = RailButton(view, item);
        Assert.True(button.Focus());
        Press(fixture.Window, Key.Space);
        HeadlessUiPump.Drain(fixture.Window);
        Assert.True(view.FindControl<Border>("LeftPanel")!.IsVisible);
        Assert.False(Rail(view).IsEffectivelyVisible);
        Assert.True(target.IsExpanded);
        var header = target.GetVisualDescendants().OfType<ToggleButton>().Single(control =>
            control.Classes.Contains("panelBlockHeader") && control.FindAncestorOfType<CollapsiblePanel>() == target);
        Assert.True(header.IsFocused);
        var viewport = view.FindControl<ScrollViewer>("LeftSettingsScroll")!;
        Assert.True(InWindow(viewport).Contains(InWindow(header)), $"{title}: {InWindow(header)} outside {InWindow(viewport)}");
    }

    [AvaloniaFact]
    public async Task KeyboardReachesEveryRailItemAndExpandRestoresThePanelAsync()
    {
        await using var fixture = await MainWindowGlobalInputFixture.CreateAsync(width: 1366);
        var view = Workspace(fixture.Window);
        var display = Assert.Single(view.GetVisualDescendants().OfType<WorkspaceHeader>()).FindControl<ToggleButton>("ViewMenuButton")!;
        Assert.True(display.Focus());
        Press(fixture.Window, Key.Tab);
        Assert.True(RailButton(view, "Expand").IsFocused);
        Press(fixture.Window, Key.Tab);
        Assert.True(RailButton(view, "Dxf").IsFocused);
        Press(fixture.Window, Key.Tab);
        Assert.True(RailButton(view, "Layers").IsFocused);
        Press(fixture.Window, Key.Tab);
        Assert.True(RailButton(view, "Edits").IsFocused);
        Assert.True(RailButton(view, "Expand").Focus());
        Press(fixture.Window, Key.Enter);
        HeadlessUiPump.Drain(fixture.Window);
        var panel = view.FindControl<Border>("LeftPanel")!;
        Assert.True(panel.IsVisible);
        Assert.True(HideButton(panel).IsFocused);
        Assert.False(Rail(view).IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task RailCountTracksVisibilityCollectionChangesAndReplacementAsync()
    {
        await using var fixture = await MainWindowGlobalInputFixture.CreateAsync(width: 1366);
        var count = Rail(Workspace(fixture.Window)).FindControl<TextBlock>("LayerCount")!;
        Assert.Equal("3 / 3", count.Text);
        fixture.Helper.LayerToggles[0].IsSelected = false;
        Assert.Equal("2 / 3", count.Text);
        fixture.Helper.LayerToggles = [new LayerToggle("one"), new LayerToggle("two", false)];
        Assert.Equal("1 / 2", count.Text);
        fixture.Helper.LayerToggles.RemoveAt(1);
        Assert.Equal("1 / 1", count.Text);
        fixture.Helper.LayerToggles.Clear();
        Assert.Equal("0 / 0", count.Text);
    }

    [AvaloniaTheory]
    [InlineData(1366)]
    [InlineData(1920)]
    public async Task CollapsedConsoleIsOneLineWithWorkingDisplayAndMoreMenusAsync(int width)
    {
        await using var fixture = await MainWindowGlobalInputFixture.CreateAsync(width: width, height: 768);
        var shell = Assert.IsType<ShellViewModel>(fixture.Window.DataContext);
        var view = Workspace(fixture.Window);
        var console = view.FindControl<ConsolePanel>("ConsolePanelHost")!;
        var header = Assert.Single(console.GetVisualDescendants().OfType<ConsoleHeader>());
        var toggle = console.FindControl<ToggleButton>("ConsoleToggleButton")!;
        var border = console.FindControl<Border>("ConsoleBorder")!;
        var grid = view.FindControl<Grid>("CanvasGrid")!;
        Assert.False(shell.IsConsoleExpanded);
        Assert.Equal(default, border.Padding);
        Assert.Equal(header.Bounds.Height + border.BorderThickness.Top, console.Bounds.Height, 1);
        Assert.Equal(0, grid.RowDefinitions[1].ActualHeight);
        Assert.Equal(InWindow(toggle).Center.Y, InWindow(header).Center.Y, 1);
        Assert.False(Assert.Single(header.GetVisualDescendants().OfType<TextBlock>(),
            text => text.Classes.Contains("nvtIcon") && text.Parent?.Parent is Grid).IsEffectivelyVisible);
        var display = header.FindControl<Button>("Display")!;
        var more = header.FindControl<Button>("More")!;
        Assert.Equal(InWindow(toggle).Center.Y, InWindow(display).Center.Y, 1);
        Assert.Equal(InWindow(display).Center.Y, InWindow(more).Center.Y, 1);
        Assert.True(InWindow(console).Contains(InWindow(more)));
        OpenMenuWithKeyboard(fixture.Window, display);
        var displayMenu = Assert.IsType<MenuFlyout>(display.Flyout);
        Assert.Same(shell.ConsoleSession.Controller.SetTimeModeCommand, Assert.IsType<MenuItem>(displayMenu.Items[0]).Command);
        displayMenu.Hide();
        HeadlessUiPump.Drain(fixture.Window);
        OpenMenuWithKeyboard(fixture.Window, more);
        var moreMenu = Assert.IsType<MenuFlyout>(more.Flyout);
        Assert.Same(shell.ConsoleSession.Controller.CopySelectionCommand, Assert.IsType<MenuItem>(moreMenu.Items[0]).Command);
        Assert.Same(shell.ConsoleSession.CopyVisibleCommand, Assert.IsType<MenuItem>(moreMenu.Items[1]).Command);
        Assert.Same(shell.ConsoleSession.SaveLogCommand, Assert.IsType<MenuItem>(moreMenu.Items[2]).Command);
        moreMenu.Hide();
        Assert.True(toggle.Focus());
        Press(fixture.Window, Key.Space);
        HeadlessUiPump.Drain(fixture.Window);
        Assert.True(shell.IsConsoleExpanded);
        Assert.True(Assert.Single(console.GetVisualDescendants().OfType<ConsoleToolbar>()).IsEffectivelyVisible);
        Assert.Equal((Avalonia.Thickness)console.FindResource("ConsolePanelPadding")!, border.Padding);
        Assert.Equal((double)console.FindResource("SplitterRowThickness")!, grid.RowDefinitions[1].ActualHeight);
    }

    private static void OpenMenuWithKeyboard(Window window, Button button)
    {
        Assert.True(button.Focus());
        Press(window, Key.Space);
        HeadlessUiPump.Drain(window);
        Assert.True(Assert.IsType<MenuFlyout>(button.Flyout).IsOpen);
    }

    private static void Press(TopLevel window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
        window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);
    }

    private static FreeformHelperView Workspace(Window window) => Assert.Single(window.GetVisualDescendants().OfType<FreeformHelperView>());
    private static LeftPanelRail Rail(FreeformHelperView view) => view.FindControl<LeftPanelRail>("LeftRail")!;
    private static Button RailButton(FreeformHelperView view, string item) =>
        Assert.Single(Rail(view).GetVisualDescendants().OfType<Button>(), button => Equals(button.Tag, item));
    private static Button HideButton(Border panel) =>
        Assert.Single(panel.GetVisualDescendants().OfType<Button>(), button => button.Classes.Contains("panelChromeToggle"));
    private static Avalonia.Rect InWindow(Control control) => new(
        Avalonia.VisualExtensions.TranslatePoint(control, default, TopLevel.GetTopLevel(control)!)!.Value, control.Bounds.Size);
}
