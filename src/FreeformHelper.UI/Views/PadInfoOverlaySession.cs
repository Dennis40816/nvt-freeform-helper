// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.ComponentModel;
using Avalonia.Controls;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.UI.Interaction;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views;

/// <summary>Owns pad-info replacement, dirty observation and close transitions on the UI thread.</summary>
internal sealed class PadInfoOverlaySession
{
    private readonly Func<FreeformHelperViewModel?> _getViewModel;
    private readonly Func<Canvas?> _getOverlay;
    private readonly Func<PadInfoPopover?> _getPopover;
    private readonly Func<Control?> _getCanvas;
    private readonly Action<Rect2?> _setBoundsWorld;
    private readonly Action<IReadOnlyList<Rect2>> _setSelectedBounds;
    private readonly Action _showOverlay;
    private INotifyPropertyChanged? _padInfoChangeNotifier; // UI thread only.
    private bool _hasPadInfoPendingChanges; // Last observed notification, UI thread only.

    public PadInfoOverlaySession(
        Func<FreeformHelperViewModel?> getViewModel,
        Func<Canvas?> getOverlay,
        Func<PadInfoPopover?> getPopover,
        Func<Control?> getCanvas,
        Action<Rect2?> setBoundsWorld,
        Action<IReadOnlyList<Rect2>> setSelectedBounds,
        Action showOverlay)
    {
        _getViewModel = getViewModel;
        _getOverlay = getOverlay;
        _getPopover = getPopover;
        _getCanvas = getCanvas;
        _setBoundsWorld = setBoundsWorld;
        _setSelectedBounds = setSelectedBounds;
        _showOverlay = showOverlay;
    }

    /// <summary>Gets or sets the waiting replacement, on the UI thread only.</summary>
    public PadInfoContext? PendingContext { get; set; }

    private Canvas? Overlay => _getOverlay();
    private PadInfoPopover? Popover => _getPopover();
    private Control? Canvas => _getCanvas();

    /// <summary>Adds the existing subscriptions on every deferred hook, including reattach.</summary>
    public void WirePopoverEvents()
    {
        if (Popover is not null)
        {
            Popover.ApplyCloseRequested += (_, _) => ClosePadInfoViaState();
            Popover.DiscardCloseRequested += (_, _) => ClosePadInfoViaState();
            Popover.CloseConfirmDismissed += (_, _) =>
            {
                PendingContext = null;
                SetPadAreaHitTest(true);
            };
            Avalonia.AvaloniaObjectExtensions.GetObservable(Popover, PadInfoPopover.IsCloseConfirmVisibleProperty)
                .Subscribe(visible => SetPadAreaHitTest(!visible));
        }
    }

    public void TryOpenPadInfo(FreeformHelperViewModel vm, object infoViewModel, IEnumerable<Rect2> bounds)
    {
        var context = new PadInfoContext(infoViewModel, PadInfoLayoutCalculator.BuildBounds(bounds), bounds.ToList());
        if (RequestClosePadInfo(context))
        {
            return;
        }

        vm.InteractionState.OpenPadInfo(context);
    }

    public void OnPadInfoChanged(PadInfoChangedEventArgs e)
    {
        if (e.Context is null)
        {
            ClosePadInfo();
            if (_getViewModel() is not { } vm) return;
            // Close and reopen synchronously; refresh the inspector only without a replacement.
            var pending = PendingContext;
            PendingContext = null;
            if (pending is null) vm.RefreshInspectorFromSelection();
            else vm.InteractionState.OpenPadInfo(pending);
            return;
        }

        _setBoundsWorld(e.Context.BoundsWorld);
        _setSelectedBounds(e.Context.SelectedBounds);
        if (Popover is not null)
        {
            Popover.DataContext = e.Context.ViewModel;
        }

        ObservePadInfoChanges(e.Context.ViewModel as IPadInfoChangeTracking);
        UpdatePadAreaInteraction();
        _showOverlay();
    }

    private void ClosePadInfo()
    {
        if (Overlay is null)
        {
            return;
        }

        Overlay.IsVisible = false;
        if (Popover is not null)
        {
            Popover.DataContext = null;
            Popover.HideCloseConfirm();
        }
        ObservePadInfoChanges(null);
        SetPadAreaHitTest(true);
        _setBoundsWorld(null);
        _setSelectedBounds(Array.Empty<Rect2>());
    }

    public bool RequestClosePadInfo(PadInfoContext? replacement = null)
    {
        if (Overlay is null || Popover is null || !Overlay.IsVisible)
        {
            PendingContext = null;
            return false;
        }

        if (Popover.DataContext is IPadInfoChangeTracking tracking && tracking.HasPendingChanges)
        {
            PendingContext = replacement;
            Popover.ShowCloseConfirm();
            SetPadAreaHitTest(false);
            return true;
        }

        PendingContext = replacement;
        ClosePadInfoViaState();
        return true;
    }

    public void ClosePadInfoViaState()
    {
        if (_getViewModel() is { } vm)
        {
            vm.InteractionState.ClosePadInfo();
            return;
        }

        ClosePadInfo();
    }

    public void SetPadAreaHitTest(bool isEnabled)
    {
        if (Canvas is null)
        {
            return;
        }

        Canvas.IsHitTestVisible = isEnabled;
    }

    public bool HandleDirtyPadInfo()
    {
        if (Popover?.DataContext is IPadInfoChangeTracking tracking && tracking.HasPendingChanges)
        {
            Popover.ShowCloseConfirm();
            SetPadAreaHitTest(false);
            return true;
        }
        return Popover?.IsCloseConfirmVisible == true;
    }

    public bool EnsureNoPendingEdits() => !HandleDirtyPadInfo();

    private void ObservePadInfoChanges(IPadInfoChangeTracking? tracking)
    {
        if (_padInfoChangeNotifier is not null)
        {
            _padInfoChangeNotifier.PropertyChanged -= OnPadInfoPropertyChanged;
        }

        _padInfoChangeNotifier = tracking as INotifyPropertyChanged;
        _hasPadInfoPendingChanges = tracking?.HasPendingChanges ?? false;

        if (_padInfoChangeNotifier is not null)
        {
            _padInfoChangeNotifier.PropertyChanged += OnPadInfoPropertyChanged;
        }
    }

    private void OnPadInfoPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IPadInfoChangeTracking.HasPendingChanges))
        {
            return;
        }

        if (sender is IPadInfoChangeTracking tracking)
        {
            _hasPadInfoPendingChanges = tracking.HasPendingChanges;
            UpdatePadAreaInteraction();
        }
    }

    private void UpdatePadAreaInteraction()
    {
        var shouldBlock = _hasPadInfoPendingChanges || (Popover?.IsCloseConfirmVisible ?? false);
        SetPadAreaHitTest(!shouldBlock);
    }
}
