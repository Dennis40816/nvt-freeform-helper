using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views.RightWorkflowSections;

public sealed partial class RightWorkflowSettingsTabView : UserControl
{
    public event EventHandler<OpenSettingsRequestedEventArgs>? OpenSettingsRequested;

    private INotifyPropertyChanged? _observedContext;

    public RightWorkflowSettingsTabView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_observedContext is not null)
        {
            _observedContext.PropertyChanged -= OnDataContextPropertyChanged;
            _observedContext = null;
        }

        base.OnDataContextChanged(e);

        if (DataContext is INotifyPropertyChanged notify)
        {
            _observedContext = notify;
            notify.PropertyChanged += OnDataContextPropertyChanged;
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnStepOpenSettingsRequested(object? sender, OpenSettingsRequestedEventArgs e)
    {
        OpenSettingsRequested?.Invoke(this, e);
    }

    private void OpenSettingsButton_Click(object? sender, RoutedEventArgs e)
    {
        var section = sender is Control control
            ? ParseSection(control.Tag)
            : null;
        OpenSettingsRequested?.Invoke(this, new OpenSettingsRequestedEventArgs(section));
    }

    private static SettingsWindowSection? ParseSection(object? tag)
    {
        if (tag is not string raw || string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return Enum.TryParse<SettingsWindowSection>(raw, ignoreCase: true, out var section)
            ? section
            : null;
    }

    private void OnDataContextPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not nameof(FreeformHelperViewModel.IsStep2Expanded)
            and not nameof(FreeformHelperViewModel.IsStep3Expanded)
            and not nameof(FreeformHelperViewModel.IsStep4Expanded)
            and not nameof(FreeformHelperViewModel.IsStep5Expanded)
            and not nameof(FreeformHelperViewModel.IsStep6Expanded))
        {
            return;
        }

        if (DataContext is not FreeformHelperViewModel vm)
        {
            return;
        }

        Control? block = e.PropertyName switch
        {
            nameof(FreeformHelperViewModel.IsStep2Expanded) when vm.IsStep2Expanded => Step2Block as Control,
            nameof(FreeformHelperViewModel.IsStep3Expanded) when vm.IsStep3Expanded => Step3Block as Control,
            nameof(FreeformHelperViewModel.IsStep4Expanded) when vm.IsStep4Expanded => Step4Block as Control,
            nameof(FreeformHelperViewModel.IsStep5Expanded) when vm.IsStep5Expanded => Step5Block as Control,
            nameof(FreeformHelperViewModel.IsStep6Expanded) when vm.IsStep6Expanded => Step6Block,
            _ => null
        };
        if (block is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => block.BringIntoView());
    }
}
