// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views.WorkflowSteps;
using Nvt.Core.Avalonia.Panels;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins the four two-way bindings of the Step 3 guard controls on the right panel. The Settings window
/// no longer shows these fields, so the panel is the only place where the user changes them.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class RightWorkflowStep3GuardBindingTests
{
    private const decimal TargetCapMaximum = 255m;
    private const decimal VirtualCapMaximum = 1000m;

    [AvaloniaFact]
    public async Task ViewModelValues_AreShownByTheStep3GuardControls()
    {
        var (viewModel, view, window) = await ShowStep3ViewAsync();
        try
        {
            viewModel.EnableTargetCoverageGuard = true;
            viewModel.TargetCoverageCapPercent = 77m;
            viewModel.EnableBoundaryVirtualAreaCap = true;
            viewModel.BoundaryVirtualAreaCapPercent = 33m;
            await HeadlessUiPump.FlushQueueAsync();

            var (targetSwitch, boundarySwitch, targetCap, virtualCap) = FindGuardControls(view);
            Assert.Equal(
                (true, 77m, true, 33m),
                (targetSwitch.IsChecked == true, targetCap.Value, boundarySwitch.IsChecked == true, virtualCap.Value));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Step3GuardControlEdits_ReachTheViewModel()
    {
        var (viewModel, view, window) = await ShowStep3ViewAsync();
        try
        {
            viewModel.EnableTargetCoverageGuard = false;
            viewModel.EnableBoundaryVirtualAreaCap = false;
            await HeadlessUiPump.FlushQueueAsync();

            var (targetSwitch, boundarySwitch, targetCap, virtualCap) = FindGuardControls(view);
            targetSwitch.IsChecked = true;
            boundarySwitch.IsChecked = true;
            targetCap.Value = 55m;
            virtualCap.Value = 222m;
            await HeadlessUiPump.FlushQueueAsync();

            Assert.Equal(
                (true, 55m, true, 222m),
                (viewModel.EnableTargetCoverageGuard,
                 viewModel.TargetCoverageCapPercent,
                 viewModel.EnableBoundaryVirtualAreaCap,
                 viewModel.BoundaryVirtualAreaCapPercent));
        }
        finally
        {
            window.Close();
        }
    }

    private static async Task<(FreeformHelperViewModel ViewModel, RightWorkflowStep3View View, Window Window)> ShowStep3ViewAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var viewModel = new FreeformHelperViewModel { IsStep3Expanded = true };
        var view = new RightWorkflowStep3View { DataContext = viewModel };
        var window = new Window { Width = 480, Height = 900, Content = view };
        window.Show();
        await HeadlessUiPump.FlushQueueAsync();
        Assert.Single(view.GetVisualDescendants().OfType<CollapsiblePanel>(),
            static panel => panel.Title == "Edit guards").IsExpanded = true;
        await HeadlessUiPump.FlushQueueAsync();
        return (viewModel, view, window);
    }

    private static (ToggleSwitch TargetSwitch, ToggleSwitch BoundarySwitch, NumberScrubber TargetCap, NumberScrubber VirtualCap)
        FindGuardControls(RightWorkflowStep3View view)
    {
        var switches = view.GetVisualDescendants().OfType<ToggleSwitch>().ToList();
        Assert.Equal(2, switches.Count);
        var targetCap = Assert.Single(
            view.GetVisualDescendants().OfType<NumberScrubber>(),
            static scrubber => scrubber.Maximum == TargetCapMaximum);
        var virtualCap = Assert.Single(
            view.GetVisualDescendants().OfType<NumberScrubber>(),
            static scrubber => scrubber.Maximum == VirtualCapMaximum);
        return (switches[0], switches[1], targetCap, virtualCap);
    }
}
