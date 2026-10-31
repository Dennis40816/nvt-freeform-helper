using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views.RightWorkflowSections;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins the Debug details fold of the Inspector tab: it is collapsed when a pad is shown
/// and it renders Snapshot details and Rule trace once it is opened.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class InspectorDebugFoldTests
{
    [AvaloniaFact]
    public void DebugDetails_CollapsedByDefault_ShowsSnapshotDetailsAndRuleTraceWhenOpened()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var vm = new FreeformHelperViewModel { IsRightPanelSettingsTab = false };
        vm.PadInspectorUi.Snapshot = PadInspectorDisplayBehaviorTests.CreateCadSnapshot(274);
        var window = new Window
        {
            Width = 420,
            Height = 900,
            Content = new RightWorkflowInspectorView { DataContext = vm },
        };
        HeadlessSessionGuardAttribute.CloseAtTestEnd(window);

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var collapsed = VisibleTexts(window);
        Assert.Contains("Debug details", collapsed);
        Assert.Contains("Notch / Freeform", collapsed);
        Assert.DoesNotContain("Snapshot details", collapsed);
        Assert.DoesNotContain(collapsed, text => text.StartsWith("Rule trace", StringComparison.Ordinal));

        vm.PadInspectorUi.IsDebugDetailsExpanded = true;
        Dispatcher.UIThread.RunJobs();

        var opened = VisibleTexts(window);
        Assert.Contains("Snapshot details", opened);
        Assert.Contains(opened, text => text.StartsWith("Rule trace", StringComparison.Ordinal));

        vm.PadInspectorUi.Snapshot = PadInspectorDisplayBehaviorTests.CreateCadSnapshot(275);
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain("Snapshot details", VisibleTexts(window));
    }

    private static List<string> VisibleTexts(Window window) =>
        window.GetVisualDescendants()
            .OfType<SelectableTextBlock>()
            .Where(block => block.IsEffectivelyVisible && !string.IsNullOrEmpty(block.Text))
            .Select(block => block.Text!)
            .ToList();
}
