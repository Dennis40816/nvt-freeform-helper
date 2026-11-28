// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using FreeformHelper.UI.Views.WorkspaceSections;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class CoordinateSidePanelWidthTests
{
    [AvaloniaFact]
    public async Task SidePanelFollowsViewportWidthAndItsActionsStayInsideAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var viewModel = new CoordinatePlannerWorkspaceViewModel(
            new CoordinatePlannerWorkspaceUseCase(),
            new CoordinatePlannerWorkspaceSession(
                TestGeometryFactory.CreateRegularGrid(1, 1, cellWidth: 30d, cellHeight: 20d),
                [TestGeometryFactory.CreateCadPad(1, "AA.drawing", 100d, 200d, 400d, 600d)],
                ["AA.drawing"], SourceRevision: 0,
                DefaultMachineWidth: 150d, DefaultMachineHeight: 200d,
                DefaultPixelWidth: 1500, DefaultPixelHeight: 2000));
        var view = new CoordinatePlannerWorkspaceView { DataContext = viewModel };
        var window = new Window { Content = view, Width = 1366, Height = 728 };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var details = Assert.Single(view.GetVisualDescendants().OfType<CoordinatePlannerWorkspaceDetailsPaneView>());
            Assert.Equal(360d, details.Bounds.Width, 0.5d);
            AssertInside(details, "Columns / sort");
            AssertInside(details, "Details");
            AssertInside(details, "Copy");
            AssertInside(details, "Copy all");
            AssertInside(details, "Export…");

            // The optional columns need the wide panel; without them the narrow one returns.
            details.ShowPixelColumn = true;
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal(460d, details.Bounds.Width, 0.5d);
            details.ShowPixelColumn = false;
            details.ShowMachineColumn = true;
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal(460d, details.Bounds.Width, 0.5d);
            details.ShowMachineColumn = false;
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal(360d, details.Bounds.Width, 0.5d);

            window.Width = 1920;
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal(460d, details.Bounds.Width, 0.5d);

            window.Width = 1366;
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal(360d, details.Bounds.Width, 0.5d);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task DraggedWidthIsKeptWhileTheViewportStaysOnTheSameSideAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var switches = 0;
        var grid = new SidePanelGrid
        {
            SidePanelSwitchedCommand = new RelayCommand(() => switches++),
            NarrowBelowWidth = 1600,
            WideSidePanelWidth = new GridLength(460),
            NarrowSidePanelWidth = new GridLength(360),
            ColumnDefinitions = new ColumnDefinitions("*,460"),
        };
        var window = new Window { Content = grid, Width = 1366, Height = 400 };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal(360d, grid.ColumnDefinitions[1].Width.Value);
            Assert.Equal(0, switches);

            grid.ColumnDefinitions[1].Width = new GridLength(420);
            window.Width = 1400;
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal(420d, grid.ColumnDefinitions[1].Width.Value);

            window.Width = 1700;
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal(460d, grid.ColumnDefinitions[1].Width.Value);
            Assert.Equal(1, switches);
        }
        finally
        {
            window.Close();
        }
    }

    private static void AssertInside(Control panel, string content)
    {
        var button = Assert.Single(panel.GetVisualDescendants().OfType<Button>(),
            item => item.Content is string text && text == content);
        var origin = button.TranslatePoint(default, panel);
        Assert.NotNull(origin);
        var right = origin.Value.X + button.Bounds.Width;
        Assert.True(origin.Value.X >= 0 && right <= panel.Bounds.Width + 0.5d,
            $"{content} spans {origin.Value.X} to {right} in a {panel.Bounds.Width} wide panel.");
    }
}
