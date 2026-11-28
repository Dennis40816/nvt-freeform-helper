// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views.RightWorkflowSections;
using FreeformHelper.UI.Views.WorkflowSteps;
using Nvt.Core.Avalonia.Panels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class CompactWorkflowCardsTests
{
    [AvaloniaFact]
    public async Task RemovedHelp_RemainsOnRelevantControlsAndTracksLiveBindingsAsync()
    {
        using var culture = TestCulture.UseEnUs();
        HeadlessAppBootstrap.EnsureInitialized();
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await CreateWorkspaceAsync(files);
        vm.IsStep1Expanded = true;
        vm.IsStep5Expanded = true;
        var step1 = new RightWorkflowStep1View { DataContext = vm };
        var step5 = new RightWorkflowStep5View { DataContext = vm };
        var window = new Window { Content = new StackPanel { Children = { step1, step5 } }, Width = 320, Height = 900 };
        try
        {
            window.Show();
            await ExpandCardAsync(step1, "Step 1 · Geometry match");
            var threshold = Assert.Single(step1.GetVisualDescendants().OfType<SelectableTextBlock>(),
                static text => text.Text?.StartsWith("Diagnostic threshold:", StringComparison.Ordinal) == true);
            await AssertTipAsync(threshold, vm.WorkspaceMatchSummary, "The threshold changes diagnostic visibility only.");
            await vm.MatchCommand.ExecuteAsync(null);
            vm.IsStep1Expanded = true;
            vm.IsStep5Expanded = true;
            vm.MatchThreshold = 0.42m;
            await ExpandCardAsync(step1, "Step 1 · Geometry match");
            Assert.Equal("Diagnostic threshold: 0.42", threshold.Text);
            await AssertTipAsync(threshold, "Overlap links are ready.", "The threshold changes diagnostic visibility only.");
            var mask = FindPanel(step1, "Regular Visibility Mask");
            await AssertTipAsync(mask, vm.RegularMask.Summary);
            await AssertTipAsync(FindCommandButton(step1, vm.ClearStep1ResultCommand),
                "Clears matching and invalidates Steps 2 to 5 and validation.");
            AssertHelpIsAbsentFromBody(step1, "Overlap links are ready.", "The threshold changes diagnostic visibility only.",
                "Clears matching and invalidates Steps 2 to 5 and validation.");
            await ExpandCardAsync(step5, "Step 5 · Notch export");
            await AssertTipAsync(FindText(step5, vm.NotchExportHandoffFileText), vm.NotchExportHandoffChecklistText);
            await AssertTipAsync(FindText(step5, vm.NotchExportHandoffVersionText), "Handoff checklist", vm.NotchExportHandoffChecklistText);
            await AssertTipAsync(FindText(step5, vm.NotchExportHandoffProfileText), "Review export safety",
                vm.NotchExportSafetyPolicySummary, "Guard changes require a new export.");
            await AssertTipAsync(FindText(step5, vm.NotchEmsSafetyShortText), vm.NotchEmsSafetyPolicySummary);
            await AssertTipAsync(FindCommandButton(step5, vm.ClearStep5ResultCommand),
                "Clears the export result and Validation Quick Trace.");
            AssertHelpIsAbsentFromBody(step5, "Handoff checklist", "Review export safety", "Guard changes require a new export.",
                "Clears the export result and Validation Quick Trace.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task RejectedMaskReason_StaysVisibleInStep1Async()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await CreateWorkspaceAsync(files);
        vm.IsStep1Expanded = true;
        var step1 = new RightWorkflowStep1View { DataContext = vm };
        var window = new Window { Content = step1, Width = 320, Height = 600 };
        var path = Path.Combine(TestFiles.CreateTempDirectory().FullName, "SeeRegular.csv");
        await TestFiles.WriteTextAsync(path, "1,0\r\n", TestContext.Current.CancellationToken);
        try
        {
            window.Show();
            await ExpandCardAsync(step1, "Step 1 · Geometry match");
            AssertHelpIsAbsentFromBody(step1, vm.RegularMask.Summary);
            vm.RegularMask.PickOpenRegularVisibilityMaskPathAsync = () => Task.FromResult<string?>(path);
            await vm.RegularMask.ImportRegularVisibilityMaskCommand.ExecuteAsync(null);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.True(vm.RegularMask.HasLoadProblem);
            Assert.True(FindText(step1, vm.RegularMask.Summary).IsEffectivelyVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task OverviewTooltips_PreserveSafetyWarningAndReadinessValuesAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await CreateWorkspaceAsync(files);
        vm.IsRightPanelSettingsTab = true;
        vm.ApplySimulationSafetyOverview(null, isStale: false, buildFailureText: "Synthetic safety failure.");
        var view = new RightWorkflowSettingsTabView { DataContext = vm };
        var window = new Window { Content = view, Width = 320, Height = 900 };
        try
        {
            window.Show();
            var overview = FindPanel(view, "Overview");
            overview.IsExpanded = true;
            await HeadlessUiPump.FlushQueueAsync();
            var warning = Assert.Single(overview.GetVisualDescendants().OfType<Border>(),
                static border => border.Classes.Contains("chipStatus"));
            Assert.True(warning.IsEffectivelyVisible);
            await AssertTipAsync(warning, "Simulation safety", "Synthetic safety failure.");
            var counts = Assert.Single(overview.GetVisualDescendants().OfType<Grid>(),
                static grid => grid.Classes.Contains("workflowOverviewInfoGrid"));
            await AssertTipAsync(counts, "Project / grid");
            Assert.True(FindText(overview, vm.PitchSizeXSummary).IsEffectivelyVisible);
            Assert.True(FindText(overview, vm.PitchSizeYSummary).IsEffectivelyVisible);
            var cadCount = Assert.Single(overview.GetVisualDescendants().OfType<SelectableTextBlock>(),
                static text => ToolTip.GetTip(text) is TextBlock tip && tip.Text == "Loaded CAD pad count after DXF import and current DXF edits.");
            Assert.Equal(vm.CadPads.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), cadCount.Text);
            AssertHelpIsAbsentFromBody(overview, "Simulation safety", "Project / grid");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(220)]
    [InlineData(328)]
    public async Task ExpandedCards_FitRightPanelIncludingPaddingAndScrollbarAsync(double width)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await CreateWorkspaceAsync(files);
        vm.IsRightPanelSettingsTab = true;
        vm.IsStep1Expanded = true;
        vm.IsStep5Expanded = true;
        vm.ApplySimulationSafetyOverview(null, isStale: false, buildFailureText: "Synthetic safety failure.");
        vm.NotchExportSummary = "CSV review exported: rows=123456.";
        var view = new RightWorkflowSettingsTabView { DataContext = vm };
        var content = new Border
        {
            Classes = { "viewportBoundContent" },
            Padding = Resource<global::Avalonia.Thickness>("InsetRight12Bottom20"),
            Child = view,
        };
        var scroll = new ScrollViewer
        {
            Classes = { "viewportBoundScroll" },
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content,
        };
        var pane = new Border { Classes = { "workspaceSidePane", "right" }, Child = scroll };
        var window = new Window { Content = pane, Width = width, Height = 320 };
        try
        {
            window.Show();
            await ExpandCardAsync(view, "Step 1 · Geometry match");
            var overview = FindPanel(view, "Overview");
            overview.IsExpanded = true;
            var mask = FindPanel(view, "Regular Visibility Mask");
            mask.IsExpanded = true;
            await HeadlessUiPump.FlushQueueAsync();
            Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
            AssertReadableCard(overview);
            AssertReadableCard(FindPanel(view, "Step 1 · Geometry match"));
            Assert.True(FindCommandButton(view, vm.RegularMask.ImportRegularVisibilityMaskCommand).IsEffectivelyEnabled);
            Assert.True(FindCommandButton(view, vm.RegularMask.ClearRegularVisibilityMaskCommand).IsEffectivelyEnabled);
            await ExpandCardAsync(view, "Step 5 · Notch export");
            AssertReadableCard(FindPanel(view, "Step 5 · Notch export"));
        }
        finally
        {
            window.Close();
        }
    }

    internal static CollapsiblePanel FindPanel(Control view, string title) =>
        Assert.Single(view.GetVisualDescendants().OfType<CollapsiblePanel>(), panel => panel.Title == title);

    internal static Button FindCommandButton(Control view, ICommand command) =>
        Assert.Single(view.GetVisualDescendants().OfType<Button>(), button => ReferenceEquals(button.Command, command));

    private static SelectableTextBlock FindText(Control view, string text) =>
        Assert.Single(view.GetVisualDescendants().OfType<SelectableTextBlock>(), block => block.Text == text);

    internal static void ActivateButton(Window window, Button button)
    {
        Assert.True(button.IsEffectivelyEnabled);
        Assert.True(button.Focus());
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
    }

    internal static async Task ExpandCardAsync(Control view, string title)
    {
        FindPanel(view, title).IsExpanded = true;
        await HeadlessUiPump.FlushQueueAsync();
    }

    private static T Resource<T>(string key)
    {
        var app = Assert.IsType<FreeformHelper.UI.App>(global::Avalonia.Application.Current);
        Assert.True(app.TryGetResource(key, app.RequestedThemeVariant, out var resource));
        return Assert.IsType<T>(resource);
    }

    private static async Task AssertTipAsync(Control control, params string[] expected)
    {
        try
        {
            ToolTip.SetIsOpen(control, true);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.True(ToolTip.GetIsOpen(control));
            var tip = Assert.IsAssignableFrom<Control>(ToolTip.GetTip(control));
            var lines = tip.GetVisualDescendants().Prepend(tip).OfType<TextBlock>().Select(static text => text.Text).ToArray();
            Assert.All(expected, text => Assert.Contains(text, lines));
        }
        finally
        {
            ToolTip.SetIsOpen(control, false);
            await HeadlessUiPump.FlushQueueAsync();
        }
    }

    private static void AssertHelpIsAbsentFromBody(Control view, params string[] sentences)
    {
        var text = view.GetVisualDescendants().OfType<Control>().Where(static control => control.IsEffectivelyVisible)
            .Select(static control => control switch
            {
                SelectableTextBlock block => block.Text,
                TextBlock block => block.Text,
                _ => null,
            }).ToArray();
        Assert.All(sentences, sentence => Assert.DoesNotContain(sentence, text));
    }

    private static void AssertReadableCard(CollapsiblePanel panel)
    {
        foreach (var control in panel.GetVisualDescendants().OfType<Control>().Where(static control => control.IsEffectivelyVisible))
        {
            var layout = control switch
            {
                SelectableTextBlock text => text.TextLayout,
                TextBlock text => text.TextLayout,
                _ => null,
            };
            if (layout is null) continue;
            Assert.All(layout.TextLines, line => Assert.False(line.HasCollapsed, $"Truncated text in {panel.Title}: {((TextBlock)control).Text}"));
            Assert.True(layout.Width <= control.Bounds.Width + 1, $"Clipped text in {panel.Title}: {control}");
            Assert.True(layout.Height <= control.Bounds.Height + 1, $"Clipped text in {panel.Title}: {control}");
            var origin = global::Avalonia.VisualExtensions.TranslatePoint(control, default, panel);
            Assert.NotNull(origin);
            Assert.True(origin.Value.X >= -1 && origin.Value.X + control.Bounds.Width <= panel.Bounds.Width + 1,
                $"Text outside {panel.Title}: {control}");
        }
    }

    internal static async Task<FreeformHelperViewModel> CreateWorkspaceAsync(OperationProgressLifecycleTestScope files)
    {
        var vm = await files.CreateLoadedAsync();
        vm.NotchPreviewAutoPlayEnabled = false;
        await vm.WaitForGridRebuildIdleAsync();
        await HeadlessUiPump.FlushQueueAsync();
        return vm;
    }
}
