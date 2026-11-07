// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using FreeformHelper.UI.Views.RightWorkflowSections;
using Nvt.Core.Avalonia.Panels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class WorkspaceIntegratedLayoutTests
{
    private static readonly string[] _expectedPanelTitles =
    [
        "Overview", "Step 1 · Geometry match", "Step 2 · Freeform tagging",
        "Step 3 · Notch compensation", "Step 5 · Notch export", "Index diagnostics", "Validation Quick Trace",
    ];

    [AvaloniaFact]
    public void SharedActionBarShowsOnePrimaryActionForEachWorkState()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var vm = new FreeformHelperViewModel();
        var view = new FreeformHelperView { DataContext = vm };
        var window = Show(view);

        AssertPrimary(window, "Open DXF", vm.OpenDxfCommand);
        SupplyCad(vm);
        AssertPrimary(window, "Match overlaps", vm.MatchCommand);
        vm.IsStep2Expanded = true;
        AssertPrimary(window, "Detect freeform", vm.AutoDetectFreeformsCommand);
        vm.IsStep2Expanded = false;
        AssertPrimary(window, "Detect freeform", vm.AutoDetectFreeformsCommand);
        vm.IsStep3Expanded = true;
        AssertPrimary(window, "Export", vm.ExportNotchCommand);
        vm.IsStep5Expanded = true;
        vm.IsStep4Expanded = true;
        AssertPrimary(window, "Export", vm.ExportNotchCommand);

        vm.IsRightPanelInspectorTab = true;
        HeadlessUiPump.Drain(window);
        Assert.Empty(VisiblePrimaryButtons(window));
    }

    [AvaloniaFact]
    public void StepThreeLeadsDirectlyToExportAndUnnumberedDiagnostics()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var settings = new RightWorkflowSettingsTabView { DataContext = new FreeformHelperViewModel() };
        var window = Show(settings);
        var panels = TopLevelWorkflowPanels(settings);

        Assert.Equal(
            _expectedPanelTitles,
            panels.Select(static panel => panel.Title));
        Assert.Collection(panels,
            static panel => Assert.False(panel.IsExpanded),
            static panel => Assert.True(panel.IsExpanded),
            static panel => Assert.False(panel.IsExpanded),
            static panel => Assert.False(panel.IsExpanded),
            static panel => Assert.False(panel.IsExpanded),
            static panel => Assert.False(panel.IsExpanded),
            static panel => Assert.False(panel.IsExpanded));
        Assert.True(window.IsVisible);
    }

    [AvaloniaFact]
    public void SelectionCountsAppearOnceAcrossSettingsAndInspector()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var vm = new FreeformHelperViewModel();
        SupplyCad(vm);
        var view = new FreeformHelperView { DataContext = vm };
        var window = Show(view);

        vm.ApplyCanvasSelection([101, 102], []);
        vm.IsStep2Expanded = true;
        AssertSelectionCounts(window, vm);
        vm.IsRightPanelInspectorTab = true;
        AssertSelectionCounts(window, vm);
        Assert.DoesNotContain(VisibleTexts(window), static text => text == "2 pads selected");
    }

    [AvaloniaFact]
    public void SourcePanelsAndWorkflowDetailsStartFolded()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var vm = new FreeformHelperViewModel();
        var left = new LeftDxfPanel { DataContext = vm };
        var window = Show(left);
        var sourcePanels = Assert.IsType<StackPanel>(left.Content).Children.OfType<CollapsiblePanel>().ToArray();

        Assert.Collection(sourcePanels,
            static panel => { Assert.Equal("Import & DXF check", panel.Title); Assert.False(panel.IsExpanded); },
            static panel => { Assert.Equal("Layers", panel.Title); Assert.True(panel.IsExpanded); },
            static panel => { Assert.Equal("DXF edits", panel.Title); Assert.False(panel.IsExpanded); },
            static panel => { Assert.Equal("Advanced edit", panel.Title); Assert.False(panel.IsExpanded); });
        window.Content = new RightWorkflowSettingsTabView { DataContext = vm };
        vm.IsStep3Expanded = true;
        HeadlessUiPump.Drain(window);
        Assert.False(FindPanel(window, "Edit guards").IsExpanded);
        Assert.False(FindPanel(window, "Play interval").IsExpanded);
        Assert.False(vm.IsStep3ToFullDetailsExpanded);
        Assert.False(vm.IsStep4Expanded);
        Assert.False(vm.IsStep6Expanded);
    }

    [AvaloniaFact]
    public void RangeMaskStatsAndInspectorDetailsStartFolded()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var vm = new FreeformHelperViewModel();
        var view = new FreeformHelperView { DataContext = vm };
        var window = Show(view);

        Assert.False(Assert.Single(view.GetVisualDescendants().OfType<HidePanelBlock>(),
            static panel => panel.Title == "Range selection").IsExpanded);
        Assert.False(FindPanel(window, "Regular Visibility Mask").IsExpanded);
        vm.IsStep2Expanded = true;
        HeadlessUiPump.Drain(window);
        Assert.False(FindPanel(window, "Stats").IsExpanded);
        vm.IsRightPanelInspectorTab = true;
        HeadlessUiPump.Drain(window);
        Assert.False(FindPanel(window, "Quick focus").IsExpanded);
        Assert.False(vm.PadInspectorUi.IsDebugDetailsExpanded);
        Assert.False(vm.IsPadInspectorRawTraceExpanded);
    }

    [AvaloniaFact]
    public void DisplayAndConsoleStartClosedWithSettingsSelected()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var shell = new ShellViewModel { IsDevPageAvailable = true };
        var vm = new FreeformHelperViewModel();
        var window = Show(new FreeformHelperView { DataContext = vm });
        var header = Assert.Single(window.GetVisualDescendants().OfType<WorkspaceHeader>());

        Assert.True(vm.IsRightPanelSettingsTab);
        Assert.False(shell.IsConsoleExpanded);
        Assert.NotEqual(true, header.FindControl<Avalonia.Controls.Primitives.ToggleButton>("ViewMenuButton")!.IsChecked);
    }

    [AvaloniaFact]
    public void ExpandedCoreHeaderKeepsTheGhostPresenter()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var panel = new CollapsiblePanel { Title = "Flat header", IsExpanded = true };
        var window = Show(panel);
        var header = Assert.Single(panel.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>(),
            static button => button.Classes.Contains("panelBlockHeader"));
        var presenter = Assert.Single(header.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>(),
            static control => control.Name == "PART_ContentPresenter");

        Assert.True(header.IsChecked);
        Assert.Null(presenter.Background);
        Assert.Null(presenter.BorderBrush);
        Assert.True(window.IsVisible);
    }

    private static Window Show(Control content)
    {
        var window = new Window { Content = content, Width = 1400, Height = 1000 };
        HeadlessSessionGuardAttribute.CloseAtTestEnd(window);
        window.Show();
        HeadlessUiPump.Drain(window);
        return window;
    }

    private static void SupplyCad(FreeformHelperViewModel vm)
    {
        var polygon = new Polygon2([new Point2(0, 0), new Point2(10, 0), new Point2(10, 10), new Point2(0, 10)]);
        var pads = new[] { new CadPad(101, "First", "PAD", polygon), new CadPad(102, "Second", "PAD", polygon) };
        FreeformHelperViewModelPrivateAccess.Cad(vm) = new CadPadSet(pads);
        vm.CadPads = new(pads);
    }

    private static void AssertPrimary(Window window, string label, ICommand command)
    {
        HeadlessUiPump.Drain(window);
        var button = Assert.Single(VisiblePrimaryButtons(window));
        Assert.Equal(label, button.Content);
        Assert.Same(command, button.Command);
        Assert.Equal(command.CanExecute(null), button.IsEffectivelyEnabled);
        Assert.IsType<WorkspaceActionBar>(button.GetVisualAncestors().OfType<WorkspaceActionBar>().Single());
    }

    private static IEnumerable<Button> VisiblePrimaryButtons(Window window) =>
        window.GetVisualDescendants().OfType<Button>()
            .Where(static button => button.IsEffectivelyVisible && button.Classes.Contains("actionPrimary"));

    private static IEnumerable<string> VisibleTexts(Window window) =>
        window.GetVisualDescendants().OfType<TextBlock>()
            .Where(static text => text.IsEffectivelyVisible && text.Text is not null)
            .Select(static text => text.Text!);

    private static void AssertSelectionCounts(Window window, FreeformHelperViewModel vm)
    {
        HeadlessUiPump.Drain(window);
        Assert.Single(VisibleTexts(window), text => text == vm.CadSelectionText);
        Assert.Single(VisibleTexts(window), text => text == vm.RegularSelectionText);
    }

    private static CollapsiblePanel FindPanel(Window window, string title) =>
        Assert.Single(window.GetVisualDescendants().OfType<CollapsiblePanel>(), panel => panel.Title == title);

    private static CollapsiblePanel[] TopLevelWorkflowPanels(RightWorkflowSettingsTabView view) =>
        Assert.IsType<StackPanel>(view.Content).Children
            .Select(static child => child as CollapsiblePanel
                ?? Assert.IsType<CollapsiblePanel>(Assert.IsAssignableFrom<UserControl>(child).Content))
            .ToArray();
}
