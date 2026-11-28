// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class WorkspaceTopBarTests
{
    [AvaloniaTheory]
    [InlineData(1024, 768)]
    [InlineData(1366, 768)]
    [InlineData(1920, 1080)]
    public async Task BothRowsStaySingleLineWithLongStatusAndWarningAsync(int width, int height)
    {
        await using var fixture = await MainWindowGlobalInputFixture.CreateAsync(devPageAvailable: true);
        fixture.Window.Width = width;
        fixture.Window.Height = height;
        fixture.Helper.StatusText = new string('W', 500) + "\nSecond status line";
        fixture.Helper.ManualRangeError = "Invalid row range.";
        HeadlessUiPump.Drain(fixture.Window);
        var row = fixture.Window.FindControl<Grid>("ShellHeaderRow")!;
        var menu = fixture.Window.FindControl<Menu>("MainMenu")!;
        var tabs = row.GetVisualDescendants().OfType<ToggleButton>().Where(b => b.IsEffectivelyVisible).ToArray();
        Assert.NotEmpty(tabs);
        Assert.All(tabs, tab => Assert.Contains("marked", tab.Classes));
        Assert.Single(tabs, tab => tab.IsChecked == true);
        Assert.All(tabs, tab => AssertInside(tab, row));
        AssertInside(menu, row);
        Assert.True(tabs.Max(tab => InWindow(tab).Right) <= InWindow(menu).Left);
        Assert.All(tabs, tab => Assert.Equal(InWindow(tabs[0]).Center.Y, InWindow(tab).Center.Y, 1));
        var header = Header(fixture.Window);
        var status = header.FindControl<SelectableTextBlock>("StatusMessage")!;
        var counts = header.FindControl<SelectableTextBlock>("SelectionCounts")!;
        var warning = header.FindControl<SelectableTextBlock>("RangeWarning")!;
        Assert.Equal(fixture.Helper.StatusDisplayText, status.Text);
        Assert.Equal(status.Text, Assert.IsType<TextBlock>(ToolTip.GetTip(status)).Text);
        Assert.True(Assert.Single(status.TextLayout.TextLines).HasCollapsed);
        Assert.Equal(Avalonia.Media.TextTrimming.CharacterEllipsis, status.TextTrimming);
        Assert.True(warning.IsEffectivelyVisible);
        Assert.Equal(fixture.Helper.ManualRangeError, warning.Text);
        Assert.Equal("Selected: CAD 0 · Regular 0", counts.Text);
        AssertInside(counts, header);
        AssertInside(warning, header);
        AssertInside(header.FindControl<ToggleButton>("RangeMenuButton")!, header);
        AssertInside(header.FindControl<ToggleButton>("ViewMenuButton")!, header);
        var band = Assert.IsType<Border>(header.Content);
        var display = header.FindControl<ToggleButton>("ViewMenuButton")!;
        Assert.Equal(display.Bounds.Height + band.Padding.Top + band.Padding.Bottom +
            band.BorderThickness.Top + band.BorderThickness.Bottom, header.Bounds.Height, 1);
        Assert.Equal(tabs.Max(tab => tab.Bounds.Height), row.Bounds.Height, 1);
        Assert.InRange(InWindow(status).Center.Y, InWindow(counts).Top, InWindow(counts).Bottom);
        Assert.Equal(InWindow(Assert.IsType<Border>(row.Parent)).Bottom, InWindow(header).Top, 1);
        ToolTip.SetIsOpen(status, true);
        HeadlessUiPump.Drain(fixture.Window);
        Assert.True(ToolTip.GetIsOpen(status));
        Assert.Equal(status.Text, Assert.IsType<TextBlock>(ToolTip.GetTip(status)).Text);
        ToolTip.SetIsOpen(status, false);
    }

    [AvaloniaTheory]
    [InlineData(ShellPage.HowToUse)]
    [InlineData(ShellPage.Dev)]
    [InlineData(ShellPage.Simulation)]
    [InlineData(ShellPage.Coordinate)]
    public async Task MenusHideOutsideTheWorkspaceAndReturnWithTheirBindingsAsync(ShellPage page)
    {
        await using var fixture = await MainWindowGlobalInputFixture.CreateAsync(devPageAvailable: true);
        var shell = Assert.IsType<ShellViewModel>(fixture.Window.DataContext);
        shell.Simulation.RequestBuildWorkspaceAsync = null;
        shell.CoordinatePlanner.RequestBuildWorkspaceAsync = null;
        var menu = fixture.Window.FindControl<Menu>("MainMenu")!;
        await ShellViewModelNavigationTests.ShowPageAsync(shell, page);
        HeadlessUiPump.Drain(fixture.Window);
        Assert.False(menu.IsEffectivelyVisible);
        shell.ShowWorkspaceCommand.Execute(null);
        HeadlessUiPump.Drain(fixture.Window);
        Assert.True(menu.IsEffectivelyVisible);
        Assert.Same(fixture.Helper, menu.DataContext);
        Assert.Same(fixture.Helper.SaveProjectCommand, Assert.IsType<MenuItem>(Assert.IsType<MenuItem>(menu.Items[0]).Items[1]).Command);
    }

    [AvaloniaFact]
    public async Task RangeKeyboardOpensAnOverlayAndInputsStillSelectPadsAsync()
    {
        await using var fixture = await MainWindowGlobalInputFixture.CreateAsync();
        var header = Header(fixture.Window);
        var button = header.FindControl<ToggleButton>("RangeMenuButton")!;
        var popup = header.FindControl<Popup>("RangeMenuPopup")!;
        var canvas = Assert.Single(fixture.Window.GetVisualDescendants().OfType<PadCanvas>());
        var before = InWindow(canvas);
        Assert.False(popup.IsOpen);
        Assert.DoesNotContain("active", button.Classes);
        Assert.True(button.Focus());
        Press(fixture.Window, Key.Space);
        HeadlessUiPump.Drain(fixture.Window);
        Assert.True(popup.IsOpen);
        Assert.Contains("active", button.Classes);
        Assert.Equal(before, InWindow(canvas));
        var rows = header.FindControl<TextBox>("ManualRowsInput")!;
        var cols = header.FindControl<TextBox>("ManualColsInput")!;
        Assert.True(rows.IsFocused);
        TabTo(TopLevel.GetTopLevel(rows)!, cols);
        Assert.True(cols.IsFocused);
        rows.Text = "0";
        cols.Text = "1-2";
        HeadlessUiPump.Drain(fixture.Window);
        Assert.Equal([1, 2], fixture.Helper.SelectedRegularPadIndices.Order());
        Assert.Equal("Selected: CAD 0 · Regular 2", header.FindControl<SelectableTextBlock>("SelectionCounts")!.Text);
        rows.Text = "bad";
        HeadlessUiPump.Drain(fixture.Window);
        Assert.True(fixture.Helper.HasManualRangeError);
        Press(TopLevel.GetTopLevel(cols)!, Key.Escape);
        HeadlessUiPump.Drain(fixture.Window);
        Assert.False(popup.IsOpen);
        Assert.DoesNotContain("active", button.Classes);
        Assert.True(button.IsFocused);
        Assert.True(header.FindControl<SelectableTextBlock>("RangeWarning")!.IsEffectivelyVisible);
        Assert.Equal(before, InWindow(canvas));
    }

    [AvaloniaFact]
    public async Task KeyboardReachesTabsMenusRangeAndDisplayAsync()
    {
        await using var fixture = await MainWindowGlobalInputFixture.CreateAsync(devPageAvailable: true);
        var row = fixture.Window.FindControl<Grid>("ShellHeaderRow")!;
        var tabs = row.GetVisualDescendants().OfType<ToggleButton>().Where(b => b.IsEffectivelyVisible).ToArray();
        Assert.True(tabs[0].Focus());
        Assert.All(tabs.Skip(1), tab => { Press(fixture.Window, Key.Tab); Assert.True(tab.IsFocused); });
        var menu = fixture.Window.FindControl<Menu>("MainMenu")!;
        Press(fixture.Window, Key.Tab);
        var file = Assert.IsType<MenuItem>(menu.Items[0]);
        Assert.True(file.IsFocused);
        AssertMenuKeyboard(fixture.Window, file);
        Press(fixture.Window, Key.Right);
        var export = Assert.IsType<MenuItem>(menu.Items[1]);
        Assert.True(export.IsFocused);
        AssertMenuKeyboard(fixture.Window, export);
        Press(fixture.Window, Key.Right);
        var settings = Assert.IsType<MenuItem>(menu.Items[2]);
        Assert.True(settings.IsFocused);
        Press(fixture.Window, Key.Down);
        Press(fixture.Window, Key.Enter);
        HeadlessUiPump.Drain(fixture.Window);
        var settingsWindow = Assert.Single(fixture.Window.OwnedWindows.OfType<SettingsWindow>());
        Assert.True(settingsWindow.IsVisible);
        settingsWindow.Close();
        fixture.Window.Activate();
        var header = Header(fixture.Window);
        Assert.True(settings.Focus());
        TabTo(fixture.Window, header.FindControl<ToggleButton>("RangeMenuButton")!);
        Press(fixture.Window, Key.Tab);
        var display = header.FindControl<ToggleButton>("ViewMenuButton")!;
        Assert.True(display.IsFocused);
        var canvas = Assert.Single(fixture.Window.GetVisualDescendants().OfType<PadCanvas>());
        var before = InWindow(canvas);
        Press(fixture.Window, Key.Space);
        HeadlessUiPump.Drain(fixture.Window);
        var popup = header.FindControl<Popup>("ViewMenuPopup")!;
        Assert.True(popup.IsOpen);
        Assert.Contains("active", display.Classes);
        var focused = Assert.Single(popup.Child!.GetVisualDescendants().OfType<ToggleSwitch>(), toggle => toggle.IsFocused);
        Assert.Equal(before, InWindow(canvas));
        Press(TopLevel.GetTopLevel(focused)!, Key.Escape);
        Assert.False(popup.IsOpen);
        Assert.DoesNotContain("active", display.Classes);
        Assert.True(display.IsFocused);
    }

    [AvaloniaFact]
    public async Task EscapeInThePinnedDisplayClosesDisplayAndKeepsRangeAsync()
    {
        await using var fixture = await MainWindowGlobalInputFixture.CreateAsync();
        var header = Header(fixture.Window);
        var display = header.FindControl<ToggleButton>("ViewMenuButton")!;
        var displayPopup = header.FindControl<Popup>("ViewMenuPopup")!;
        var rangePopup = header.FindControl<Popup>("RangeMenuPopup")!;
        Assert.True(display.Focus());
        Press(fixture.Window, Key.Space);
        HeadlessUiPump.Drain(fixture.Window);
        header.FindControl<ToggleButton>("RangeMenuButton")!.IsChecked = true;
        HeadlessUiPump.Drain(fixture.Window);
        var toggle = displayPopup.Child!.GetVisualDescendants().OfType<ToggleSwitch>().First();
        Assert.True(toggle.Focus());
        Assert.True(rangePopup.IsOpen);

        Press(TopLevel.GetTopLevel(toggle)!, Key.Escape);
        HeadlessUiPump.Drain(fixture.Window);

        Assert.False(displayPopup.IsOpen);
        Assert.True(rangePopup.IsOpen);
        Assert.True(display.IsFocused);
    }

    [AvaloniaTheory]
    [InlineData("ViewMenuButton", "ViewMenuPopup")]
    [InlineData("RangeMenuButton", "RangeMenuPopup")]
    public async Task EscapeOnTheHeaderButtonClosesItsOpenPopupAsync(string buttonName, string popupName)
    {
        await using var fixture = await MainWindowGlobalInputFixture.CreateAsync();
        var header = Header(fixture.Window);
        var button = header.FindControl<ToggleButton>(buttonName)!;
        var popup = header.FindControl<Popup>(popupName)!;
        // Open while the focus is elsewhere, as a hover does, then reach the button.
        button.IsChecked = true;
        HeadlessUiPump.Drain(fixture.Window);
        Assert.True(button.Focus());
        Assert.True(popup.IsOpen);

        Press(fixture.Window, Key.Escape);
        HeadlessUiPump.Drain(fixture.Window);

        Assert.False(popup.IsOpen);
        Assert.False(button.IsChecked);
        Assert.True(button.IsFocused);
    }

    private static void AssertMenuKeyboard(Window window, MenuItem root)
    {
        Press(window, Key.Down);
        HeadlessUiPump.Drain(window);
        Assert.True(root.IsSubMenuOpen);
        var enabled = root.Items.OfType<MenuItem>().Where(item => item.IsEffectivelyEnabled).ToArray();
        Assert.NotEmpty(enabled);
        Assert.True(enabled[0].IsFocused);
        Assert.All(enabled.Skip(1), item => { Press(window, Key.Down); Assert.True(item.IsFocused); });
        Press(window, Key.Escape);
        Assert.False(root.IsSubMenuOpen);
    }

    private static WorkspaceHeader Header(Window window) =>
        Assert.Single(window.GetVisualDescendants().OfType<WorkspaceHeader>());

    private static Avalonia.Rect InWindow(Control control) => new(
        Avalonia.VisualExtensions.TranslatePoint(control, default, TopLevel.GetTopLevel(control)!)!.Value, control.Bounds.Size);

    private static void AssertInside(Control child, Control parent)
    {
        Assert.True(child.IsEffectivelyVisible);
        Assert.True(InWindow(parent).Contains(InWindow(child)), $"{child.Name}: {InWindow(child)} outside {InWindow(parent)}");
    }

    private static void Press(TopLevel window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
        window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);
    }

    private static void TabTo(TopLevel window, Control target)
    {
        var visited = new HashSet<IInputElement>();
        do
        {
            Press(window, Key.Tab);
            var focused = window.FocusManager!.GetFocusedElement();
            Assert.NotNull(focused);
            Assert.True(visited.Add(focused), $"Tab cycles before reaching {target.Name}: {focused}");
        }
        while (!target.IsFocused);
    }
}
