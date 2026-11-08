// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using FreeformHelper.UI.Views.WorkspaceSections;
using Nvt.Core.Avalonia.Panels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class CoordinatePlannerIntegratedLayoutTests
{
    private static readonly string[] _footerLabels = ["Details", "Copy selected", "Copy all", "Export…"];
    private static readonly string[] _dismissLabels = ["Close"];

    [AvaloniaFact]
    public async Task EmptyAndPlannedStatesEachHaveOnePrimaryActionAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var host = new CoordinatePlannerHostViewModel { RequestBuildWorkspaceAsync = () => Task.CompletedTask };
        var view = new CoordinatePlannerHostView { DataContext = host };
        var window = CreateWindow(view);
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var build = OnlyPrimary(view);
            Assert.Equal("Build from current workspace", build.Content);
            Assert.Same(host.OpenCoordinatePlannerCommand, build.Command);

            host.CurrentWorkspace = CreateWorkspace();
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal("Export…", OnlyPrimary(view).Content);
            host.CurrentWorkspace.ShowCalibrationDetails = true;
            host.CurrentWorkspace.ShowCustomArray = true;
            foreach (var panel in view.GetVisualDescendants().OfType<CollapsiblePanel>().ToArray())
            {
                panel.IsExpanded = true;
            }

            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal("Export…", OnlyPrimary(view).Content);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task RefreshAppearsOnlyForAnExistingPlanWithChangedSourceAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var host = new CoordinatePlannerHostViewModel();
        var view = new CoordinatePlannerHostView { DataContext = host };
        var window = CreateWindow(view);
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var refresh = Assert.IsType<Button>(view.FindControl<Button>("RefreshPlanButton"));
            Assert.False(refresh.IsEffectivelyVisible);
            host.CurrentWorkspace = CreateWorkspace();
            await HeadlessUiPump.FlushQueueAsync();
            Assert.False(refresh.IsEffectivelyVisible);
            host.UpdateSourceRevision(1);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.True(refresh.IsEffectivelyVisible);
            Assert.Same(host.OpenCoordinatePlannerCommand, refresh.Command);
            Assert.Contains("clears custom points and paths", TipText(refresh) ?? string.Empty, StringComparison.Ordinal);
            Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(),
                text => text.IsEffectivelyVisible && text.Text == host.StaleWorkspaceBannerText);
            Assert.DoesNotContain("actionPrimary", refresh.Classes);
            host.UpdateSourceRevision(0);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.False(refresh.IsEffectivelyVisible);
            host.CurrentWorkspace = null;
            host.UpdateSourceRevision(2);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.False(refresh.IsEffectivelyVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task DestructiveActionsDescribeTheirScopeAndDisclosureKeepsEnableStateAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var workspace = CreateWorkspace();
        var view = new CoordinatePlannerWorkspaceView { DataContext = workspace };
        var window = CreateWindow(view);
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var array = Section(view, "Array settings");
            Assert.False(array.IsExpanded);
            array.IsExpanded = true;
            Assert.False(workspace.ShowCustomArray);
            workspace.ShowCustomArray = true;
            array.IsExpanded = false;
            Assert.True(workspace.ShowCustomArray);
            array.IsExpanded = true;
            Section(view, "Calibration").IsExpanded = true;
            Section(view, "Custom point / path").IsExpanded = true;
            await HeadlessUiPump.FlushQueueAsync();

            AssertVisibleConsequence(view, "Sync", CoordinatePlannerWorkspaceViewModel.SyncConsequenceText);
            AssertVisibleConsequence(view, "Use safe AA corners", CoordinatePlannerWorkspaceViewModel.SafeCornersConsequenceText);
            AssertVisibleConsequence(view, "Clear custom", CoordinatePlannerWorkspaceViewModel.ClearCustomConsequenceText);
            Assert.Contains("overwrites", CoordinatePlannerWorkspaceViewModel.SyncConsequenceText, StringComparison.Ordinal);
            Assert.Contains("every custom point and path", CoordinatePlannerWorkspaceViewModel.ClearCustomConsequenceText, StringComparison.Ordinal);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task FooterActionsAlignRightWithNonConfirmActionsBeforeExportAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var view = new CoordinatePlannerWorkspaceView { DataContext = CreateWorkspace() };
        var window = CreateWindow(view);
        var detail = new CoordinateArtifactDetailWindow();
        try
        {
            window.Show();
            detail.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var table = Assert.Single(view.GetVisualDescendants().OfType<CoordinatePlannerWorkspaceDetailsPaneView>());
            var footer = Assert.IsType<StackPanel>(table.FindControl<StackPanel>("CoordinateFooterActions"));
            Assert.Equal(HorizontalAlignment.Right, footer.HorizontalAlignment);
            Assert.Equal(_footerLabels, footer.Children.OfType<Button>().Select(static button => button.Content));
            Assert.Contains("actionPrimary", Assert.IsType<Button>(footer.Children[^1]).Classes);
            var dismiss = Assert.IsType<StackPanel>(detail.FindControl<StackPanel>("DetailFooterActions"));
            Assert.Equal(HorizontalAlignment.Right, dismiss.HorizontalAlignment);
            Assert.Equal(_dismissLabels, dismiss.Children.OfType<Button>().Select(static button => button.Content));
            Assert.DoesNotContain(dismiss.Children.OfType<Button>(), static button => button.Classes.Contains("actionPrimary"));
        }
        finally
        {
            detail.Close();
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task RowSelectionMarksArtifactAndDetailsIsTheOnlyDialogEntryAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var workspace = CreateWorkspace();
        var view = new CoordinatePlannerWorkspaceView { DataContext = workspace };
        var window = CreateWindow(view);
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var requests = new List<CoordinateArtifactRow>();
            workspace.RequestArtifactDetailAsync = row =>
            {
                requests.Add(row);
                return Task.CompletedTask;
            };
            var table = Assert.Single(view.GetVisualDescendants().OfType<CoordinatePlannerWorkspaceDetailsPaneView>());
            var list = Assert.IsType<ListBox>(table.FindControl<ListBox>("CoordinateArtifactList"));
            var details = Action(view, "Details");
            Assert.False(details.IsEffectivelyEnabled);
            Assert.False(Action(view, "Copy selected").IsEffectivelyEnabled);
            var row = workspace.ArtifactRows[0];
            list.SelectedItem = row;
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Same(row, workspace.SelectedArtifactRow);
            Assert.Empty(requests);
            Assert.True(details.IsEffectivelyEnabled);
            Assert.True(Action(view, "Copy selected").IsEffectivelyEnabled);
            Assert.Same(workspace.ShowArtifactDetailCommand, details.Command);
            await workspace.ShowArtifactDetailCommand.ExecuteAsync(details.CommandParameter);
            Assert.Same(row, Assert.Single(requests));
            Assert.False(table.ShowPixelColumn);
            Assert.False(table.ShowMachineColumn);
            var snapshot = workspace.Snapshot;
            table.ShowPixelColumn = true;
            table.ShowMachineColumn = true;
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Same(snapshot, workspace.Snapshot);
            Assert.Same(row, workspace.SelectedArtifactRow);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task DetailDisclosureAvoidsDuplicateCoordinatesAndCloseKeepsSelectionAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var workspace = CreateWorkspace();
        var row = workspace.ArtifactRows[0];
        workspace.SelectedArtifactRow = row;
        var detail = new CoordinateArtifactDetailWindow { DataContext = row };
        try
        {
            detail.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var coordinates = Section(detail, "Coordinates");
            var source = Section(detail, "Source bounds and detail");
            Assert.True(coordinates.IsExpanded);
            Assert.False(source.IsExpanded);
            source.IsExpanded = true;
            await HeadlessUiPump.FlushQueueAsync();
            Assert.False(coordinates.IsExpanded);
            Assert.Contains(detail.GetVisualDescendants().OfType<SelectableTextBlock>(),
                block => block.IsEffectivelyVisible && block.Text == row.SourceText);
            Assert.Contains(detail.GetVisualDescendants().OfType<SelectableTextBlock>(),
                block => block.IsEffectivelyVisible && block.Text == row.DetailText);
            coordinates.IsExpanded = true;
            Assert.False(source.IsExpanded);
            Action(detail, "Close").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await HeadlessUiPump.FlushQueueAsync();
            Assert.False(detail.IsVisible);
            Assert.Same(row, workspace.SelectedArtifactRow);
        }
        finally
        {
            detail.Close();
        }
    }

    [AvaloniaFact]
    public async Task ExpandedSharedHeadersResetTheStockCheckedPresenterInBothThemesAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var view = new CoordinatePlannerWorkspaceView { DataContext = CreateWorkspace() };
        var window = CreateWindow(view);
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var section = Section(view, "Calibration");
            section.IsExpanded = true;
            window.RequestedThemeVariant = ThemeVariant.Dark;
            await HeadlessUiPump.FlushQueueAsync();
            AssertHeaderPresenter(section);
            window.RequestedThemeVariant = ThemeVariant.Light;
            await HeadlessUiPump.FlushQueueAsync();
            AssertHeaderPresenter(section);
        }
        finally
        {
            window.Close();
        }
    }

    private static Window CreateWindow(Control content) => new() { Content = content, Width = 1400, Height = 900 };

    private static CoordinatePlannerWorkspaceViewModel CreateWorkspace() => new(
        new CoordinatePlannerWorkspaceUseCase(),
        new CoordinatePlannerWorkspaceSession(
            TestGeometryFactory.CreateRegularGrid(1, 1, cellWidth: 10d, cellHeight: 5d),
            Array.Empty<CadPad>(), Array.Empty<string>(), SourceRevision: 0,
            DefaultMachineWidth: 100d, DefaultMachineHeight: 50d,
            DefaultPixelWidth: 1000, DefaultPixelHeight: 500));

    private static Button OnlyPrimary(Control root) => Assert.Single(root.GetVisualDescendants().OfType<Button>(),
        static button => button.IsEffectivelyVisible && button.Classes.Contains("actionPrimary"));

    private static Button Action(Control root, string label) => Assert.Single(root.GetVisualDescendants().OfType<Button>(),
        button => button.Content as string == label);

    private static CollapsiblePanel Section(Control root, string title) => Assert.Single(root.GetVisualDescendants().OfType<CollapsiblePanel>(),
        section => section.Title == title);

    private static string? TipText(Control control) =>
        ToolTip.GetTip(control) switch
        {
            string text => text,
            TextBlock block => block.Text,
            _ => null,
        };

    private static void AssertVisibleConsequence(Control root, string label, string consequence)
    {
        var action = Action(root, label);
        Assert.DoesNotContain("actionPrimary", action.Classes);
        Assert.Equal(HorizontalAlignment.Right, action.HorizontalAlignment);
        Assert.Equal(consequence, TipText(action));
        Assert.Contains(root.GetVisualDescendants().OfType<TextBlock>(),
            text => text.IsEffectivelyVisible && text.Text == consequence && text.HorizontalAlignment == HorizontalAlignment.Left);
    }

    private static void AssertHeaderPresenter(CollapsiblePanel section)
    {
        var header = Assert.Single(section.GetVisualDescendants().OfType<ToggleButton>(),
            static button => button.Classes.Contains("panelBlockHeader"));
        Assert.True(header.IsChecked);
        var presenter = Assert.Single(header.GetVisualDescendants().OfType<ContentPresenter>(),
            static control => control.Name == "PART_ContentPresenter");
        Assert.Null(presenter.Background);
        Assert.Null(presenter.BorderBrush);
        Assert.Equal(section.FindResource("BrushActionGhostForeground"), presenter.Foreground);
    }
}
