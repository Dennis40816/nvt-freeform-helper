// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views.RightWorkflowSections;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class RightWorkflowInspectorSnapshotDetailsTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void SnapshotDetailsShowOnlyMatchConfidenceWithTheOldSpacing(bool isRegular)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var snapshot = isRegular
            ? PadInspectorDisplayBehaviorTests.CreateRegularSnapshot(4616)
            : PadInspectorDisplayBehaviorTests.CreateCadSnapshot(274);
        var vm = new FreeformHelperViewModel { IsRightPanelSettingsTab = false };
        vm.PadInspectorUi.Snapshot = snapshot;
        vm.PadInspectorUi.IsDebugDetailsExpanded = true;
        var view = new RightWorkflowInspectorView { DataContext = vm };
        var window = new Window { Width = 420, Height = 900, Content = view };
        HeadlessSessionGuardAttribute.CloseAtTestEnd(window);

        window.Show();
        HeadlessUiPump.Drain(window);

        var heading = view.GetVisualDescendants().OfType<SelectableTextBlock>()
            .Single(block => block.Text == "Snapshot details");
        var panel = heading.GetVisualAncestors().OfType<StackPanel>().First();
        var texts = panel.GetVisualDescendants().OfType<SelectableTextBlock>()
            .Where(block => block.IsEffectivelyVisible).ToArray();
        var details = Assert.IsType<PadInspectorSnapshotDetails>(snapshot.Details);
        string[] expected = ["Snapshot details", "Match confidence", details.MatchConfidenceText];
        Assert.Equal(expected, texts.Select(block => block.Text));

        var grid = texts[1].GetVisualAncestors().OfType<Grid>().First();
        var headerGrid = Assert.IsType<Grid>(heading.Parent);
        var gridOrigin = grid.TranslatePoint(default, panel)!.Value;
        double gap = Assert.IsType<double>(view.FindResource("Space6"));
        // The old pair of content controls left an empty slot before regular details and after CAD details.
        Assert.Equal(headerGrid.Bounds.Bottom + gap + (snapshot.HasRegular ? gap : 0), gridOrigin.Y, 6);
        Assert.Equal(snapshot.HasCad ? gap : 0, panel.Bounds.Height - gridOrigin.Y - grid.Bounds.Height, 6);
        Assert.Equal(
            Assert.IsType<double>(view.FindResource("Space8")),
            texts[2].TranslatePoint(default, grid)!.Value.X - texts[1].Bounds.Right,
            6);
    }
}
