// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Nvt.Core.Avalonia.Panels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class PadInfoCadIntegratedLayoutTests
{
    [AvaloniaFact]
    public async Task CommonActionsAppearOnceInOrderAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var info = CreateInfo();
        var popover = new PadInfoPopover { DataContext = info };
        var window = new Window { Content = popover, Width = 360, Height = 800 };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var header = Assert.Single(popover.GetVisualDescendants().OfType<PadHeader>());
            Assert.Collection(header.GetVisualDescendants().OfType<Button>(),
                button => Assert.Equal("Locate match", AutomationProperties.GetName(button)),
                button => Assert.Equal("Notch detail", AutomationProperties.GetName(button)),
                button => Assert.Equal("Select area bucket", AutomationProperties.GetName(button)));
            var buttons = popover.GetVisualDescendants().OfType<Button>().ToArray();
            Assert.Single(buttons, button => button.Command == info.LocateMatchCommand);
            Assert.Single(buttons, button => button.Command == info.NotchDetailCommand);
            Assert.Single(buttons, button => button.Command == info.SelectAreaBucketCommand);
            Assert.DoesNotContain(buttons, static button => button.Content as string == "Open Notch detail");
            Assert.DoesNotContain(buttons, static button => button.Classes.Contains("padInfoHeaderActionButton"));
            Assert.DoesNotContain(buttons, static button => button.Classes.Contains("padInfoOverrideIconButton"));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task AreaBucketAvailabilityFollowsColorCadByAreaAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var fixture = new SuppressedWorkspaceBehaviorTests.WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        vm.ShowRegular = false;
        vm.ColorCadByArea = false;
        var view = new FreeformHelperView { DataContext = vm };
        var window = new Window { Content = view, Width = 1200, Height = 800 };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var cad = vm.CadPads[0];
            vm.ApplyCanvasSelection([cad.Id], []);
            OpenCadPadInfo(view, cad);
            await HeadlessUiPump.FlushQueueAsync();
            var disabled = FindAction(view, "Select area bucket");
            Assert.True(disabled.IsEffectivelyVisible);
            Assert.False(disabled.IsEffectivelyEnabled);
            Assert.Equal("Turn on Color CAD by area first", TipText(disabled));
            var tooltipHost = Assert.IsType<Border>(disabled.Parent);
            Assert.Equal("Turn on Color CAD by area first", TipText(tooltipHost));

            vm.InteractionState.ClosePadInfo();
            vm.ColorCadByArea = true;
            await HeadlessUiPump.FlushQueueAsync();
            OpenCadPadInfo(view, cad);
            await HeadlessUiPump.FlushQueueAsync();

            var enabled = FindAction(view, "Select area bucket");
            Assert.True(enabled.IsEffectivelyVisible);
            Assert.True(enabled.IsEffectivelyEnabled);
            Assert.NotNull(enabled.Command);
            Assert.True(enabled.Command.CanExecute(enabled.CommandParameter));
        }
        finally
        {
            window.Close();
            vm.ClearSelectionCommand.Execute(null);
        }
    }

    [AvaloniaFact]
    public async Task NarrowHeaderKeepsThreeIconActionsWithTooltipsAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var popover = new PadInfoPopover { DataContext = CreateInfo() };
        var window = new Window { Content = popover, Width = 280, Height = 800 };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var header = Assert.Single(popover.GetVisualDescendants().OfType<PadHeader>());
            Assert.False(header.ShowActionLabels);
            Assert.Equal(3, header.GetVisualDescendants().OfType<Button>().Count());
            Assert.All(header.GetVisualDescendants().OfType<Button>(), static button =>
            {
                Assert.Contains("actionIconButton", button.Classes);
                Assert.True(button.IsEffectivelyVisible);
                Assert.False(string.IsNullOrWhiteSpace(TipText(button)));
            });
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task RelationExplainsAllocationAndAdvancedSectionsStartCollapsedAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var highlighted = new List<int>();
        var info = CreateInfo(ids => highlighted.AddRange(ids));
        var popover = new PadInfoPopover { DataContext = info };
        var window = new Window { Content = popover, Width = 360, Height = 800 };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var text = popover.GetVisualDescendants().OfType<SelectableTextBlock>()
                .Where(static block => block.IsEffectivelyVisible).Select(static block => block.Text).ToArray();
            Assert.Contains("Area split to Regular pads", text);
            Assert.Contains("Click a card to highlight that pad", text);
            var sections = popover.GetVisualDescendants().OfType<CollapsiblePanel>().ToArray();
            Assert.False(Assert.Single(sections, static section => section.Title == "Output mapping").IsExpanded);
            var geometry = Assert.Single(sections, static section => section.Title == "Geometry / Debug");
            Assert.False(geometry.IsExpanded);
            Assert.False(info.ShowLowFrequencyDetailSection);
            var card = Assert.Single(popover.GetVisualDescendants().OfType<Button>(),
                button => button.Command == info.TargetAllocationItems[0].HighlightCommand);
            Assert.Equal(info.TargetAllocationItems[0].RawText, TipText(card));
            var highlightCommand = card.Command;
            Assert.NotNull(highlightCommand);
            highlightCommand.Execute(card.CommandParameter);
            Assert.Equal(101, Assert.Single(highlighted));

            geometry.SetCurrentValue(CollapsiblePanel.IsExpandedProperty, true);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.True(info.ShowGeometryDetails);
            Assert.Equal(info.HasToFullDiagnosticsText, info.ShowNotchDiagnostics);
            Assert.Equal(info.HasRuleTrace, info.ShowRuleTrace);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task CloseFooterPlacesDiscardBeforeApplyOnRightAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var popover = new PadInfoPopover { DataContext = CreateInfo() };
        var window = new Window { Content = popover, Width = 360, Height = 800 };
        try
        {
            window.Show();
            popover.ShowCloseConfirm();
            await HeadlessUiPump.FlushQueueAsync();
            var buttons = popover.GetVisualDescendants().OfType<Button>()
                .Where(static button => button.Content as string is "Discard" or "Apply").ToArray();
            Assert.Collection(buttons,
                static button => Assert.Equal("Discard", button.Content),
                static button => Assert.Equal("Apply", button.Content));
            var row = Assert.IsType<StackPanel>(buttons[0].Parent);
            Assert.Equal(HorizontalAlignment.Right, row.HorizontalAlignment);
            var message = Assert.Single(popover.GetVisualDescendants().OfType<SelectableTextBlock>(),
                static block => block.Text == "Unsaved changes. Apply or discard.");
            Assert.Equal(HorizontalAlignment.Left, message.HorizontalAlignment);
            Assert.Same(message.Parent, row.Parent);
            Assert.True(message.TranslatePoint(default, popover)!.Value.Y < row.TranslatePoint(default, popover)!.Value.Y);
        }
        finally
        {
            window.Close();
        }
    }

    private static string? TipText(Control control) =>
        ToolTip.GetTip(control) switch
        {
            string text => text,
            TextBlock block => block.Text,
            _ => null,
        };

    private static Button FindAction(Control root, string name) =>
        Assert.Single(root.GetVisualDescendants().OfType<Button>(),
            button => AutomationProperties.GetName(button) == name);

    private static void OpenCadPadInfo(FreeformHelperView view, CadPad cad)
    {
        var canvas = Assert.IsType<PadCanvas>(view.FindControl<PadCanvas>("PadCanvas"));
        using var pointer = new Pointer(1, PointerType.Mouse, isPrimary: true);
        var root = Assert.IsAssignableFrom<TopLevel>(TopLevel.GetTopLevel(canvas));
        var position = Assert.IsType<Point>(canvas.TranslatePoint(canvas.WorldToScreen(cad.Centroid), root));
        canvas.RaiseEvent(new PointerPressedEventArgs(canvas, pointer, root, position, 0,
            new PointerPointProperties(RawInputModifiers.RightMouseButton, PointerUpdateKind.RightButtonPressed),
            KeyModifiers.None, clickCount: 1));
    }

    private static CadPadInfoViewModel CreateInfo(Action<IReadOnlyCollection<int>>? highlight = null)
    {
        var target = new NotchV22TargetAllocation(0, 9, 60, 0.75, 75, true, false, 0, 1,
            [new NotchV22TargetRegularArea(101, 60)], [101]);
        var allocation = new NotchV22TargetAllocationSummary(80, 80, 0.5, 40, [target],
            new NotchV22TargetCoverageProjection([target], 75, 0.75, false));
        return new CadPadInfoViewModel(
            pads: [TestGeometryFactory.CreateCadPad(274, "AA", 0, 0, 10, 8)],
            getDxfIndex: static _ => 112,
            getCadOutputFwDiffOverride: null,
            isDxfIndexAnchor: null,
            getCustomValue: static _ => 0,
            applyCustomValue: null,
            setCadOutputFwDiffOverride: static (_, _) => { },
            clearCadOutputFwDiffOverride: null,
            setDxfIndexAnchor: null,
            clearDxfIndexAnchor: null,
            notchDetailCommand: new RelayCommand(static () => { }),
            selectAreaBucket: static () => { },
            getMatchedRegularPadIds: static _ => [101],
            focusCadMatches: static _ => { },
            highlightRegularPads: highlight ?? (static _ => { }),
            getCadV22CompensationPreview: static _ => (0.75, 0.25, 1, true),
            getCadV22TargetAllocationSummary: _ => allocation);
    }
}
