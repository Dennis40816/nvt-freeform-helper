// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class FreeformHelperViewPartialMergeCharacterizationTests
{
    [AvaloniaTheory]
    [InlineData("Left", 0, 321.0, GridUnitType.Pixel)]
    [InlineData("Right", 4, 321.0, GridUnitType.Pixel)]
    [InlineData("Left", 0, 2.5, GridUnitType.Star)]
    [InlineData("Right", 4, 2.5, GridUnitType.Star)]
    [InlineData("Left", 0, 1.0, GridUnitType.Auto)]
    [InlineData("Right", 4, 1.0, GridUnitType.Auto)]
    public void SidebarRestoresGridLengthAndRenderedWidthAfterRepeatedHide(
        string side, int column, double value, GridUnitType unit)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var view = new FreeformHelperView();
        var grid = Assert.IsType<Grid>(view.FindControl<Grid>("LayoutGrid"));
        var expectedWidth = new GridLength(value, unit);
        grid.ColumnDefinitions[column].Width = expectedWidth;
        var owner = Show(view);
        var panel = Assert.IsType<Border>(view.FindControl<Border>($"{side}Panel"));
        var overlay = Assert.IsType<CanvasOverlayControls>(view.FindControl<CanvasOverlayControls>("CanvasOverlayControls"));
        var hide = Assert.Single(panel.GetVisualDescendants().OfType<Button>(),
            static button => button.Classes.Contains("panelChromeToggle"));
        var show = Assert.IsType<Button>(overlay.FindControl<Button>($"Show{side}PanelButton"));
        var renderedWidth = panel.Bounds.Width;
        Assert.True(renderedWidth > 0);
        Assert.False(show.IsVisible);

        Click(hide);
        Click(hide);
        HeadlessUiPump.Drain(owner);

        Assert.False(panel.IsVisible);
        Assert.Equal(new GridLength(0), grid.ColumnDefinitions[column].Width);
        Assert.True(show.IsVisible);

        Click(show);
        HeadlessUiPump.Drain(owner);

        Assert.True(panel.IsVisible);
        Assert.Equal(expectedWidth, grid.ColumnDefinitions[column].Width);
        Assert.Equal(renderedWidth, panel.Bounds.Width, precision: 6);
        Assert.False(show.IsVisible);

        var resizedWidth = new GridLength(380);
        grid.ColumnDefinitions[column].Width = resizedWidth;
        Click(hide);
        Click(show);
        HeadlessUiPump.Drain(owner);

        Assert.Equal(resizedWidth, grid.ColumnDefinitions[column].Width);
        Assert.True(panel.IsVisible);
        Assert.False(show.IsVisible);
    }

    [AvaloniaFact]
    public void ConsoleRowResynchronizesOnReattachAndObservesLaterExpansionChanges()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var shell = new ShellViewModel { IsDevPageAvailable = true };
        var view = new FreeformHelperView();
        var expandedHeight = new GridLength(2.5, GridUnitType.Star);
        view.Resources["ConsolePanelRowHeight"] = expandedHeight;
        view.Resources["ConsolePanelMinHeight"] = 48.0;
        view.Resources["ConsoleCollapsedMinHeight"] = 24.0;
        var owner = Show(view, shell);
        var row = Assert.IsType<Grid>(view.FindControl<Grid>("CanvasGrid")).RowDefinitions[2];
        Assert.Equal(GridLength.Auto, row.Height);
        Assert.Equal(24.0, row.MinHeight);

        shell.IsConsoleExpanded = true;
        Assert.Equal(expandedHeight, row.Height);
        Assert.Equal(48.0, row.MinHeight);

        owner.Content = null;
        HeadlessUiPump.Drain(owner);
        shell.IsConsoleExpanded = false;
        Assert.Equal(expandedHeight, row.Height);
        Assert.Equal(48.0, row.MinHeight);

        owner.Content = view;
        HeadlessUiPump.Drain(owner);
        Assert.Equal(GridLength.Auto, row.Height);
        Assert.Equal(24.0, row.MinHeight);

        shell.IsConsoleExpanded = true;
        Assert.Equal(expandedHeight, row.Height);
        Assert.Equal(48.0, row.MinHeight);

        shell.IsConsoleExpanded = false;
        Assert.Equal(GridLength.Auto, row.Height);
        Assert.Equal(24.0, row.MinHeight);
    }

    [AvaloniaFact]
    public async Task MappingReportReusesVisibleWindowAndCreatesNewWindowAfterCloseAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var host = new FakeCadLoadSpinnerHost();
        using var spinner = host.Install();
        var vm = new FreeformHelperViewModel();
        var view = new FreeformHelperView { DataContext = vm };
        var owner = Show(view);
        var openReportAsync = vm.OpenIndexMappingReportAsync;
        Assert.NotNull(openReportAsync);
        var firstReport = CreateReport("First report");
        await openReportAsync(firstReport);
        var window = Assert.Single(owner.OwnedWindows.OfType<IndexMappingReportWindow>());
        Assert.True(window.IsVisible);
        Assert.Same(firstReport, window.DataContext);
        var nextReport = CreateReport("Next report");

        await openReportAsync(nextReport);

        Assert.Same(window, Assert.Single(owner.OwnedWindows.OfType<IndexMappingReportWindow>()));
        Assert.True(window.IsVisible);
        Assert.Same(nextReport, window.DataContext);

        window.Close();
        await openReportAsync(firstReport);

        var reopened = Assert.Single(owner.OwnedWindows.OfType<IndexMappingReportWindow>());
        Assert.NotSame(window, reopened);
        Assert.True(reopened.IsVisible);
        Assert.Same(firstReport, reopened.DataContext);
    }

    [AvaloniaFact]
    public async Task MappingReportWithoutOwnerDoesNotOpenUntilReattachedAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var host = new FakeCadLoadSpinnerHost();
        using var spinner = host.Install();
        var vm = new FreeformHelperViewModel();
        var view = new FreeformHelperView { DataContext = vm };
        var owner = Show(view);
        var openReportAsync = vm.OpenIndexMappingReportAsync;
        Assert.NotNull(openReportAsync);
        owner.Content = null;
        HeadlessUiPump.Drain(owner);
        Assert.Null(TopLevel.GetTopLevel(view));
        Assert.Null(vm.OpenIndexMappingReportAsync);
        var openedReports = 0;
        using var subscription = Window.WindowOpenedEvent.AddClassHandler(
            typeof(IndexMappingReportWindow), (_, _) => openedReports++);
        var report = CreateReport("Ownerless report");

        await openReportAsync(report);
        await HeadlessUiPump.FlushQueueAsync();

        Assert.Equal(0, openedReports);
        Assert.Empty(owner.OwnedWindows.OfType<IndexMappingReportWindow>());

        owner.Content = view;
        HeadlessUiPump.Drain(owner);
        await openReportAsync(report);

        var window = Assert.Single(owner.OwnedWindows.OfType<IndexMappingReportWindow>());
        Assert.Equal(1, openedReports);
        Assert.True(window.IsVisible);
        Assert.Same(report, window.DataContext);
    }

    [AvaloniaFact]
    public async Task MappingReportWithoutOwnerStillReusesAnAlreadyVisibleWindowAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var host = new FakeCadLoadSpinnerHost();
        using var spinner = host.Install();
        var vm = new FreeformHelperViewModel();
        var view = new FreeformHelperView { DataContext = vm };
        var owner = Show(view);
        var openReportAsync = vm.OpenIndexMappingReportAsync;
        Assert.NotNull(openReportAsync);
        await openReportAsync(CreateReport("First report"));
        var window = Assert.Single(owner.OwnedWindows.OfType<IndexMappingReportWindow>());
        owner.Content = null;
        HeadlessUiPump.Drain(owner);
        Assert.Null(TopLevel.GetTopLevel(view));
        Assert.Null(vm.OpenIndexMappingReportAsync);
        var nextReport = CreateReport("Ownerless replacement");

        await openReportAsync(nextReport);

        Assert.Same(window, Assert.Single(owner.OwnedWindows.OfType<IndexMappingReportWindow>()));
        Assert.True(window.IsVisible);
        Assert.Same(nextReport, window.DataContext);
    }

    private static Window Show(FreeformHelperView view, object? dataContext = null)
    {
        var window = new Window { Content = view, DataContext = dataContext, Width = 1400, Height = 1000 };
        HeadlessSessionGuardAttribute.CloseAtTestEnd(window);
        window.Show();
        HeadlessUiPump.Drain(window);
        return window;
    }

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static IndexMappingReportViewModel CreateReport(string summary)
    {
        var report = new DxfRegularMappingReport(0, 0, 0, 0, 0, 0, 0, false, summary, 0, false, [], []);
        return new IndexMappingReportViewModel(summary, report);
    }
}
