using Avalonia.Controls;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views;

public sealed partial class FreeformHelperView
{
    private Task ShowIndexMappingReportWindowAsync(IndexMappingReportViewModel viewModel)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (_indexMappingReportWindow is not null && _indexMappingReportWindow.IsVisible)
        {
            _indexMappingReportWindow.DataContext = viewModel;
            _indexMappingReportWindow.Activate();
            return Task.CompletedTask;
        }

        if (owner is null)
        {
            return Task.CompletedTask;
        }

        var window = new IndexMappingReportWindow
        {
            DataContext = viewModel
        };
        _indexMappingReportWindow = window;
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_indexMappingReportWindow, window))
            {
                _indexMappingReportWindow = null;
            }
        };

        window.Show(owner);
        return Task.CompletedTask;
    }
}

