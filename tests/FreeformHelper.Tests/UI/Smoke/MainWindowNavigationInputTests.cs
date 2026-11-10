// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class MainWindowNavigationInputTests
{
    [AvaloniaTheory]
    [InlineData(ShellPage.Workspace)]
    [InlineData(ShellPage.HowToUse)]
    [InlineData(ShellPage.Dev)]
    [InlineData(ShellPage.Simulation)]
    [InlineData(ShellPage.Coordinate)]
    public async Task ClickShellTab_CurrentPage_PreservesSelectionWithoutNavigationNotifications(ShellPage page)
    {
        using var scenario = await NavigationWindow.CreateAsync(page);
        var tab = scenario.GetTab(page);
        var currentViewModel = scenario.Shell.CurrentViewModel;
        var notifications = new ConcurrentQueue<string?>();
        scenario.Shell.PropertyChanged += (_, args) => notifications.Enqueue(args.PropertyName);
        Assert.True(tab.IsChecked);

        await scenario.ClickAsync(tab);

        Assert.True(tab.IsChecked);
        Assert.Equal(page, scenario.Shell.SelectedPage);
        Assert.Same(currentViewModel, scenario.Shell.CurrentViewModel);
        Assert.DoesNotContain(notifications, IsNavigationProjection);
    }

    [AvaloniaTheory]
    [InlineData(ShellPage.Workspace, ShellPage.HowToUse)]
    [InlineData(ShellPage.Workspace, ShellPage.Dev)]
    [InlineData(ShellPage.Workspace, ShellPage.Simulation)]
    [InlineData(ShellPage.Workspace, ShellPage.Coordinate)]
    [InlineData(ShellPage.HowToUse, ShellPage.Workspace)]
    [InlineData(ShellPage.HowToUse, ShellPage.Dev)]
    [InlineData(ShellPage.HowToUse, ShellPage.Simulation)]
    [InlineData(ShellPage.HowToUse, ShellPage.Coordinate)]
    [InlineData(ShellPage.Dev, ShellPage.Workspace)]
    [InlineData(ShellPage.Dev, ShellPage.HowToUse)]
    [InlineData(ShellPage.Dev, ShellPage.Simulation)]
    [InlineData(ShellPage.Dev, ShellPage.Coordinate)]
    [InlineData(ShellPage.Simulation, ShellPage.Workspace)]
    [InlineData(ShellPage.Simulation, ShellPage.HowToUse)]
    [InlineData(ShellPage.Simulation, ShellPage.Dev)]
    [InlineData(ShellPage.Simulation, ShellPage.Coordinate)]
    [InlineData(ShellPage.Coordinate, ShellPage.Workspace)]
    [InlineData(ShellPage.Coordinate, ShellPage.HowToUse)]
    [InlineData(ShellPage.Coordinate, ShellPage.Dev)]
    [InlineData(ShellPage.Coordinate, ShellPage.Simulation)]
    public async Task ClickShellTab_OtherPage_ChangesSelectionAndContentOnce(ShellPage source, ShellPage target)
    {
        using var scenario = await NavigationWindow.CreateAsync(source);
        var oldTab = scenario.GetTab(source);
        var newTab = scenario.GetTab(target);
        var currentViewModel = scenario.Shell.CurrentViewModel;
        var notifications = new ConcurrentQueue<string?>();
        scenario.Shell.PropertyChanged += (_, args) => notifications.Enqueue(args.PropertyName);
        Assert.True(oldTab.IsChecked);
        Assert.False(newTab.IsChecked);

        await scenario.ClickAsync(newTab);

        Assert.True(newTab.IsChecked);
        Assert.False(oldTab.IsChecked);
        Assert.Equal(target, scenario.Shell.SelectedPage);
        Assert.NotSame(currentViewModel, scenario.Shell.CurrentViewModel);
        Assert.Single(notifications, static name => name == nameof(ShellViewModel.CurrentViewModel));
    }

    private static bool IsNavigationProjection(string? name) => name is
        nameof(ShellViewModel.CurrentViewModel) or nameof(ShellViewModel.IsWorkspaceActive) or
        nameof(ShellViewModel.IsHowToUseActive) or nameof(ShellViewModel.IsDevActive) or
        nameof(ShellViewModel.IsSimulationActive) or nameof(ShellViewModel.IsCoordinateActive);

    private sealed class NavigationWindow : IDisposable
    {
        private readonly MainWindow _window;

        private NavigationWindow(ShellViewModel shell)
        {
            Shell = shell;
            _window = new MainWindow { Width = 1280, Height = 900, WindowState = WindowState.Normal };
            _window.SetShellViewModel(shell);
            _window.Show();
        }

        public ShellViewModel Shell { get; }

        public static async Task<NavigationWindow> CreateAsync(ShellPage page)
        {
            HeadlessAppBootstrap.EnsureInitialized();
            var shell = new ShellViewModel { IsConsoleExpanded = false, IsDevPageAvailable = true };
            shell.Simulation.RequestBuildWorkspaceAsync = null;
            shell.CoordinatePlanner.RequestBuildWorkspaceAsync = null;
            await ShellViewModelNavigationTests.ShowPageAsync(shell, page);
            var scenario = new NavigationWindow(shell);
            await HeadlessUiPump.RunningDispatcher.InvokeAsync(scenario._window.UpdateLayout, DispatcherPriority.Background);
            return scenario;
        }

        public ToggleButton GetTab(ShellPage page) => _window.GetVisualDescendants().OfType<ToggleButton>()
            .Single(button => button.Classes.Contains("shellTab") && Equals(button.Content, TabContent(page)));

        public async Task ClickAsync(ToggleButton tab)
        {
            var point = tab.TranslatePoint(new Rect(tab.Bounds.Size).Center, _window);
            Assert.NotNull(point);
            _window.MouseMove(point.Value);
            _window.MouseDown(point.Value, MouseButton.Left);
            _window.MouseUp(point.Value, MouseButton.Left);
            await HeadlessUiPump.YieldAsync(DispatcherPriority.Background);
        }

        public void Dispose()
        {
            Shell.FreeformHelper.HasUnsavedChanges = false;
            _window.Close();
            HeadlessUiPump.RunningDispatcher.RunJobs();
        }

        private static string TabContent(ShellPage page) => page switch
        {
            ShellPage.Workspace => "Freeform Helper",
            ShellPage.HowToUse => "How To Use",
            ShellPage.Dev => "Dev",
            ShellPage.Simulation => "Simulation",
            ShellPage.Coordinate => "Coordinate",
            _ => throw new ArgumentOutOfRangeException(nameof(page)),
        };
    }

    [AvaloniaFact]
    public async Task TopLevelKeys_SelectAllDeleteAndUndo_UpdateLoadedProjectAsync()
    {
        using var session = await LoadedSecondaryWindowSession.CreateAsync();
        var helper = session.Helper;
        var cadIds = helper.CadPads.Select(static pad => pad.Id).Order().ToArray();
        var regularIndices = helper.RegularPads.Select(static pad => pad.Index).Order().ToArray();
        FocusWorkspaceButton(session);

        PressKey(session.Owner, Key.A, RawInputModifiers.Control, PhysicalKey.A);

        Assert.Equal(cadIds, helper.InteractionState.SelectedCadIds.Order());
        Assert.Equal(regularIndices, helper.InteractionState.SelectedRegularIndices.Order());
        PressKey(session.Owner, Key.Delete, RawInputModifiers.None, PhysicalKey.Delete);
        await helper.WaitForGridRebuildIdleAsync();
        Assert.Equal(cadIds.Length, helper.DeletedCadPadCount);
        Assert.Empty(helper.CadPads);
        Assert.True(helper.HasUnsavedChanges);
        Assert.True(helper.CanUndo);

        FocusWorkspaceButton(session);
        PressKey(session.Owner, Key.Z, RawInputModifiers.Control, PhysicalKey.Z);
        await helper.WaitForGridRebuildIdleAsync();

        Assert.Equal(0, helper.DeletedCadPadCount);
        Assert.Equal(cadIds, helper.CadPads.Select(static pad => pad.Id).Order());
    }

    [AvaloniaFact]
    public async Task TopLevelKeyF_RestoresFittedCanvasViewportAsync()
    {
        using var session = await LoadedSecondaryWindowSession.CreateAsync();
        var canvas = session.View.GetVisualDescendants().OfType<PadCanvas>().Single();
        var center = new Point2(10, 5);
        canvas.FitToContent();
        await HeadlessUiPump.FlushQueueAsync();
        var fitted = canvas.WorldToScreen(center);
        canvas.ResetView();
        await HeadlessUiPump.FlushQueueAsync();
        Assert.NotEqual(fitted, canvas.WorldToScreen(center));
        FocusWorkspaceButton(session);

        PressKey(session.Owner, Key.F, RawInputModifiers.None, PhysicalKey.F);
        await HeadlessUiPump.WaitForConditionAsync(() => canvas.WorldToScreen(center) == fitted,
            "The F shortcut did not restore the fitted canvas viewport.");

        Assert.Equal(fitted, canvas.WorldToScreen(center));
    }

    [AvaloniaFact]
    public async Task WorkflowSettingsClicks_OpenEachSectionAndCancelDraftEditsAsync()
    {
        using var session = await LoadedSecondaryWindowSession.CreateAsync();
        session.Helper.IsRightPanelSettingsTab = true;
        foreach (var block in session.View.GetVisualDescendants().OfType<HidePanelBlock>().ToArray()) block.IsExpanded = true;
        await HeadlessUiPump.FlushQueueAsync();
        var original = session.Helper.XChannels;
        var dirty = session.Helper.HasUnsavedChanges;

        foreach (var section in Enum.GetValues<SettingsWindowSection>())
        {
            UiEventProbe.Click(session.View, button => Equals(button.Tag, section.ToString()) &&
                button.Classes.Contains(section == SettingsWindowSection.General ? "workflowOverviewAction" : "workflowStepAction"));
            var dialog = await HeadlessUiPump.WaitForOwnedWindowAsync<SettingsWindow>(session.Owner, nameof(SettingsWindow));
            try
            {
                var draft = Assert.IsType<SettingsWindowViewModel>(dialog.DataContext);
                Assert.Equal(section, draft.SelectedSection);
                Assert.Equal(original, draft.XChannels);
                draft.XChannels = original + 1;
                draft.CancelCommand.Execute(null);
                await HeadlessUiPump.WaitForConditionAsync(() => !dialog.IsVisible,
                    "Cancelling the workflow settings draft did not close its window.");

                Assert.Equal(original, session.Helper.XChannels);
                Assert.Equal(dirty, session.Helper.HasUnsavedChanges);
            }
            finally
            {
                dialog.Close();
            }
        }
    }

    [AvaloniaFact]
    public async Task PanelButtons_HideAndShowBothWorkspacePanelsAsync()
    {
        using var session = await LoadedSecondaryWindowSession.CreateAsync();
        var overlay = session.View.GetVisualDescendants().OfType<CanvasOverlayControls>().Single();

        AssertPanelButtonsToggleVisibility(session.View, overlay, "Left");
        AssertPanelButtonsToggleVisibility(session.View, overlay, "Right");
    }

    [AvaloniaFact]
    public async Task RestoreNotchExportSelectionClick_ShowsThePendingWindowAsync()
    {
        using var session = await NotchSelectionSession.CreateAsync();
        session.Model.HidePanelForInspectCommand.Execute(null);
        Assert.False(session.Window.IsVisible);
        Assert.False(session.Result.IsCompleted);

        UiEventProbe.Click(session.View, static button => button.Classes.Contains("notchExportRestoreHintAction"));
        await HeadlessUiPump.WaitForConditionAsync(() => session.Window.IsVisible,
            "The restore button did not show the hidden notch selection window.");

        Assert.Same(session.Window, Assert.Single(session.Owner.OwnedWindows.OfType<NotchExportSelectionWindow>()));
        Assert.False(session.Result.IsCompleted);
        Assert.Equal(6, session.Model.SelectedCount);
    }

    [AvaloniaFact]
    public async Task TopLevelControlShiftE_RestoresHiddenNotchSelectionAsync()
    {
        using var session = await NotchSelectionSession.CreateAsync();
        session.Model.HidePanelForInspectCommand.Execute(null);
        Assert.False(session.Window.IsVisible);
        session.Owner.Activate();
        var restore = session.View.GetVisualDescendants().OfType<Button>()
            .Single(static button => button.Classes.Contains("notchExportRestoreHintAction"));
        Assert.True(restore.Focus());

        PressKey(session.Owner, Key.E, RawInputModifiers.Control | RawInputModifiers.Shift, PhysicalKey.E);
        await HeadlessUiPump.WaitForConditionAsync(() => session.Window.IsVisible,
            "Ctrl+Shift+E did not restore the hidden notch selection window.");

        Assert.False(session.Result.IsCompleted);
        Assert.Equal(6, session.Model.SelectedCount);
    }

    private static void PressKey(Window window, Key key, RawInputModifiers modifiers, PhysicalKey physicalKey)
    {
        window.KeyPress(key, modifiers, physicalKey, null);
        window.KeyRelease(key, modifiers, physicalKey, null);
    }

    private static void FocusWorkspaceButton(LoadedSecondaryWindowSession session)
    {
        session.Owner.Activate();
        var overlay = session.View.GetVisualDescendants().OfType<CanvasOverlayControls>().Single();
        var button = overlay.GetVisualDescendants().OfType<Button>()
            .Single(button => ReferenceEquals(button.Command, session.Helper.FitCommand));
        Assert.True(button.Focus());
    }

    private static void AssertPanelButtonsToggleVisibility(FreeformHelperView view, CanvasOverlayControls overlay, string side)
    {
        var panel = view.FindControl<Border>(side + "Panel")!;
        var show = overlay.FindControl<Button>("Show" + side + "PanelButton")!;
        Assert.True(panel.IsVisible);
        Assert.False(show.IsVisible);

        UiEventProbe.Click(panel, static button => button.Classes.Contains("panelChromeToggle"));

        Assert.False(panel.IsVisible);
        Assert.True(show.IsVisible);
        UiEventProbe.Click(overlay, button => ReferenceEquals(button, show));

        Assert.True(panel.IsVisible);
        Assert.False(show.IsVisible);
    }
}
