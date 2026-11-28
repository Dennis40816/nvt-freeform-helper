// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Owns the CAD load spinner host and visibility transitions on the UI thread.
/// </summary>
internal sealed class CadLoadSpinnerVisibilitySession
{
    private ICadLoadSpinnerHost? _host;
    private bool _isVisible;

    /// <summary>Gets the current host.</summary>
    public ICadLoadSpinnerHost? Host => _host;

    /// <summary>Creates the host if needed and warms it up.</summary>
    public void Warmup(Func<ICadLoadSpinnerHost> factory)
    {
        _host ??= factory();
        _host.Warmup();
    }

    /// <summary>Synchronizes host visibility with the observed loading indicators.</summary>
    public void Sync(
        bool hasObservedViewModel,
        bool cadOverlayVisible,
        bool modalSpinnerVisible,
        Func<Window?> resolveOwner,
        Func<ICadLoadSpinnerHost> factory)
    {
        if (!hasObservedViewModel)
        {
            Hide();
            return;
        }

        var shouldShow = cadOverlayVisible || modalSpinnerVisible;
        if (shouldShow)
        {
            CadLoadSpinnerDebugState.RecordViewHostSync("show", overlayVisible: true);
            Show(resolveOwner, factory);
            return;
        }

        CadLoadSpinnerDebugState.RecordViewHostSync("hide", overlayVisible: false);
        Hide();
    }

    /// <summary>Hides and releases the host without resetting the visibility flag.</summary>
    public void Detach()
    {
        _host?.Hide();
        _host?.Dispose();
        _host = null;
    }

    private void Show(Func<Window?> resolveOwner, Func<ICadLoadSpinnerHost> factory)
    {
        if (_isVisible)
        {
            return;
        }

        var owner = resolveOwner();
        if (owner is null)
        {
            return;
        }

        _host ??= factory();
        _host.Show(owner);
        _isVisible = true;
    }

    private void Hide()
    {
        if (!_isVisible)
        {
            return;
        }

        _host?.Hide();
        _isVisible = false;
    }
}
