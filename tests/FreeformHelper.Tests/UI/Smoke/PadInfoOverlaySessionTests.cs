// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.ComponentModel;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Interaction;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class PadInfoOverlaySessionTests
{
    [AvaloniaFact]
    public async Task CleanReplacementClosesAndReopensSynchronouslyWithoutInspectorRefreshAsync()
    {
        using var files = new SuppressedWorkspaceBehaviorTests.WorkspaceFixture();
        var vm = await files.OpenAsync();
        using var host = new SessionHost(vm);
        var pad = vm.CadPads[0];
        vm.ApplyCanvasSelection([], []);
        var snapshot = vm.BuildCadPadInspectorSnapshot(pad.Id);
        Assert.NotNull(snapshot);
        vm.PadInspectorUi.Snapshot = snapshot;
        host.Open(new object());
        host.Events.Clear();
        var replacement = new object();
        PadInfoContext? activeDuringClose = null;
        vm.InteractionState.PadInfoChanged += (_, e) =>
        {
            if (e.Context is null) activeDuringClose = vm.InteractionState.ActivePadInfo;
        };

        host.Session.TryOpenPadInfo(vm, replacement, [pad.Bounds]);

        Assert.Same(replacement, vm.InteractionState.ActivePadInfo?.ViewModel);
        Assert.Same(vm.InteractionState.ActivePadInfo, activeDuringClose);
        Assert.Same(snapshot, vm.PadInspectorUi.Snapshot);
        Assert.Null(host.Session.PendingContext);
        Assert.Equal<string>(["overlay:hidden", "content:clear", "bounds:clear", "selection:clear",
            "bounds:set", "selection:set", "content:set", "show", "overlay:visible"], host.Events);
        Assert.Equal(pad.Bounds, host.BoundsWorld);
        Assert.Equal(pad.Bounds, Assert.Single(host.SelectedBounds));
    }

    [AvaloniaTheory]
    [InlineData("Apply")]
    [InlineData("Discard")]
    public async Task DirtyReplacementWaitsForCommandThenReopensBeforeCloseEventReturnsAsync(string button)
    {
        using var files = new SuppressedWorkspaceBehaviorTests.WorkspaceFixture();
        var vm = await files.OpenAsync();
        using var host = new SessionHost(vm);
        vm.ApplyCanvasSelection([], []);
        var snapshot = vm.BuildCadPadInspectorSnapshot(vm.CadPads[0].Id);
        Assert.NotNull(snapshot);
        vm.PadInspectorUi.Snapshot = snapshot;
        var tracking = new TrackingInfo(host.Events);
        host.Open(tracking);
        tracking.SetPending(true);
        var replacement = host.Context(new object());
        host.Events.Clear();

        Assert.True(host.Session.RequestClosePadInfo(replacement));

        Assert.Same(tracking, host.Popover.DataContext);
        Assert.Same(replacement, host.Session.PendingContext);
        Assert.True(host.Popover.IsCloseConfirmVisible);
        Assert.False(host.Canvas.IsHitTestVisible);
        await HeadlessUiPump.FlushQueueAsync();
        host.Events.Clear();
        UiEventProbe.Click(FindButton(host.Popover, button));

        Assert.False(tracking.HasPendingChanges);
        Assert.Same(replacement, vm.InteractionState.ActivePadInfo);
        Assert.Same(replacement.ViewModel, host.Popover.DataContext);
        Assert.Null(host.Session.PendingContext);
        Assert.Same(snapshot, vm.PadInspectorUi.Snapshot);
        Assert.Equal(new[] { button, "overlay:hidden", "content:clear", "hit:enabled", "bounds:clear",
            "selection:clear", "bounds:set", "selection:set", "content:set", "show", "overlay:visible" }, host.Events);
        tracking.SetPending(true);
        Assert.True(host.Canvas.IsHitTestVisible);
    }

    [AvaloniaFact]
    public async Task ApplyingWithCommandCloseCallbackAlsoClosesReopenedReplacementAsync()
    {
        using var files = new SuppressedWorkspaceBehaviorTests.WorkspaceFixture();
        var vm = await files.OpenAsync();
        using var host = new SessionHost(vm);
        vm.ApplyCanvasSelection([], []);
        var info = new RegularPadInfoViewModel(
            pads: [vm.RegularPads[0]], toDisplayRow: vm.ToDisplayRow, getDxfIndex: null,
            getMatchedCadPadIds: null, getMatchedCadLinks: null, applyPadSize: null,
            resetPadSize: null, setFreeform: null, getCrossIcOwnerShares: null,
            getNotchRowsByRegularPad: null, highlightCadOwnerPads: null,
            focusRegularMatches: null, highlightRegularMatches: null, closePadInfo: host.Session.ClosePadInfoViaState);
        host.Open(info);
        info.PadWidth += 1m;
        var replacement = host.Context(new object());
        Assert.True(host.Session.RequestClosePadInfo(replacement));
        await HeadlessUiPump.FlushQueueAsync();
        var notifications = new List<PadInfoContext?>();
        vm.InteractionState.PadInfoChanged += (_, e) => notifications.Add(e.Context);

        UiEventProbe.Click(FindButton(host.Popover, "Apply"));

        Assert.Equal(new PadInfoContext?[] { replacement, null, null }, notifications);
        Assert.Null(vm.InteractionState.ActivePadInfo);
        Assert.Null(vm.PadInspectorUi.Snapshot);
        Assert.False(host.Overlay.IsVisible);
        Assert.True(host.Canvas.IsHitTestVisible);
    }

    [AvaloniaFact]
    public async Task DismissalEnablesHitTestingBeforeClearingWaitingReplacementAsync()
    {
        using var files = new SuppressedWorkspaceBehaviorTests.WorkspaceFixture();
        var vm = await files.OpenAsync();
        using var host = new SessionHost(vm);
        var tracking = new TrackingInfo(host.Events);
        host.Open(tracking);
        tracking.SetPending(true);
        var replacement = host.Context(new object());
        Assert.True(host.Session.RequestClosePadInfo(replacement));
        await HeadlessUiPump.FlushQueueAsync();
        var pendingWhenEnabled = new List<PadInfoContext?>();
        host.Canvas.PropertyChanged += (_, e) =>
        {
            if (e.Property == InputElement.IsHitTestVisibleProperty && host.Canvas.IsHitTestVisible)
                pendingWhenEnabled.Add(host.Session.PendingContext);
        };

        host.PressInsidePopover();

        Assert.Null(host.Session.PendingContext);
        Assert.True(tracking.HasPendingChanges);
        Assert.False(host.Popover.IsCloseConfirmVisible);
        Assert.True(host.Canvas.IsHitTestVisible);
        // The confirmation observable runs before CloseConfirmDismissed clears the replacement.
        Assert.Same(replacement, Assert.Single(pendingWhenEnabled));
        host.Session.ClosePadInfoViaState();
        Assert.Null(vm.InteractionState.ActivePadInfo);
        Assert.Null(host.Popover.DataContext);
        Assert.False(host.Overlay.IsVisible);
    }

    [AvaloniaFact]
    public void DirtyNotificationsFilterAndSwitchObserversWithoutDerivingTheCachedFlag()
    {
        using var host = new SessionHost(null);
        var first = new TrackingInfo(host.Events);
        host.Open(first);
        first.HasPendingChanges = true;
        first.Notify("Other");
        first.Notify(null);
        first.Notify(string.Empty);
        Assert.True(host.Canvas.IsHitTestVisible);
        first.Notify(nameof(IPadInfoChangeTracking.HasPendingChanges));
        Assert.False(host.Canvas.IsHitTestVisible);

        var second = new TrackingInfo(host.Events);
        host.Open(second);
        first.Notify(nameof(IPadInfoChangeTracking.HasPendingChanges));
        Assert.True(host.Canvas.IsHitTestVisible);
        host.Popover.ShowCloseConfirm();
        Assert.False(host.Session.EnsureNoPendingEdits());
        host.Popover.HideCloseConfirm();
        Assert.True(host.Session.EnsureNoPendingEdits());
        second.SetPending(true);
        Assert.False(host.Session.EnsureNoPendingEdits());
        host.Popover.HideCloseConfirm();
        Assert.True(host.Canvas.IsHitTestVisible);
        second.Notify(nameof(IPadInfoChangeTracking.HasPendingChanges));
        Assert.False(host.Canvas.IsHitTestVisible);
    }

    [AvaloniaFact]
    public void MissingOverlayClearsReplacementOnRequestButDoesNotCleanUpOnClose()
    {
        using var host = new SessionHost(null);
        var tracking = new TrackingInfo(host.Events);
        host.Open(tracking);
        var bounds = host.BoundsWorld;
        host.Session.PendingContext = host.Context(new object());
        host.ControlsAvailable = false;

        Assert.False(host.Session.RequestClosePadInfo());
        Assert.Null(host.Session.PendingContext);
        host.Session.ClosePadInfoViaState();

        Assert.Same(tracking, host.Popover.DataContext);
        Assert.Equal(bounds, host.BoundsWorld);
        Assert.True(host.Overlay.IsVisible);
        tracking.SetPending(true);
        Assert.False(host.Canvas.IsHitTestVisible);
        host.ControlsAvailable = true;
        host.Session.ClosePadInfoViaState();
        Assert.Null(host.Popover.DataContext);
        Assert.Null(host.BoundsWorld);
        Assert.Empty(host.SelectedBounds);
        Assert.True(host.Canvas.IsHitTestVisible);
        tracking.Notify(nameof(IPadInfoChangeTracking.HasPendingChanges));
        Assert.True(host.Canvas.IsHitTestVisible);
    }

    [AvaloniaFact]
    public async Task ViewPendingContextProxyForwardsToTheSameCloseSessionAsync()
    {
        await using var workspace = await MainWindowGlobalInputFixture.CreateAsync();
        var view = Assert.Single(workspace.Window.GetVisualDescendants().OfType<FreeformHelperView>());
        var popover = Assert.IsType<PadInfoPopover>(view.FindControl<PadInfoPopover>("PadInfoPopover"));
        var pad = workspace.Helper.CadPads[0];
        var tracking = new TrackingInfo([]);
        workspace.Helper.InteractionState.OpenPadInfo(new PadInfoContext(tracking, pad.Bounds, [pad.Bounds]));
        tracking.SetPending(true);
        var replacement = new PadInfoContext(new object(), pad.Bounds, [pad.Bounds]);
        view._pendingPadInfoContext = replacement;
        Assert.Same(replacement, view._pendingPadInfoContext);
        popover.ShowCloseConfirm();
        await HeadlessUiPump.FlushQueueAsync();

        UiEventProbe.Click(FindButton(popover, "Discard"));

        Assert.Same(replacement, workspace.Helper.InteractionState.ActivePadInfo);
        Assert.Null(view._pendingPadInfoContext);
        Assert.Same(replacement.ViewModel, popover.DataContext);
    }

    private static Button FindButton(PadInfoPopover popover, string content)
        => Assert.Single(popover.GetVisualDescendants().OfType<Button>(),
            candidate => candidate.Command is null && candidate.Content is string text && text == content);

    private sealed class SessionHost : IDisposable
    {
        private readonly FreeformHelperViewModel? _viewModel;
        private readonly Window _window;

        public SessionHost(FreeformHelperViewModel? viewModel)
        {
            _viewModel = viewModel;
            Session = new PadInfoOverlaySession(
                () => _viewModel, () => ControlsAvailable ? Overlay : null, () => Popover, () => Canvas,
                bounds =>
                {
                    BoundsWorld = bounds;
                    Events.Add(bounds is null ? "bounds:clear" : "bounds:set");
                    if (bounds is null) AssertClosedBeforeBoundsClear();
                },
                bounds =>
                {
                    SelectedBounds = bounds;
                    Events.Add(bounds.Count == 0 ? "selection:clear" : "selection:set");
                },
                () =>
                {
                    Events.Add("show");
                    Assert.NotNull(Popover.DataContext);
                    Overlay.IsVisible = true;
                });
            Overlay.Children.Add(Popover);
            _window = new Window { Content = Overlay, Width = 640, Height = 480 };
            _window.Show();
            Session.WirePopoverEvents();
            Overlay.PropertyChanged += (_, e) =>
            {
                if (e.Property == Avalonia.Visual.IsVisibleProperty)
                    Events.Add(Overlay.IsVisible ? "overlay:visible" : "overlay:hidden");
            };
            Popover.PropertyChanged += (_, e) =>
            {
                if (e.Property == Avalonia.StyledElement.DataContextProperty)
                    Events.Add(Popover.DataContext is null ? "content:clear" : "content:set");
            };
            Canvas.PropertyChanged += (_, e) =>
            {
                if (e.Property == InputElement.IsHitTestVisibleProperty)
                    Events.Add(Canvas.IsHitTestVisible ? "hit:enabled" : "hit:disabled");
            };
            if (_viewModel is not null) _viewModel.InteractionState.PadInfoChanged += OnPadInfoChanged;
        }

        public PadInfoOverlaySession Session { get; }
        public Canvas Overlay { get; } = new() { IsVisible = false };
        public PadInfoPopover Popover { get; } = new();
        public Control Canvas { get; } = new Border();
        public bool ControlsAvailable { get; set; } = true;
        public Rect2? BoundsWorld { get; private set; }
        public IReadOnlyList<Rect2> SelectedBounds { get; private set; } = [];
        public List<string> Events { get; } = [];

        public PadInfoContext Context(object info) => new(info, BoundsWorld ?? new Rect2(0, 0, 10, 10),
            [BoundsWorld ?? new Rect2(0, 0, 10, 10)]);

        public void Open(object info)
        {
            var context = Context(info);
            if (_viewModel is null) Session.OnPadInfoChanged(new PadInfoChangedEventArgs(context));
            else _viewModel.InteractionState.OpenPadInfo(context);
        }

        public void PressInsidePopover()
        {
            var panel = Assert.Single(Popover.GetVisualDescendants().OfType<Border>(),
                static border => border.Classes.Contains("padInfoPanel"));
            using var pointer = new Pointer(1, PointerType.Mouse, isPrimary: true);
            panel.RaiseEvent(new PointerPressedEventArgs(panel, pointer, _window, default, 0,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
                KeyModifiers.None, clickCount: 1));
        }

        public void Dispose()
        {
            if (_viewModel is not null) _viewModel.InteractionState.PadInfoChanged -= OnPadInfoChanged;
            _window.Close();
        }

        private void OnPadInfoChanged(object? sender, PadInfoChangedEventArgs e) => Session.OnPadInfoChanged(e);

        private void AssertClosedBeforeBoundsClear()
        {
            Assert.False(Overlay.IsVisible);
            Assert.Null(Popover.DataContext);
            Assert.False(Popover.IsCloseConfirmVisible);
            Assert.True(Canvas.IsHitTestVisible);
        }
    }

    private sealed class TrackingInfo : IPadInfoChangeTracking, INotifyPropertyChanged
    {
        public TrackingInfo(List<string> events)
        {
            ApplyChangesCommand = new RelayCommand(() =>
            {
                events.Add("Apply");
                SetPending(false);
            });
            DiscardChangesCommand = new RelayCommand(() =>
            {
                events.Add("Discard");
                SetPending(false);
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public bool HasPendingChanges { get; set; }
        public ICommand ApplyChangesCommand { get; }
        public ICommand DiscardChangesCommand { get; }

        public void SetPending(bool value)
        {
            HasPendingChanges = value;
            Notify(nameof(HasPendingChanges));
        }

        public void Notify(string? propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
