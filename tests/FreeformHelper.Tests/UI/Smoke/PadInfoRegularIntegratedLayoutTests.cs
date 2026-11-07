// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.VisualTree;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Nvt.Core.Avalonia.Panels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class PadInfoRegularIntegratedLayoutTests
{
    [AvaloniaFact]
    public async Task CommonActionsAppearOnceInOrderAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var focused = new List<int>();
        var info = CreateInfo(focus: focused.Add);
        var popover = new PadInfoPopover { DataContext = info };
        var window = new Window { Content = popover, Width = 360, Height = 800 };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var header = Assert.Single(popover.GetVisualDescendants().OfType<PadHeader>());
            Assert.Collection(header.GetVisualDescendants().OfType<Button>(),
                static button => Assert.Equal("Locate match", AutomationProperties.GetName(button)));
            var action = Assert.Single(popover.GetVisualDescendants().OfType<Button>(),
                button => button.Command == info.LocateMatchCommand);
            Assert.True(action.IsEffectivelyVisible);
            Assert.True(action.IsEffectivelyEnabled);
            Assert.False(string.IsNullOrWhiteSpace(TipText(action)));
            Assert.DoesNotContain(popover.GetVisualDescendants().OfType<Button>(),
                static button => button.Classes.Contains("padInfoHeaderActionButton"));

            ExecuteAction(action);
            Assert.Equal(101, Assert.Single(focused));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task UnavailableLocateStaysInItsSlotWithReasonAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var popover = new PadInfoPopover { DataContext = CreateInfo(matched: false) };
        var window = new Window { Content = popover, Width = 360, Height = 800 };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var header = Assert.Single(popover.GetVisualDescendants().OfType<PadHeader>());
            var action = Assert.Single(header.GetVisualDescendants().OfType<Button>());
            Assert.Equal("Locate match", AutomationProperties.GetName(action));
            Assert.True(action.IsEffectivelyVisible);
            Assert.False(action.IsEffectivelyEnabled);
            Assert.Equal("No related CAD pads are available", TipText(action));
            Assert.Equal(TipText(action), TipText(Assert.IsType<Border>(action.Parent)));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task NarrowHeaderKeepsIconAndTooltipAsync()
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
            var action = Assert.Single(header.GetVisualDescendants().OfType<Button>());
            Assert.Contains("actionIconButton", action.Classes);
            Assert.True(action.IsEffectivelyVisible);
            Assert.False(string.IsNullOrWhiteSpace(TipText(action)));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task RelationSharesTitleHintAndPreservesCardHighlightsAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var highlighted = new List<int>();
        var info = CreateInfo(highlight: ids => highlighted.AddRange(ids));
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
            Assert.Single(text, value => value == info.OwnerSummaryText);
            var overlap = Assert.Single(info.MatchAreaCards);
            var card = Assert.Single(popover.GetVisualDescendants().OfType<Button>(),
                button => button.Command == overlap.HighlightCommand);
            Assert.Equal(overlap.RawText, TipText(card));
            Assert.Contains(overlap.RatioText, text);
            ExecuteAction(card);
            Assert.Equal(201, Assert.Single(highlighted));

            var rows = Assert.Single(popover.GetVisualDescendants().OfType<CollapsiblePanel>(),
                static section => section.Title == "Notch row flow");
            Assert.False(rows.IsExpanded);
            rows.SetCurrentValue(CollapsiblePanel.IsExpandedProperty, true);
            await HeadlessUiPump.FlushQueueAsync();
            var flow = Assert.Single(info.NotchRowFlowCards);
            var rowCard = Assert.Single(popover.GetVisualDescendants().OfType<Button>(),
                button => button.Command == flow.HighlightCommand);
            Assert.Equal(flow.RawText, TipText(rowCard));
            Assert.Contains(rowCard.GetVisualDescendants().OfType<SelectableTextBlock>(),
                block => block.Text?.Contains(flow.RatioText, StringComparison.Ordinal) == true);
            ExecuteAction(rowCard);
            Assert.Equal(2, highlighted.Count);
            Assert.All(highlighted, static id => Assert.Equal(201, id));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task GeometryStartsCollapsedAndUsesExistingStateAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var info = CreateInfo();
        var popover = new PadInfoPopover { DataContext = info };
        var window = new Window { Content = popover, Width = 360, Height = 800 };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var geometry = Assert.Single(popover.GetVisualDescendants().OfType<CollapsiblePanel>(),
                static section => section.Title == "Geometry / Debug");
            Assert.False(geometry.IsExpanded);
            Assert.False(info.ShowGeometryDetails);
            geometry.SetCurrentValue(CollapsiblePanel.IsExpandedProperty, true);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.True(info.ShowGeometryDetails);
            var text = popover.GetVisualDescendants().OfType<SelectableTextBlock>()
                .Where(static block => block.IsEffectivelyVisible).Select(static block => block.Text).ToArray();
            Assert.Contains(info.AreaText, text);
            Assert.Contains(info.BoundsText, text);
            Assert.DoesNotContain("Match / freeform", text);

            info.ToggleGeometryDetailsCommand.Execute(null);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.False(geometry.IsExpanded);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task SizeActionsPlaceDiscardBeforeApplyOnRightAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var info = CreateInfo();
        var popover = new PadInfoPopover { DataContext = info };
        var window = new Window { Content = popover, Width = 360, Height = 800 };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var buttons = popover.GetVisualDescendants().OfType<Button>().ToArray();
            var discard = Assert.Single(buttons, button => button.Command == info.DiscardChangesCommand);
            var apply = Assert.Single(buttons, button => button.Command == info.ApplyChangesCommand);
            var row = Assert.IsType<StackPanel>(discard.Parent);
            Assert.Same(row, apply.Parent);
            Assert.Equal(HorizontalAlignment.Right, row.HorizontalAlignment);
            Assert.Collection(row.Children, control => Assert.Same(discard, control), control => Assert.Same(apply, control));
            Assert.Contains("actionGhost", discard.Classes);
            Assert.Contains("actionPrimary", apply.Classes);
            Assert.Single(buttons, static button => button.Classes.Contains("actionPrimary"));
            Assert.False(apply.IsEffectivelyEnabled);
            var message = Assert.Single(popover.GetVisualDescendants().OfType<SelectableTextBlock>(),
                static block => block.Text == "Unsaved changes");
            Assert.False(message.IsEffectivelyVisible);
            Assert.Equal(HorizontalAlignment.Left, message.HorizontalAlignment);
            var initialWidth = info.PadWidth;
            info.PadWidth = initialWidth + 1m;
            await HeadlessUiPump.FlushQueueAsync();
            Assert.True(apply.IsEffectivelyEnabled);
            Assert.True(message.IsEffectivelyVisible);
            ExecuteAction(discard);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal(initialWidth, info.PadWidth);
            Assert.False(info.HasPendingChanges);
            Assert.False(message.IsEffectivelyVisible);

            var reset = Assert.Single(buttons, button => button.Command == info.ResetPadSizeCommand);
            var resetRow = Assert.IsType<StackPanel>(reset.Parent);
            Assert.NotSame(row, resetRow);
            Assert.Equal(HorizontalAlignment.Right, resetRow.HorizontalAlignment);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task FreeformKeepsFourExistingChoicesInOneSeparateRowAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var info = CreateInfo();
        var popover = new PadInfoPopover { DataContext = info };
        var window = new Window { Content = popover, Width = 360, Height = 800 };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var none = Assert.Single(popover.GetVisualDescendants().OfType<Button>(),
                button => button.Command == info.SetFreeformNoneCommand);
            var row = Assert.IsType<StackPanel>(none.Parent);
            Assert.Equal(HorizontalAlignment.Left, row.HorizontalAlignment);
            Assert.Equal(Orientation.Horizontal, row.Orientation);
            Assert.Collection(row.Children.OfType<Button>(),
                button => Assert.Same(info.SetFreeformNoneCommand, button.Command),
                button => Assert.Same(info.SetFreeformXCommand, button.Command),
                button => Assert.Same(info.SetFreeformYCommand, button.Command),
                button => Assert.Same(info.SetFreeformXYCommand, button.Command));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task CloseFooterPlacesDiscardBeforeApplyWithMessageAboveAsync()
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
                .Where(static button => button.Command is null && (button.Content as string is "Discard" or "Apply")).ToArray();
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

    private static void ExecuteAction(Button button)
    {
        Assert.True(button.IsEffectivelyEnabled);
        var command = button.Command;
        Assert.NotNull(command);
        command.Execute(button.CommandParameter);
    }

    private static string? TipText(Control control) =>
        ToolTip.GetTip(control) switch
        {
            string text => text,
            TextBlock block => block.Text,
            _ => null,
        };

    private static RegularPadInfoViewModel CreateInfo(
        bool matched = true,
        Action<int>? focus = null,
        Action<IReadOnlyCollection<int>>? highlight = null)
    {
        return new RegularPadInfoViewModel(
            pads: [TestGeometryFactory.CreateRegularPad(3, 11, 101, 0, 0, 10, 8, diffIndex: 9)],
            toDisplayRow: static row => row,
            getDxfIndex: static _ => 112,
            getMatchedCadPadIds: _ => matched ? [201] : [],
            getMatchedCadLinks: id => matched ? [new PadMatchLink(201, id, 60, 0.75, 0.6)] : [],
            applyPadSize: static (_, _, _, _) => { },
            resetPadSize: static (_, _) => { },
            setFreeform: static (_, _) => { },
            getCrossIcOwnerShares: static _ => [(201, 75)],
            getNotchRowsByRegularPad: static id =>
                [new FreeformHelperViewModel.NotchRowPreview(2, NotchAlgorithmVersion.V22, 0, 9, id, 201, "values", "comment")],
            highlightCadOwnerPads: highlight ?? (static _ => { }),
            focusRegularMatches: focus ?? (static _ => { }),
            highlightRegularMatches: null);
    }
}
