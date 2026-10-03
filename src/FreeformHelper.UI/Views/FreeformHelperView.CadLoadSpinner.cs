using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views;

public sealed partial class FreeformHelperView
{
    internal static Func<ICadLoadSpinnerHost> CadLoadSpinnerHostFactory { get; set; } = static () => CadLoadSpinnerHostService.SharedHost;

    internal ICadLoadSpinnerHost? CadLoadSpinnerHostForTest => _cadLoadSpinnerHost;

    private void AttachCadLoadSpinnerWindow(FreeformHelperViewModel viewModel)
    {
        if (_observedViewModel is not null)
        {
            _observedViewModel.PropertyChanged -= OnObservedViewModelPropertyChanged;
        }

        _observedViewModel = viewModel;
        _observedViewModel.PropertyChanged += OnObservedViewModelPropertyChanged;
        _cadLoadSpinnerHost ??= CadLoadSpinnerHostFactory();
        _cadLoadSpinnerHost.Warmup();
        SyncCadLoadSpinnerHost();
    }

    private void DetachCadLoadSpinnerWindow(FreeformHelperViewModel viewModel)
    {
        if (!ReferenceEquals(_observedViewModel, viewModel))
        {
            return;
        }

        _observedViewModel.PropertyChanged -= OnObservedViewModelPropertyChanged;
        _observedViewModel = null;
        _cadLoadSpinnerHost?.Hide();
        _cadLoadSpinnerHost?.Dispose();
        _cadLoadSpinnerHost = null;
    }

    private void OnObservedViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(FreeformHelperViewModel.IsCadLoadCanvasOverlayVisible), StringComparison.Ordinal) &&
            !string.Equals(e.PropertyName, nameof(FreeformHelperViewModel.IsModalLoadingSpinnerVisible), StringComparison.Ordinal))
        {
            return;
        }

        var isCurrentUiThread = UiThread.IsCurrent(out var dispatcher, out _);
        if (dispatcher is null)
        {
            return;
        }

        if (isCurrentUiThread)
        {
            SyncCadLoadSpinnerHost();
            return;
        }

        _ = dispatcher.InvokeAsync(SyncCadLoadSpinnerHost, DispatcherPriority.Normal);
    }

    private void SyncCadLoadSpinnerHost()
    {
        if (_observedViewModel is null)
        {
            HideCadLoadSpinnerHost();
            return;
        }

        var shouldShow = _observedViewModel.IsCadLoadCanvasOverlayVisible ||
                         _observedViewModel.IsModalLoadingSpinnerVisible;
        if (shouldShow)
        {
            CadLoadSpinnerDebugState.RecordViewHostSync("show", overlayVisible: true);
            ShowCadLoadSpinnerHost();
            return;
        }

        CadLoadSpinnerDebugState.RecordViewHostSync("hide", overlayVisible: false);
        HideCadLoadSpinnerHost();
    }

    private void ShowCadLoadSpinnerHost()
    {
        if (_isCadLoadSpinnerHostVisible)
        {
            return;
        }

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
        {
            return;
        }

        _cadLoadSpinnerHost ??= CadLoadSpinnerHostFactory();
        _cadLoadSpinnerHost.Show(owner);
        _isCadLoadSpinnerHostVisible = true;
    }

    private void HideCadLoadSpinnerHost()
    {
        if (!_isCadLoadSpinnerHostVisible)
        {
            return;
        }

        _cadLoadSpinnerHost?.Hide();
        _isCadLoadSpinnerHostVisible = false;
    }
}
