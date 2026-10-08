using Avalonia.Controls;
using Avalonia.Input;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views;

public sealed partial class DxfEditChangeListWindow : Window
{
    private DxfEditChangeListViewModel? _currentViewModel;

    public DxfEditChangeListWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_currentViewModel is not null)
        {
            _currentViewModel.CloseRequested -= OnCloseRequested;
        }

        if (DataContext is DxfEditChangeListViewModel viewModel)
        {
            _currentViewModel = viewModel;
            viewModel.CloseRequested += OnCloseRequested;
        }
        else
        {
            _currentViewModel = null;
        }
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled || _currentViewModel is null || !_currentViewModel.Editing.IsProjectEditingEnabled)
        {
            return;
        }

        if (e.Key == Key.A && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _currentViewModel.AreAllVisibleRowsSelected = true;
            e.Handled = true;
        }
    }

}
