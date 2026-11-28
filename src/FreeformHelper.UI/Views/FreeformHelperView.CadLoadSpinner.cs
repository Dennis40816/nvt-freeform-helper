using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Nvt.Core.Avalonia.Threading;

namespace FreeformHelper.UI.Views;

public sealed partial class FreeformHelperView
{
    internal static Func<ICadLoadSpinnerHost> CadLoadSpinnerHostFactory { get; set; } = static () => CadLoadSpinnerHostService.SharedHost;

    internal ICadLoadSpinnerHost? CadLoadSpinnerHostForTest => _cadLoadSpinner.Host;

    private void AttachCadLoadSpinnerWindow(FreeformHelperViewModel viewModel)
    {
        if (_observedViewModel is not null)
        {
            _observedViewModel.PropertyChanged -= OnObservedViewModelPropertyChanged;
        }

        _observedViewModel = viewModel;
        _observedViewModel.PropertyChanged += OnObservedViewModelPropertyChanged;
        _cadLoadSpinner.Warmup(CadLoadSpinnerHostFactory);
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
        _cadLoadSpinner.Detach();
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
        _cadLoadSpinner.Sync(
            _observedViewModel is not null,
            _observedViewModel?.IsCadLoadCanvasOverlayVisible ?? false,
            _observedViewModel?.IsModalLoadingSpinnerVisible ?? false,
            () => TopLevel.GetTopLevel(this) as Window,
            CadLoadSpinnerHostFactory);
    }
}
