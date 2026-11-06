// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using FreeformHelper.UI.Views.RightWorkflowSections;
using FreeformHelper.UI.Views.WorkflowSteps;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class WorkflowOpenSettingsEntryTests
{
    private static readonly Point2[] _padCorners = [new(0, 0), new(10, 0), new(10, 10), new(0, 10)];
    private static readonly string[] _sectionNames = ["General", "Step1", "Step2", "Step3", "Step4", "Step5"];

    [AvaloniaFact]
    public async Task WorkflowStepOpenSettingsButtons_OpenOwnedWindowAtExpectedCategoryAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var vm = CreateWorkspaceViewModel();
        var view = new FreeformHelperView { DataContext = vm };
        var owner = new Window { Content = view, Width = 1280, Height = 900 };
        try
        {
            owner.Show();
            await HeadlessUiPump.FlushQueueAsync();

            await AssertStepOpensSettingsAsync<RightWorkflowStep1View>(
                view, owner, SettingsWindowSection.Step1GeometryMatch, "Step1");
            await AssertStepOpensSettingsAsync<RightWorkflowStep2View>(
                view, owner, SettingsWindowSection.Step2FreeformTagging, "Step2");
            await AssertStepOpensSettingsAsync<RightWorkflowStep3View>(
                view, owner, SettingsWindowSection.Step3NotchCompensation, "Step3");
            await AssertStepOpensSettingsAsync<RightWorkflowStep4View>(
                view, owner, SettingsWindowSection.Step4IndexDiagnostics, "Step4");
            await AssertStepOpensSettingsAsync<RightWorkflowStep5View>(
                view, owner, SettingsWindowSection.Step5NotchExport, "Step5");
        }
        finally
        {
            CloseWindows(owner);
            await vm.WaitForGridRebuildIdleAsync().WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        }
    }

    [AvaloniaFact]
    public async Task OverviewOpenSettingsButtons_ReuseOwnedWindowAndNavigateCategoriesAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var vm = CreateWorkspaceViewModel();
        var view = new FreeformHelperView { DataContext = vm };
        var owner = new Window { Content = view, Width = 1280, Height = 900 };
        try
        {
            owner.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var settingsTab = Assert.Single(view.GetVisualDescendants().OfType<RightWorkflowSettingsTabView>());
            var overview = Assert.Single(settingsTab.GetVisualDescendants().OfType<HidePanelBlock>(),
                static block => block.Title == "Overview");
            overview.IsExpanded = true;
            await HeadlessUiPump.FlushQueueAsync();

            var window = await ClickOpenSettingsAsync(overview, owner, SettingsWindowSection.General, "General");
            Assert.Same(window, await ClickOpenSettingsAsync(
                overview, owner, SettingsWindowSection.Step3NotchCompensation, "Step3"));
            Assert.Same(window, await ClickOpenSettingsAsync(
                overview, owner, SettingsWindowSection.Step5NotchExport, "Step5"));
            Assert.Same(window, await ClickOpenSettingsAsync(overview, owner, SettingsWindowSection.General, "General"));
        }
        finally
        {
            CloseWindows(owner);
            await vm.WaitForGridRebuildIdleAsync().WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        }
    }

    private static FreeformHelperViewModel CreateWorkspaceViewModel() => new()
    {
        IsRightPanelSettingsTab = true,
        RegularPads = [new RegularPad(0, 0, 0, new Polygon2(_padCorners))],
    };

    private static async Task AssertStepOpensSettingsAsync<TStep>(
        FreeformHelperView view, Window owner, SettingsWindowSection section, string sectionName)
        where TStep : UserControl
    {
        var step = Assert.Single(view.GetVisualDescendants().OfType<TStep>());
        var block = Assert.Single(step.GetVisualDescendants().OfType<HidePanelBlock>());
        block.IsExpanded = true;
        await HeadlessUiPump.FlushQueueAsync();

        var window = await ClickOpenSettingsAsync(step, owner, section, sectionName);

        window.Close();
        await HeadlessUiPump.FlushQueueAsync();
        Assert.Empty(owner.OwnedWindows);
        Assert.True(owner.IsEnabled);
    }

    private static async Task<SettingsWindow> ClickOpenSettingsAsync(
        Control source, Window owner, SettingsWindowSection section, string sectionName)
    {
        var button = Assert.Single(source.GetVisualDescendants().OfType<Button>(),
            button => button.Tag is string tag && string.Equals(tag, section.ToString(), StringComparison.Ordinal));
        Assert.True(button.IsEffectivelyVisible);
        Assert.True(button.IsEffectivelyEnabled);

        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await HeadlessUiPump.FlushQueueAsync();

        var window = Assert.IsType<SettingsWindow>(Assert.Single(owner.OwnedWindows));
        Assert.Same(owner, window.Owner);
        Assert.True(window.IsVisible);
        var draft = Assert.IsType<SettingsWindowViewModel>(window.DataContext);
        Assert.Equal(section, draft.SelectedSection);
        foreach (var name in _sectionNames)
        {
            var selected = string.Equals(name, sectionName, StringComparison.Ordinal);
            var navigation = Assert.IsType<ToggleButton>(window.FindControl<ToggleButton>($"{name}NavButton"));
            Assert.Equal<bool?>(selected, navigation.IsChecked);
            var content = window.FindControl<Control>($"{name}Section");
            Assert.NotNull(content);
            Assert.Equal(selected, content.IsEffectivelyVisible);
        }

        return window;
    }

    private static void CloseWindows(Window owner)
    {
        foreach (var window in owner.OwnedWindows.ToArray())
        {
            window.Close();
        }

        owner.Close();
    }
}
