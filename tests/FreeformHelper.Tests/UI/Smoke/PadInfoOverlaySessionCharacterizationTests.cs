// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.Interaction;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;
using Point = Avalonia.Point;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class PadInfoOverlaySessionCharacterizationTests
{
    [AvaloniaFact]
    public async Task CleanContextRequestReopensBeforeCloseNotificationReturnsAsync()
    {
        await using var workspace = await MainWindowGlobalInputFixture.CreateAsync();
        var view = Assert.Single(workspace.Window.GetVisualDescendants().OfType<FreeformHelperView>());
        var canvas = GetCanvas(view);
        var pad = workspace.Helper.CadPads[0];
        workspace.Helper.ApplyCanvasSelection([pad.Id], []);
        canvas.SetSelection([pad.Id], []);
        canvas.FocusSelectionWithMinZoomAt(1, new Point(140, 120));
        await HeadlessUiPump.FlushQueueAsync();
        var original = new PadInfoContext(new object(), pad.Bounds, [pad.Bounds]);
        workspace.Helper.InteractionState.OpenPadInfo(original);
        var notifications = new List<PadInfoContext?>();
        PadInfoContext? activeDuringClose = null;
        workspace.Helper.InteractionState.PadInfoChanged += (_, e) =>
        {
            notifications.Add(e.Context);
            if (e.Context is null) activeDuringClose = workspace.Helper.InteractionState.ActivePadInfo;
        };

        PressPointer(canvas, canvas.WorldToScreen(pad.Centroid), rightButton: true);
        await HeadlessUiPump.FlushQueueAsync();

        var replacement = Assert.IsType<PadInfoContext>(workspace.Helper.InteractionState.ActivePadInfo);
        Assert.NotSame(original, replacement);
        Assert.IsType<CadPadInfoViewModel>(replacement.ViewModel);
        Assert.Equal(new PadInfoContext?[] { replacement, null }, notifications);
        Assert.Same(replacement, activeDuringClose);
        Assert.Same(replacement.ViewModel, GetPopover(view).DataContext);
        Assert.True(GetOverlay(view).IsVisible);
        Assert.Equal(pad.Id, Assert.Single(workspace.Helper.SelectedCadPadIds));
        Assert.Equal(pad.Id, Assert.Single(workspace.Helper.InteractionState.SelectedCadIds));
    }

    [AvaloniaFact]
    public async Task InsideClickEnablesHitTestingWhileEditsRemainDirtyAsync()
    {
        await using var workspace = await MainWindowGlobalInputFixture.CreateAsync();
        var view = Assert.Single(workspace.Window.GetVisualDescendants().OfType<FreeformHelperView>());
        var info = OpenRegularInfo(workspace.Helper);
        await HeadlessUiPump.FlushQueueAsync();
        info.PadWidth += 1m;
        var popover = GetPopover(view);
        Assert.False(GetCanvas(view).IsHitTestVisible);
        Assert.False(Assert.IsType<Func<bool>>(workspace.Helper.EnsureNoPendingEdits)());
        Assert.True(popover.IsCloseConfirmVisible);
        var dismissals = 0;
        popover.CloseConfirmDismissed += (_, _) => dismissals++;
        var scroll = Assert.Single(popover.GetVisualDescendants().OfType<ScrollViewer>(),
            static control => control.Name == "RegularPadInfoScroll");

        PressPointer(scroll, new Point(1, 1));

        Assert.Equal(1, dismissals);
        Assert.False(popover.IsCloseConfirmVisible);
        Assert.True(info.HasPendingChanges);
        Assert.True(GetCanvas(view).IsHitTestVisible);
        Assert.Same(info, workspace.Helper.InteractionState.ActivePadInfo?.ViewModel);
        // A later dirty notification still uses the dirty-or-confirmation predicate.
        info.PadWidth += 1m;
        Assert.False(GetCanvas(view).IsHitTestVisible);
    }

    [AvaloniaTheory]
    [InlineData("Apply", false, 1)]
    [InlineData("Discard", false, 1)]
    [InlineData("Apply", true, 2)]
    [InlineData("Discard", true, 2)]
    public async Task ReattachRetainsRepeatedCloseSubscriptionsAsync(string button, bool reattach, int expectedCloses)
    {
        using var files = new SuppressedWorkspaceBehaviorTests.WorkspaceFixture();
        var vm = await files.OpenAsync();
        var view = new FreeformHelperView { DataContext = vm };
        var window = new Window { Content = view, Width = 1200, Height = 800 };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            if (reattach)
            {
                window.Content = null;
                window.Content = view;
                await HeadlessUiPump.FlushQueueAsync();
            }
            var info = OpenRegularInfo(vm);
            info.PadWidth += 1m;
            Assert.False(Assert.IsType<Func<bool>>(vm.EnsureNoPendingEdits)());
            await HeadlessUiPump.FlushQueueAsync();
            var replacement = new PadInfoContext(new object(), vm.CadPads[0].Bounds, [vm.CadPads[0].Bounds]);
            var closes = 0;
            vm.InteractionState.PadInfoChanged += (_, e) =>
            {
                if (e.Context is not null) return;
                closes++;
                if (closes == 1) vm.InteractionState.OpenPadInfo(replacement);
            };

            UiEventProbe.Click(FindButton(GetPopover(view), button));

            Assert.False(info.HasPendingChanges);
            Assert.Equal(expectedCloses, closes);
            if (reattach) Assert.Null(vm.InteractionState.ActivePadInfo);
            else Assert.Same(replacement, vm.InteractionState.ActivePadInfo);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task DetachedDirtyObserverSurvivesAndDeferredReattachReplaysConfirmationVisibilityAsync()
    {
        using var files = new SuppressedWorkspaceBehaviorTests.WorkspaceFixture();
        var vm = await files.OpenAsync();
        var view = new FreeformHelperView { DataContext = vm };
        var window = new Window { Content = view, Width = 1200, Height = 800 };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var info = OpenRegularInfo(vm);
            var canvas = GetCanvas(view);
            window.Content = null;
            Assert.Null(vm.EnsureNoPendingEdits);

            info.PadWidth += 1m;

            Assert.False(canvas.IsHitTestVisible);
            Assert.True(GetOverlay(view).IsVisible);
            Assert.Same(info, GetPopover(view).DataContext);
            window.Content = view;
            Assert.False(canvas.IsHitTestVisible);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.True(info.HasPendingChanges);
            Assert.False(GetPopover(view).IsCloseConfirmVisible);
            Assert.True(canvas.IsHitTestVisible);
            info.PadWidth += 1m;
            Assert.False(canvas.IsHitTestVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OpeningBeforeDeferredHooksDoesNotReplayTheContextAsync(bool detachBeforeHooks)
    {
        using var files = new SuppressedWorkspaceBehaviorTests.WorkspaceFixture();
        var vm = await files.OpenAsync();
        var view = new FreeformHelperView { DataContext = vm };
        var window = new Window { Content = view, Width = 1200, Height = 800 };
        try
        {
            window.Show();
            var info = OpenRegularInfo(vm);
            Assert.False(GetOverlay(view).IsVisible);
            if (detachBeforeHooks) window.Content = null;
            await HeadlessUiPump.FlushQueueAsync();
            Assert.False(GetOverlay(view).IsVisible);
            Assert.NotSame(info, GetPopover(view).DataContext);
            if (detachBeforeHooks)
            {
                window.Content = view;
                OpenRegularInfo(vm);
                Assert.False(GetOverlay(view).IsVisible);
                await HeadlessUiPump.FlushQueueAsync();
                Assert.False(GetOverlay(view).IsVisible);
            }

            var next = OpenRegularInfo(vm);

            Assert.Same(next, GetPopover(view).DataContext);
            Assert.True(GetOverlay(view).IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    private static RegularPadInfoViewModel OpenRegularInfo(FreeformHelperViewModel vm)
    {
        var pad = vm.RegularPads[0];
        // Keep the command's close callback absent so only the popover close event is measured.
        var info = new RegularPadInfoViewModel(
            pads: [pad], toDisplayRow: vm.ToDisplayRow, getDxfIndex: null,
            getMatchedCadPadIds: null, getMatchedCadLinks: null, applyPadSize: null,
            resetPadSize: null, setFreeform: null, getCrossIcOwnerShares: null,
            getNotchRowsByRegularPad: null, highlightCadOwnerPads: null,
            focusRegularMatches: null, highlightRegularMatches: null, closePadInfo: null);
        vm.InteractionState.OpenPadInfo(new PadInfoContext(info, pad.Bounds, [pad.Bounds]));
        return info;
    }

    private static PadCanvas GetCanvas(FreeformHelperView view)
        => Assert.IsType<PadCanvas>(view.FindControl<PadCanvas>("PadCanvas"));

    private static PadInfoPopover GetPopover(FreeformHelperView view)
        => Assert.IsType<PadInfoPopover>(view.FindControl<PadInfoPopover>("PadInfoPopover"));

    private static Canvas GetOverlay(FreeformHelperView view)
        => Assert.IsType<Canvas>(view.FindControl<Canvas>("PadInfoOverlay"));

    private static Button FindButton(PadInfoPopover popover, string content)
        => Assert.Single(popover.GetVisualDescendants().OfType<Button>(),
            candidate => candidate.Command is null && candidate.Content is string text && text == content);

    private static void PressPointer(Control target, Point position, bool rightButton = false)
    {
        using var pointer = new Pointer(1, PointerType.Mouse, isPrimary: true);
        var root = Assert.IsAssignableFrom<TopLevel>(TopLevel.GetTopLevel(target));
        var rootPosition = Assert.IsType<Point>(Avalonia.VisualExtensions.TranslatePoint(target, position, root));
        target.RaiseEvent(new PointerPressedEventArgs(target, pointer, root, rootPosition, 0,
            new PointerPointProperties(
                rightButton ? RawInputModifiers.RightMouseButton : RawInputModifiers.LeftMouseButton,
                rightButton ? PointerUpdateKind.RightButtonPressed : PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None, clickCount: 1));
    }
}
