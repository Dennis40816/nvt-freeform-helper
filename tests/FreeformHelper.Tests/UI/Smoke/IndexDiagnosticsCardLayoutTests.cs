// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views.WorkflowSteps;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class IndexDiagnosticsCardLayoutTests
{
    private static readonly ThemeVariant[] _themes = [ThemeVariant.Dark, ThemeVariant.Light];

    [AvaloniaFact]
    public async Task CardRetainsCommandsShiftBindingAndEnableRulesAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var files = new LoadingModeTestScope();
        var model = await OpenDuplicateWorkspaceAsync(files);
        var view = new RightWorkflowStep4View { DataContext = model };
        var window = new Window { Width = 360, Height = 1200, Content = view };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var buttons = view.GetVisualDescendants().OfType<Button>().ToArray();
            Assert.Single(buttons, button => button.Command == model.AnalyzeIndexMappingCommand);
            Assert.Single(buttons, button => button.Command == model.ClearStep4ResultCommand);
            var select = Assert.Single(buttons, button => button.Command == model.SelectStep4DuplicateDiffGroupCommand);
            Assert.Same(Assert.Single(model.Step4DuplicateDiffGroups), select.CommandParameter);
            select.Command!.Execute(select.CommandParameter);
            await HeadlessUiPump.FlushQueueAsync();
            var shift = Assert.Single(buttons, button => button.Command == model.OffsetSelectedCadOutputFwDiffIndicesCommand);
            Assert.Equal(model.CanOffsetSelectedCadOutputFwDiffIndices, shift.IsEnabled);
            Assert.True(shift.IsEnabled);
            Assert.Single(view.GetVisualDescendants().OfType<SelectableTextBlock>(), text =>
                text.IsEffectivelyVisible && text.Text?.StartsWith("Gap diffs stay empty.", StringComparison.Ordinal) == true);
            var input = Assert.Single(view.GetVisualDescendants().OfType<NumberScrubber>());
            input.Value = -3;
            Assert.Equal(-3, model.Step4Index.ShiftDelta);
            model.Step4Index.ShiftDelta = -2;
            Assert.Equal(-2, input.Value);
            model.ClearStep4ResultCommand.Execute(null);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal(model.CanOffsetSelectedCadOutputFwDiffIndices, shift.IsEnabled);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task CountsAppearOnceAndCardFitsMinimumPanelWidthAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var culture = TestCulture.UseEnUs();
        using var files = new LoadingModeTestScope();
        var model = await OpenDuplicateWorkspaceAsync(files);
        var view = new RightWorkflowStep4View { DataContext = model };
        var window = new Window { Height = 1200, Content = view };
        try
        {
            window.Width = Assert.IsType<double>(view.FindResource("PanelRightMinWidth"));
            window.Show();
            foreach (var theme in _themes)
            {
                window.RequestedThemeVariant = theme;
                await HeadlessUiPump.FlushQueueAsync();
                var group = Assert.Single(model.Step4DuplicateDiffGroups);
                var texts = view.GetVisualDescendants().OfType<SelectableTextBlock>()
                    .Where(static text => text.IsEffectivelyVisible).ToArray();
                Assert.Equal("Duplicate Output FW Diff · 2 CAD in 1 group", model.Step4Index.DuplicateDiffSummary);
                Assert.Equal("CAD Output FW Diff: 3 auto · 0 override", model.Step4Index.AssignmentCountText);
                var heading = Assert.Single(texts, text => text.Text == model.Step4Index.DuplicateDiffSummary);
                var row = Assert.Single(texts, text => text.Text == group.CompactText);
                var assignment = Assert.Single(texts, text => text.Text == model.Step4Index.AssignmentCountText);
                Assert.Equal($"{group.TitleText} · CAD {string.Join(", ", group.CadPadIds)}", row.Text);
                Assert.DoesNotContain(texts, text => text.Text == group.CadSummaryText || text.Text == group.SuggestionText);
                Assert.DoesNotContain(texts, text => text.Text == model.CadOutputFwDiffAssignmentSummary);
                Assert.DoesNotContain(texts, text => text.Text == model.DxfRegularMappingSummary);
                Assert.Equal(group.CadSummaryText, Assert.IsType<TextBlock>(ToolTip.GetTip(row)).Text);
                var buttons = view.GetVisualDescendants().OfType<Button>().ToArray();
                var select = Assert.Single(buttons, button => button.Command == model.SelectStep4DuplicateDiffGroupCommand);
                Assert.Equal(group.SuggestionText, Assert.IsType<TextBlock>(ToolTip.GetTip(select)).Text);
                var analyze = Assert.Single(buttons, button => button.Command == model.AnalyzeIndexMappingCommand);
                var shift = Assert.Single(buttons, button => button.Command == model.OffsetSelectedCadOutputFwDiffIndicesCommand);
                var clear = Assert.Single(buttons, button => button.Command == model.ClearStep4ResultCommand);
                Assert.True(Position(analyze, view).Y < Position(heading, view).Y);
                Assert.True(Position(row, view).Y < Position(shift, view).Y);
                Assert.True(Position(shift, view).Y < Position(assignment, view).Y);
                Assert.True(Position(assignment, view).Y < Position(clear, view).Y);
                AssertContentFits(view);
            }

            Assert.Single(model.LayerToggles, static layer => layer.Name == "alpha").IsSelected = false;
            await FreeformHelperViewModelTests.WaitForGridRebuildAsync(model);
            model.IsStep4Expanded = true;
            await HeadlessUiPump.FlushQueueAsync();
            Assert.False(model.HasStep4DuplicateDiffGroups);
            Assert.DoesNotContain(view.GetVisualDescendants().OfType<SelectableTextBlock>(),
                text => text.IsEffectivelyVisible && text.Text == model.Step4Index.DuplicateDiffSummary);
            AssertContentFits(view);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task AssignmentLineCountsPadsThatStayWithoutADiffAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var culture = TestCulture.UseEnUs();
        using var files = new LoadingModeTestScope();
        var model = await DxfCharacterizationWorkspace.OpenAsync(files, false,
            ("alpha", 0, 0, 15, 10), ("far", 400, 400, 410, 410));
        await model.MatchCommand.ExecuteAsync(null);

        Assert.EndsWith(" · 1 unresolved", model.Step4Index.AssignmentCountText, StringComparison.Ordinal);
    }

    private static void AssertContentFits(Control root)
    {
        foreach (var control in root.GetVisualDescendants().OfType<Control>()
                     .Where(static control => control.IsEffectivelyVisible && control is Button or SelectableTextBlock or NumberScrubber or TextBlock { Text: { Length: > 0 } }))
        {
            var position = Position(control, root);
            Assert.True(control.Bounds.Width > 0, $"{control} has no width.");
            Assert.InRange(position.X, -1, root.Bounds.Width);
            Assert.InRange(position.X + control.Bounds.Width, 0, root.Bounds.Width + 1);
            Assert.True(control.Bounds.Height + 1 >= control.DesiredSize.Height, $"{control} is vertically clipped.");
            Assert.InRange(position.Y + control.Bounds.Height, 0, root.Bounds.Height + 1);
        }
    }

    private static global::Avalonia.Point Position(Control control, Control root) =>
        Assert.IsType<global::Avalonia.Point>(global::Avalonia.VisualExtensions.TranslatePoint(control, default, root));

    private static async Task<FreeformHelperViewModel> OpenDuplicateWorkspaceAsync(
        LoadingModeTestScope files)
    {
        var model = await DxfCharacterizationWorkspace.OpenAsync(files, false,
            ("alpha", 0, 0, 15, 10), ("beta", 0, 0, 14, 10), ("other", 30, 0, 40, 20));
        await model.MatchCommand.ExecuteAsync(null);
        model.IsStep4Expanded = true;
        return model;
    }
}
