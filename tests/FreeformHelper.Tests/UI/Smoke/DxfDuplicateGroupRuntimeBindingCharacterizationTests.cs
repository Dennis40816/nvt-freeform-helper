// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Views.WorkflowSteps;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfDuplicateGroupRuntimeBindingCharacterizationTests
{
    [AvaloniaFact]
    public async Task DuplicateGroupsBinding_ShowsMatchedRowsAndClearsThemAfterLayerFilteringAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var culture = TestCulture.UseEnUs();
        using var files = new LoadingModeTestScope();
        var vm = await DxfCharacterizationWorkspace.OpenAsync(files, false,
            ("alpha", 0, 0, 15, 10), ("beta", 0, 0, 14, 10), ("other", 30, 0, 40, 20));
        vm.IsStep4Expanded = true;
        var view = new RightWorkflowStep4View { DataContext = vm };
        var window = new Window { Width = 600, Height = 1400, Content = view };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var rows = Assert.Single(view.GetVisualDescendants().OfType<ItemsControl>());
            Assert.Empty(rows.Items);

            await vm.MatchCommand.ExecuteAsync(null);
            vm.IsStep4Expanded = true;
            await HeadlessUiPump.FlushQueueAsync();

            var group = Assert.Single(vm.Step4DuplicateDiffGroups);
            Assert.Same(group, Assert.Single(rows.Items));
            Assert.Contains(rows.GetVisualDescendants().OfType<SelectableTextBlock>(), text => text.Text == group.TitleText);
            Assert.Contains(rows.GetVisualDescendants().OfType<SelectableTextBlock>(), text => text.Text == group.CadSummaryText);
            Assert.Single(vm.LayerToggles, static layer => layer.Name == "alpha").IsSelected = false;
            await WaitForGridRebuildAsync(vm);
            vm.IsStep4Expanded = true;
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Empty(rows.Items);
            Assert.False(vm.HasStep4DuplicateDiffGroups);
            Assert.DoesNotContain(rows.GetVisualDescendants().OfType<SelectableTextBlock>(), text => text.Text == group.TitleText);
        }
        finally
        {
            window.Close();
        }
    }
}
