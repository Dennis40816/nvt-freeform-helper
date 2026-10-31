using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
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
        HeadlessUiPump.Drain(window);

        var collapsed = VisibleTexts(window);
        Assert.Contains("Debug details", collapsed);
        Assert.Contains("Notch / Freeform", collapsed);
        Assert.DoesNotContain("Snapshot details", collapsed);
        Assert.DoesNotContain(collapsed, text => text.StartsWith("Rule trace", StringComparison.Ordinal));

        vm.PadInspectorUi.IsDebugDetailsExpanded = true;
        HeadlessUiPump.Drain(window);

        var opened = VisibleTexts(window);
        Assert.Contains("Snapshot details", opened);
        Assert.Contains(opened, text => text.StartsWith("Rule trace", StringComparison.Ordinal));
        Assert.DoesNotContain(opened, text => text.Contains("same snapshot source", StringComparison.Ordinal));
        Assert.DoesNotContain(opened, text => text.Contains("Focus by CAD, REG, or DIFF id.", StringComparison.Ordinal));
        Assert.Contains(window.GetVisualDescendants().OfType<ToggleButton>(), button => button.Classes.Contains("workspaceFoldHeader"));
        var ruleTrace = window.GetVisualDescendants()
            .OfType<SelectableTextBlock>()
            .Single(block => block.Text?.StartsWith("Rule trace", StringComparison.Ordinal) == true);
        var ruleTraceTip = Assert.IsType<TextBlock>(ToolTip.GetTip(ruleTrace));
        Assert.Equal("Shows rule-by-rule reasoning for the current pad.", ruleTraceTip.Text);

        vm.PadInspectorUi.Snapshot = PadInspectorDisplayBehaviorTests.CreateCadSnapshot(275);
        HeadlessUiPump.Drain(window);

        Assert.DoesNotContain("Snapshot details", VisibleTexts(window));
    }

    private static List<string> VisibleTexts(Window window) =>
        window.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(block => block.IsEffectivelyVisible && !string.IsNullOrEmpty(block.Text))
            .Select(block => block.Text!)
            .ToList();
}
