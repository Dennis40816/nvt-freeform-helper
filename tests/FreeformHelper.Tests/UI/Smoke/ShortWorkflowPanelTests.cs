// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.Logging;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using FreeformHelper.UI.Views.WorkflowSteps;
using NLog;
using NLog.Config;
using Nvt.Core.Avalonia.Panels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ShortWorkflowPanelTests
{
    [AvaloniaFact]
    public void GuardRowsStayVisibleAndKeepTheirTwoWayBindingsAndNumericContracts()
    {
        var vm = new FreeformHelperViewModel { IsStep3Expanded = true, NotchPreviewAutoPlayEnabled = false };
        var view = new RightWorkflowStep3View { DataContext = vm };
        var window = Show(view);
        var target = Scrubber(view, 255m);
        var boundary = Scrubber(view, 1000m);
        var switches = view.GetVisualDescendants().OfType<ToggleSwitch>().ToArray();
        Assert.Equal(3, switches.Length);
        Assert.Equal((0m, 1m, "0"), (target.Minimum, target.SmallChange, target.FormatString));
        Assert.Equal((0m, 10m, "0.#"), (boundary.Minimum, boundary.SmallChange, boundary.FormatString));
        Assert.All(new[] { target, boundary }, static input => Assert.True(input.IsEffectivelyVisible));
        Assert.All(view.GetVisualDescendants().OfType<CollapsiblePanel>()
            .Where(static panel => panel.Title != "Step 3 · Notch compensation"), static panel => Assert.False(panel.IsExpanded));

        vm.EnableTargetCoverageGuard = false;
        vm.EnableBoundaryVirtualAreaCap = false;
        HeadlessUiPump.Drain(window);
        Assert.False(switches[0].IsChecked);
        Assert.False(switches[1].IsChecked);
        Assert.False(target.IsEffectivelyEnabled);
        Assert.False(boundary.IsEffectivelyEnabled);
        switches[0].IsChecked = true;
        switches[1].IsChecked = true;
        target.Value = 87m;
        boundary.Value = 210m;
        HeadlessUiPump.Drain(window);
        Assert.Equal((true, 87m, true, 210m),
            (vm.EnableTargetCoverageGuard, vm.TargetCoverageCapPercent, vm.EnableBoundaryVirtualAreaCap, vm.BoundaryVirtualAreaCapPercent));
        Assert.True(target.IsEffectivelyEnabled);
        Assert.True(boundary.IsEffectivelyEnabled);
        vm.TargetCoverageCapPercent = 91m;
        vm.BoundaryVirtualAreaCapPercent = 320m;
        HeadlessUiPump.Drain(window);
        Assert.Equal((91m, 320m), (target.Value, boundary.Value));
    }

    [AvaloniaFact]
    public void PlaybackIntervalIsAlwaysVisibleAndFollowsPreviewAvailability()
    {
        var vm = new FreeformHelperViewModel { IsStep3Expanded = true, NotchPreviewAutoPlayEnabled = false };
        var view = new RightWorkflowStep3View { DataContext = vm };
        var window = Show(view);
        var interval = Scrubber(view, 5000m);
        var autoPlay = view.GetVisualDescendants().OfType<ToggleSwitch>().ToArray()[2];
        Assert.Equal((200m, 100m, "0"), (interval.Minimum, interval.SmallChange, interval.FormatString));
        Assert.True(interval.IsEffectivelyVisible);
        Assert.False(interval.IsEffectivelyEnabled);
        Assert.False(autoPlay.IsEffectivelyEnabled);
        Assert.Single(interval.GetVisualAncestors().OfType<CollapsiblePanel>());

        var polygon = TestGeometryFactory.CreateRect(0, 0, 10, 10);
        vm.NotchCanvasPreviewItems = [new(101, 1, 1, true, [polygon], [polygon], [polygon], [polygon])];
        HeadlessUiPump.Drain(window);
        Assert.True(interval.IsEffectivelyEnabled);
        Assert.True(autoPlay.IsEffectivelyEnabled);
        vm.NotchPreviewAutoPlayIntervalMs = 900m;
        HeadlessUiPump.Drain(window);
        Assert.Equal(900m, interval.Value);
        interval.Value = 1100m;
        HeadlessUiPump.Drain(window);
        Assert.Equal(1100m, vm.NotchPreviewAutoPlayIntervalMs);
        vm.NotchCanvasPreviewItems = [];
        HeadlessUiPump.Drain(window);
        Assert.True(interval.IsEffectivelyVisible);
        Assert.False(interval.IsEffectivelyEnabled);
        Assert.False(autoPlay.IsEffectivelyEnabled);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AutoPlayLabelIsVisibleToTheRightAndClickingItToggles(bool enabled)
    {
        var vm = new FreeformHelperViewModel { NotchPreviewAutoPlayEnabled = false };
        var view = new FreeformHelperView { DataContext = vm };
        var window = Show(view, 1920, 1080);
        var polygon = TestGeometryFactory.CreateRect(0, 0, 10, 10);
        vm.NotchCanvasPreviewItems = [new(101, 1, 1, true, [polygon], [polygon], [polygon], [polygon])];
        vm.IsStep3Expanded = true;
        vm.NotchPreviewAutoPlayEnabled = enabled;
        HeadlessUiPump.Drain(window);
        var step = Assert.Single(view.GetVisualDescendants().OfType<RightWorkflowStep3View>());
        var toggle = step.GetVisualDescendants().OfType<ToggleSwitch>().ToArray()[2];
        Assert.True(toggle.IsEffectivelyEnabled);
        var label = Assert.Single(step.GetVisualDescendants().OfType<TextBlock>(),
            static text => text.Text == "Auto play" && text.IsEffectivelyVisible);
        AssertTextFits(label);
        AssertWithin(label, view.FindControl<ScrollViewer>("RightSettingsScroll")!, horizontalOnly: false);
        Assert.True(BoundsIn(label, step).Left >= BoundsIn(toggle, step).Right);
        Assert.InRange(Math.Abs(BoundsIn(label, step).Center.Y - BoundsIn(toggle, step).Center.Y), 0, 1);
        TestContext.Current.TestOutputHelper?.WriteLine($"Auto play ({enabled}): switch {BoundsIn(toggle, step)}, label {BoundsIn(label, step)}.");
        var click = global::Avalonia.VisualExtensions.TranslatePoint(label,
            new global::Avalonia.Point(label.Bounds.Width / 2, label.Bounds.Height / 2), window)!.Value;
        Assert.True(step.GetVisualDescendants().OfType<ToggleSwitch>().First().Focus());
        window.MouseDown(click, global::Avalonia.Input.MouseButton.Left);
        window.MouseUp(click, global::Avalonia.Input.MouseButton.Left);
        HeadlessUiPump.Drain(window);
        Assert.Equal(!enabled, vm.NotchPreviewAutoPlayEnabled);
        Assert.True(toggle.IsFocused);
        vm.NotchPreviewAutoPlayEnabled = enabled;
        HeadlessUiPump.Drain(window);
        Assert.Equal(enabled, toggle.IsChecked);
    }

    [AvaloniaFact]
    public async Task FooterKeepsEveryStateTextAndCollapsesTheExportHintAsync()
    {
        using var fixture = new SuppressedWorkspaceBehaviorTests.WorkspaceFixture();
        var vm = fixture.CreateViewModel();
        var bar = new WorkspaceActionBar { DataContext = vm };
        var window = Show(new StackPanel { Children = { bar } });
        AssertFooter(window, bar, "Open DXF", "Open DXF or use File > Load Project to begin.");
        await vm.OpenDxfCommand.ExecuteAsync(null);
        await vm.WaitForGridRebuildIdleAsync();
        AssertFooter(window, bar, "Match overlaps", "Step 1 · Geometry match");
        vm.IsStep2Expanded = true;
        AssertFooter(window, bar, "Detect freeform", "Step 2 · Freeform tagging");
        vm.IsStep3Expanded = true;
        AssertFooter(window, bar, "Export notch…", string.Empty);
        var exportHeight = bar.Bounds.Height;
        vm.IsStep5Expanded = true;
        AssertFooter(window, bar, "Export notch…", string.Empty);
        Assert.Equal(exportHeight, bar.Bounds.Height);
        vm.IsStep2Expanded = true;
        AssertFooter(window, bar, "Detect freeform", "Step 2 · Freeform tagging");
        Assert.True(bar.Bounds.Height > exportHeight);
    }

    [AvaloniaTheory]
    [InlineData(360)]
    [InlineData(220)]
    public void StepThreeControlsFitTheRightPanelWithoutClippingOrOverlap(double panelWidth)
    {
        var vm = new FreeformHelperViewModel { IsStep3Expanded = true, NotchPreviewAutoPlayEnabled = false };
        var view = new FreeformHelperView { DataContext = vm };
        var window = Show(view, 1920, 1080);
        var layout = view.FindControl<Grid>("LayoutGrid")!;
        layout.ColumnDefinitions[4].Width = new global::Avalonia.Controls.GridLength(panelWidth);
        var step = Assert.Single(view.GetVisualDescendants().OfType<RightWorkflowStep3View>());
        Scrubber(step, 1000m).Value = 1000m;
        HeadlessUiPump.Drain(window);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        vm.IsStep3Expanded = true;
        HeadlessUiPump.Drain(window);
        var scroll = view.FindControl<ScrollViewer>("RightSettingsScroll")!;
        Assert.Equal(panelWidth, view.FindControl<Border>("RightPanel")!.Bounds.Width, 2);
        Assert.False(vm.IsStep3ToFullDetailsExpanded);
        var controls = step.GetVisualDescendants().OfType<Control>()
            .Where(static control => control.IsEffectivelyVisible &&
                control is NumberScrubber or ToggleSwitch or Button or SelectableTextBlock or TextBlock { Text: "Auto play" }).ToArray();
        Assert.All(controls, control => AssertWithin(control, step, horizontalOnly: true));
        Assert.Equal("1000", Assert.Single(Scrubber(step, 1000m).GetVisualDescendants().OfType<TextBox>()).Text);
        var inputs = step.GetVisualDescendants().OfType<NumberScrubber>().ToArray();
        Assert.Equal(3, inputs.Length);
        Assert.All(inputs, AssertNumericTextFits);
        AssertNoOverlap(controls, step);
        Assert.All(controls.OfType<TextBlock>(), AssertTextFits);
        if (panelWidth == 360)
        {
            var target = AssertGuardRow(step, 255m, "Target coverage cap", "%");
            var boundary = AssertGuardRow(step, 1000m, "ToFull area cap", "% overlap");
            Assert.Equal(target.X, boundary.X, 2);
            var switches = step.GetVisualDescendants().OfType<ToggleSwitch>().ToArray();
            Assert.Equal("Target coverage cap", AutomationProperties.GetName(switches[0]));
            Assert.Equal("ToFull area cap", AutomationProperties.GetName(switches[1]));
            Assert.All(controls, control => AssertWithin(control, scroll, horizontalOnly: false));
            Assert.Equal(0, scroll.Offset.Y);
        }
    }

    [AvaloniaFact]
    public async Task DetectionKeepsStatisticsInThePanelAndConsoleWithAShortBannerAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var previous = LogManager.Configuration;
        try
        {
            var config = new LoggingConfiguration();
            using var target = new InAppConsoleTarget { Layout = "${message}" };
            config.AddRule(LogLevel.Info, LogLevel.Fatal, target);
            LogManager.Configuration = config;
            using var fixture = new SuppressedWorkspaceBehaviorTests.WorkspaceFixture();
            var vm = await ManualRangeStatusCoverageTests.OpenGridAsync(fixture);
            var view = new FreeformHelperView { DataContext = vm };
            var window = Show(view);
            await vm.MatchCommand.ExecuteAsync(null);
            Assert.Equal("Matching complete.", vm.StatusDisplayText);
            await vm.AutoDetectFreeformsCommand.ExecuteAsync(null);
            vm.IsStep2Expanded = true;
            HeadlessUiPump.Drain(window);
            Assert.Equal("Freeform stats (auto-detect): total=4, tagged=0, none=4.", vm.FreeformAutoDetectSummary);
            Assert.Equal(vm.FreeformAutoDetectSummary, vm.StatusText);
            var banner = Assert.Single(view.GetVisualDescendants().OfType<SelectableTextBlock>(),
                static text => text.Classes.Contains("workspaceHeaderStatus"));
            Assert.Equal("Freeform detection complete.", banner.Text);
            Assert.True(banner.IsEffectivelyVisible);
            var step2 = Assert.Single(view.GetVisualDescendants().OfType<RightWorkflowStep2View>());
            Assert.Single(step2.GetVisualDescendants().OfType<SelectableTextBlock>(),
                text => text.IsEffectivelyVisible && text.Text == vm.FreeformAutoDetectSummary);
            var stats = Assert.Single(step2.GetVisualDescendants().OfType<CollapsiblePanel>(), static panel => panel.Title == "Stats");
            Assert.True(stats.IsEffectivelyVisible);
            Assert.False(stats.IsExpanded);
            using var snapshot = await ConsoleLogTestSupport.CaptureAsync();
            Assert.Contains(snapshot.Entries, entry => ConsoleLogTestSupport.Read(entry.TextContent) == vm.FreeformAutoDetectSummary);
            vm.StatusText = "Notch export complete.";
            HeadlessUiPump.Drain(window);
            Assert.Equal("Notch export complete.", banner.Text);
        }
        finally
        {
            LogManager.Configuration = previous;
        }
    }

    private static NumberScrubber Scrubber(Control view, decimal maximum) =>
        Assert.Single(view.GetVisualDescendants().OfType<NumberScrubber>(), input => input.Maximum == maximum);

    private static Window Show(Control view, double width = 480, double height = 900)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var window = new Window { Content = view, Width = width, Height = height };
        HeadlessSessionGuardAttribute.CloseAtTestEnd(window);
        window.Show();
        HeadlessUiPump.Drain(window);
        return window;
    }

    private static void AssertFooter(Window window, WorkspaceActionBar bar, string action, string hint)
    {
        HeadlessUiPump.Drain(window);
        Assert.Equal(action, Assert.Single(bar.GetVisualDescendants().OfType<Button>(),
            static button => button.Classes.Contains("actionPrimary")).Content);
        var text = Assert.Single(bar.GetVisualDescendants().OfType<SelectableTextBlock>());
        Assert.Equal(hint, text.Text);
        Assert.Equal(hint.Length > 0, text.IsEffectivelyVisible);
        if (hint.Length == 0)
        {
            Assert.Equal(0, text.DesiredSize.Height);
            var stack = Assert.IsType<StackPanel>(text.Parent);
            Assert.Equal(stack.Children[1].Bounds.Height, stack.Bounds.Height);
        }
    }

    private static void AssertWithin(Control control, Control host, bool horizontalOnly)
    {
        var origin = global::Avalonia.VisualExtensions.TranslatePoint(control, default, host)!.Value;
        Assert.True(origin.X >= -0.5 && origin.X + control.Bounds.Width <= host.Bounds.Width + 0.5,
            $"{control.GetType().Name} {control.Name} exceeds {host.GetType().Name} horizontally: {origin}, {control.Bounds} / {host.Bounds}.");
        if (!horizontalOnly)
        {
            Assert.True(origin.Y >= -0.5 && origin.Y + control.Bounds.Height <= host.Bounds.Height + 0.5,
                $"{control.GetType().Name} exceeds the visible right-panel viewport.");
        }
    }

    private static global::Avalonia.Rect AssertGuardRow(Control step, decimal maximum, string labelText, string unitText)
    {
        var input = Scrubber(step, maximum);
        var row = Assert.IsAssignableFrom<Control>(input.Parent!.Parent);
        var toggle = Assert.Single(row.GetVisualDescendants().OfType<ToggleSwitch>());
        var label = Assert.Single(row.GetVisualDescendants().OfType<SelectableTextBlock>(), text => text.Text == labelText);
        var unit = Assert.Single(row.GetVisualDescendants().OfType<SelectableTextBlock>(), text => text.Text == unitText);
        var inputBounds = BoundsIn(input, step);
        foreach (var control in new Control[] { toggle, label, unit })
        {
            Assert.True(control.IsEffectivelyVisible);
            var bounds = BoundsIn(control, step);
            Assert.True(Math.Abs(inputBounds.Center.Y - bounds.Center.Y) <= 1,
                $"{labelText}: {control.GetType().Name} must share the input row: {bounds} / {inputBounds}.");
        }
        Assert.True(BoundsIn(toggle, step).Right <= BoundsIn(label, step).Left);
        Assert.True(BoundsIn(label, step).Right <= inputBounds.Left);
        Assert.True(inputBounds.Right <= BoundsIn(unit, step).Left);
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"{labelText}: row width {row.Bounds.Width}, switch {BoundsIn(toggle, step)}, label {BoundsIn(label, step)}, " +
            $"input {inputBounds}, unit {BoundsIn(unit, step)}.");
        return inputBounds;
    }

    private static void AssertNumericTextFits(NumberScrubber input)
    {
        Assert.True(input.IsEffectivelyVisible);
        var editor = Assert.Single(input.GetVisualDescendants().OfType<TextBox>());
        Assert.True(editor.Bounds.Width >= 36, "The numeric editor must show all four digits.");
        var presenter = Assert.Single(editor.GetVisualDescendants().OfType<TextPresenter>());
        Assert.True(presenter.IsEffectivelyVisible);
        Assert.Equal(editor.Text, presenter.Text);
        var glyphSize = new global::Avalonia.Size(presenter.TextLayout.Width, presenter.TextLayout.Height);
        Assert.True(glyphSize.Width > 0 && glyphSize.Height > 0);
        var hosts = presenter.GetVisualAncestors().OfType<Control>().Prepend(presenter)
            .TakeWhile(control => control != input).Where(static control => control.ClipToBounds).Append(input);
        foreach (var host in hosts)
        {
            var origin = global::Avalonia.VisualExtensions.TranslatePoint(presenter, default, host)!.Value;
            Assert.True(origin.X >= -0.5 && origin.X + glyphSize.Width <= host.Bounds.Width + 0.5,
                $"Numeric text {editor.Text} is clipped by {host.GetType().Name}: {origin.X} + {glyphSize.Width} / {host.Bounds.Width}.");
        }
        TestContext.Current.TestOutputHelper?.WriteLine($"Numeric {editor.Text}: glyphs {glyphSize}, input width {input.Bounds.Width}.");
    }

    private static global::Avalonia.Rect BoundsIn(Control control, Control host) =>
        new(global::Avalonia.VisualExtensions.TranslatePoint(control, default, host)!.Value, control.Bounds.Size);

    private static void AssertTextFits(TextBlock text)
    {
        Assert.True(text.IsEffectivelyVisible && text.Bounds.Width > 0 && text.Bounds.Height > 0,
            $"{text.Text} must have visible, non-zero bounds: {text.Bounds}.");
        Assert.True(text.TextLayout.Width <= text.Bounds.Width + 0.5 && text.TextLayout.Height <= text.Bounds.Height + 0.5,
            $"{text.Text} is clipped: text {text.TextLayout.Width} x {text.TextLayout.Height}, bounds {text.Bounds}.");
        foreach (var ancestor in text.GetVisualAncestors().OfType<Control>().Where(static control => control.ClipToBounds))
        {
            AssertWithin(text, ancestor, horizontalOnly: false);
        }
    }

    private static void AssertNoOverlap(Control[] controls, Control host)
    {
        for (var index = 0; index < controls.Length; index++)
        {
            var first = controls[index];
            var firstBounds = new global::Avalonia.Rect(global::Avalonia.VisualExtensions.TranslatePoint(first, default, host)!.Value, first.Bounds.Size);
            foreach (var second in controls.Skip(index + 1))
            {
                if (first.GetVisualAncestors().Contains(second) || second.GetVisualAncestors().Contains(first)) continue;
                var secondBounds = new global::Avalonia.Rect(global::Avalonia.VisualExtensions.TranslatePoint(second, default, host)!.Value, second.Bounds.Size);
                var intersection = firstBounds.Intersect(secondBounds);
                Assert.True(intersection.Width <= 0.5 || intersection.Height <= 0.5,
                    $"{first.GetType().Name} overlaps {second.GetType().Name}.");
            }
        }
    }
}
