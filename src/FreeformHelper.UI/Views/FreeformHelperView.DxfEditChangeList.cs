using Avalonia.Controls;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views;

public sealed partial class FreeformHelperView
{
    private Task ShowDxfEditChangeListWindowAsync(DxfEditChangeListViewModel viewModel)
    {
        viewModel.Editing.AttachProject(DataContext as FreeformHelperViewModel);
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (_dxfEditChangeListWindow is not null && _dxfEditChangeListWindow.IsVisible)
        {
            _dxfEditChangeListWindow.DataContext = viewModel;
            _dxfEditChangeListWindow.Activate();
            return Task.CompletedTask;
        }

        var window = new DxfEditChangeListWindow
        {
            DataContext = viewModel
        };

        _dxfEditChangeListWindow = window;
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_dxfEditChangeListWindow, window))
            {
                _dxfEditChangeListWindow = null;
            }
        };

        if (owner is not null)
        {
            window.Show(owner);
            return Task.CompletedTask;
        }

        window.Show();
        return Task.CompletedTask;
    }
}
