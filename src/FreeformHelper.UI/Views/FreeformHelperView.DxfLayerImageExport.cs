using Avalonia.Controls;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views;

public sealed partial class FreeformHelperView
{
    private async Task<DxfLayerImageExportRequest?> ShowDxfLayerImageExportWindowAsync(IReadOnlyList<string> layerNames)
    {
        var initialRequest = (DataContext as FreeformHelperViewModel)
            ?.BuildDxfLayerImageExportInitialRequest(layerNames);
        var viewModel = new DxfLayerImageExportViewModel(layerNames, initialRequest);
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
        {
            return viewModel.BuildRequest();
        }

        var window = new DxfLayerImageExportWindow
        {
            DataContext = viewModel
        };

        var confirmed = await window.ShowDialog<bool>(owner);
        return confirmed ? viewModel.BuildRequest() : null;
    }
}
