// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using FreeformHelper.UI.Views.WorkspaceSections;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class IndexMappingReportIntegratedLayoutTests
{
    private static string? TipText(Control control) =>
        ToolTip.GetTip(control) switch
        {
            string text => text,
            TextBlock block => block.Text,
            _ => null,
        };

    private static readonly string[] _step4FooterLabels = ["Clear override", "Apply override", "Apply diff repair"];
    private static readonly string[] _step5FooterLabels = ["Apply segment repairs", "Apply visible repairs"];
    private static readonly string[] _columnLabels = ["CAD", "REG", "Result", "Reason"];
    private static readonly ThemeVariant[] _themes = [ThemeVariant.Dark, ThemeVariant.Light];

    [AvaloniaFact]
    public async Task OverviewAppearsOnceAndFilterModeUsesOnlyToggleSelectionAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var model = CreateViewModel();
        var window = new IndexMappingReportWindow { DataContext = model };
        try
        {
            window.Show();
            foreach (var theme in _themes)
            {
                window.RequestedThemeVariant = theme;
                await HeadlessUiPump.FlushQueueAsync();
                Assert.Single(VisibleText(window), text => text == model.OpenIssueSummaryText);
                Assert.Single(VisibleText(window), text => text == model.OverallStatusSummaryText);
                Assert.Single(VisibleText(window), text => text == model.MaskAuditSummary);
                Assert.Single(VisibleText(window), static text => text!.Contains("Open issues", StringComparison.Ordinal));
                Assert.DoesNotContain(VisibleText(window), static text => text!.StartsWith("Mode", StringComparison.Ordinal));
                Assert.DoesNotContain(VisibleText(window), text => text == model.SelectedFilterLabel);
            }

            var filters = Assert.Single(window.GetVisualDescendants().OfType<IndexMappingReportFilterBarView>());
            var toggles = filters.GetVisualDescendants().OfType<ToggleButton>()
                .Where(toggle => toggle.Command == model.SetDecisionFilterModeCommand).ToArray();
            Assert.Equal(Enum.GetValues<IndexMappingDecisionFilterMode>().Length, toggles.Length);
            Assert.All(toggles, toggle => Assert.Same(model.SetDecisionFilterModeCommand, toggle.Command));
            var all = Assert.Single(toggles, static toggle => toggle.IsChecked == true);
            Click(window, all);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.True(all.IsChecked);
            var changed = Assert.Single(toggles,
                static toggle => toggle.CommandParameter is IndexMappingDecisionFilterMode.ChangedByMask);
            Click(window, changed);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Same(changed, Assert.Single(toggles, static toggle => toggle.IsChecked == true));
            Assert.Contains("active", changed.Classes);
            Assert.Equal(model.ChangedFilterText, changed.Content);
            Click(window, changed);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal(IndexMappingDecisionFilterMode.All,
                Assert.Single(toggles, static toggle => toggle.IsChecked == true).CommandParameter);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task RepairStepsHaveOnePrimaryAndFooterPlacesSecondaryActionsFirstOnTheRightAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var model = CreateViewModel();
        var window = new IndexMappingReportWindow { DataContext = model };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var footer = Assert.Single(window.GetVisualDescendants().OfType<IndexMappingReportFooterView>());
            var actions = Assert.IsType<StackPanel>(footer.FindControl<StackPanel>("RepairActions"));
            Assert.Equal(HorizontalAlignment.Right, actions.HorizontalAlignment);
            Assert.Equal(1, Grid.GetColumn(actions));
            foreach (var step in Enum.GetValues<IndexMappingVerificationStep>())
            {
                model.SelectVerificationStepCommand.Execute(step);
                await HeadlessUiPump.FlushQueueAsync();
                var visible = actions.Children.OfType<Button>().Where(static button => button.IsEffectivelyVisible).ToArray();
                var primary = window.GetVisualDescendants().OfType<Button>()
                    .Where(static button => button.IsEffectivelyVisible && button.Classes.Contains("actionPrimary")).ToArray();
                if (step is IndexMappingVerificationStep.Step4 or IndexMappingVerificationStep.Step5)
                {
                    Assert.Same(visible[^1], Assert.Single(primary));
                    Assert.All(visible[..^1], static button => Assert.Contains("actionNeutral", button.Classes));
                    Assert.Equal(step == IndexMappingVerificationStep.Step4 ? _step4FooterLabels : _step5FooterLabels,
                        visible.Select(static button => button.Content as string));
                    Assert.Same(step == IndexMappingVerificationStep.Step4
                        ? model.ApplyDiffOverrideCommand : model.ApplyCadOutputFwDiffOverridesCommand, primary[0].Command);
                }
                else
                {
                    Assert.Empty(primary);
                    Assert.Empty(visible);
                }
            }

            model.ActionStatus = "Repair complete.";
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal(model.ActionStatus, footer.FindControl<SelectableTextBlock>("LastActionStatus")!.Text);
            AssertRepairCommands(footer, model);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task DiagnosticsStartCollapsedAndExpandedDetailsKeepAttributionSingleAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var model = CreateViewModel();
        var work = new RecordingBackgroundWorkRunner();
        model.BackgroundWork.Runner = work;
        var window = new IndexMappingReportWindow { DataContext = model };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var sections = window.GetVisualDescendants().OfType<HidePanelBlock>().ToArray();
            Assert.Equal(4, sections.Length);
            Assert.All(sections, static section => Assert.False(section.IsExpanded));
            Assert.False(model.IsAlgorithmTraceExpanded);
            Assert.Equal(0, work.StartedCount(IndexMappingReportViewModel.TraceWorkName));
            AssertAttributionOnce(window);
            foreach (var section in sections)
            {
                section.IsExpanded = true;
            }

            await work.LastStartedAsync(IndexMappingReportViewModel.TraceWorkName);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.True(model.IsAlgorithmTraceExpanded);
            AssertAttributionOnce(window);
            Assert.DoesNotContain(model.AlgorithmTraceDetailEntries, static entry => entry.Label is "source" or "confidence");
            model.SelectVerificationStepCommand.Execute(IndexMappingVerificationStep.Step5);
            await HeadlessUiPump.FlushQueueAsync();
            AssertAttributionOnce(window);
            Assert.Single(VisibleText(window), text => text == model.OpenIssueSummaryText);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task ControlFFocusesSearchAndEscapeAndClearRestoreRowsAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var model = CreateViewModel();
        var window = new IndexMappingReportWindow { DataContext = model };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var filters = Assert.Single(window.GetVisualDescendants().OfType<IndexMappingReportFilterBarView>());
            var search = Assert.IsType<TextBox>(filters.FindControl<TextBox>("SearchBox"));
            var clear = Assert.Single(filters.GetVisualDescendants().OfType<Button>(),
                button => button.Command == model.ClearSearchKeywordCommand);
            Assert.False(clear.IsVisible);
            Assert.Single(window.GetVisualDescendants().OfType<Button>(),
                button => button.Command == model.ApplyDiffOverrideCommand).Focus();
            window.KeyPress(Key.F, RawInputModifiers.Control, PhysicalKey.F, "f");
            window.KeyRelease(Key.F, RawInputModifiers.Control, PhysicalKey.F, "f");
            Assert.Same(search, window.FocusManager!.GetFocusedElement());
            window.KeyTextInput("CAD 9201");
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal("CAD 9201", model.SearchKeyword);
            Assert.Single(model.DecisionRows);
            Assert.True(clear.IsVisible);
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal(string.Empty, search.Text);
            Assert.Equal(2, model.DecisionRows.Count);
            Assert.Equal(IndexMappingDecisionFilterMode.All, model.SelectedFilterMode);
            model.SearchKeyword = "CAD 9202";
            clear.Command!.Execute(clear.CommandParameter);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal(string.Empty, model.SearchKeyword);
            Assert.False(clear.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task FourColumnsKeepSelectionLocateTooltipsAndOptionalStepSyncAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var located = new List<(int? Cad, int? Regular)>();
        var model = CreateViewModel((cad, regular) => located.Add((cad, regular)));
        var window = new IndexMappingReportWindow { DataContext = model };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var activeStep = Assert.Single(window.GetVisualDescendants().OfType<ToggleButton>(),
                button => button.Command == model.SelectVerificationStepCommand && button.IsChecked == true);
            Click(window, activeStep);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal(IndexMappingVerificationStep.Step4,
                Assert.Single(window.GetVisualDescendants().OfType<ToggleButton>(),
                    button => button.Command == model.SelectVerificationStepCommand && button.IsChecked == true).CommandParameter);
            var left = Assert.Single(window.GetVisualDescendants().OfType<IndexMappingReportLeftPaneView>());
            var header = Assert.Single(left.GetVisualDescendants().OfType<Border>(),
                static border => border.Classes.Contains("workspaceTableHeader"));
            Assert.Equal(_columnLabels, header.GetVisualDescendants().OfType<TextBlock>().Select(static text => text.Text));
            var list = Assert.IsType<ListBox>(left.FindControl<ListBox>("DecisionList"));
            var row = model.DecisionRows[1];
            var rowButton = Assert.Single(list.GetVisualDescendants().OfType<Button>(),
                button => ReferenceEquals(button.CommandParameter, row));
            Assert.Same(model.SelectDecisionCommand, rowButton.Command);
            rowButton.Command!.Execute(rowButton.CommandParameter);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Same(row, model.SelectedDecision);
            Assert.Same(row, list.SelectedItem);
            Assert.Equal((row.CadPadId, row.LocateRegularPadIndex), Assert.Single(located));
            var result = Assert.Single(rowButton.GetVisualDescendants().OfType<TextBlock>(),
                static text => Grid.GetColumn(text) == 2);
            var reason = Assert.Single(rowButton.GetVisualDescendants().OfType<TextBlock>(),
                static text => Grid.GetColumn(text) == 3);
            Assert.Equal(row.StatusDisplay, TipText(result));
            Assert.Equal(row.ReasonText, TipText(reason));
            Assert.False(model.SyncFilterWithStepRail);
            model.SelectedFilterMode = IndexMappingDecisionFilterMode.ChangedByMask;
            model.SelectVerificationStepCommand.Execute(IndexMappingVerificationStep.Step5);
            Assert.Equal(IndexMappingDecisionFilterMode.ChangedByMask, model.SelectedFilterMode);
            model.SyncFilterWithStepRail = true;
            model.SelectVerificationStepCommand.Execute(IndexMappingVerificationStep.Step5);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal(IndexMappingDecisionFilterMode.CountMismatch, model.SelectedFilterMode);
            Assert.Equal("Sync filter with step", Assert.Single(window.GetVisualDescendants().OfType<CheckBox>()).Content);
            Assert.Same(model.LocateSelectedIssueCommand, Assert.Single(window.GetVisualDescendants().OfType<Button>(),
                button => button.Content as string == "Locate").Command);
        }
        finally
        {
            window.Close();
        }
    }

    private static void AssertAttributionOnce(Control root)
    {
        Assert.Single(VisibleText(root), static text => text == "Source");
        Assert.Single(VisibleText(root), static text => text == "Confidence");
    }

    private static void Click(Window window, Control target)
    {
        var point = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window);
        Assert.NotNull(point);
        window.MouseDown(point.Value, MouseButton.Left);
        window.MouseUp(point.Value, MouseButton.Left);
    }

    private static void AssertRepairCommands(Control footer, IndexMappingReportViewModel model)
    {
        var buttons = footer.GetVisualDescendants().OfType<Button>().ToArray();
        Assert.Equal(model.CanClearSelectedIssueOverride,
            Assert.Single(buttons, button => button.Command == model.ClearOverrideCommand).IsEnabled);
        Assert.Equal(model.CanApplySelectedIssueOverride,
            Assert.Single(buttons, button => button.Command == model.ApplyOverrideCommand).IsEnabled);
        Assert.Equal(model.CanApplySelectedDiffOverride,
            Assert.Single(buttons, button => button.Command == model.ApplyDiffOverrideCommand).IsEnabled);
        Assert.Equal(model.CanApplySelectedSegmentDiffOverrides,
            Assert.Single(buttons, button => button.Command == model.ApplySegmentDiffOverridesCommand).IsEnabled);
        Assert.Equal(model.CanApplyCadOutputFwDiffOverrides,
            Assert.Single(buttons, button => button.Command == model.ApplyCadOutputFwDiffOverridesCommand).IsEnabled);
    }

    private static IEnumerable<string?> VisibleText(Control root) =>
        root.GetVisualDescendants().OfType<Control>().Where(static control => control.IsEffectivelyVisible)
            .Select(static control => control switch
            {
                SelectableTextBlock text => text.Text,
                TextBlock text => text.Text,
                _ => null,
            }).Where(static text => text is not null);

    private static IndexMappingReportViewModel CreateViewModel(Action<int?, int?>? locate = null)
    {
        var mismatch = new DxfRegularMappingIssue(DxfRegularMappingIssueKind.CountMismatch, "count mismatch");
        var report = new DxfRegularMappingReport(
            CadCount: 2, RegularCount: 3, MappedCount: 2, UnmappedCadCount: 0, UnmappedRegularCount: 0,
            LowConfidenceCount: 0, AmbiguousCount: 0, HasIssues: true, Summary: "summary",
            TotalIssueCount: 1, IssuesTruncated: false, Issues: [mismatch], SampleIssues: [mismatch],
            MaskAuditRows: [CreateMaskRow(9201, 611, 200), CreateMaskRow(9202, 612, 201)],
            MaskChangedCount: 2, MaskRemovedCount: 0);
        return new IndexMappingReportViewModel("summary", report,
            locateTarget: (cad, regular) => { locate?.Invoke(cad, regular); return true; },
            applyOverride: static (_, _) => true,
            applyDiffOverride: static (_, _) => true,
            clearOverride: static _ => true);
    }

    private static DxfRegularMaskAuditRow CreateMaskRow(int cadId, int regularId, int diff) => new(
        CadPadId: cadId, OrderedCadIndex: cadId,
        RawSeed: new CadBestMatchSeed(regularId, 1, diff, 0.8, 0.8),
        MaskedSeed: new CadBestMatchSeed(regularId, 1, diff, 0.8, 0.8),
        CsvConfirmedCandidates: [new DxfRegularMaskAuditCandidate(regularId, 1, diff, 0.8, 0.8)],
        CsvConfirmedConfidence: 0.8, SuggestedDiffIndex: diff, CurrentAssignedDiffIndex: diff - 1,
        Status: DxfRegularMaskAuditStatus.ChangedByMask, Message: "Review diff assignment.",
        Mode: CadOutputFwDiffAssignmentMode.CsvConstrained,
        ReasonCode: CadOutputFwDiffAssignmentReasonCode.GapCompensationCandidate,
        DecisionSource: CadOutputFwDiffAssignmentDecisionSource.AutoRepaired,
        PrimaryAssignedDiffIndex: diff - 1, RepairSuggestionDiffIndex: diff,
        IcIndex: 1, RowIndex: 9, SegmentIndex: 0, SegmentMemberCount: 2);
}
