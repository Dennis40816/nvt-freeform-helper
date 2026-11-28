// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using FreeformHelper.UI.Views.WorkspaceSections;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SimulationIntegratedLayoutTests
{
    private static readonly int[] _diffIndices = [10, 11];
    private static readonly string[] _overrideActions = ["Use global", "Apply override"];
    private static readonly string[] _replayExports = ["Copy", "Export CSV", "Export JSON"];

    [AvaloniaFact]
    public async Task ManualEditingTransfersTheOnlyPrimaryFromEditToApplyAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var viewModel = CreateWorkspace();
        var view = new SimulationWorkspaceView { DataContext = viewModel };
        var window = CreateWindow(view);
        try
        {
            window.Show();
            viewModel.SelectRegularPad(0);
            await HeadlessUiPump.FlushQueueAsync();
            var edit = Assert.Single(VisiblePrimaryActions(view));
            Assert.Equal("Edit", edit.Content);
            edit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await HeadlessUiPump.FlushQueueAsync();
            var apply = Assert.Single(VisiblePrimaryActions(view));
            Assert.Same(viewModel.ApplySelectedOverrideCommand, apply.Command);
            Assert.False(edit.IsEffectivelyVisible);
            viewModel.SelectedManualValue = 140;
            apply.Command!.Execute(null);
            Assert.Equal("140", viewModel.SelectedBeforeText);
            viewModel.SelectedInputSourceOption = viewModel.SourceOptions[2];
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Empty(VisiblePrimaryActions(view));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task CsvPrimaryMovesFromImportToPlaybackAndSliderUsesExistingNavigationAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var viewModel = CreateWorkspace();
        var view = new SimulationWorkspaceView { DataContext = viewModel };
        var window = CreateWindow(view);
        var csvPath = TestFiles.WriteTempCsv("""
            LogVer:"1.0",Xch:"2",Ych:"1",
            timestamp(mm:ss:nnn):"09:14:165",Frame index:"0",Break point index:"0",DiffData:
            100,50
            timestamp(mm:ss:nnn):"09:14:265",Frame index:"1",Break point index:"0",DiffData:
            200,80
            """);
        try
        {
            window.Show();
            viewModel.SelectedInputSourceOption = viewModel.SourceOptions[1];
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Same(viewModel.ImportCsvCommand, Assert.Single(VisiblePrimaryActions(view)).Command);
            viewModel.PickOpenDiffCsvPathsAsync = () => Task.FromResult<IReadOnlyList<string>>([csvPath]);
            viewModel.RefreshExternalBindings();
            await viewModel.ImportCsvCommand.ExecuteAsync(null);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Same(viewModel.TogglePlaybackCommand, Assert.Single(VisiblePrimaryActions(view)).Command);
            var slider = Assert.Single(view.GetVisualDescendants().OfType<Slider>());
            viewModel.TogglePlaybackCommand.Execute(null);
            slider.Value = 1;
            Assert.Equal(1, viewModel.SelectedFrameSliderIndex);
            Assert.True(viewModel.IsPlaybackRunning);
            viewModel.SelectRegularPad(0);
            Assert.Equal("200", viewModel.SelectedBeforeText);
            viewModel.PreviousFrameCommand.Execute(null);
            Assert.False(viewModel.IsPlaybackRunning);
            Assert.Equal(0, slider.Value);
            Assert.Equal("100", viewModel.SelectedBeforeText);
            Assert.Single(view.GetVisualDescendants().OfType<TextBlock>(),
                text => text.IsEffectivelyVisible && text.Text == viewModel.FrameSummaryText);
            viewModel.ClearCsvCommand.Execute(null);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Same(viewModel.ImportCsvCommand, Assert.Single(VisiblePrimaryActions(view)).Command);
        }
        finally
        {
            viewModel.ClearCsvCommand.Execute(null);
            window.Close();
            File.Delete(csvPath);
        }
    }

    [AvaloniaFact]
    public async Task CopperSettingsResultsAndRunHaveOneOwnerAndSelectionUsesExistingCommandAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var viewModel = CreateWorkspace();
        viewModel.SelectedInputSourceOption = viewModel.SourceOptions[2];
        var view = new SimulationWorkspaceView { DataContext = viewModel };
        var window = CreateWindow(view);
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var panel = Assert.Single(view.GetVisualDescendants().OfType<HidePanelBlock>(),
                static block => block.Title == "Copper path replay");
            Assert.False(panel.IsExpanded);
            Assert.Empty(VisiblePrimaryActions(view));
            panel.IsExpanded = true;
            await HeadlessUiPump.FlushQueueAsync();
            var run = Assert.Single(VisiblePrimaryActions(view));
            Assert.Same(viewModel.ReplayCopperPathCommand, run.Command);
            Assert.Single(view.GetVisualDescendants().OfType<Button>(), button => button.Command == run.Command);
            Assert.Single(view.GetVisualDescendants().OfType<NumberScrubber>(),
                scrubber => TipText(scrubber) == "Copper center X in regular-grid world coordinates.");
            Assert.Single(panel.GetVisualDescendants().OfType<TextBlock>(), static text => text.Text == "Start X");
            var rows = Assert.Single(view.GetVisualDescendants().OfType<ListBox>());
            Assert.Contains(rows, panel.GetVisualDescendants());
            run.Command!.Execute(null);
            await HeadlessUiPump.FlushQueueAsync();
            rows.SelectedIndex = 1;
            var expected = viewModel.CopperPathReplayRows[1];
            Assert.Same(expected, viewModel.SelectedCopperPathReplayRow);
            Assert.Equal(expected.CenterX, viewModel.CopperCenterX);
            Assert.Equal(expected.CenterY, viewModel.CopperCenterY);
            viewModel.CopperCenterX = expected.CenterX + 1;
            rows.ScrollIntoView(expected);
            await HeadlessUiPump.FlushQueueAsync();
            var rowAction = Assert.Single(rows.GetVisualDescendants().OfType<Button>(),
                button => ReferenceEquals(button.CommandParameter, expected));
            rowAction.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(expected.CenterX, viewModel.CopperCenterX);
            viewModel.SelectCopperPathReplayRowCommand.Execute(viewModel.CopperPathReplayRows[0]);
            Assert.Same(viewModel.SelectedCopperPathReplayRow, rows.SelectedItem);
            var details = Assert.Single(view.GetVisualDescendants().OfType<SimulationWorkspaceDetailsPaneView>());
            Assert.Empty(details.GetVisualDescendants().OfType<ListBox>());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task FooterActionsAlignRightWithUseGlobalBeforeApplyAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var viewModel = CreateWorkspace();
        var view = new SimulationWorkspaceView { DataContext = viewModel };
        var window = CreateWindow(view);
        try
        {
            window.Show();
            viewModel.SelectRegularPad(0);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Single(VisiblePrimaryActions(view)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await HeadlessUiPump.FlushQueueAsync();
            var actions = Assert.IsType<StackPanel>(view.FindControl<StackPanel>("OverrideFooterActions"));
            Assert.Equal(HorizontalAlignment.Right, actions.HorizontalAlignment);
            Assert.Equal(_overrideActions, actions.Children.OfType<Button>().Select(static button => button.Content));
            var clear = Assert.Single(view.GetVisualDescendants().OfType<Button>(),
                button => button.Command == viewModel.ClearAllManualOverridesCommand);
            var clearRow = Assert.IsType<Grid>(clear.Parent);
            Assert.Equal(1, Grid.GetColumn(clear));
            Assert.Equal(0, Grid.GetColumn(Assert.Single(clearRow.Children.OfType<TextBlock>())));
            viewModel.SelectedInputSourceOption = viewModel.SourceOptions[2];
            var replay = Assert.Single(view.GetVisualDescendants().OfType<SimulationWorkspaceCopperReplayView>());
            var exports = Assert.IsType<StackPanel>(replay.FindControl<StackPanel>("ReplayExportActions"));
            Assert.Equal(HorizontalAlignment.Right, exports.HorizontalAlignment);
            Assert.Equal(_replayExports, exports.Children.OfType<Button>().Select(static button => button.Content));
            Assert.Equal(HorizontalAlignment.Right,
                Assert.IsType<StackPanel>(replay.FindControl<StackPanel>("ReplayRunActions")).HorizontalAlignment);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task BuildFailureKeepsOnePrimaryRecoveryActionAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var host = new SimulationHostViewModel { RequestBuildWorkspaceAsync = () => Task.CompletedTask };
        var view = new SimulationHostView { DataContext = host };
        var window = CreateWindow(view);
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Same(host.OpenSimulationCommand, Assert.Single(VisiblePrimaryActions(view)).Command);
            host.SetBuildFailure("No Regular grid in the current workspace.");
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Same(host.OpenSimulationCommand, Assert.Single(VisiblePrimaryActions(view)).Command);
            Assert.Equal("Retry build", host.BuildActionText);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task WorkspaceViewIsVisibleOnlyWhileTheHostHasAUsableWorkspaceAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var host = new SimulationHostViewModel { RequestBuildWorkspaceAsync = () => Task.CompletedTask };
        var view = new SimulationHostView { DataContext = host };
        var window = CreateWindow(view);
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var workspaceView = view.GetVisualDescendants().OfType<SimulationWorkspaceView>().Single();

            Assert.False(workspaceView.IsVisible, "No workspace: the workspace view stays hidden.");
            host.CurrentWorkspace = CreateWorkspace();
            await HeadlessUiPump.FlushQueueAsync();
            Assert.True(workspaceView.IsVisible, "A workspace without a build failure is shown.");
            host.SetBuildFailure("No Regular grid in the current workspace.");
            await HeadlessUiPump.FlushQueueAsync();
            Assert.False(workspaceView.IsVisible, "A workspace with a build failure is hidden behind the failure overlay.");
            host.SetBuildFailure(null);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.True(workspaceView.IsVisible, "Clearing the failure shows the workspace again.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task ConsoleStartsCollapsedAndFollowsTheShellStateAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var shell = new ShellViewModel { IsDevPageAvailable = true, IsConsoleExpanded = false };
        var view = new SimulationWorkspaceView { DataContext = CreateWorkspace() };
        var window = CreateWindow(view);
        window.DataContext = shell;
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var console = Assert.IsType<ConsolePanel>(view.FindControl<ConsolePanel>("SimulationConsole"));
            var toggle = Assert.IsType<ToggleButton>(console.FindControl<ToggleButton>("ConsoleToggleButton"));
            Assert.False(toggle.IsChecked);
            var collapsedHeight = console.Bounds.Height;
            toggle.IsChecked = true;
            await HeadlessUiPump.FlushQueueAsync();
            Assert.True(shell.IsConsoleExpanded);
            Assert.True(console.Bounds.Height > collapsedHeight);
            toggle.IsChecked = false;
            Assert.False(shell.IsConsoleExpanded);
        }
        finally
        {
            window.Close();
        }
    }

    private static string? TipText(Control control) =>
        ToolTip.GetTip(control) switch
        {
            string text => text,
            TextBlock block => block.Text,
            _ => null,
        };

    private static IEnumerable<Button> VisiblePrimaryActions(Control view) => view.GetVisualDescendants()
        .OfType<Button>().Where(static button => button.IsEffectivelyVisible && button.Classes.Contains("actionPrimary"));

    private static Window CreateWindow(Control content) => new() { Content = content, Width = 1400, Height = 1000 };

    private static SimulationWorkspaceViewModel CreateWorkspace()
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid(_diffIndices);
        var table = new NotchTable(new[]
        {
            new NotchTableRow(icIndex: 0, diffIndex: 10, regularPadIndex: 0, cadPadId: 313,
                v22Node: new NotchV22Node(10, 94, 11, 44, 65535, 0, 0), comment: "sample"),
        });
        return new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            new SimulationWorkspaceSession(grid, table, 65535, SourceRevision: 0,
                ActiveRegularPadIds: grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
                CadPadsForCopperProjection: new[]
                {
                    TestGeometryFactory.CreateCadPad(100, "SENSOR", 0, 0, 1, 1),
                    TestGeometryFactory.CreateCadPad(101, "SENSOR", 1, 0, 2, 1),
                },
                CadOutputFwDiffIndexByCadId: new Dictionary<int, int> { [100] = 10, [101] = 11 },
                CadIcIndexByCadId: new Dictionary<int, int> { [100] = 0, [101] = 0 }));
    }
}
