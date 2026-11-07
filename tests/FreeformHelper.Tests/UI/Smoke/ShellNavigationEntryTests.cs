// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ShellNavigationEntryTests
{
    private static readonly Point2[] _padCorners = [new(0, 0), new(10, 0), new(10, 10), new(0, 10)];

    [AvaloniaFact]
    public Task LeftPanelHideAndOverlayShowRestoreVisibilityAsync() => AssertPanelHideAndShowAsync(isLeft: true);

    [AvaloniaFact]
    public Task RightPanelHideAndOverlayShowRestoreVisibilityAsync() => AssertPanelHideAndShowAsync(isLeft: false);

    [AvaloniaFact]
    public async Task RestoreExportSelectionButtonShowsPanelHiddenForInspectAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var vm = CreateWorkspaceViewModel();
        var view = new FreeformHelperView { DataContext = vm };
        var owner = CreateWorkspaceWindow(view);
        var selection = new NotchExportSelectionViewModel(new NotchTable(
            [new NotchTableRow(0, 0, 0, null, new NotchV22Node(0, 100, 1, 25, 65535, 0, 0), "navigation")]));
        NotchExportSelectionWindow? exportWindow = null;
        Task<NotchTable?>? completion = null;
        try
        {
            owner.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var openSelectionAsync = vm.OpenNotchExportSelectionAsync;
            Assert.NotNull(openSelectionAsync);
            completion = openSelectionAsync(selection);
            exportWindow = Assert.Single(owner.OwnedWindows.OfType<NotchExportSelectionWindow>());
            await HeadlessUiPump.FlushQueueAsync();
            var hint = Assert.IsType<Border>(Assert.Single(view.GetVisualDescendants().OfType<WorkspaceActionBar>()).FindControl<Border>("RestoreHint"));
            var restore = Assert.Single(view.GetVisualDescendants().OfType<Button>(),
                static button => Equals(button.Content, "Restore Export Notch Rows"));
            Assert.True(exportWindow.IsVisible);
            Assert.False(hint.IsVisible);
            Assert.False(completion.IsCompleted);
            Assert.True(selection.HidePanelForInspectCommand.CanExecute(null));

            selection.HidePanelForInspectCommand.Execute(null);
            await HeadlessUiPump.FlushQueueAsync();

            Assert.False(exportWindow.IsVisible);
            Assert.True(hint.IsVisible);
            Assert.True(restore.IsEffectivelyVisible);
            restore.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await HeadlessUiPump.FlushQueueAsync();

            Assert.True(exportWindow.IsVisible);
            Assert.False(hint.IsVisible);
            Assert.False(completion.IsCompleted);
            Assert.True(owner.IsEnabled);
        }
        finally
        {
            exportWindow?.Close();
            foreach (var owned in owner.OwnedWindows.ToArray())
            {
                owned.Close();
            }
            owner.Close();
            if (completion is not null)
            {
                await completion.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            }
        }
    }

    [AvaloniaFact]
    public async Task FKeyFitsCanvasAndTextInputFocusLeavesViewUnchangedAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var view = new FreeformHelperView { DataContext = CreateWorkspaceViewModel() };
        var input = new TextBox { Text = "Workspace notes" };
        var content = new DockPanel();
        DockPanel.SetDock(input, Dock.Top);
        content.Children.Add(input);
        content.Children.Add(view);
        var window = CreateWorkspaceWindow(content);
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var canvas = Assert.IsType<PadCanvas>(view.FindControl<PadCanvas>("PadCanvas"));
            canvas.FitBoundsOverride = new Rect2(1000, 2000, 1010, 2010);
            Assert.True(canvas.Focus());
            canvas.ResetView();
            await HeadlessUiPump.FlushQueueAsync();
            var reset = canvas.GetViewFrameSnapshot();
            var fit = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.F };

            window.RaiseEvent(fit);
            await HeadlessUiPump.FlushQueueAsync();

            Assert.True(fit.Handled);
            Assert.NotEqual(reset.WorldToScreen, canvas.GetViewFrameSnapshot().WorldToScreen);
            Assert.True(input.Focus());
            canvas.ResetView();
            await HeadlessUiPump.FlushQueueAsync();
            var beforeTyping = canvas.GetViewFrameSnapshot();
            var typing = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.F, Source = input };

            input.RaiseEvent(typing);
            await HeadlessUiPump.FlushQueueAsync();

            Assert.False(typing.Handled);
            Assert.Equal(beforeTyping.WorldToScreen, canvas.GetViewFrameSnapshot().WorldToScreen);
            Assert.Equal("Workspace notes", input.Text);
        }
        finally
        {
            window.Close();
        }
    }

    private static FreeformHelperViewModel CreateWorkspaceViewModel() => new()
    {
        RegularPads = [new RegularPad(0, 0, 0, new Polygon2(_padCorners))],
    };

    private static Window CreateWorkspaceWindow(Control content) => new() { Content = content, Width = 1280, Height = 900 };

    private static async Task AssertPanelHideAndShowAsync(bool isLeft)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var view = new FreeformHelperView { DataContext = CreateWorkspaceViewModel() };
        var owner = CreateWorkspaceWindow(view);
        try
        {
            owner.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var panel = Assert.IsType<Border>(view.FindControl<Border>(isLeft ? "LeftPanel" : "RightPanel"));
            var overlay = Assert.IsType<CanvasOverlayControls>(view.FindControl<CanvasOverlayControls>("CanvasOverlayControls"));
            var show = Assert.IsType<Button>(overlay.FindControl<Button>(isLeft ? "ShowLeftPanelButton" : "ShowRightPanelButton"));
            var hide = Assert.Single(panel.GetVisualDescendants().OfType<Button>(),
                static button => button.Classes.Contains("panelChromeToggle"));
            var grid = Assert.IsType<Grid>(view.FindControl<Grid>("LayoutGrid"));
            var column = grid.ColumnDefinitions[isLeft ? 0 : 4];
            var expandedWidth = column.Width;
            Assert.True(panel.IsVisible);
            Assert.False(show.IsVisible);
            Assert.True(expandedWidth.Value > 0);

            hide.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await HeadlessUiPump.FlushQueueAsync();

            Assert.False(panel.IsVisible);
            Assert.True(show.IsEffectivelyVisible);
            Assert.Equal(0d, column.Width.Value);
            show.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await HeadlessUiPump.FlushQueueAsync();

            Assert.True(panel.IsVisible);
            Assert.False(show.IsVisible);
            Assert.Equal(expandedWidth, column.Width);
        }
        finally
        {
            owner.Close();
        }
    }
}
