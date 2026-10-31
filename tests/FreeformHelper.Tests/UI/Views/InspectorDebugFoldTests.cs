using Avalonia.Controls;
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

        vm.PadInspectorUi.Snapshot = PadInspectorDisplayBehaviorTests.CreateCadSnapshot(275);
        HeadlessUiPump.Drain(window);

        Assert.DoesNotContain("Snapshot details", VisibleTexts(window));
    }

    [Fact]
    public void InspectorView_FoldsDebugDetailsUnderSharedAccordionAndDropsDeveloperText()
    {
        var axaml = File.ReadAllText(Path.Combine(TestPaths.RepoRoot, "src", "FreeformHelper.UI", "Views", "RightWorkflowSections", "RightWorkflowInspectorView.axaml"));

        Assert.DoesNotContain("same snapshot source as Popover and CLI", axaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Focus by CAD, REG, or DIFF id.", axaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Shows rule-by-rule reasoning for the current pad.\"", axaml, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"Shows rule-by-rule reasoning for the current pad.\"", axaml, StringComparison.Ordinal);

        var fold = axaml.IndexOf("Classes=\"workspaceFoldHeader\"", StringComparison.Ordinal);
        Assert.True(fold > 0, "The Inspector debug fold uses the shared workspaceFoldHeader accordion.");
        Assert.Contains("IsChecked=\"{Binding PadInspectorUi.IsDebugDetailsExpanded, Mode=TwoWay}\"", axaml, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding PadInspectorUi.IsDebugDetailsExpanded}\"", axaml, StringComparison.Ordinal);
        Assert.True(axaml.IndexOf("Text=\"Snapshot details\"", StringComparison.Ordinal) > fold);
        Assert.True(axaml.IndexOf("StringFormat='Rule trace (x{0})'", StringComparison.Ordinal) > fold);
        Assert.DoesNotContain("Background=\"#", axaml, StringComparison.Ordinal);
        Assert.DoesNotContain("FontSize=", axaml, StringComparison.Ordinal);
    }
    private static List<string> VisibleTexts(Window window) =>
        window.GetVisualDescendants()
            .OfType<SelectableTextBlock>()
            .Where(block => block.IsEffectivelyVisible && !string.IsNullOrEmpty(block.Text))
            .Select(block => block.Text!)
            .ToList();
}
